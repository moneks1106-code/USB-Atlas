using UsbAtlas.Core;
namespace UsbAtlas.Tests;

public class PortAndRegressionTests
{
    private static UsbSnapshot Snapshot(params UsbNode[] nodes) => new(Guid.NewGuid(), DateTimeOffset.UtcNow, "fixture", true, nodes, []);
    [Fact] public void CompanionMetadataDoesNotProveRfExposure()
    {
        var receiver = new UsbNode { Id = "receiver", Name = "Receiver", Kind = NodeKind.Device, Role = DeviceRole.Receiver, Speed = LinkSpeed.High };
        var s = Snapshot(receiver);
        Assert.Equal(Assessment.NotAssessable, new UsbRuleEngine(new UsbEvidenceRegistry()).Evaluate(s).Single(f => f.RuleId == "R08").Status);
    }
    [Fact] public void MissingDataAfterReplugDoesNotProveProblemResolved()
    {
        var a = new UsbNode { Id = "a", Kind = NodeKind.Device, Name = "device", VendorId = "1234", ProductId = "5678", SerialNumber = "serial" };
        var b = a with { Id = "b" };
        Finding Finding(Assessment status, string id) => new("R45", id, status, "speed", "", [], [], ["source"], "USB_SPEED", "");
        var comparison = new UsbSnapshotManager("unused").Compare(Snapshot(a), Snapshot(b), [Finding(Assessment.Problem, "a")], [Finding(Assessment.NotAssessable, "b")]);
        Assert.Empty(comparison.ResolvedProblems);
    }
    [Fact] public void DuplicateSerialAtSamePathRemainsWeakIdentity()
    {
        var a = new UsbNode { Id = "a", Kind = NodeKind.Device, Name = "device", InstanceId = "a", VendorId = "1234", ProductId = "5678", SerialNumber = "duplicate" };
        var b = a with { Id = "b", InstanceId = "b" };
        var s = Snapshot(a, b);
        Assert.All(new UsbSnapshotManager("unused").Compare(s, s with { Revision = Guid.NewGuid() }).Devices, d => Assert.Equal("SAME_PATH_ONLY", d.IdentityConfidence));
    }
}
