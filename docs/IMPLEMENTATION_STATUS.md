# Implementation status — 0.1.5

Development release for Windows 11 x64. The interface is English and contains one tree
page, an assessment heading, in-place details, explicit CPU/chipset model hints and compact
empty-port cards. All observed parent relationships remain individually inspectable.

## Implemented

- Read-only SetupAPI/CfgMgr32 and hub GET IOCTL enumeration.
- Controller, root-hub, hub, logical-port and physical-device topology.
- Actual link speed and available descriptors, endpoint, companion, TT and power metadata.
- Composite-device normalization and available logical input-function roles.
- Hotplug invalidation and two verification captures, with bounded retries.
- Local snapshots, identity-aware comparison, user-confirmed mappings and assessment services.
- R01–R45 contracts with explicit missing observations and source limitations.
- English presentation of known generated text from older German snapshots, preserving data.

## Assessment coverage

R29 can describe the offered service interval of a qualifying High-Speed interrupt endpoint.
R30 can report reliably identified port changes. R44 keeps camera transfer mode unknown
without separate stream evidence. R45 distinguishes link capability from a proven functional
requirement. Documented connection error statuses create enumeration findings.

Additional load, RF and mode-dependent rules have tested conditional algorithms but no
general live observation producer. Unavailable conditions remain unassessable. Full catalog
registration is not full real-world diagnostic coverage.

## Explicit limitations

- No physical end-to-end latency, RF loss or active traffic measurement.
- No automatic physical socket-position inference or universal front/rear recommendation.
- No verified motherboard-wide CPU/chipset mapping or performance ranking.
- No automatic system, driver, firmware, registry or power-plan changes.
- Real identical-device replug, additional platforms/hubs/docks, DPI/accessibility and clean-VM
  validation are outstanding. See [validation](VALIDATION.md) and [evidence](RESEARCH_V4_1.md).
