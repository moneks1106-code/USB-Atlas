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
- **Three honest checks on the observed data.** No guesswork, each with its source and limits:
  - the service interval a HID device's open interrupt endpoint offers (e.g. 125 µs,
    up to 8000 reports per second), which is the ceiling, not a measured rate;
  - SuperSpeed-capable devices that are running on a USB 2 link, so you can check cable and port;
  - ports where Windows reports a connection error.
- **CPU or chipset hint for root hubs.** For known PCI IDs the root hub card says
  *Likely CPU/SoC* or *Likely chipset*, with the public source behind the hint. Unknown
  IDs say so instead of guessing.
- **Expert mode** adds descriptors, open pipes, offered endpoints, transaction translator
  flags and every data gap the scan hit.

## What it is not

It is a topology viewer and configuration check, not a benchmark. It does not measure
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

## License

MIT, see [LICENSE](LICENSE). Third-party components are listed in
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
