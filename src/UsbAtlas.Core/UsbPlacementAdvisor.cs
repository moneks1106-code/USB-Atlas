namespace UsbAtlas.Core;

public sealed class UsbPlacementAdvisor(UsbPortMapper mapper)
{
    public PlacementProposal[] Propose(UsbSnapshot snapshot, Finding[] findings, PortMapping[] mappings)
    {
        if (!snapshot.Consistent) return [];
        var proposals = new List<PlacementProposal>();
        foreach (var finding in findings.Where(f => f.Status is Assessment.PROBLEM or Assessment.PRUEFEN)
            .OrderBy(f => f.Status == Assessment.PROBLEM ? 0 : 1))
        {
            var device = snapshot.Find(finding.NodeId);
            if (device == null || !device.IsPhysical) continue;
            // Only root-link countertests; a free port cannot promise an unobserved speed/power budget.
            var targets = snapshot.Ports.Where(p => mapper.IsAvailableTarget(snapshot, p)
                && snapshot.Find(p.ParentId)?.Kind == NodeKind.RootHub
                && mapper.Resolve(snapshot, p.Id, mappings) != null).ToArray();
            var target = targets.FirstOrDefault(p => p.Id != device.ParentId);
            if (target == null)
                proposals.Add(new(device.Id, finding.RuleId, null, "First map an accessible, available port.", finding.Explanation,
                    "A specific comparison can be offered after the port mapping is confirmed.", "No reliably mapped available target port exists."));
            else
                proposals.Add(new(device.Id, finding.RuleId, target.Id,
                    $"Comparison test: connect {device.Name} to ‘{mapper.Resolve(snapshot, target.Id, mappings)!.Label}’ and refresh.", finding.Explanation,
                    "Capture the new connection, speed and hub chain, then check the specific limitation again.",
                    "The available port's suitability is not fully known before connecting. Restore the previous connection if a new limitation appears; no latency improvement is promised."));
        }
        return proposals.ToArray();
    }
}
