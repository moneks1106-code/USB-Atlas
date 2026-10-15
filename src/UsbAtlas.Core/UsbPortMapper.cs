namespace UsbAtlas.Core;

public sealed class UsbPortMapper
{
    public string Fingerprint(UsbSnapshot snapshot, UsbNode port)
    {
        var hub = snapshot.Find(port.ParentId);
        if (hub == null) return "UNKNOWN";
        return hub.Kind == NodeKind.RootHub
            ? $"{hub.ControllerId}|{hub.InstanceId}|{hub.Id}"
            : hub.SerialKey != null ? $"{hub.SerialKey}|{hub.Id}"
            : $"UNSTABLE:{snapshot.Revision}:{hub.Id}";
    }
    public PortMapping Confirm(UsbSnapshot snapshot, string portId, string label, IEnumerable<PortMapping> existing)
    {
        var port = snapshot.Find(portId);
        if (!snapshot.Consistent || port?.Kind != NodeKind.Port || port.UserConnectable == false)
            throw new InvalidOperationException("This port cannot be mapped as an accessible physical socket.");
        label = label.Trim();
        if (label.Length is < 1 or > 80) throw new ArgumentException("Use 1 to 80 characters for the port name.");
        if (existing.Any(m => m.PortId != portId && StringComparer.OrdinalIgnoreCase.Equals(m.Label, label)))
            throw new ArgumentException("This port name is already in use.");
        return new(portId, label, Fingerprint(snapshot, port), DateTimeOffset.UtcNow);
    }
    public PortMapping? Resolve(UsbSnapshot snapshot, string? portId, IEnumerable<PortMapping> mappings)
    {
        var port = snapshot.Find(portId);
        return port?.Kind == NodeKind.Port ? mappings.FirstOrDefault(m => m.PortId == port.Id && m.HubFingerprint == Fingerprint(snapshot, port)) : null;
    }
    public bool IsAvailableTarget(UsbSnapshot snapshot, UsbNode port)
    {
        if (port.Kind != NodeKind.Port || port.State != PortState.Empty || port.UserConnectable != true) return false;
        // An empty protocol side can belong to an occupied physical receptacle.
        if (port.CompanionPort == 0) return true;
        if (port.CompanionPort == null || string.IsNullOrEmpty(port.CompanionHub)) return false;
        static string Normalize(string? path) => (path ?? "").Replace("\\\\?\\", "").Replace("\\\\.\\", "").ToUpperInvariant();
        var companionHub = snapshot.Nodes.FirstOrDefault(n => n.Kind is NodeKind.RootHub or NodeKind.Hub
            && Normalize(n.HubSymbolicLink) == Normalize(port.CompanionHub));
        return companionHub != null && snapshot.Ports.Any(p => p.ParentId == companionHub.Id && p.PortNumber == port.CompanionPort && p.State == PortState.Empty);
    }
}
