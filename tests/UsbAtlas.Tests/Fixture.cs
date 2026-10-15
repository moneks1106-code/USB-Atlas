using UsbAtlas.Core;

namespace UsbAtlas.Tests;

/// <summary>A small, valid tree: controller → root hub → ports → devices, with one external hub.</summary>
internal static class Fixture
{
    public static readonly Endpoint InterruptIn = new(0x81, 3, 64, 1, 0, 0);

    public static UsbNode Controller(string id = "c", string vendor = "1022", string product = "15B6") =>
        new() { Id = id, Kind = NodeKind.Controller, Name = "USB xHCI Compliant Host Controller", ControllerId = id, VendorId = vendor, ProductId = product };

    public static UsbNode RootHub(string controller = "c") =>
        new() { Id = controller + "/root", Kind = NodeKind.RootHub, Name = "Root hub", ParentId = controller, ControllerId = controller };

    public static UsbNode Port(string hub, int number, PortState state = PortState.Connected) =>
        new() { Id = $"{hub}/{number}", Kind = NodeKind.Port, Name = $"Port {number}", ParentId = hub, ControllerId = "c", PortNumber = number, State = state, UserConnectable = true };

    public static UsbNode Device(string port, string name = "Gaming Mouse", LinkSpeed speed = LinkSpeed.High, bool hid = true) =>
        new()
        {
            Id = port + "/device", Kind = NodeKind.Device, Name = name, ParentId = port, ControllerId = "c",
            VendorId = "1234", ProductId = "5678", SerialNumber = "serial-" + port, Speed = speed, Role = DeviceRole.Mouse,
            InterfaceClasses = hid ? [3] : [], OpenPipes = hid ? [InterruptIn] : [], OfferedEndpoints = hid ? [InterruptIn] : []
        };

    public static UsbNode Hub(string port, string name = "Generic USB Hub") =>
        new() { Id = port + "/device", Kind = NodeKind.Hub, Name = name, ParentId = port, ControllerId = "c", Speed = LinkSpeed.High, Tt = TtMode.Multi, HubDepth = 1, Role = DeviceRole.Hub };

    public static UsbSnapshot Snapshot(params UsbNode[] nodes) =>
        new(Guid.NewGuid(), DateTimeOffset.UtcNow, "fixture", true, nodes, []);

    /// <summary>Controller, root hub, two ports: a mouse on port 1, port 2 empty.</summary>
    public static UsbSnapshot Simple() =>
        Snapshot(Controller(), RootHub(), Port("c/root", 1), Device("c/root/1"), Port("c/root", 2, PortState.Empty));
}
