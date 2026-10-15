using System.Text.Json;
using System.Text.Json.Serialization;

namespace UsbAtlas.Core;

/// <summary>Stores the most recent consistent snapshot and compares two snapshots by device identity.</summary>
public sealed class UsbSnapshotManager(string directory)
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SemaphoreSlim ioGate = new(1, 1);

    public string DirectoryPath => directory;
    private string LatestPath => Path.Combine(directory, "latest.json");

    public async Task SaveLatestAsync(UsbSnapshot snapshot)
    {
        Validate(snapshot);
        if (!snapshot.Consistent) throw new InvalidOperationException("Inconsistent scans are not saved.");
        await WriteAtomicAsync(LatestPath, snapshot);
    }

    public async Task<UsbSnapshot?> LoadLatestAsync()
    {
        if (!File.Exists(LatestPath)) return null;
        await using var stream = File.OpenRead(LatestPath);
        var snapshot = await JsonSerializer.DeserializeAsync<UsbSnapshot>(stream, JsonOptions)
            ?? throw new InvalidDataException("The saved snapshot is empty.");
        Validate(snapshot);
        return snapshot;
    }

    private static void Validate(UsbSnapshot snapshot)
    {
        if (snapshot.Schema != UsbSnapshot.SchemaVersion || snapshot.Nodes == null || snapshot.Warnings == null)
            throw new InvalidDataException("Snapshot schema is unsupported or incomplete.");
        if (snapshot.Nodes.Select(n => n.Id).Distinct().Count() != snapshot.Nodes.Length)
            throw new InvalidDataException("Snapshot contains duplicate node identifiers.");
        var ids = snapshot.Nodes.Select(n => n.Id).ToHashSet();
        if (snapshot.Nodes.Any(n => n.ParentId != null && !ids.Contains(n.ParentId)))
            throw new InvalidDataException("Snapshot references a parent node that was not captured.");
    }

    private async Task WriteAtomicAsync<T>(string path, T value)
    {
        await ioGate.WaitAsync();
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, value, JsonOptions);
                await stream.FlushAsync();
            }
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            ioGate.Release();
        }
    }

    public SnapshotComparison Compare(UsbSnapshot before, UsbSnapshot after, Finding[]? beforeFindings = null,
        Finding[]? afterFindings = null, IReadOnlyDictionary<string, string>? manualMatches = null)
    {
        if (!before.Consistent || !after.Consistent) throw new InvalidOperationException("Comparison requires two consistent snapshots.");
        var result = new List<DeviceDelta>();
        var used = new HashSet<string>();
        var graphA = new UsbDomainGraph(before); var graphB = new UsbDomainGraph(after);
        foreach (var a in before.Devices)
        {
            UsbNode? b = null;
            string confidence = "UNKNOWN";
            if (manualMatches != null && manualMatches.TryGetValue(a.Id, out var manualId))
            {
                b = after.Find(manualId);
                if (b == null || !b.IsPhysical || b.VendorId != a.VendorId || b.ProductId != a.ProductId
                    || (a.SerialKey != null && b.SerialKey != null && a.SerialKey != b.SerialKey) || used.Contains(b.Id))
                    throw new ArgumentException("Invalid or conflicting manual device match.");
                confidence = "USER_CONFIRMED";
            }
            else if (a.SerialKey != null && before.Devices.Count(d => d.SerialKey == a.SerialKey) == 1)
            {
                var matches = after.Devices.Where(d => d.SerialKey == a.SerialKey).ToArray();
                if (matches.Length == 1) { b = matches[0]; confidence = "UNIQUE_SERIAL"; }
            }
            if (b == null)
            {
                var same = after.Devices.Where(d => d.Id == a.Id && d.InstanceId != null && d.InstanceId == a.InstanceId
                    && d.VendorId == a.VendorId && d.ProductId == a.ProductId && d.SerialKey == a.SerialKey).ToArray();
                if (same.Length == 1)
                {
                    b = same[0];
                    confidence = a.SerialKey != null && before.Devices.Count(d => d.SerialKey == a.SerialKey) == 1
                        && after.Devices.Count(d => d.SerialKey == a.SerialKey) == 1 ? "SAME_PATH_SERIAL" : "SAME_PATH_ONLY";
                }
            }
            if (b != null && used.Contains(b.Id)) b = null;
            if (b == null)
            {
                bool candidate = after.Devices.Any(d => d.VendorId == a.VendorId && d.ProductId == a.ProductId
                    && (a.SerialKey == null || d.SerialKey == null || d.SerialKey == a.SerialKey));
                result.Add(new(a.Id, null, candidate ? "AMBIGUOUS" : "UNKNOWN", candidate ? "Confirmation required" : "No longer captured",
                    [candidate ? "A possible match exists. VID/PID or ContainerId alone do not establish physical identity." : "Removed or no longer observable; this does not automatically resolve a problem."],
                    a.ParentId, null, a.ControllerId, null, graphA.HubChain(a).Select(h => h.Id).ToArray(), []));
                continue;
            }
            used.Add(b.Id);
            var changes = new List<string>();
            if (a.ParentId != b.ParentId) changes.Add("Logical port changed (R30)");
            if (a.LocationPath != b.LocationPath) changes.Add("Windows device path changed");
            if (a.ControllerId != b.ControllerId) changes.Add("Host controller changed");
            if (!graphA.HubChain(a).Select(h => h.Id).SequenceEqual(graphB.HubChain(b).Select(h => h.Id))) changes.Add("Hub chain changed");
            if (a.Speed != b.Speed) changes.Add($"Link type: {a.Speed} → {b.Speed}");
            if (!graphA.Upstreams(a).SequenceEqual(graphB.Upstreams(b))) changes.Add("USB upstream domain changed");
            result.Add(new(a.Id, b.Id, confidence, changes.Count > 0 ? "Changed" : "Unchanged", changes.ToArray(),
                a.ParentId, b.ParentId, a.ControllerId, b.ControllerId,
                graphA.HubChain(a).Select(h => h.Id).ToArray(), graphB.HubChain(b).Select(h => h.Id).ToArray()));
        }
        foreach (var b in after.Devices.Where(d => !used.Contains(d.Id)))
            result.Add(new(null, b.Id, "UNKNOWN", "Newly captured / match pending", [], null, b.ParentId, null, b.ControllerId, [], graphB.HubChain(b).Select(h => h.Id).ToArray()));
        var resolved = new List<string>(); var added = new List<string>();
        foreach (var pair in result.Where(d => d.BeforeId != null && d.AfterId != null && d.IdentityConfidence is "UNIQUE_SERIAL" or "SAME_PATH_SERIAL" or "USER_CONFIRMED"))
        {
            var old = (beforeFindings ?? []).Where(f => f.NodeId == pair.BeforeId).ToArray();
            var current = (afterFindings ?? []).Where(f => f.NodeId == pair.AfterId).ToArray();
            foreach (var problem in old.Where(f => f.Status == Assessment.Problem))
                if (current.Any(f => f.RuleId == problem.RuleId && f.Status == Assessment.Ok)) resolved.Add($"{problem.RuleId}: {before.Find(pair.BeforeId)!.Name}");
            foreach (var problem in current.Where(f => f.Status == Assessment.Problem))
                if (!old.Any(f => f.RuleId == problem.RuleId && f.Status == Assessment.Problem)) added.Add($"{problem.RuleId}: {after.Find(pair.AfterId)!.Name}");
        }
        string conclusion = result.All(d => d.Kind == "Unchanged")
            ? "No observable topology change. No performance difference was measured."
            : "The captured changes describe topology. No performance or latency improvement was measured.";
        if (resolved.Count > 0) conclusion = "A previously established configuration limitation was checked again and is resolved. No latency change was measured.";
        return new(before.Revision, after.Revision, result.ToArray(), resolved.ToArray(), added.ToArray(), conclusion);
    }
}
