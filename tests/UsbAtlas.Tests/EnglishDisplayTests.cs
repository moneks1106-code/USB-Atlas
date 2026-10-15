using System.Text.Json;
using UsbAtlas.Core;
using UsbAtlas.UI;

namespace UsbAtlas.Tests;

public class EnglishDisplayTests
{
    private static UsbNode Node(string name, NodeKind kind = NodeKind.Device) => new() { Id = "n", Name = name, Kind = kind };

    [Fact] public void LegacyPortsAndRootHubsDisplayInEnglishWithoutChangingObservation()
    {
        var port = Node("Anschluss 18", NodeKind.Port) with { PortNumber = 18 };
        Assert.Equal("Port 18", EnglishDisplay.NodeName(port)); Assert.Equal("Anschluss 18", port.Name);
        Assert.Equal("Port 9", EnglishDisplay.NodeName(Node("Anschluss 9", NodeKind.Port)));
        Assert.Equal("Root hub", EnglishDisplay.NodeName(Node("Root-Hub", NodeKind.RootHub)));
    }
    [Theory] [InlineData("USB-Eingabegerät", "USB input device")]
    [InlineData("USB-Verbundgerät", "USB composite device")]
    [InlineData("USB-Gerät 1234:ABCD", "USB device 1234:ABCD")]
    public void KnownGenericWindowsDescriptionsHaveEnglishDisplayNames(string original, string expected)
    { Assert.Equal(expected, EnglishDisplay.NodeName(Node(original))); }
    [Fact] public void ProductNamesAndUserTextRemainUnchanged()
    {
        Assert.Equal("RØDE PodMic USB", EnglishDisplay.NodeName(Node("RØDE PodMic USB")));
        Assert.Equal("Mein Gerät", EnglishDisplay.NodeName(Node("Mein Gerät")));
        Assert.Equal("Meine Firma", EnglishDisplay.Manufacturer("Meine Firma"));
        Assert.Equal("Standard USB host controller", EnglishDisplay.Manufacturer("(Standard-USB-Hostcontroller)"));
    }
    [Fact] public void LegacyDiagnosticIncludesEnglishContextAndOriginalQueryNumber()
    {
        string old = "Konfigurationsdeskriptor: USB-Abfrage 260 nicht verfügbar.";
        Assert.Equal("Configuration descriptor: USB query 260 is unavailable.", EnglishDisplay.ScanDetail(old));
        Assert.Equal("Konfigurationsdeskriptor: USB-Abfrage 260 nicht verfügbar.", old);
        Assert.Equal("Hub ports unknown: Hub descriptor is incomplete.", EnglishDisplay.ScanDetail("Hubports unbekannt: Hubdeskriptor unvollständig."));
    }
    [Fact] public void EnglishDiagnosticsPassThroughUnchanged()
    {
        const string text = "Socket properties: USB query 278 is unavailable.";
        Assert.Equal(text, EnglishDisplay.ScanDetail(text));
    }
    [Theory] [InlineData(Assessment.HINWEIS, "NOTE")] [InlineData(Assessment.PRUEFEN, "CHECK")]
    [InlineData(Assessment.NICHT_BEWERTBAR, "NOT ASSESSABLE")]
    public void AssessmentPresentationIsEnglish(Assessment value, string label)
    { Assert.Equal(label, EnglishDisplay.AssessmentName(value)); }
    [Fact] public void SerializedStatusAndComparisonIdentifiersStayCompatible()
    {
        Assert.Equal("\"NICHT_BEWERTBAR\"", JsonSerializer.Serialize(Assessment.NICHT_BEWERTBAR, UsbSnapshotManager.JsonOptions));
        Assert.Equal("Changed", EnglishDisplay.ChangeKind("Geändert"));
        Assert.Equal("Unchanged", EnglishDisplay.ChangeKind("Unverändert"));
    }
    [Fact] public void RuleOutputAndMissingPreconditionsAreEnglish()
    {
        var node = Node("Fixture") with { ControllerId = "c", Speed = LinkSpeed.Full, InterfaceClasses = [3] };
        var snapshot = new UsbSnapshot(Guid.NewGuid(), DateTimeOffset.UtcNow, "fixture", true, [node], []);
        var findings = new UsbRuleEngine(new UsbEvidenceRegistry()).Evaluate(snapshot);
        Assert.Equal(45, findings.Length);
        Assert.Equal("HID on chipset xHCI rather than CPU xHCI", findings.Single(f => f.RuleId == "R15").Title);
        Assert.StartsWith("Controller membership is captured.", findings.Single(f => f.RuleId == "R15").Explanation);
        Assert.Contains("Active transfer mode and ongoing transfers", findings.Single(f => f.RuleId == "R44").Missing);
    }
}
