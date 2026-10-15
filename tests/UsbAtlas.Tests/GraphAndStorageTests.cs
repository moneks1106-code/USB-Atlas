using UsbAtlas.Core;

namespace UsbAtlas.Tests;

public class DomainGraphTests
{
    [Fact]
    public void HubChainListsHubsNearestFirstUpToTheRootHub()
    {
        var hub = Fixture.Hub("c/root/1");
        var device = Fixture.Device(hub.Id + "/2") with { HubDepth = 1 };
        var snapshot = Fixture.Snapshot(Fixture.Controller(), Fixture.RootHub(), Fixture.Port("c/root", 1), hub, Fixture.Port(hub.Id, 2), device);
        var chain = new UsbDomainGraph(snapshot).HubChain(device);
        Assert.Equal([hub.Id, "c/root"], chain.Select(h => h.Id));
    }

    [Fact]
    public void HubChainToleratesCycles()
    {
        var a = new UsbNode { Id = "a", Kind = NodeKind.Hub, Name = "a", ParentId = "b" };
        var b = new UsbNode { Id = "b", Kind = NodeKind.Hub, Name = "b", ParentId = "a" };
        var device = new UsbNode { Id = "d", Kind = NodeKind.Device, Name = "d", ParentId = "a" };
        Assert.Equal(2, new UsbDomainGraph(Fixture.Snapshot(a, b, device)).HubChain(device).Length);
    }
}

public class SnapshotManagerTests
{
    private static string TempDirectory() => Path.Combine(Path.GetTempPath(), "UsbAtlasTests", Guid.NewGuid().ToString());

    [Fact]
    public async Task LatestSnapshotRoundTripsAndInconsistentScansAreRejected()
    {
        var directory = TempDirectory();
        var manager = new UsbSnapshotManager(directory);
        var snapshot = Fixture.Simple();
        try
        {
            Assert.Null(await manager.LoadLatestAsync());
            await manager.SaveLatestAsync(snapshot);
            var loaded = await manager.LoadLatestAsync();
            Assert.NotNull(loaded);
            Assert.Equal(snapshot.Revision, loaded.Revision);
            Assert.Equal(snapshot.Nodes.Length, loaded.Nodes.Length);
            Assert.Equal(LinkSpeed.High, loaded.Find("c/root/1/device")!.Speed);
            await Assert.ThrowsAsync<InvalidOperationException>(() => manager.SaveLatestAsync(snapshot with { Consistent = false }));
            Assert.Single(Directory.EnumerateFiles(directory));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task SnapshotWithDanglingParentIsRejected()
    {
        var manager = new UsbSnapshotManager(TempDirectory());
        var orphan = Fixture.Snapshot(Fixture.Device("missing-port"));
        await Assert.ThrowsAsync<InvalidDataException>(() => manager.SaveLatestAsync(orphan));
    }

    [Fact]
    public async Task MeasurementLeaseBlocksHardwareWorkUntilReleased()
    {
        var gate = new DiagnosticActivityGate();
        using var measurement = await gate.EnterAsync();
        var scan = gate.EnterAsync();
        Assert.False(scan.IsCompleted);
        measurement.Dispose();
        using var scanLease = await scan;
    }
}
