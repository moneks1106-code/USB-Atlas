using UsbAtlas.Core;

namespace UsbAtlas.UI;

/// <summary>
/// Presentation-only inference for a root hub or controller: which part of the platform the
/// controller most likely belongs to. Based on the controller's product name in pci.ids, never on
/// vendor, port number or device name alone.
/// </summary>
internal sealed record ControllerPlacementHint(string Label, string Explanation, string? Source)
{
    private static readonly UsbControllerCatalog Catalog = new();

    internal static ControllerPlacementHint For(UsbSnapshot snapshot, UsbNode node)
    {
        var controller = node.Kind == NodeKind.Controller ? node : snapshot.Find(node.ControllerId);
        if (controller?.Kind != NodeKind.Controller)
            return new("Connection origin unknown", "No associated host controller was captured.", null);

        string id = $"{controller.VendorId}:{controller.ProductId}".ToUpperInvariant();
        var entry = Catalog.Find(controller.VendorId, controller.ProductId);
        if (entry == null)
            return new("Connection origin unknown",
                $"The controller catalog (pci.ids {Catalog.Version}) has no Intel or AMD USB host controller with PCI ID {id}. Vendor, port number and device name alone are not used to infer the placement.",
                null);

        string source = $"{Catalog.Source} · version {Catalog.Version} · {Catalog.Url}";
        string listed = $"PCI {id} is listed in pci.ids {Catalog.Version} as \"{entry.Name}\".";
        const string noWiring = "This does not verify the motherboard wiring or the physical socket location";
        return entry.Placement switch
        {
            ControllerPlacement.CpuSoc => new("Likely CPU/SoC",
                $"{listed} That name belongs to a processor or SoC, so the controller is most likely integrated in the CPU package. {noWiring}, and does not demonstrate lower latency.",
                source),
            ControllerPlacement.Chipset => new("Likely chipset",
                $"{listed} That name belongs to a chipset (PCH or southbridge), a separate chip on the motherboard that connects to the CPU over DMI or PCIe. {noWiring}, and does not demonstrate higher latency.",
                source),
            ControllerPlacement.OnPackageChipset => new("Likely chipset (on CPU package)",
                $"{listed} That name belongs to a mobile platform whose chipset die sits inside the CPU package. Topologically it is still a chipset controller. {noWiring}, and does not demonstrate a latency difference.",
                source),
            _ => new("Likely add-in controller",
                $"{listed} That name belongs to a discrete Thunderbolt or USB controller chip on the motherboard, an add-in card or a dock. {noWiring}, and does not demonstrate a latency difference.",
                source)
        };
    }
}
