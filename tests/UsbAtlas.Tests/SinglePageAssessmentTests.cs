using UsbAtlas.Core;
using UsbAtlas.UI;

namespace UsbAtlas.Tests;

public class SinglePageAssessmentTests
{
    private static UsbNode Controller(string pid = "15B6", string vendor = "1022") => Fixture.Controller("c", vendor, pid);
    private static UsbSnapshot Snapshot(params UsbNode[] nodes) => Fixture.Snapshot(nodes);
    private static Finding Finding(Assessment status) => new("fixture", "d", status, "Title", "Explanation", [], [], [], "", "");

    [Theory] [InlineData("15B6")] [InlineData("15b7")] [InlineData("15B8")]
    public void ExactAmdSocModelsHaveQualifiedInference(string id)
    {
        var c = Controller(id);
        var hint = ControllerPlacementHint.For(Snapshot(c), c);
        Assert.Equal("Likely CPU/SoC", hint.Label);
        Assert.NotNull(hint.Source);
    }

    [Fact]
    public void ChipsetIsQualifiedAndSourced()
    {
        var c = Controller("43FC");
        var hint = ControllerPlacementHint.For(Snapshot(c), c);
        Assert.Equal("Likely chipset", hint.Label);
        Assert.Contains("prom21-xhci", hint.Source);
    }

    [Theory] [InlineData("43F4", "1022")] [InlineData("15B6", "8086")] [InlineData("FFFF", "1022")]
    public void BridgeOtherVendorAndUnknownIdsAreNotGuessed(string id, string vendor)
    {
        var c = Controller(id, vendor);
        Assert.Equal("Connection origin unknown", ControllerPlacementHint.For(Snapshot(c), c).Label);
    }

    [Fact]
    public void RootUsesItsControllerRatherThanItsOwnUsbIds()
    {
        var c = Controller("43FC");
        var root = Fixture.RootHub() with { VendorId = "1022", ProductId = "15B6" };
        Assert.Equal(ControllerPlacementHint.For(Snapshot(c, root), c), ControllerPlacementHint.For(Snapshot(c, root), root));
        Assert.Equal("Connection origin unknown", ControllerPlacementHint.For(Snapshot(root with { ParentId = null }), root).Label);
    }

    [Fact]
    public void ReferencedNonControllerDoesNotSupplyPlacement()
    {
        var fake = Controller() with { Kind = NodeKind.Device };
        var root = Fixture.RootHub();
        Assert.Equal("Connection origin unknown", ControllerPlacementHint.For(Snapshot(fake, root), root).Label);
    }

    [Fact]
    public void NoScanAndNoDevicesDoNotGetCleanBillOfHealth()
    {
        Assert.Contains("first scan", ConfigurationSummary.Create(null, []).Title);
        Assert.Contains("cannot be assessed", ConfigurationSummary.Create(Snapshot(Controller()), []).Title);
    }

    [Fact]
    public void ProblemsTakePrecedenceOverGapsAndInconsistency()
    {
        var s = Fixture.Simple() with { Consistent = false, Warnings = ["missing"] };
        var result = ConfigurationSummary.Create(s, [Finding(Assessment.Problem), Finding(Assessment.NotAssessable)]);
        Assert.Equal("Your USB configuration has one detected problem.", result.Title);
        Assert.Contains("cannot yet be assessed", result.Body);
    }

    [Fact]
    public void InconsistentScanIsNotCalledHealthy()
    {
        Assert.Contains("reliably", ConfigurationSummary.Create(Fixture.Simple() with { Consistent = false }, []).Title);
    }

    [Fact]
    public void UnknownRulesQualifyUnremarkableObservedConfiguration()
    {
        var result = ConfigurationSummary.Create(Fixture.Simple(), [Finding(Assessment.NotAssessable)]);
        Assert.Contains("observed", result.Title);
        Assert.Contains("Missing data", result.Body);
    }

    [Fact]
    public void TestNeedsAreVisibleWithoutCallingThemProblems()
    {
        Assert.Contains("Further checks", ConfigurationSummary.Create(Fixture.Simple(), [Finding(Assessment.Check)]).Title);
    }

    [Fact]
    public void CleanObservedConfigurationDoesNotPromiseLatency()
    {
        var result = ConfigurationSummary.Create(Fixture.Simple(), [Finding(Assessment.Ok)]);
        Assert.Equal("Your observed USB configuration looks healthy.", result.Title);
        Assert.Contains("not a latency measurement", result.Body);
    }
}
