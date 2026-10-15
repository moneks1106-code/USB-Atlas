# Validation — 0.2.0

## Automated

- 159 xUnit tests cover descriptor parsing, composite normalization, rule preconditions and
  observation contracts, identity-aware comparison, snapshot persistence, the configuration
  summary, controller catalog and hints, port mapping, observation import, tree layout, colors
  and the view model with a fake scanner.
- Core, Windows and CLI projects and the test suite build and run on Linux with
  `-p:EnableWindowsTargeting=true`. The WinUI project builds on Windows only.

## Manual, outstanding for 0.2.0

- Launch of the WinUI app on real hardware after the 0.2.0 refactor: tree, node details,
  assessment flyout, expert mode and the error bar.
- Cross-check against USBView/USBTreeView on more PCs and hub/dock types.
- Clean Windows 11 VM without development tools.
- Different DPI/text scaling, keyboard and screen-reader acceptance.

The public Windows package is built without application debug symbols and with mapped
source paths. Private hardware captures are not distributed.
