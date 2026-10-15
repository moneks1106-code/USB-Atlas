using UsbAtlas.Core;

namespace UsbAtlas.Tests;

public class RuleEngineTests
{
    private readonly UsbRuleEngine engine = new();

    private Finding[] Evaluate(params UsbNode[] nodes) => engine.Evaluate(Fixture.Snapshot(nodes));

    [Fact]
    public void HighSpeedHidWithIntervalOneOffers125Microseconds()
    {
        var finding = Evaluate(Fixture.Device("p")).Single(f => f.RuleId == UsbRuleEngine.HidServiceInterval);
        Assert.Equal(Assessment.Ok, finding.Status);
        Assert.Contains("125 µs", finding.Explanation);
        Assert.Contains("8000 reports per second", finding.Explanation);
        Assert.Contains("not measured", finding.Limitations);
    }

    [Fact]
    public void FullSpeedHidReportsMillisecondInterval()
    {
        var device = Fixture.Device("p", speed: LinkSpeed.Full) with { OpenPipes = [new(0x81, 3, 8, 2)] };
        var finding = Evaluate(device).Single(f => f.RuleId == UsbRuleEngine.HidServiceInterval);
        Assert.Contains("2 ms", finding.Explanation);
        Assert.Contains("500 reports per second", finding.Explanation);
    }

    [Fact]
    public void FastestOpenInterruptPipeIsReported()
    {
        var device = Fixture.Device("p") with { OpenPipes = [new(0x82, 3, 64, 4), new(0x81, 3, 64, 1)] };
        var finding = Evaluate(device).Single(f => f.RuleId == UsbRuleEngine.HidServiceInterval);
        Assert.Contains("0x81", finding.Explanation);
    }

    [Fact]
    public void NonHidAndHidWithoutOpenInterruptPipeGetNoIntervalFinding()
    {
        Assert.DoesNotContain(Evaluate(Fixture.Device("p", hid: false)), f => f.RuleId == UsbRuleEngine.HidServiceInterval);
        Assert.DoesNotContain(Evaluate(Fixture.Device("p") with { OpenPipes = [] }), f => f.RuleId == UsbRuleEngine.HidServiceInterval);
        Assert.DoesNotContain(Evaluate(Fixture.Device("p", speed: LinkSpeed.Unknown)), f => f.RuleId == UsbRuleEngine.HidServiceInterval);
    }

    [Theory]
    [InlineData(LinkSpeed.Super, Assessment.Ok)]
    [InlineData(LinkSpeed.SuperPlus, Assessment.Ok)]
    [InlineData(LinkSpeed.High, Assessment.Note)]
    [InlineData(LinkSpeed.Full, Assessment.Note)]
    public void SuperSpeedCapableDeviceIsCheckedAgainstItsLink(LinkSpeed speed, Assessment expected)
    {
        var device = Fixture.Device("p", "SSD", speed, hid: false) with { SuperSpeedCapable = true };
        Assert.Equal(expected, Evaluate(device).Single(f => f.RuleId == UsbRuleEngine.SuperSpeedLink).Status);
    }

    [Fact]
    public void UnknownCapabilityOrSpeedGivesNoSuperSpeedFinding()
    {
        Assert.DoesNotContain(Evaluate(Fixture.Device("p", hid: false)), f => f.RuleId == UsbRuleEngine.SuperSpeedLink);
        var unknownSpeed = Fixture.Device("p", hid: false, speed: LinkSpeed.Unknown) with { SuperSpeedCapable = true };
        Assert.DoesNotContain(Evaluate(unknownSpeed), f => f.RuleId == UsbRuleEngine.SuperSpeedLink);
    }

    [Fact]
    public void PortWithConnectionErrorIsAProblem()
    {
        var port = Fixture.Port("c/root", 3, PortState.Error) with { ConnectionStatus = 4 };
        var finding = Evaluate(Fixture.RootHub(), port).Single();
        Assert.Equal(UsbRuleEngine.PortError, finding.RuleId);
        Assert.Equal(Assessment.Problem, finding.Status);
        Assert.Equal(port.Id, finding.NodeId);
        Assert.Contains("USB_CONNECTION_STATUS=4", finding.Explanation);
    }

    [Fact]
    public void HealthyTreeHasNoProblemsOrNotes()
    {
        var findings = engine.Evaluate(Fixture.Simple());
        Assert.DoesNotContain(findings, f => f.Status != Assessment.Ok);
        Assert.All(findings, f => { Assert.NotEmpty(f.Sources); Assert.NotEmpty(f.Limitations); Assert.NotEmpty(f.Observed); });
    }

    [Fact]
    public void InconsistentSnapshotIsNotEvaluated()
    {
        Assert.Empty(engine.Evaluate(Fixture.Simple() with { Consistent = false }));
    }
}

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
