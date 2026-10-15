using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using UsbAtlas.Core;
using static UsbAtlas.Windows.UsbIoctl;

namespace UsbAtlas.Windows;

public sealed class UsbTopologyScanner(DiagnosticActivityGate gate) : IUsbTopologyScanner
{
    public async Task<UsbSnapshot> ScanAsync(CancellationToken cancellationToken = default)
    {
        using var lease = await gate.EnterAsync(cancellationToken);
        return await Task.Run(() => ScanStable(cancellationToken), cancellationToken);
    }

    private static UsbSnapshot ScanStable(CancellationToken token)
    {
        using var changes = new ChangeMonitor();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var generation = changes.Generation;
            var a = Capture(token);
            var b = Capture(token);
            if (generation == changes.Generation && Fingerprint(a) == Fingerprint(b))
                return b with { Consistent = true, Warnings = b.Warnings.Concat(changes.Available ? [] : new[] { "Hotplug notifications unavailable; consistency checked with two complete verification scans." }).ToArray() };
        }
        throw new IOException("USB topology changed during capture. Leave devices connected briefly and refresh again. The previous snapshot is retained.");
    }

    private static string Fingerprint(UsbSnapshot s) => string.Join('\n', s.Nodes.OrderBy(n => n.Id).Select(n =>
        $"{n.Id}|{n.ParentId}|{n.InstanceId}|{n.ContainerId}|{n.SerialKey}|{n.ConnectionStatus}|{n.Speed}|{n.CurrentConfiguration}|{n.ConfigurationDescriptorHex}|{string.Join(',', n.OpenPipes)}|{string.Join(',', n.Unknowns)}"));

    private static UsbSnapshot Capture(CancellationToken token)
    {
        var inventory = new PnpInventory();
        var pnp = inventory.Read();
        var nodes = new List<UsbNode>();
        var warnings = new List<string>();
        var controllers = inventory.Interfaces(NativeMethods.HostControllerGuid);
        var hubs = inventory.Interfaces(NativeMethods.HubGuid);
        foreach (var controller in controllers)
        {
            token.ThrowIfCancellationRequested();
            var device = pnp.FirstOrDefault(p => Eq(p.InstanceId, controller.InstanceId));
            var id = controller.InstanceId;
            var (vendor, product) = PciIds(id);
            nodes.Add(new UsbNode { Id = id, Kind = NodeKind.Controller, Name = device?.Name ?? "USB controller", InstanceId = id,
                LocationPath = device?.LocationPath, VendorId = vendor, ProductId = product,
                ControllerId = id, ControllerClass = new UsbControllerClassifier().Classify(vendor, product) });
            try
            {
                using var host = new UsbIoctl(controller.Path);
                var rootName = host.Name(258);
                if (string.IsNullOrEmpty(rootName)) throw new InvalidDataException("Root hub name is missing.");
                ReadHub(rootName, id + "/root", id, id, 0, null, hubs, pnp, nodes, token);
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidDataException)
            { warnings.Add($"{device?.Name ?? id}: {ex.Message}"); }
        }
        if (controllers.Length == 0) warnings.Add("No host controllers were captured. This does not prove that the system has no USB devices.");
        return new(Guid.NewGuid(), DateTimeOffset.UtcNow, Environment.OSVersion.VersionString, false, nodes.ToArray(), warnings.ToArray());
    }

    private static void ReadHub(string path, string id, string parent, string controller, int depth, UsbNode? physical,
        (string Path, string InstanceId)[] hubs, PnpDevice[] pnp, List<UsbNode> nodes, CancellationToken token)
    {
        if (depth > 7) throw new InvalidDataException("Unusual hub depth; recursion limited.");
        var hubInterface = hubs.FirstOrDefault(h => Normalize(h.Path) == Normalize(path));
        var hubPnp = pnp.FirstOrDefault(p => Eq(p.InstanceId, hubInterface.InstanceId));
        var hubNode = physical ?? new UsbNode { Id = id, ParentId = parent, ControllerId = controller, Kind = NodeKind.RootHub,
            Name = "Root-Hub", InstanceId = hubPnp?.InstanceId, LocationPath = hubPnp?.LocationPath, HubDepth = depth };
        hubNode = hubNode with { HubSymbolicLink = path };
        try
        {
            using var hub = new UsbIoctl(path);
            var info = hub.Get(258, 76);
            if (info.Length < 7) throw new InvalidDataException("Hub descriptor is incomplete.");
            int count = info[6];
            var unknowns = new List<string>(hubNode.Unknowns);
            bool? multiCapable = null, powered = null;
            var tt = TtMode.Unknown;
            try
            {
                var caps = hub.Get(276, 4);
                if (caps.Length < 4) throw new InvalidDataException("Hub flags are incomplete.");
                var flags = U32(caps, 0);
                multiCapable = (flags & 4) != 0; powered = (flags & 64) != 0;
                tt = depth == 0 || (flags & 2) == 0 ? TtMode.NotApplicable : (flags & 8) != 0 ? TtMode.Multi : TtMode.Single;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidDataException) { unknowns.Add("TT/hub power: " + ex.Message); }
            hubNode = hubNode with { Tt = tt, MultiTtCapable = multiCapable, BusPowered = powered, Unknowns = unknowns.ToArray() };
            nodes.Add(hubNode);
            for (int port = 1; port <= count; port++)
            {
                token.ThrowIfCancellationRequested();
                ReadPort(hub, hubNode, port, hubs, pnp, nodes, token);
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidDataException)
        { nodes.Add(hubNode with { Unknowns = hubNode.Unknowns.Append("Hub ports unknown: " + ex.Message).ToArray() }); }
    }

    private static void ReadPort(UsbIoctl hub, UsbNode hubNode, int number, (string Path, string InstanceId)[] hubs,
        PnpDevice[] pnp, List<UsbNode> nodes, CancellationToken token)
    {
        var id = hubNode.Id + "/" + number;
        var port = new UsbNode { Id = id, Kind = NodeKind.Port, ParentId = hubNode.Id, ControllerId = hubNode.ControllerId,
            Name = $"Port {number}", PortNumber = number, HubDepth = hubNode.HubDepth };
        var unknowns = new List<string>();
        try
        {
            var props = hub.Get(278, 8192, number);
            if (props.Length < 16) throw new InvalidDataException("Port properties are incomplete.");
            uint flags = U32(props, 8);
            port = port with { UserConnectable = (flags & 1) != 0, ConnectorTypeC = (flags & 8) != 0,
                CompanionPort = U16(props, 14), CompanionHub = props.Length > 16 ? Encoding.Unicode.GetString(props, 16, (props.Length - 16) & ~1).TrimEnd('\0') : null };
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidDataException) { unknowns.Add("Socket properties: " + ex.Message); }
        byte[] connection;
        try
        {
            connection = hub.Get(274, 35 + 11 * 32, number);
            if (connection.Length < 35) throw new InvalidDataException("Connection information is incomplete.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidDataException)
        { nodes.Add(port with { Unknowns = unknowns.Append("Connection: " + ex.Message).ToArray() }); return; }
        int status = (int)U32(connection, 31);
        port = port with { State = status == 0 ? PortState.Empty : status == 1 ? PortState.Connected : status is >= 2 and <= 8 ? PortState.Error : PortState.Unknown, ConnectionStatus = status };
        if (status is 9 or 10) unknowns.Add("Windows reports a temporary enumeration/reset state; refresh again.");
        nodes.Add(port with { Unknowns = unknowns.ToArray() });
        if (status != 1) return;
        unknowns.Clear();
        LinkSpeed speed = connection[23] switch { 0 => LinkSpeed.Low, 1 => LinkSpeed.Full, 2 => LinkSpeed.High, 3 => LinkSpeed.Super, _ => LinkSpeed.Unknown };
        bool? capable = null;
        try
        {
            var v2 = hub.Get(279, 16, number, b => { Put(b, 4, 16); Put(b, 8, 4); });
            if (v2.Length < 16) throw new InvalidDataException("EX_V2 is incomplete.");
            uint flags = U32(v2, 12); capable = (flags & 2) != 0;
            if ((flags & 4) != 0) speed = LinkSpeed.SuperPlus;
            else if ((flags & 1) != 0) speed = LinkSpeed.Super;
            if (speed >= LinkSpeed.Super && (flags & 1) == 0) { speed = LinkSpeed.Unknown; unknowns.Add("Conflicting EX/EX_V2 speed reports."); }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidDataException) { unknowns.Add("SuperSpeed capability: " + ex.Message); }
        string? key = null;
        try { key = hub.Name(264, number); }
        catch (Exception ex) when (ex is Win32Exception or InvalidDataException) { unknowns.Add("PnP correlation: " + ex.Message); }
        var matches = pnp.Where(p => key != null && Eq(p.DriverKey, key)).ToArray();
        var device = matches.Length == 1 ? matches[0] : null;
        if (device == null) unknowns.Add("Physical PnP node could not be correlated uniquely.");
        var children = device == null ? [] : Descendants(pnp, device.InstanceId);
        string? serial = null, product = null, manufacturer = device?.Manufacturer;
        try
        {
            var languages = hub.Descriptor(number, 3, 0, 0, 255);
            if (languages.Length < 4 || languages[1] != 3) throw new InvalidDataException("Language descriptor is missing.");
            ushort lang = U16(languages, 2);
            product = hub.StringDescriptor(number, connection[19], lang);
            manufacturer = hub.StringDescriptor(number, connection[18], lang) ?? manufacturer;
            serial = hub.StringDescriptor(number, connection[20], lang);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidDataException) { unknowns.Add("String/serial descriptor: " + ex.Message); }
        byte[]? config = null;
        Endpoint[] offered = []; byte[] classes = [];
        try
        {
            for (byte index = 0; index < Math.Min(16, (int)connection[21]); index++)
            {
                var header = hub.Descriptor(number, 2, index, 0, 9);
                if (header.Length < 9) throw new InvalidDataException("Configuration header is incomplete.");
                if (header[5] != connection[22]) continue;
                config = hub.Descriptor(number, 2, index, 0, U16(header, 2));
                (offered, classes) = UsbDeviceNormalizer.ParseConfiguration(config);
                break;
            }
            if (config == null) unknowns.Add("Active configuration could not be determined.");
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidDataException) { unknowns.Add("Configuration descriptor: " + ex.Message); }
        var pipes = new List<Endpoint>();
        uint pipeCount = U32(connection, 27);
        for (int i = 0; i < Math.Min(pipeCount, 32); i++)
        {
            int at = 35 + 11 * i;
            if (at + 11 > connection.Length) { unknowns.Add("Open pipes were only partially captured."); break; }
            pipes.Add(new(connection[at + 2], (byte)(connection[at + 3] & 3), U16(connection, at + 4), connection[at + 6]));
        }
        var node = new UsbNode { Id = id + "/device", ParentId = id, ControllerId = hubNode.ControllerId,
            Kind = connection[24] != 0 ? NodeKind.Hub : NodeKind.Device,
            Name = product ?? device?.Name ?? $"USB device {U16(connection, 12):X4}:{U16(connection, 14):X4}", Manufacturer = manufacturer,
            InstanceId = device?.InstanceId, ParentInstanceId = device?.ParentId, ContainerId = device?.ContainerId,
            LocationPath = device?.LocationPath, SerialNumber = serial,
            VendorId = U16(connection, 12).ToString("X4"), ProductId = U16(connection, 14).ToString("X4"),
            DeviceClass = connection[8], CurrentConfiguration = connection[22], Speed = speed, SuperSpeedCapable = capable,
            HubDepth = hubNode.HubDepth, DeviceDescriptorHex = Convert.ToHexString(connection.AsSpan(4, 18)),
            ConfigurationDescriptorHex = config == null ? null : Convert.ToHexString(config), InterfaceClasses = classes,
            OfferedEndpoints = offered, OpenPipes = pipes.ToArray(), LogicalInterfaces = children.Select(p => p.InstanceId).ToArray(),
            Role = new UsbDeviceNormalizer().Classify(connection[8], classes, children.Select(p => p.Class)),
            FunctionRoles = new UsbDeviceNormalizer().Functions(children.Select(p => p.Class)), Unknowns = unknowns.ToArray() };
        if (node.Kind == NodeKind.Hub)
        {
            try
            {
                var childPath = hub.Name(261, number);
                if (string.IsNullOrEmpty(childPath)) throw new InvalidDataException("Hub name is missing.");
                ReadHub(childPath, node.Id, id, hubNode.ControllerId!, hubNode.HubDepth + 1,
                    node with { HubDepth = hubNode.HubDepth + 1 }, hubs, pnp, nodes, token);
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidDataException)
            { nodes.Add(node with { Unknowns = node.Unknowns.Append("Hub subtree unknown: " + ex.Message).ToArray() }); }
        }
        else nodes.Add(node);
    }

    private static PnpDevice[] Descendants(PnpDevice[] all, string parent)
    {
        var result = new List<PnpDevice>();
        var pending = new Queue<string>(); pending.Enqueue(parent);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { parent };
        while (pending.TryDequeue(out var id))
            foreach (var child in all.Where(p => Eq(p.ParentId, id)))
                if (visited.Add(child.InstanceId)) { result.Add(child); pending.Enqueue(child.InstanceId); }
        return result.ToArray();
    }
    private static (string?, string?) PciIds(string id)
    {
        var m = System.Text.RegularExpressions.Regex.Match(id, @"VEN_([0-9A-F]{4})&DEV_([0-9A-F]{4})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? (m.Groups[1].Value, m.Groups[2].Value) : (null, null);
    }
    private static bool Eq(string? a, string? b) => a != null && b != null && StringComparer.OrdinalIgnoreCase.Equals(a, b);
    private static string Normalize(string name) => name.Replace("\\\\?\\", "").Replace("\\\\.\\", "").ToUpperInvariant();

    private sealed class ChangeMonitor : IDisposable
    {
        private readonly NativeMethods.NotificationCallback callback;
        private readonly IntPtr handle;
        private int generation;
        public int Generation => Volatile.Read(ref generation);
        public bool Available => handle != IntPtr.Zero;
        public ChangeMonitor()
        {
            callback = (_, _, _, _, _) => { Interlocked.Increment(ref generation); return 0; };
            var filter = new byte[416]; Put(filter, 0, 416); Put(filter, 4, 1);
            if (NativeMethods.CM_Register_Notification(filter, IntPtr.Zero, callback, out handle) != 0) handle = IntPtr.Zero;
        }
        public void Dispose() { if (handle != IntPtr.Zero) NativeMethods.CM_Unregister_Notification(handle); GC.KeepAlive(callback); }
    }
}
