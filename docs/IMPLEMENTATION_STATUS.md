# Implementation status — 0.2.0

USB Atlas is a read-only USB topology viewer for Windows 11 x64 with a single tree page
and an explainable, evidence-limited assessment.

## Implemented

- Read-only SetupAPI/CfgMgr32 and hub GET IOCTL enumeration.
- Controller, root hub, hub, logical port and device topology with link speed, descriptors,
  endpoint, companion, transaction translator and power metadata.
- Composite-device normalization and logical input-function roles.
- Hotplug invalidation and two verification captures per refresh, with bounded retries.
- The latest consistent snapshot is stored locally and shown at startup.
- R01–R45 contracts (`rules-v4.1.json`, `UsbRuleEngine`) with explicit missing observations
  and source limitations. Revision-bound `RuleObservations` carry separately validated
  facts (mode contracts, active isochronous or sustained load, RF exposure, feasible tests).
- Snapshot comparison by device identity (`UsbSnapshotManager.Compare`) and the R30
  port-change rule, available in Core for a future comparison view.
- Controller placement hint (`UsbControllerCatalog`, `ControllerPlacementHint`): 266 USB host
  controllers from pci.ids 2026.10.15 plus the research §8.2 extras, classified as CPU/SoC,
  chipset, on-package chipset or add-in controller, each with confidence and source. Intel
  controllers without an entry fall back to the §8.2 location rule (bus 0 device 0x14 = PCH,
  0x0D = CPU Type-C subsystem, behind a bridge = add-in) at medium confidence. Regenerate
  with `tools/generate-controller-catalog.py`.
- `UsbPortMapper` and `UsbPlacementAdvisor` (research §20.2 modules `UsbPortMap`,
  `UsbPlacementView`) are available in Core with tests; the mapping UI is still removed.
- External observations: `UsbObservationStore` reads `observations/<revision>.json` and feeds
  `RuleObservations` into the rule engine. See [OBSERVATIONS.md](OBSERVATIONS.md).
- Expert mode with descriptors, open pipes, offered endpoints and data gaps.

## Assessment coverage

R29 describes the offered service interval of a qualifying High-Speed interrupt endpoint.
R45 distinguishes SuperSpeed capability from the link in use. R36 notes devices on ports
reported as not user-connectable (internal headers). R15 and R17 record controller
membership without inferring a conflict. Documented connection error statuses create
enumeration findings. R13, R01, R03, R05, R06, R07, R08, R28 and R45's problem branch
evaluate when an observation file supplies their preconditions. Everything else remains "not
assessable"; the assessment flyout lists it with the missing preconditions. R20 (selective
suspend versus driver opt-out) has no observation producer yet.

## Changed in 0.2.0

- Enum values and comparison kinds are English (`Ok`, `Note`, `Check`, `Problem`,
  `NotAssessable`; `Changed`, `Unchanged`). The German-to-English display layer is gone.
- The view model only contains what the single tree page uses. Baseline, port mapping,
  role assignment, export and guide code without a UI since 0.1.5 was removed from the view
  model. `TestFeasibleDevices` can be supplied through an observation file.
- Only `latest.json` is written instead of an unbounded snapshot history.
- MIT license added.

## Limitations

- No end-to-end latency, report rate, RF or traffic measurement.
- No physical socket position inference and no front/rear recommendation.
- The placement hint is a name-based inference, not a board fact. Controllers from other
  vendors (ASMedia, VIA, Renesas) and IDs missing from pci.ids say "unknown".
- No system, driver, firmware, registry or power-plan changes.
