# Validation — 0.1.5

## Completed

- 112 automated tests pass, with no skipped tests.
- Release build and unpackaged self-contained x64 publish succeed without warnings or errors.
- Real Windows launch and UI checks cover the English heading, controls, root hints,
  assessment findings, technical details and legacy generated-text presentation.
- A real setup with four controllers, 27 logical ports and six physical devices was checked.
  All ports fit the maximized tree using compact cards for 21 confirmed-empty ports.
- Layout cases include maximized content sizes of 1860×760 and 2480×1120, narrow windows,
  nested hubs, collapsed branches, bounds, non-overlap and connection routing.
- Tests cover descriptor parsing, composite normalization, explicit rule preconditions,
  identity ambiguity, snapshot persistence, companion-port occupancy and comparison limits.

Private hardware captures, serial numbers and device instance IDs are not distributed.
The public Windows package is built without application debug symbols and with mapped source
paths, then checked for accidental local-account/build-path data.

## Outstanding

- Independent USBView/USBTreeView cross-checks across more PCs and hub/dock types.
- Real manual replug between controllers and with two identical devices.
- A clean Windows 11 VM without development tools.
- 1080p/1440p/4K, different DPI/text scaling, keyboard and screen-reader acceptance.
- Separate validated observation sources for currently unavailable load, RF and device modes.

These checks do not establish optimal performance or a measured gaming-latency improvement.
