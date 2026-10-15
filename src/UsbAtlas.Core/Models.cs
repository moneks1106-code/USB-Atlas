using System.Text.Json.Serialization;

namespace UsbAtlas.Core;

public enum NodeKind { Controller, RootHub, Hub, Port, Device }
public enum LinkSpeed { Unknown, Low, Full, High, Super, SuperPlus }
public enum PortState { Unknown, Empty, Connected, Error }
public enum DeviceRole { Unknown, Mouse, Keyboard, Gamepad, Receiver, Audio, Camera, Capture, Storage, Hub, InternalControl, Other }
public enum Assessment { Ok, Note, Check, Problem, NotAssessable }
public enum TtMode { Unknown, Single, Multi, NotApplicable }

public sealed record Endpoint(byte Address, byte TransferType, ushort MaxPacketSize, byte Interval,
    byte? InterfaceNumber = null, byte? AlternateSetting = null)
{
    [JsonIgnore] public bool IsInterruptIn => TransferType == 3 && (Address & 0x80) != 0;

    /// <summary>Service interval offered by the descriptor, in microseconds. Not a measured report rate.</summary>
    public double? ServiceIntervalUs(LinkSpeed speed)
    {
        if (TransferType != 3 || Interval == 0) return null;
        if (speed is LinkSpeed.High or LinkSpeed.Super or LinkSpeed.SuperPlus)
            return Interval <= 16 ? 125d * (1 << (Interval - 1)) : null;
        if (speed is LinkSpeed.Full or LinkSpeed.Low)
            return 1000d * Interval;
        return null;
    }
}

/// <summary>One observed node of the USB tree: controller, root hub, hub, logical port or device.</summary>
public sealed record UsbNode
{
    public required string Id { get; init; }
    public required NodeKind Kind { get; init; }
    public required string Name { get; init; }
    public string? ParentId { get; init; }
    public string? ControllerId { get; init; }
    public string? InstanceId { get; init; }
    public string? ParentInstanceId { get; init; }
    public string? ContainerId { get; init; }
    public string? LocationPath { get; init; }
    public string? HubSymbolicLink { get; init; }
    public string? Manufacturer { get; init; }
    public string? SerialNumber { get; init; }
    public string? VendorId { get; init; }
    public string? ProductId { get; init; }
    public DeviceRole Role { get; init; }
    public DeviceRole[] FunctionRoles { get; init; } = [];
    public int HubDepth { get; init; }
    public int? PortNumber { get; init; }
    public int? ConnectionStatus { get; init; }
    public PortState State { get; init; }
    public LinkSpeed Speed { get; init; }
    public bool? SuperSpeedCapable { get; init; }
    public bool? UserConnectable { get; init; }
    public bool? ConnectorTypeC { get; init; }
    public string? CompanionHub { get; init; }
    public int? CompanionPort { get; init; }
    public TtMode Tt { get; init; }
    public bool? MultiTtCapable { get; init; }
    public bool? BusPowered { get; init; }
    public byte? DeviceClass { get; init; }
    public byte? CurrentConfiguration { get; init; }
    public string? DeviceDescriptorHex { get; init; }
    public string? ConfigurationDescriptorHex { get; init; }
    public byte[] InterfaceClasses { get; init; } = [];
    public Endpoint[] OfferedEndpoints { get; init; } = [];
    public Endpoint[] OpenPipes { get; init; } = [];
    public string[] LogicalInterfaces { get; init; } = [];
    public string[] Unknowns { get; init; } = [];

    [JsonIgnore] public bool IsPhysical => Kind is NodeKind.Device or NodeKind.Hub;
    [JsonIgnore] public bool IsHid => DeviceClass == 3 || InterfaceClasses.Contains((byte)3);
    [JsonIgnore] public string? SerialKey => !string.IsNullOrWhiteSpace(SerialNumber) && VendorId != null && ProductId != null
        ? $"{VendorId}:{ProductId}:{SerialNumber}" : null;
}

public sealed record UsbSnapshot(Guid Revision, DateTimeOffset CapturedUtc, string OsVersion,
    bool Consistent, UsbNode[] Nodes, string[] Warnings)
{
    public const int SchemaVersion = 1;
    public int Schema { get; init; } = SchemaVersion;

    public UsbNode? Find(string? id) => Nodes.FirstOrDefault(n => n.Id == id);
    [JsonIgnore] public IEnumerable<UsbNode> Controllers => Nodes.Where(n => n.Kind == NodeKind.Controller);
    [JsonIgnore] public IEnumerable<UsbNode> Devices => Nodes.Where(n => n.IsPhysical);
    [JsonIgnore] public IEnumerable<UsbNode> Ports => Nodes.Where(n => n.Kind == NodeKind.Port);
}

/// <summary>Result of one rule on one node. Every finding names what was observed, what is missing and what it does not prove.</summary>
public sealed record Finding(string RuleId, string NodeId, Assessment Status, string Title, string Explanation,
    string[] Observed, string[] Missing, string[] Sources, string Scope, string Limitations);
public sealed record PortMapping(string PortId, string Label, string HubFingerprint, DateTimeOffset ConfirmedUtc);
public sealed record PlacementProposal(string DeviceId, string RuleId, string? TargetPortId,
    string Instruction, string Reason, string ExpectedChange, string Limitations);
public sealed record DeviceDelta(string? BeforeId, string? AfterId, string IdentityConfidence, string Kind,
    string[] Changes, string? BeforePort, string? AfterPort, string? BeforeController, string? AfterController,
    string[] BeforeHubs, string[] AfterHubs);
public sealed record SnapshotComparison(Guid BeforeRevision, Guid AfterRevision, DeviceDelta[] Devices,
    string[] ResolvedProblems, string[] NewProblems, string Conclusion);

public interface IUsbTopologyScanner
{
    Task<UsbSnapshot> ScanAsync(CancellationToken cancellationToken = default);
}

/// <summary>Serializes hardware access so that two scans never run at the same time.</summary>
public sealed class DiagnosticActivityGate
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<IDisposable> EnterAsync(CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        return new Lease(gate);
    }

    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private int disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0) gate.Release();
        }
    }
}
