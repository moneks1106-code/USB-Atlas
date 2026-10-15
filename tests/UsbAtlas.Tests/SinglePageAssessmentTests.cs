using UsbAtlas.Core;
using UsbAtlas.UI;

namespace UsbAtlas.Tests;

public class SinglePageAssessmentTests
{
    private static UsbNode Controller(string pid = "15B6", string vendor = "1022") => Fixture.Controller("c", vendor, pid);
    private static UsbSnapshot Snapshot(params UsbNode[] nodes) => Fixture.Snapshot(nodes);
    private static Finding Finding(Assessment status) => new("fixture", "d", status, "Title", "Explanation", [], [], "");

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
        Assert.Contains("No USB devices", ConfigurationSummary.Create(Snapshot(Controller()), []).Title);
    }

    [Fact]
    public void ProblemsTakePrecedenceOverGapsAndInconsistency()
    {
        var s = Fixture.Simple() with { Consistent = false, Warnings = ["missing"] };
        var result = ConfigurationSummary.Create(s, [Finding(Assessment.Problem), Finding(Assessment.Problem)]);
        Assert.Equal("2 ports report connection errors.", result.Title);
        Assert.Contains("could not be read", result.Body);
    }

    [Fact]
    public void InconsistentScanIsNotCalledHealthy()
    {
        Assert.Contains("inconsistent", ConfigurationSummary.Create(Fixture.Simple() with { Consistent = false }, []).Title);
    }

    [Fact]
    public void NotesAreVisibleWithoutCallingThemErrors()
    {
        var result = ConfigurationSummary.Create(Fixture.Simple(), [Finding(Assessment.Note)]);
        Assert.Equal("No connection errors. One note is worth a look.", result.Title);
        Assert.Contains("not a latency measurement", result.Body);
    }

    [Fact]
    public void CleanObservedConfigurationDoesNotPromiseLatency()
    {
        var result = ConfigurationSummary.Create(Fixture.Simple(), [Finding(Assessment.Ok)]);
        Assert.Equal("No connection errors in the observed USB tree.", result.Title);
        Assert.Contains("not a latency measurement", result.Body);
        Assert.DoesNotContain("could not be read", result.Body);
    }
}
