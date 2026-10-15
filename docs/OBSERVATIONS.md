# External observations

USB Atlas only observes topology and descriptors. Rules that need a measured report rate,
an active stream, a confirmed RF exposure or a feasible comparison test stay "not
assessable" until a separately validated observation is supplied. Any tool can do that by
writing one JSON file per snapshot revision; USB Atlas never writes these files itself.

## Location

```
%LOCALAPPDATA%\UsbAtlas\observations\<revision>.json
```

`<revision>` is the `Revision` GUID of the snapshot in `%LOCALAPPDATA%\UsbAtlas\latest.json`.
A file whose `Revision` field does not match its snapshot is ignored and reported in the
error bar. Observations bind to one captured topology; after a refresh the new snapshot has a
new revision and needs a new file.

## Format

```json
{
  "Revision": "4e6f7c0a-…",
  "Modes": [
    {
      "DeviceId": "PCI\\VEN_1022&DEV_15B6…/root/3/device",
      "EndpointAddress": 129,
      "InterfaceNumber": 0,
      "ReportsPerSecond": 8000,
      "OrdinaryInterrupt": true,
      "Source": "mouse-tester session 2026-10-15T20:14 (foreground, QPC)",
      "Validated": true
    }
  ],
  "ActiveIsoDevices": [],
  "ActiveSustainedDevices": [],
  "RequiredSuperSpeedDevices": [],
  "ConfirmedRfExposedDevices": [],
  "TestFeasibleDevices": [],
  "FailedDevices": [],
  "ValidatedEvidence": {
    "ACTIVE_ISO:<deviceId>": "source of that fact"
  }
}
```

- `DeviceId` is the node `Id` from the snapshot (`<controller>/root/<port>/device`).
- A mode contract counts only when `Validated` is true, `Source` is non-empty,
  `ReportsPerSecond` is between 1 and 32000, `OrdinaryInterrupt` is true and the endpoint
  address and interface number match exactly one offered interrupt-IN endpoint with an open
  pipe. `UsbRuleEngine` rejects anything else silently and the rule stays "not assessable".
- Device lists (`ActiveIsoDevices` and the others) count only together with a matching
  `ValidatedEvidence` entry `FACT:<deviceId>` naming the source. Keys: `ACTIVE_ISO`,
  `SUSTAINED_LOAD`, `SS_REQUIRED`, `RF_24GHZ_EXPOSURE`, `TEST_FEASIBLE`.
- A measured application arrival rate is not a USB bus measurement. The source string should
  say what was measured and how (foreground, QPC, window length), as the research protocol
  requires.
