using UsbAtlas.Core;
namespace UsbAtlas.Tests;

public class AdvancedRuleTests
{
    private readonly UsbRuleEngine engine = new(new UsbEvidenceRegistry());
    private static readonly Endpoint Interrupt = new(0x81, 3, 64, 1, 0, 0);
    private static UsbNode Device(string id, LinkSpeed speed, DeviceRole role = DeviceRole.Mouse) => new()
    { Id = id, Kind = NodeKind.Device, Name = id, ParentId = "p" + id, ControllerId = "c", VendorId = "1234", ProductId = "5678", SerialNumber = id,
      Role = role, Speed = speed, InterfaceClasses = [3], OfferedEndpoints = [Interrupt], OpenPipes = [Interrupt] };
    private static UsbSnapshot Shared(LinkSpeed aSpeed = LinkSpeed.Full, TtMode tt = TtMode.Single, LinkSpeed bSpeed = LinkSpeed.Full)
    {
        var hub = new UsbNode { Id = "hub", Name = "Hub", Kind = NodeKind.Hub, ParentId = "root", Speed = LinkSpeed.High, Tt = tt };
        var root = new UsbNode { Id = "root", Name = "root", Kind = NodeKind.RootHub };
        var a = Device("a", aSpeed); var b = Device("b", bSpeed, DeviceRole.Camera);
        var pa = new UsbNode { Id = "pa", Name = "port", Kind = NodeKind.Port, ParentId = "hub" }; var pb = pa with { Id = "pb" };
        return new(Guid.NewGuid(), DateTimeOffset.UtcNow, "fixture", true, [root, hub, pa, pb, a, b], []);
    }
    private Finding Evaluate(UsbSnapshot snapshot, string rule, RuleObservations inputs) => engine.Evaluate(snapshot, inputs).First(f => f.NodeId == "a" && f.RuleId == rule);
    private static RuleObservations Load(UsbSnapshot s) => RuleObservations.Empty(s.Revision) with
    { ActiveIsoDevices = ["b"], TestFeasibleDevices = ["a"], ValidatedEvidence = new() { ["ACTIVE_ISO:b"] = "fixture-stream-collector", ["TEST_FEASIBLE:a"] = "fixture-valid-target" } };

    [Theory] [InlineData(TtMode.Single, Assessment.PRUEFEN)] [InlineData(TtMode.Multi, Assessment.NICHT_BEWERTBAR)] [InlineData(TtMode.Unknown, Assessment.NICHT_BEWERTBAR)]
    public void SingleTtRiskNeedsActiveFullSpeedIsoAndKnownTt(TtMode tt, Assessment expected)
    { var s = Shared(tt: tt); Assert.Equal(expected, Evaluate(s, "R06", Load(s)).Status); }
    [Fact] public void OfferedIsoWithoutCollectorNeverTriggersContention()
    {
        var s = Shared(); var inputs = Load(s) with { ValidatedEvidence = [] };
        Assert.Equal(Assessment.NICHT_BEWERTBAR, Evaluate(s, "R05", inputs).Status);
    }
    [Fact] public void SharedLoadIsHypothesisWithEvidenceAndNeverAutomaticProblem()
    {
        var s = Shared(); var f = Evaluate(s, "R05", Load(s));
        Assert.Equal(Assessment.PRUEFEN, f.Status); Assert.Contains("fixture-stream-collector", f.Sources); Assert.NotEmpty(f.Observed); Assert.Empty(f.Missing);
    }
    [Fact] public void SeparateUsb2AndSsTrafficCannotTriggerIsoSharingRule()
    { var s = Shared(bSpeed: LinkSpeed.Super); Assert.Equal(Assessment.NICHT_BEWERTBAR, Evaluate(s, "R05", Load(s)).Status); }
    [Theory] [InlineData(false, Assessment.HINWEIS)] [InlineData(true, Assessment.PROBLEM)]
    public void RequiredSuperSpeedNeedsAnEvidenceBackedFunctionRequirement(bool evidence, Assessment expected)
    {
        var s = Shared(); s = s with { Nodes = s.Nodes.Select(n => n.Id == "a" ? n with { SuperSpeedCapable = true } : n).ToArray() };
        var inputs = RuleObservations.Empty(s.Revision) with { RequiredSuperSpeedDevices = ["a"], ValidatedEvidence = evidence ? new() { ["SS_REQUIRED:a"] = "fixture-vendor-mode-requirement" } : [] };
        Assert.Equal(expected, Evaluate(s, "R45", inputs).Status);
    }
    [Fact] public void HubAndHighPollingCapacityWithoutModeContractAreNotRisk()
    { var s = Shared(LinkSpeed.High); Assert.Equal(Assessment.NICHT_BEWERTBAR, Evaluate(s, "R01", Load(s)).Status); }
    [Fact] public void VerifiedModeBehindHubCanOfferReversibleTest()
    {
        var s = Shared(LinkSpeed.High); var inputs = Load(s) with { Modes = [new("a", 0x81, 8000, true, "fixture-verified-mode", true, 0)] };
        Assert.Equal(Assessment.PRUEFEN, Evaluate(s, "R01", inputs).Status);
    }
    [Fact] public void UnvalidatedPeerModeCannotTriggerTwoHighPollingRule()
    {
        var s = Shared(LinkSpeed.High, bSpeed: LinkSpeed.High); var inputs = Load(s) with { Modes = [new("a", 0x81, 8000, true, "fixture", true, 0), new("b", 0x81, 8000, true, "", true, 0)] };
        Assert.Equal(Assessment.NICHT_BEWERTBAR, Evaluate(s, "R03", inputs).Status);
    }
    [Fact] public void NativeLinkContractCannotBeFakedFromMarketingName()
    {
        var s = Shared(); var inputs = RuleObservations.Empty(s.Revision) with { Modes = [new("a", 0x81, 8000, false, "fixture-marketing-8k", true, 0)] };
        Assert.Equal(Assessment.NICHT_BEWERTBAR, Evaluate(s, "R13", inputs).Status);
    }
    [Fact] public void ReplugRuleUsesOnlyConfirmedPhysicalIdentity()
    {
        var s = Shared(); var after = s with { Revision = Guid.NewGuid(), Nodes = s.Nodes.Select(n => n.Id == "a" ? n with { Id = "a2", ParentId = "pb" } : n).ToArray() };
        var comparison = new UsbSnapshotManager("unused").Compare(s, after);
        var f = Assert.Single(engine.EvaluateChanges(s, after, comparison));
        Assert.Equal("R30", f.RuleId); Assert.Equal(Assessment.HINWEIS, f.Status);
    }
    [Fact] public void UnclearIdentityCannotTriggerReplugRule()
    {
        var s = Shared(); s = s with { Nodes = s.Nodes.Select(n => n with { SerialNumber = null }).ToArray() };
        var after = s with { Revision = Guid.NewGuid(), Nodes = s.Nodes.Select(n => n.Id == "a" ? n with { Id = "a2", ParentId = "pb" } : n).ToArray() };
        Assert.Empty(engine.EvaluateChanges(s, after, new UsbSnapshotManager("unused").Compare(s, after)));
    }
}
