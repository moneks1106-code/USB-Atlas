#!/usr/bin/env python3
"""Builds src/UsbAtlas.Core/controller-catalog.json from the PCI ID Project's pci.ids.

Usage: python3 tools/generate-controller-catalog.py path/to/pci.ids

Intel (8086) and AMD (1022) USB host controllers are classified from their pci.ids product
names with the rules below; add-on vendors (ASMedia, VIA, Renesas, NEC, Fresco Logic, Etron)
are always discrete controllers. A few IDs from the research document that pci.ids does not
name are appended with their own source and confidence. Names that no rule covers are listed
on stderr and left out, so a wrong guess never ends up in the app.
"""
import json
import re
import sys
from pathlib import Path

SKIP = re.compile(r"Device Controller|OTG|xDCI|Client Controller|PCIe Root Port|Moorestown|Medfield|Merrifield|Quark|"
                  r"CE Media|Puma 7|EP80579|EG20T|Poulsbo|Ariel|FCH |UDCI|Function Control|Host Router", re.I)

# Vendors whose USB host controllers are always separate chips (on the board, an add-in card or a dock).
ADD_ON_VENDORS = {"1b21": "ASMedia", "1106": "VIA", "1912": "Renesas", "1033": "NEC", "1b73": "Fresco Logic", "1b6f": "Etron"}

# IDs from the research document §8.2 that pci.ids does not name (yet). Confidence as given there.
EXTRA = [
    ("1022:15B9", "Phoenix USB 3.1 xHCI (observed, not named in pci.ids)", "CpuSoc", "Medium", "Research §8.2 (Ubuntu certification, observed)"),
    ("1022:15BA", "Phoenix USB 3.1 xHCI (observed, not named in pci.ids)", "CpuSoc", "Medium", "Research §8.2 (Ubuntu certification, observed)"),
    ("1022:43D0", "X470 Series Chipset USB 3.1 xHCI Controller (forum report)", "Chipset", "Medium", "Research §8.2 (forum report)"),
    ("8086:7EB0", "Meteor Lake-M Thunderbolt 4 USB Controller", "CpuSoc", "High", "coreboot src/include/device/pci_ids.h"),
    ("8086:4D31", "Wildcat Lake Thunderbolt 4 USB Controller", "CpuSoc", "High", "coreboot src/include/device/pci_ids.h"),
    ("8086:4D7D", "Wildcat Lake USB 3.2 xHCI Host Controller", "OnPackageChipset", "Medium", "coreboot src/include/device/pci_ids.h"),
]

# Explicit decisions for names whose wording alone would mislead.
OVERRIDES = {
    "8086:9C31": "OnPackageChipset",  # Lynx Point-LP (Haswell-U), PCH on package
    "8086:51ED": "OnPackageChipset",  # Alder Lake-P PCH, on package
    "8086:54ED": "OnPackageChipset",  # Alder Lake-N PCH, on die
    "8086:7E7D": "OnPackageChipset",  # Meteor Lake-P IOE/PCH tile
    "8086:464E": "CpuSoc",            # Alder Lake-N Processor xHCI
    "8086:9C26": "OnPackageChipset",  # Lynx Point-LP EHCI
    "8086:9C2D": "OnPackageChipset",  # Lynx Point-LP EHCI
    "8086:4DED": "OnPackageChipset",  # Jasper Lake: PCH function on the SoC die (research §8.2)
    "8086:4B7D": "OnPackageChipset",  # Elkhart Lake: PCH function on the SoC die (research §8.2)
    "8086:777D": "OnPackageChipset",  # Arrow Lake-H / Lunar Lake "Standalone xHCI" = PCH tile (research §8.2)
    "8086:A87D": "OnPackageChipset",  # Lunar Lake PCH tile
    "8086:E37D": "OnPackageChipset",  # Panther Lake PCH tile
    "8086:E47D": "OnPackageChipset",  # Panther Lake PCH tile
    "8086:D37D": "OnPackageChipset",  # Nova Lake PCH tile
}

def classify(vendor, did, name):
    key = f"{vendor}:{did}".upper()
    if key in OVERRIDES:
        return OVERRIDES[key]
    if SKIP.search(name):
        return None
    if vendor in ADD_ON_VENDORS:
        return "DiscreteController"
    if vendor == "1022":
        if re.search(r"Chipset|AMD-7\d\d|AMD-8111", name) or did == "43bc":
            return "Chipset"
        return "CpuSoc"
    # Intel
    if re.search(r"\[.*Ridge.*\]|JHL\d|DSL\d", name):
        return "DiscreteController"
    if "Thunderbolt" in name:
        return "CpuSoc"
    if "On-Package" in name or re.search(r"Point-LP|Lake-LP", name):
        return "OnPackageChipset"
    if re.search(r"Chipset|PCH|ICH|82801|PIIX|6300ESB|631xESB|NM10|82440MX|C610/X99|DH89", name, re.I):
        return "Chipset"
    if re.search(r"Atom|Celeron|Pentium|Processor|Elkhart Lake|Jasper Lake", name):
        return "CpuSoc"
    return None

def main(path):
    version = None
    vendor = None
    entries = []
    unclassified = []
    for line in Path(path).read_text(encoding="utf-8", errors="replace").splitlines():
        if line.startswith("#\tVersion:"):
            version = line.split(":", 1)[1].strip()
        if not line or line.startswith("#"):
            continue
        if not line.startswith("\t"):
            vendor = line[:4].lower()
            continue
        if line.startswith("\t\t") or (vendor not in ("8086", "1022") and vendor not in ADD_ON_VENDORS):
            continue
        did, name = line[1:5].lower(), line[7:].strip()
        if not re.search(r"usb|xhci", name, re.I):
            continue
        placement = classify(vendor, did, name)
        if placement is None:
            if not SKIP.search(name):
                unclassified.append(f"{vendor}:{did} {name}")
            continue
        entries.append({"id": f"{vendor}:{did}".upper(), "name": name, "placement": placement, "confidence": "High", "source": f"pci.ids {version}"})
    known = {e["id"] for e in entries}
    for id_, name, placement, confidence, source in EXTRA:
        if id_ not in known:
            entries.append({"id": id_, "name": name, "placement": placement, "confidence": confidence, "source": source})
    entries.sort(key=lambda e: e["id"])
    catalog = {
        "source": "The PCI ID Repository (pci.ids), distributed under the 3-clause BSD License",
        "url": "https://raw.githubusercontent.com/pciutils/pciids/master/pci.ids",
        "version": version,
        "entries": entries,
    }
    out = Path(__file__).resolve().parent.parent / "src" / "UsbAtlas.Core" / "controller-catalog.json"
    out.write_text(json.dumps(catalog, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    for u in unclassified:
        print("unclassified:", u, file=sys.stderr)
    print(f"{len(entries)} entries written to {out} (pci.ids {version})")

if __name__ == "__main__":
    main(sys.argv[1])
