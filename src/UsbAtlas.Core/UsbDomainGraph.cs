namespace UsbAtlas.Core;

public sealed class UsbDomainGraph(UsbSnapshot snapshot)
{
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

    public string[] Upstreams(UsbNode device)
    {
        if (device.Speed == LinkSpeed.Unknown) return [];
        string side = device.Speed >= LinkSpeed.Super ? "SS" : "USB2";
        // Root hub ports have independent links. Only intervening hub uplinks are shared.
        return HubChain(device).Where(h => h.Kind == NodeKind.Hub).Select(h => $"{side}:{h.Id}").ToArray();
    }
    public bool SharesUpstream(UsbNode a, UsbNode b) => Upstreams(a).Intersect(Upstreams(b)).Any();
    public string? TransactionTranslator(UsbNode device)
    {
        if (device.Speed is not (LinkSpeed.Full or LinkSpeed.Low)) return null;
        var hub = HubChain(device).FirstOrDefault(h => h.Speed == LinkSpeed.High);
        if (hub == null || hub.Tt is TtMode.Unknown or TtMode.NotApplicable) return null;
        if (hub.Tt == TtMode.Single) return "TT:" + hub.Id;
        // Multi-TT is per downstream port, not per controller.
        var child = device;
        var visited = new HashSet<string>();
        while (child.ParentId != hub.Id && visited.Add(child.Id))
        { var parent = snapshot.Find(child.ParentId); if (parent == null) return null; child = parent; }
        return "TT:" + hub.Id + ":" + child.Id;
    }
}
