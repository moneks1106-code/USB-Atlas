using System.Text.Json;
using System.Text.Json.Serialization;

namespace UsbAtlas.Core;

/// <summary>Stores the most recent consistent snapshot under the application data directory.</summary>
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
}
