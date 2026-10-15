# Evidence and assessment contracts

This public document summarizes the implemented V4.1-derived contracts. It is not a copy
of the private authoring research or development prompts. References in the application
to this path and sections 11/20 refer to the contracts and limits below.

## 11. Rule contracts

The embedded `src/UsbAtlas.Core/rules-v4.1.json` catalog registers R01–R45. Registration
does not imply that all required observations are available. `UsbRuleEngine.Required`
lists the missing preconditions for each rule family. An unobserved precondition leaves
the case unassessable; it is not treated as a fault or a performance penalty.

- Ordinary interrupt rates require a verified mode contract, endpoint association and
  actual link speed. A product name containing "8k" does not establish those properties.
- A High-Speed interrupt-IN endpoint with bInterval=1 offers a 125 µs service interval;
  this does not measure actual report rate or physical input latency.
- Camera class, offered descriptors and open pipes do not establish active isochronous
  traffic. Load-dependent rules require separate, revision-bound observations.
- Shared host-controller membership alone does not establish a bandwidth conflict.
- A lower-speed link on a SuperSpeed-capable device is not automatically a functional
  problem. A PROBLEM finding requires a separately established functional requirement.
- Port changes require reliable device identity. Matching VID/PID or a missing device
  does not by itself establish the same physical device or a resolved problem.

## 20. Limits and release gates

USB Atlas reads topology and supported metadata. It does not measure end-to-end latency,
RF packet loss, active transfer load, interrupt moderation effects or power-management
performance. It does not change registry, firmware, drivers, power plans or USB routing.

CPU/chipset labels are explicitly model-based inferences, separate from verified controller
classes and assessment decisions. Unknown models remain unknown. No CPU-port bonus,
chipset penalty or latency improvement is inferred from topology.

The original historical research references have not all been independently reverified.
The application therefore keeps explicit uncertainty and narrowly bounded rule conditions.
Multi-PC, real replug/identity, hub/dock, DPI and clean-Windows validation remain incomplete.

## Primary references

- [Microsoft USBView sample](https://github.com/microsoft/Windows-driver-samples/tree/main/usb/usbview)
- [USB endpoint descriptor](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbspec/ns-usbspec-_usb_endpoint_descriptor)
- [Windows USB bandwidth allocation](https://learn.microsoft.com/en-us/windows-hardware/drivers/usbcon/usb-bandwidth-allocation)
- [USB connection information EX_V2 flags](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbioctl/ns-usbioctl-_usb_node_connection_information_ex_v2_flags)
- [Hub capability flags](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbioctl/ns-usbioctl-_usb_hub_cap_flags)
- [LocationPath registry subkey](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/locationpath-registry-subkey)
- [Elgato Facecam USB transfer modes](https://help.elgato.com/hc/en-us/articles/4681224843917-Elgato-Facecam-How-To-Change-USB-Transfer-Mode)
- [Intel USB 3.0 radio-frequency interference paper](https://cdrdv2-public.intel.com/841692/usb30radiofrequencyinterference.pdf)
- [PCI ID Project registry](https://raw.githubusercontent.com/pciutils/pciids/master/pci.ids), model facts checked against version 2026.10.15
- [Linux PROM21 xHCI documentation](https://www.kernel.org/doc/html/latest/hwmon/prom21-xhci.html), checked 2026-10-15

No third-party USB analysis program, PCI ID database or Linux source code is bundled.
