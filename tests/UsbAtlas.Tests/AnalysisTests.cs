using UsbAtlas.Core;
namespace UsbAtlas.Tests;

public class AnalysisTests
{
    private static UsbNode Device(string id = "dev", string parent = "port", string? serial = "s") => new()
    { Id = id, Name = "Testgerät", Kind = NodeKind.Device, ParentId = parent, ControllerId = "c", InstanceId = id, VendorId = "1234", ProductId = "5678", SerialNumber = serial, Speed = LinkSpeed.High };
    private static UsbSnapshot Snapshot(params UsbNode[] nodes) => new(Guid.NewGuid(), DateTimeOffset.UtcNow, "fixture", true, nodes, []);
    private static readonly UsbRuleEngine Engine = new(new UsbEvidenceRegistry());
    private static Finding Rule(UsbSnapshot s, string id, RuleObservations? inputs = null) => Engine.Evaluate(s, inputs).First(f => f.RuleId == id);

    [Fact] public void All45RulesHaveExplicitObservabilityContracts()
    {
        var s = Snapshot(Device()); var findings = Engine.Evaluate(s);
        Assert.Equal(45, findings.Length);
        Assert.All(findings, f => { Assert.NotEmpty(f.Sources); Assert.NotEmpty(f.Limitations); if (f.Status == Assessment.NotAssessable) Assert.NotEmpty(f.Missing); });
        Assert.DoesNotContain(findings, f => f.Status is Assessment.Problem or Assessment.Check);
    }
    [Fact] public void FullSpeedWithoutVerifiedModeIsNotAProblem()
    { var s = Snapshot(Device() with { Speed = LinkSpeed.Full, OpenPipes = [new(0x81, 3, 64, 1)] }); Assert.Equal(Assessment.NotAssessable, Rule(s, "R13").Status); }
    [Theory] [InlineData(DeviceRole.Mouse)] [InlineData(DeviceRole.Gamepad)]
    public void VerifiedOrdinary8kFullSpeedContractIsIncompatible(DeviceRole role)
    {
        var endpoint = new Endpoint(0x81, 3, 64, 1, 0, 0);
        var s = Snapshot(Device() with { Role = role, Speed = LinkSpeed.Full, OpenPipes = [endpoint], OfferedEndpoints = [endpoint] });
        var input = RuleObservations.Empty(s.Revision) with { Modes = [new("dev", 0x81, 8000, true, "fixture-contract", true, 0)] };
        Assert.Equal(Assessment.Problem, Rule(s, "R13", input).Status);
    }
    [Fact] public void MalformedManufacturerProfileCannotTriggerProblem()
    {
        var s = Snapshot(Device() with { Speed = LinkSpeed.Full, OpenPipes = [new(0x81, 3, 64, 1)] });
        var input = RuleObservations.Empty(s.Revision) with { Modes = [new("dev", 0x81, 8000, true, "", true)] };
        Assert.Equal(Assessment.NotAssessable, Rule(s, "R13", input).Status);
    }
    [Fact] public void DescriptorIntervalIsNotMeasuredPolling()
    { var s = Snapshot(Device() with { InterfaceClasses = [3], OpenPipes = [new(0x81, 3, 64, 1)] }); Assert.Equal(Assessment.Ok, Rule(s, "R29").Status); Assert.DoesNotContain(Engine.Evaluate(s), f => f.Status == Assessment.Problem); }
    [Theory] [InlineData((byte)1)] [InlineData((byte)2)]
    public void OfferedCameraEndpointDoesNotProveActiveIso(byte type)
    {
        var s = Snapshot(Device() with { Role = DeviceRole.Camera, OfferedEndpoints = [new(0x81, type, 512, 1)] });
        Assert.Equal(Assessment.NotAssessable, Rule(s, "R44").Status);
        Assert.Equal(Assessment.NotAssessable, Rule(s, "R05").Status);
    }
    [Fact] public void SameControllerIsNotSharedRootBandwidth()
    {
        var a = Device(); var b = Device("b", "port2"); var s = Snapshot(a, b);
        Assert.False(new UsbDomainGraph(s).SharesUpstream(a, b));
        Assert.DoesNotContain(Engine.Evaluate(s), f => f.Status == Assessment.Problem);
    }
    [Fact] public void Usb2AndSsSidesOfPhysicalHubAreSeparateDomains()
    {
        var hub = new UsbNode { Id = "hub", Name = "Hub", Kind = NodeKind.Hub, Speed = LinkSpeed.High };
        var port = new UsbNode { Id = "port", Name = "Port", Kind = NodeKind.Port, ParentId = "hub" };
        var a = Device(); var b = Device("b") with { Speed = LinkSpeed.Super };
        var s = Snapshot(hub, port, a, b);
        Assert.False(new UsbDomainGraph(s).SharesUpstream(a, b));
    }
    [Theory] [InlineData(TtMode.Single, true)] [InlineData(TtMode.Multi, false)] [InlineData(TtMode.Unknown, false)]
    public void TransactionTranslatorRequiresKnownActualMode(TtMode mode, bool shared)
    {
        var hub = new UsbNode { Id = "hub", Kind = NodeKind.Hub, Name = "Hub", Speed = LinkSpeed.High, Tt = mode };
        var p = new UsbNode { Id = "p", Name = "Port", Kind = NodeKind.Port, ParentId = "hub" };
        var q = p with { Id = "q" }; var a = Device("a", "p") with { Speed = LinkSpeed.Full }; var b = Device("b", "q") with { Speed = LinkSpeed.Full };
        var graph = new UsbDomainGraph(Snapshot(hub, p, q, a, b));
        Assert.Equal(shared, graph.TransactionTranslator(a) != null && graph.TransactionTranslator(a) == graph.TransactionTranslator(b));
    }
    [Fact] public void IdenticalTopologyDoesNotProduceArtificialDeltas()
    { var a = Snapshot(Device()); var b = a with { Revision = Guid.NewGuid() }; Assert.Equal("Unchanged", new UsbSnapshotManager("unused").Compare(a, b).Devices.Single().Kind); }
    [Fact] public void SerialIdentityTracksControllerAndPortChange()
    {
        var a = Snapshot(Device()); var b = Snapshot(Device("new", "new-port") with { ControllerId = "c2" });
        var delta = new UsbSnapshotManager("unused").Compare(a, b).Devices.Single();
        Assert.Equal("UNIQUE_SERIAL", delta.IdentityConfidence); Assert.Contains("Host controller changed", delta.Changes);
    }
    [Fact] public void NoSerialReplugNeedsExplicitConfirmation()
    {
        var a = Snapshot(Device(serial: null)); var b = Snapshot(Device("new", "new-port", null)); var manager = new UsbSnapshotManager("unused");
        Assert.Contains(manager.Compare(a, b).Devices, d => d.IdentityConfidence == "AMBIGUOUS");
        Assert.Equal("USER_CONFIRMED", manager.Compare(a, b, manualMatches: new Dictionary<string, string> { ["dev"] = "new" }).Devices.Single().IdentityConfidence);
    }
    [Fact] public void DuplicateSerialsAreNotTrustedAcrossReplug()
    {
        var a = Snapshot(Device(), Device("other")); var b = Snapshot(Device("new"), Device("other2"));
        Assert.DoesNotContain(new UsbSnapshotManager("unused").Compare(a, b).Devices, d => d.IdentityConfidence == "UNIQUE_SERIAL");
    }
    [Fact] public void DisconnectDoesNotCountAsProblemResolved()
    {
        var a = Snapshot(Device()); var b = Snapshot();
        var finding = new Finding("R13", "dev", Assessment.Problem, "", "", ["contract"], [], ["source"], "USB_SPEED", "");
        Assert.Empty(new UsbSnapshotManager("unused").Compare(a, b, [finding], []).ResolvedProblems);
    }
    [Fact] public void InconsistentSnapshotCannotBeCompared()
    { var a = Snapshot(Device()); Assert.Throws<InvalidOperationException>(() => new UsbSnapshotManager("unused").Compare(a, a with { Consistent = false })); }
    [Fact] public void StaleObservationsAreRejected()
    { var s = Snapshot(Device()); Assert.Throws<ArgumentException>(() => Engine.Evaluate(s, RuleObservations.Empty(Guid.NewGuid()))); }
}
