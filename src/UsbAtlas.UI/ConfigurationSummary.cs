using UsbAtlas.Core;

namespace UsbAtlas.UI;

/// <summary>The heading above the tree. Confirmed problems come first; missing observations limit the conclusion.</summary>
internal sealed record ConfigurationSummary(string Title, string Body)
{
    internal static ConfigurationSummary Create(UsbSnapshot? snapshot, IEnumerable<Finding> findings)
    {
        if (snapshot == null)
            return new("Your USB tree is waiting for its first scan.", "Refresh scans your devices and checks the observable connections.");

        var list = findings.ToArray();
        int problems = list.Count(f => f.Status == Assessment.Problem);
        int tests = list.Count(f => f.Status == Assessment.Check);
        bool incomplete = !snapshot.Consistent || snapshot.Warnings.Length > 0 || snapshot.Nodes.Any(n => n.Unknowns.Length > 0)
            || list.Any(f => f.Status == Assessment.NotAssessable);

        if (problems > 0)
            return new(problems == 1 ? "Your USB configuration has one detected problem." : $"Your USB configuration has {problems} detected problems.",
                "Assessment details show the observed errors and their evidence." + (incomplete ? " Other properties cannot yet be assessed." : ""));
        if (!snapshot.Controllers.Any() || !snapshot.Devices.Any())
            return new("Your USB configuration cannot be assessed yet.", "No controllers or connected devices were captured. Refresh the snapshot and check the scan details.");
        if (!snapshot.Consistent)
            return new("Your USB configuration cannot be assessed reliably yet.", "The snapshot is inconsistent. Refresh to run another verification scan.");

        string title = tests > 0 ? "No confirmed USB problems. Further checks are available." : "Your observed USB configuration looks healthy.";
        string body = (tests > 0 ? $"{tests} evidence-based comparison tests are listed in the assessment details. " : "No verified configuration problems detected. ")
            + (incomplete ? "Missing data limits the assessment; optimal performance has not been established." : "This is a configuration check, not a latency measurement.");
        return new(title, body);
    }
}
