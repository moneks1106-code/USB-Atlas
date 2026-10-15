using UsbAtlas.Core;

namespace UsbAtlas.UI;

/// <summary>The heading above the tree: what the observed configuration shows, in one sentence.</summary>
internal sealed record ConfigurationSummary(string Title, string Body)
{
    internal static ConfigurationSummary Create(UsbSnapshot? snapshot, IEnumerable<Finding> findings)
    {
        if (snapshot == null)
            return new("Your USB tree is waiting for its first scan.", "Refresh reads your controllers, hubs, ports and devices.");

        var list = findings.ToArray();
        int problems = list.Count(f => f.Status == Assessment.Problem);
        int notes = list.Count(f => f.Status == Assessment.Note);
        bool gaps = snapshot.Warnings.Length > 0 || snapshot.Nodes.Any(n => n.Unknowns.Length > 0);
        string gapText = gaps ? " Some properties could not be read; see the scan data gaps." : "";

        if (problems > 0)
            return new(problems == 1 ? "One port reports a connection error." : $"{problems} ports report connection errors.",
                "Assessment details list the affected ports and the reported status." + gapText);
        if (!snapshot.Controllers.Any() || !snapshot.Devices.Any())
            return new("No USB devices were captured.", "Refresh and check the scan data gaps in the assessment details.");
        if (!snapshot.Consistent)
            return new("The snapshot is inconsistent.", "The topology changed during the scan. Refresh to run another verification scan.");
        if (notes > 0)
            return new(notes == 1 ? "No connection errors. One note is worth a look." : $"No connection errors. {notes} notes are worth a look.",
                "Assessment details explain each note. This is a configuration check, not a latency measurement." + gapText);
        return new("No connection errors in the observed USB tree.",
            "This is a configuration check, not a latency measurement." + gapText);
    }
}
