namespace UsbAtlas.Core;

/// <summary>Parent-chain queries over a snapshot.</summary>
public sealed class UsbDomainGraph(UsbSnapshot snapshot)
{
    /// <summary>Hubs between the device and its controller, nearest first. The root hub is included.</summary>
    public UsbNode[] HubChain(UsbNode device)
    {
        var result = new List<UsbNode>();
        var visited = new HashSet<string>();
        var current = snapshot.Find(device.ParentId);
        while (current != null && visited.Add(current.Id))
        {
            if (current.Kind is NodeKind.Hub or NodeKind.RootHub) result.Add(current);
            current = snapshot.Find(current.ParentId);
        }
        return result.ToArray();
    }
}
