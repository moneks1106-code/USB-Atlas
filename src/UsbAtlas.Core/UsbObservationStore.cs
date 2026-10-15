using System.Text.Json;

namespace UsbAtlas.Core;

/// <summary>
/// Reads separately validated observations for one snapshot revision from
/// <c>observations/&lt;revision&gt;.json</c> under the application data directory. Any external
/// tool can write such a file after a measurement; USB Atlas never writes it itself.
/// </summary>
public sealed class UsbObservationStore(string directory)
{
    public string FolderPath => Path.Combine(directory, "observations");

    public string PathFor(Guid revision) => Path.Combine(FolderPath, revision + ".json");

    /// <summary>Returns the observations for the revision, or null when no file exists. A file for another revision is rejected.</summary>
    public async Task<RuleObservations?> LoadAsync(Guid revision)
    {
        var path = PathFor(revision);
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        var observations = await JsonSerializer.DeserializeAsync<RuleObservations>(stream, UsbSnapshotManager.JsonOptions)
            ?? throw new InvalidDataException("The observation file is empty.");
        if (observations.Revision != revision)
            throw new InvalidDataException($"The observation file belongs to revision {observations.Revision}, not {revision}.");
        return observations with
        {
            Modes = observations.Modes ?? [],
            ActiveIsoDevices = observations.ActiveIsoDevices ?? [],
            ActiveSustainedDevices = observations.ActiveSustainedDevices ?? [],
            RequiredSuperSpeedDevices = observations.RequiredSuperSpeedDevices ?? [],
            ConfirmedRfExposedDevices = observations.ConfirmedRfExposedDevices ?? [],
            TestFeasibleDevices = observations.TestFeasibleDevices ?? [],
            FailedDevices = observations.FailedDevices ?? [],
            ValidatedEvidence = observations.ValidatedEvidence ?? []
        };
    }
}
