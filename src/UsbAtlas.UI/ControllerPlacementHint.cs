using UsbAtlas.Core;

namespace UsbAtlas.UI;

internal sealed record ControllerPlacementHint(string Label, string Explanation, string? Source)
{
    // Presentation-only inference, catalog revision 2026-10-15. Never a measured/verified controller class.
    internal static ControllerPlacementHint For(UsbSnapshot snapshot, UsbNode node)
    {
        var controller = node.Kind == NodeKind.Controller ? node : snapshot.Find(node.ControllerId);
        if (controller?.Kind != NodeKind.Controller)
            return new("Connection origin unknown", "No associated host controller was captured.", null);
        string id = $"{controller.VendorId}:{controller.ProductId}".ToUpperInvariant();
        return id switch
        {
            "1022:15B6" or "1022:15B7" or "1022:15B8" => new("Likely CPU/SoC",
                $"PCI {id}: The PCI ID registry identifies this model as Raphael/Granite Ridge. CPU/SoC integration is inferred from that model. This does not verify the motherboard wiring or the physical socket location, and does not demonstrate lower latency.",
                "PCI-ID Project · pci.ids 2026.10.15 · https://raw.githubusercontent.com/pciutils/pciids/master/pci.ids"),
            "1022:43FC" => new("Likely chipset",
                $"PCI {id}: Linux kernel documentation identifies this controller as PROM21 xHCI and PROM21 as chipset IP. A chipset connection is therefore likely. This does not verify the physical socket location or demonstrate higher latency.",
                "Linux Kernel · prom21-xhci · checked 2026-10-15 · https://www.kernel.org/doc/html/latest/hwmon/prom21-xhci.html"),
            _ => new("Connection origin unknown", $"This hint catalog has no supported model mapping for PCI {id}. Vendor, port number and device name alone are not used to infer CPU or chipset placement.", null)
        };
    }
}
