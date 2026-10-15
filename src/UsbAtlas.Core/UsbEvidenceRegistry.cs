using System.Text.Json;

namespace UsbAtlas.Core;

public sealed record RuleDefinition(string Id, string Criterion, string Evidence, string Action, string ResearchStatus, string Source);
public sealed class UsbEvidenceRegistry
{
    public RuleDefinition[] Rules { get; }
    public UsbEvidenceRegistry()
    {
        using var stream = typeof(UsbEvidenceRegistry).Assembly.GetManifestResourceStream("UsbAtlas.Core.rules-v4.1.json")!;
        Rules = JsonSerializer.Deserialize<RuleDefinition[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
    public static readonly Dictionary<string, string> Sources = new()
    {
        ["MS_ENDPOINT"] = "https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbspec/ns-usbspec-_usb_endpoint_descriptor",
        ["MS_USBVIEW"] = "https://github.com/microsoft/Windows-driver-samples/tree/main/usb/usbview",
        ["MS_CONNV2"] = "https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbioctl/ns-usbioctl-_usb_node_connection_information_ex_v2_flags",
        ["MS_BW"] = "https://learn.microsoft.com/en-us/windows-hardware/drivers/usbcon/usb-bandwidth-allocation",
        ["MS_TT"] = "https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbioctl/ns-usbioctl-_usb_hub_cap_flags",
        ["MS_PORTPROPS"] = "https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbioctl/ns-usbioctl-_usb_port_properties",
        ["MS_LOC"] = "https://learn.microsoft.com/en-us/windows-hardware/drivers/install/locationpath-registry-subkey",
        ["ELG_TRANS"] = "https://help.elgato.com/hc/en-us/articles/4681224843917-Elgato-Facecam-How-To-Change-USB-Transfer-Mode",
        ["INTEL_RF"] = "https://cdrdv2-public.intel.com/841692/usb30radiofrequencyinterference.pdf"
    };
}

// Supplied only by a separately validated, revision-bound observation source. The P0 scanner
// does not observe active traffic/selected alternate settings, RF or vendor mode contracts.
public sealed record ModeContract(string DeviceId, byte EndpointAddress, int ReportsPerSecond, bool OrdinaryInterrupt,
    string Source, bool Validated, byte? InterfaceNumber = null);
public sealed record RuleObservations(Guid Revision, ModeContract[] Modes, string[] ActiveIsoDevices,
    string[] ActiveSustainedDevices, string[] RequiredSuperSpeedDevices, string[] ConfirmedRfExposedDevices,
    string[] TestFeasibleDevices, string[] FailedDevices)
{
    public static RuleObservations Empty(Guid revision) => new(revision, [], [], [], [], [], [], []);
    // Facts must come from an accepted, revision-bound collector. A bare device list is insufficient.
    public Dictionary<string, string> ValidatedEvidence { get; init; } = [];
    public bool Has(string fact, string deviceId, string[] devices) => devices.Contains(deviceId)
        && ValidatedEvidence.TryGetValue(fact + ":" + deviceId, out var source) && !string.IsNullOrWhiteSpace(source);
}
