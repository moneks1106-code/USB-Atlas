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
- Presentation-only CPU/chipset hint for known controller PCI IDs (`ControllerPlacementHint`).
- Expert mode with descriptors, open pipes, offered endpoints and data gaps.

## Assessment coverage

R29 describes the offered service interval of a qualifying High-Speed interrupt endpoint.
R45 distinguishes SuperSpeed capability from the link in use. R15 and R17 record controller
membership without inferring a conflict. Documented connection error statuses create
enumeration findings. All other rules need observations this scanner does not capture and
remain "not assessable"; the assessment flyout lists them with their missing preconditions.

## Changed in 0.2.0

- Enum values and comparison kinds are English (`Ok`, `Note`, `Check`, `Problem`,
  `NotAssessable`; `Changed`, `Unchanged`). The German-to-English display layer is gone.
- The view model only contains what the single tree page uses. Baseline, port mapping,
  role assignment, export and guide code without a UI since 0.1.5 was removed. The
  placement advisor and port mapper went with it; `TestFeasibleDevices` can be supplied
  through `RuleObservations` when a mapping source exists again.
- Only `latest.json` is written instead of an unbounded snapshot history.
- MIT license added.

## Limitations

- No end-to-end latency, report rate, RF or traffic measurement.
- No physical socket position inference and no front/rear recommendation.
- The model-hint catalog covers a handful of AMD controller IDs. Others say "unknown".
- No system, driver, firmware, registry or power-plan changes.
