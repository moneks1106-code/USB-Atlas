using System.Text.RegularExpressions;
using UsbAtlas.Core;

namespace UsbAtlas.UI;

// Display old generated scan text in English without rewriting stored observations or user labels.
internal static class EnglishDisplay
{
    internal static string NodeName(UsbNode node) => node.Kind switch
    {
        NodeKind.Port when node.PortNumber is { } number => $"Port {number}",
        NodeKind.Port => Regex.Replace(node.Name, @"^Anschluss (\d+)$", "Port $1"),
        NodeKind.RootHub => "Root hub",
        NodeKind.Controller => node.Name.Replace("USB-Controller", "USB controller").Replace("Hostcontroller", "Host Controller"),
        _ => node.Name switch
        {
            "USB-Eingabegerät" => "USB input device", "USB-Verbundgerät" => "USB composite device",
            "USB-Audiogerät" => "USB audio device", "USB-Massenspeichergerät" => "USB mass storage device",
            "Generischer USB-Hub" => "Generic USB hub", "Generischer SuperSpeed-USB-Hub" => "Generic SuperSpeed USB hub",
            _ => Regex.Replace(node.Name, @"^USB-Gerät ([0-9A-Fa-f]{4}:[0-9A-Fa-f]{4})$", "USB device $1")
        }
    };
    internal static string Manufacturer(string? name) => name switch
    {
        "(Standardsystemgeräte)" or "Standardsystemgeräte" => "Standard system devices",
        "(Standard-USB-Hostcontroller)" or "Standard-USB-Hostcontroller" => "Standard USB host controller",
        _ => name ?? "unknown"
    };
    internal static string AssessmentName(Assessment value) => value switch
    {
        Assessment.HINWEIS => "NOTE", Assessment.PRUEFEN => "CHECK", Assessment.NICHT_BEWERTBAR => "NOT ASSESSABLE",
        _ => value.ToString()
    };
    internal static string ChangeKind(string value) => value switch
    {
        "Geändert" => "Changed", "Unverändert" => "Unchanged", "Bestätigung erforderlich" => "Confirmation required",
        "Nicht mehr erfasst" => "No longer captured", "Neu erfasst / Zuordnung offen" => "Newly captured / match pending", _ => value
    };
    private static readonly (string German, string English)[] LegacyScanText =
    [
        ("Hotplug-Benachrichtigung nicht verfügbar; Konsistenz durch zwei vollständige Kontrollscans geprüft.", "Hotplug notifications unavailable; consistency checked with two complete verification scans."),
        ("Keine Hostcontroller erfasst. Dies ist kein Nachweis eines USB-freien Systems.", "No host controllers were captured. This does not prove that the system has no USB devices."),
        ("Root-Hub-Name fehlt.", "Root hub name is missing."),
        ("Ungewöhnliche Hubtiefe; Rekursion begrenzt.", "Unusual hub depth; recursion limited."),
        ("Hubdeskriptor unvollständig.", "Hub descriptor is incomplete."), ("Hubflags unvollständig.", "Hub flags are incomplete."),
        ("TT/Hub-Power:", "TT/hub power:"), ("Hubports unbekannt:", "Hub ports unknown:"),
        ("Anschlusseigenschaften unvollständig.", "Port properties are incomplete."), ("Buchseneigenschaften:", "Socket properties:"),
        ("Verbindungsinformation unvollständig.", "Connection information is incomplete."), ("Verbindung:", "Connection:"),
        ("Windows meldet einen vorübergehenden Enumerations-/Resetzustand; erneut aktualisieren.", "Windows reports a temporary enumeration/reset state; refresh again."),
        ("EX_V2 unvollständig.", "EX_V2 is incomplete."), ("Widersprüchliche Speed-Angaben EX/EX_V2.", "Conflicting EX/EX_V2 speed reports."),
        ("SuperSpeed-Fähigkeit:", "SuperSpeed capability:"), ("PnP-Korrelation:", "PnP correlation:"),
        ("Physischer PnP-Knoten nicht eindeutig korreliert.", "Physical PnP node could not be correlated uniquely."),
        ("Sprachdeskriptor fehlt.", "Language descriptor is missing."), ("String-/Seriendeskriptor:", "String/serial descriptor:"),
        ("Konfigurationskopf unvollständig.", "Configuration header is incomplete."), ("Aktive Konfiguration nicht ermittelt.", "Active configuration could not be determined."),
        ("Konfigurationsdeskriptor:", "Configuration descriptor:"), ("Ungültiger Konfigurationsdeskriptor.", "Invalid configuration descriptor."),
        ("Unvollständiger Konfigurationsdeskriptor.", "Incomplete configuration descriptor."), ("Ungültige Deskriptorlänge.", "Invalid descriptor length."),
        ("Offene Pipes nur teilweise erfasst.", "Open pipes were only partially captured."),
        ("Hubname fehlt.", "Hub name is missing."), ("Hub-Unterbaum unbekannt:", "Hub subtree unknown:"),
        ("USB-Handle konnte nicht geöffnet werden.", "Could not open the USB handle."),
        ("Ungültige USB-Antwortlänge.", "Invalid USB response length."), ("Deskriptorantwort zu kurz.", "Descriptor response is too short."),
        ("Hostcontroller", "Host Controller")
    ];
    internal static string ScanDetail(string text)
    {
        foreach (var (german, english) in LegacyScanText) text = text.Replace(german, english, StringComparison.Ordinal);
        return Regex.Replace(text, @"USB-Abfrage (\d+) nicht verfügbar\.", "USB query $1 is unavailable.");
    }
}
