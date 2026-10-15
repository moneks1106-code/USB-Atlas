# USB Atlas

See how your USB devices are actually wired: which controller, which root hub, which
hub in between, and at what link speed. One window, one tree, read-only.

USB Atlas is a Windows 11 x64 desktop app (C# / .NET 10 / WinUI 3). It reads the live
USB topology through SetupAPI, CfgMgr32 and read-only hub queries, the same data that
USBView shows, and draws it as a color-coded tree you can click through.

<!-- Add a screenshot here: docs/screenshot.png -->

## What you get

- **The whole tree on one page.** Controllers at the top, then root hubs, hubs, logical
  ports and devices. Path colors link a port to its device; a level badge tells you what
  each card is. Collapse branches with + / −, show or hide empty ports.
- **Hub depth and link speed per device.** Click a device to see whether it is on a root
  port or behind one or more hubs, which port it sits on, and whether it negotiated
  Low, Full, High or SuperSpeed.
- **An explainable assessment.** 45 rule contracts (R01–R45) are evaluated against the
  observed data. Each finding names what was observed, what is missing, its source and its
  limits. From the data this scanner captures, the rules that can conclude today are the
  offered HID service interval, the SuperSpeed link check and Windows connection errors.
  Rules that need traffic, mode or RF observations stay "not assessable" until a separate,
  validated observation source feeds them; they are listed, not hidden. Any measurement tool
  can supply such observations as a JSON file per snapshot, see
  [docs/OBSERVATIONS.md](docs/OBSERVATIONS.md).
- **CPU or chipset hint for root hubs.** The root hub card says *Likely CPU/SoC*,
  *Likely chipset*, *Likely chipset (on CPU package)* or *Likely add-in controller*, based on
  the controller's name in the PCI ID Repository. The catalog covers 266 Intel, AMD,
  ASMedia, VIA, Renesas, Fresco Logic and Etron USB host controllers from ICH to the current
  Core Ultra and Ryzen platforms. Click the card for the entry, its source and confidence.
  Unknown Intel IDs fall back to the PCI location rule at medium confidence; everything
  else says "unknown" instead of guessing.
- **Expert mode** adds descriptors, open pipes, offered endpoints, transaction translator
  flags and every data gap the scan hit.

## What it is not

It is a topology viewer and evidence-limited configuration check, not a benchmark. It does not measure
latency, report rates or traffic, does not rank ports, and does not change any setting,
driver or power plan. Model hints do not verify motherboard wiring.

## Run

Download the latest portable ZIP from the
[releases page](https://github.com/moneks1106-code/USB-Atlas/releases), extract the whole
folder and run `UsbAtlas.UI.exe`. The .NET and Windows App SDK runtimes are included;
no installation or administrator rights are needed.

Startup shows the last saved snapshot. **Refresh** reads the hardware twice and only
accepts a capture when both passes agree.

The latest snapshot is stored as `%LOCALAPPDATA%\UsbAtlas\latest.json`. It contains
device identifiers and serial numbers. Nothing is uploaded.

## Build

Requires the .NET SDK pinned in `global.json` and Windows for the WinUI project.

```powershell
dotnet test tests/UsbAtlas.Tests -c Release
dotnet build src/UsbAtlas.UI -c Release
./build-release.ps1
```

The test project links the UI logic (layout, colors, summary, view model) directly and runs
on Linux as well. For a read-only hardware smoke check without the UI:

```powershell
dotnet run --project src/UsbAtlas.Cli -c Release -- --output artifacts/scan.json
```

## Documentation

- [Rule contracts and limits](docs/RESEARCH_V4_1.md)
- [External observations](docs/OBSERVATIONS.md)
- [Implementation status](docs/IMPLEMENTATION_STATUS.md)
- [Validation](docs/VALIDATION.md)

## License

MIT, see [LICENSE](LICENSE). Third-party components are listed in
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
