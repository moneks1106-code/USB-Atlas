# Implementation status — 0.2.0

USB Atlas is a read-only USB topology viewer for Windows 11 x64 with a single tree page.

## Implemented

- Read-only SetupAPI/CfgMgr32 and hub GET IOCTL enumeration.
- Controller, root hub, hub, logical port and device topology with link speed, descriptors,
  endpoint, companion, transaction translator and power metadata.
- Composite-device normalization and logical input-function roles.
- Hotplug invalidation and two verification captures per refresh, with bounded retries.
- The latest consistent snapshot is stored locally and shown at startup.
- Three checks on observed data, each with source and limitation:
  `HID_SERVICE_INTERVAL`, `SUPERSPEED_LINK` and `PORT_ERROR` in `UsbRuleEngine`.
- Presentation-only CPU/chipset hint for known controller PCI IDs (`ControllerPlacementHint`).
- Expert mode with descriptors, open pipes, offered endpoints and data gaps.

## Removed in 0.2.0

- The 45-rule catalog and its observation contracts. Only three of those rules could ever
  produce a result from data this scanner captures; the rest reported "not assessable".
- Snapshot comparison, baseline, port mapping, role assignment, export and guide pages,
  which had no UI since 0.1.5.
- German enum values and the English display layer for them.
- Unbounded snapshot history. Only `latest.json` is written now.

## Limitations

- No end-to-end latency, report rate, RF or traffic measurement.
- No physical socket position inference and no front/rear recommendation.
- The model-hint catalog covers a handful of AMD controller IDs. Others say "unknown".
- No system, driver, firmware, registry or power-plan changes.
