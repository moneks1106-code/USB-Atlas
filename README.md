# USB Atlas

A read-only Windows 11 x64 application with one page for your USB tree and an
explainable assessment of the observed configuration. C# / .NET 10 / WinUI 3.

**Development release. The full hardware validation matrix is not yet complete.**
The application reads real Windows USB data. It does not generate demo devices,
calculate a latency score, modify system settings or make network requests.

## Download

[Download USB Atlas 0.1.5 for Windows x64](https://github.com/moneks1106-code/USB-Atlas/releases/download/v0.1.5/USB-Atlas-0.1.5-win-x64.zip)

See the [release page](https://github.com/moneks1106-code/USB-Atlas/releases/tag/v0.1.5) for the ZIP and SHA-256 checksum.

## Run

Extract the entire portable release folder and run `UsbAtlas.UI.exe`. Keep all files
and subfolders beside the executable. The .NET and Windows App SDK runtimes are included.
No MSIX installation or administrator rights are requested. A clean Windows VM without
development tools has not yet been validated.

1. **Refresh** reads the USB hardware twice and checks that the captures are consistent.
2. **Your USB tree** uses the full content area, with controllers at the top and hubs,
   ports and devices below. There is no sidebar or additional page.
3. **Empty logical ports** adds compact, individually clickable cards beneath their hub.
   The current setup with 27 ports and six devices fits the maximized window without
   scrolling. Small windows or much larger topologies may still scroll to keep text readable.
4. The **heading** assesses observable properties. Confirmed problems take priority;
   missing data limits the conclusion. **Assessment details** explains findings on the same page.
5. **Root hubs** show **Likely CPU/SoC**, **Likely chipset** or **Connection origin unknown**.
   Click a node for the reasoning and source. Model hints do not verify motherboard wiring
   or demonstrate a latency difference.
6. Level colors identify controllers, hubs, ports and devices. Path colors and labels link
   a port to its device; colors are not a performance ranking. No zoom control is required.
7. Click a node for details. **Expert mode** adds technical observations.

The interface and application-generated diagnostics are English. Known labels and scan
messages from earlier German snapshots are rendered in English without rewriting those
snapshots. Product names, identifiers and user-provided socket labels are preserved.

Local snapshots and settings are stored in `%LOCALAPPDATA%/UsbAtlas`. They may contain
device identifiers and serial numbers. There is no automatic upload. Startup explicitly
shows the last saved snapshot; refresh to capture the current hardware state.

## Scope and limitations

The scanner uses SetupAPI, CfgMgr32 and read-only hub IOCTL queries. It captures controller,
hub, port and device topology, available link speed, descriptors and supported metadata.
Composite interfaces remain one physical device. Snapshots, identity matching and comparison
services remain available internally; the former comparison, mapping and export pages were
removed at the user's request.

All 45 rule contracts are registered, but many require observations that this scanner does
not capture. Missing observations remain unassessable. Open pipes and descriptors do not
prove active traffic. There is no physical end-to-end latency measurement, RF packet-loss
measurement, active-stream sensor or verified manufacturer mode database.

A presentation-only model catalog dated 2026-10-15 covers AMD 1022:15B6/15B7/15B8
(likely CPU/SoC) and 1022:43FC (likely chipset). Other IDs remain unknown. These inferences
are not written into verified controller classes or used to rank performance. The source
and uncertainty are visible in the node details.

## Build and test

```powershell
dotnet restore UsbAtlas.slnx
dotnet test tests/UsbAtlas.Tests -c Release
dotnet build src/UsbAtlas.UI -c Release
./build-release.ps1
```

Read-only hardware smoke check:

```powershell
dotnet run --project src/UsbAtlas.Cli -c Release -- --output artifacts/scan.json
```

The SDK version is pinned in `global.json`. Network access is needed for dependency restore,
not for running the app. Public evidence summaries and outstanding hardware checks are documented under `docs/`.
Private authoring research, development prompts and hardware captures are not distributed.
