using System.Text.RegularExpressions;
using UsbAtlas.Core;

namespace UsbAtlas.UI;

/// <summary>
/// Presentation-only inference for a root hub or controller: which part of the platform the
/// controller most likely belongs to. First by the controller's product name in the catalog, then,
/// for Intel controllers without an entry, by the documented PCI location rule. Never a board fact.
/// </summary>
internal sealed partial record ControllerPlacementHint(string Label, string Explanation, string? Source)
{
    private static readonly UsbControllerCatalog Catalog = new();
    private const string NoWiring = "This does not verify the motherboard wiring or the physical socket location";

    internal static ControllerPlacementHint For(UsbSnapshot snapshot, UsbNode node)
    {
        var controller = node.Kind == NodeKind.Controller ? node : snapshot.Find(node.ControllerId);
        if (controller?.Kind != NodeKind.Controller)
            return new("Connection origin unknown", "No associated host controller was captured.", null);

        string id = $"{controller.VendorId}:{controller.ProductId}".ToUpperInvariant();
        var entry = Catalog.Find(controller.VendorId, controller.ProductId);
        if (entry != null) return FromCatalog(id, entry);
        return FromLocation(id, controller)
            ?? new("Connection origin unknown",
                $"The controller catalog (pci.ids {Catalog.Version}) has no USB host controller with PCI ID {id}, and the PCI location path gives no usable hint. Vendor, port number and device name alone are not used to infer the placement.",
                null);
    }

    private static ControllerPlacementHint FromCatalog(string id, ControllerCatalogEntry entry)
    {
        string confidence = entry.Confidence == "High" ? "" : $" Confidence: {entry.Confidence.ToLowerInvariant()}, because the ID comes from a secondary source.";
        string listed = $"PCI {id} is listed as \"{entry.Name}\" ({entry.Source}).";
        return entry.Placement switch
        {
            ControllerPlacement.CpuSoc => new("Likely CPU/SoC",
                $"{listed} That name belongs to a processor or SoC, so the controller is most likely integrated in the CPU package.{confidence} {NoWiring}, and does not demonstrate lower latency.",
                entry.Source),
            ControllerPlacement.Chipset => new("Likely chipset",
                $"{listed} That name belongs to a chipset (PCH or southbridge), a separate chip on the motherboard that connects to the CPU over DMI or PCIe.{confidence} {NoWiring}, and does not demonstrate higher latency.",
                entry.Source),
            ControllerPlacement.OnPackageChipset => new("Likely chipset (on CPU package)",
                $"{listed} That name belongs to a platform whose chipset function sits on the CPU package or die. Topologically it is still a chipset controller.{confidence} {NoWiring}, and does not demonstrate a latency difference.",
                entry.Source),
            _ => new("Likely add-in controller",
                $"{listed} That name belongs to a discrete USB or Thunderbolt controller chip on the motherboard, an add-in card or a dock.{confidence} {NoWiring}, and does not demonstrate a latency difference.",
                entry.Source)
        };
    }

    /// <summary>
    /// Location rule from the research document §8.2: on Intel platforms the PCH xHCI is device 0x14
    /// on bus 0 and the CPU Thunderbolt/USB4 xHCI is device 0x0D on bus 0. A controller behind a PCIe
    /// bridge is an add-in or Thunderbolt controller on any platform. Medium confidence.
    /// </summary>
    private static ControllerPlacementHint? FromLocation(string id, UsbNode controller)
    {
        if (string.IsNullOrEmpty(controller.LocationPath)) return null;
        var tokens = PciToken().Matches(controller.LocationPath).Select(m => Convert.ToInt32(m.Groups[1].Value, 16)).ToArray();
        if (tokens.Length == 0) return null;
        const string source = "Location rule, research document §8.2 (medium confidence)";
        string path = $"PCI {id} has no catalog entry. Its location path is {controller.LocationPath}.";
        if (tokens.Length >= 2)
            return new("Probably add-in controller",
                $"{path} The controller sits behind a PCIe bridge, which on desktop and mobile platforms points to an add-in card, an on-board extra controller or a Thunderbolt controller. Confidence: medium. {NoWiring}.",
                source);
        if (controller.VendorId?.Equals("8086", StringComparison.OrdinalIgnoreCase) != true) return null;
        return tokens[0] switch
        {
            0x14 => new("Probably chipset",
                $"{path} Intel places the PCH xHCI at device 0x14 on bus 0. Confidence: medium, because only the location was matched. {NoWiring}, and does not demonstrate higher latency.",
                source),
            0x0D => new("Probably CPU (Thunderbolt/USB4 subsystem)",
                $"{path} Intel places the CPU Type-C subsystem xHCI at device 0x0D on bus 0. Confidence: medium, because only the location was matched. {NoWiring}, and does not demonstrate lower latency.",
                source),
            _ => null
        };
    }

    [GeneratedRegex(@"#PCI\(([0-9A-Fa-f]{2})[0-9A-Fa-f]{2}\)")]
    private static partial Regex PciToken();
}
