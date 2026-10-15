using UsbAtlas.Core;
namespace UsbAtlas.Tests;

public class PortAndRegressionTests
{
    private static UsbSnapshot Snapshot(params UsbNode[] nodes) => new(Guid.NewGuid(), DateTimeOffset.UtcNow, "fixture", true, nodes, []);
    [Theory] [InlineData(PortState.Connected, false)] [InlineData(PortState.Unknown, false)] [InlineData(PortState.Empty, true)]
    public void EmptyProtocolSideIsNotNecessarilyAnEmptySocket(PortState companionState, bool available)
    {
        var root = new UsbNode { Id = "root", Kind = NodeKind.RootHub, Name = "root", HubSymbolicLink = "hub-name" };
        var a = new UsbNode { Id = "a", Kind = NodeKind.Port, Name = "Port1", ParentId = "root", State = PortState.Empty, UserConnectable = true, CompanionHub = "hub-name", CompanionPort = 2, PortNumber = 1 };
        var b = a with { Id = "b", PortNumber = 2, State = companionState, CompanionPort = 1 };
        Assert.Equal(available, new UsbPortMapper().IsAvailableTarget(Snapshot(root, a, b), a));
    }
    [Fact] public void MissingCompanionAndPhysicalDataCannotInventAvailability()
    {
        var port = new UsbNode { Id = "p", Kind = NodeKind.Port, Name = "Port", State = PortState.Empty, UserConnectable = true };
        Assert.False(new UsbPortMapper().IsAvailableTarget(Snapshot(port), port));
    }
    [Fact] public void ExternalHubWithoutSerialInvalidatesUserMapAcrossScans()
    {
        var hub = new UsbNode { Id = "h", Kind = NodeKind.Hub, Name = "Hub", VendorId = "1234", ProductId = "5678" };
        var port = new UsbNode { Id = "p", Kind = NodeKind.Port, Name = "Port", ParentId = "h" };
        var a = Snapshot(hub, port); var b = a with { Revision = Guid.NewGuid() }; var mapper = new UsbPortMapper();
        var mapping = mapper.Confirm(a, "p", "Monitor links", []);
        Assert.NotNull(mapper.Resolve(a, "p", [mapping])); Assert.Null(mapper.Resolve(b, "p", [mapping]));
    }
    [Fact] public void CompanionMetadataDoesNotProveRfExposure()
    {
        var receiver = new UsbNode { Id = "receiver", Name = "Receiver", Kind = NodeKind.Device, Role = DeviceRole.Receiver, Speed = LinkSpeed.High };
        var s = Snapshot(receiver);
        Assert.Equal(Assessment.NICHT_BEWERTBAR, new UsbRuleEngine(new UsbEvidenceRegistry()).Evaluate(s).Single(f => f.RuleId == "R08").Status);
    }
    [Fact] public void MissingDataAfterReplugDoesNotProveProblemResolved()
    {
        var a = new UsbNode { Id = "a", Kind = NodeKind.Device, Name = "device", VendorId = "1234", ProductId = "5678", SerialNumber = "serial" };
        var b = a with { Id = "b" };
        Finding Finding(Assessment status, string id) => new("R45", id, status, "speed", "", [], [], ["source"], "USB_SPEED", "");
        var comparison = new UsbSnapshotManager("unused").Compare(Snapshot(a), Snapshot(b), [Finding(Assessment.PROBLEM, "a")], [Finding(Assessment.NICHT_BEWERTBAR, "b")]);
        Assert.Empty(comparison.ResolvedProblems);
    }
    [Fact] public void DuplicateSerialAtSamePathRemainsWeakIdentity()
    {
        var a = new UsbNode { Id = "a", Kind = NodeKind.Device, Name = "device", InstanceId = "a", VendorId = "1234", ProductId = "5678", SerialNumber = "duplicate" };
        var b = a with { Id = "b", InstanceId = "b" };
        var s = Snapshot(a, b);
        Assert.All(new UsbSnapshotManager("unused").Compare(s, s with { Revision = Guid.NewGuid() }).Devices, d => Assert.Equal("SAME_PATH_ONLY", d.IdentityConfidence));
    }
    [Fact] public async Task MeasurementLeaseBlocksHardwareWorkUntilReleased()
    {
        var gate = new DiagnosticActivityGate();
        using var measurement = await gate.EnterAsync();
        var scan = gate.EnterAsync(); Assert.False(scan.IsCompleted);
        measurement.Dispose(); using var scanLease = await scan;
    }
}
