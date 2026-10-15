namespace UsbAtlas.Core;

public sealed class UsbRuleEngine(UsbEvidenceRegistry registry)
{
    public Finding[] Evaluate(UsbSnapshot snapshot, RuleObservations? observations = null)
    {
        var input = observations ?? RuleObservations.Empty(snapshot.Revision);
        if (input.Revision != snapshot.Revision) throw new ArgumentException("Rule observations belong to a different snapshot revision.");
        var findings = new List<Finding>();
        var graph = new UsbDomainGraph(snapshot);
        foreach (var device in snapshot.Devices)
        {
            foreach (var definition in registry.Rules)
            {
                var id = definition.Id;
                string[] required = Required(id);
                var missing = new List<string>(required);
                var observed = new List<string>();
                var usedSources = new HashSet<string>();
                bool Has(string fact, string deviceId, string[] devices)
                {
                    if (!input.Has(fact, deviceId, devices)) return false;
                    usedSources.Add(input.ValidatedEvidence[fact + ":" + deviceId]); return true;
                }
                var status = Assessment.NICHT_BEWERTBAR;
                var explanation = "The required properties were not captured. This does not imply poor performance.";
                var scope = "USB configuration";
                string[] sources = ["docs/RESEARCH_V4_1.md §§11,20"];
                void Set(Assessment value, string message, params string[] facts)
                { status = value; explanation = message; observed.AddRange(facts); missing.Clear(); }
                var interrupt = device.OpenPipes.Where(p => p.IsInterruptIn).ToArray();
                ModeContract? ValidMode(UsbNode target) => input.Modes.FirstOrDefault(m => m.DeviceId == target.Id && m.Validated && !string.IsNullOrWhiteSpace(m.Source)
                    && m.ReportsPerSecond is > 0 and <= 32000 && m.OrdinaryInterrupt
                    && target.OfferedEndpoints.Count(p => p.Address == m.EndpointAddress && p.IsInterruptIn
                        && p.InterfaceNumber == m.InterfaceNumber) == 1
                    && target.OpenPipes.Any(p => p.IsInterruptIn && p.Address == m.EndpointAddress));
                var mode = ValidMode(device);
                bool highPolling = mode?.ReportsPerSecond >= 2000;
                bool test = Has("TEST_FEASIBLE", device.Id, input.TestFeasibleDevices);
                bool iso = Has("ACTIVE_ISO", device.Id, input.ActiveIsoDevices);
                var peers = snapshot.Devices.Where(p => p.Id != device.Id && graph.SharesUpstream(device, p)).ToArray();
                if (!snapshot.Consistent) { missing.Add("Consistent snapshot"); }
                else switch (id)
                {
                    case "R13" when mode?.ReportsPerSecond >= 8000 && device.Speed == LinkSpeed.Full:
                        Set(Assessment.PROBLEM, "The verified device mode requires 8,000 ordinary interrupt transfers per second. The current Full-Speed connection does not support that mode.",
                            "Speed=Full", $"Endpoint={mode.EndpointAddress:X2}", $"Contract={mode.Source}"); sources = [UsbEvidenceRegistry.Sources["MS_ENDPOINT"], mode.Source]; scope = "USB_SPEED"; break;
                    case "R13" when mode != null && device.Speed is LinkSpeed.High or LinkSpeed.Super or LinkSpeed.SuperPlus:
                        Set(Assessment.HINWEIS, "Link speed alone establishes neither actual report frequency nor latency.", $"Speed={device.Speed}", mode.Source); break;
                    case "R29" when (device.DeviceClass == 3 || device.InterfaceClasses.Contains((byte)3)) && device.Speed == LinkSpeed.High && interrupt.Any(p => p.Interval == 1):
                        Set(Assessment.OK, "This High-Speed interrupt endpoint offers a 125 µs service interval. It does not require a SuperSpeed link. The actual report frequency has not been measured.",
                            "Speed=High", "Interrupt-IN bInterval=1"); sources = [UsbEvidenceRegistry.Sources["MS_ENDPOINT"]]; break;
                    case "R44" when device.Role == DeviceRole.Camera || device.InterfaceClasses.Contains((byte)14):
                        if (iso) Set(Assessment.HINWEIS, "An active isochronous stream was established separately. Camera class alone would not be sufficient evidence.", "Active isochronous observation");
                        else { missing = ["Active transfer mode and ongoing transfers"]; explanation = "The camera may offer multiple transfer types. An ongoing stream and its load are unknown."; }
                        sources = [UsbEvidenceRegistry.Sources["ELG_TRANS"]]; break;
                    case "R45" when device.SuperSpeedCapable == true && device.Speed != LinkSpeed.Unknown:
                        if (device.Speed >= LinkSpeed.Super) Set(Assessment.OK, "The SuperSpeed-capable device is using a SuperSpeed link.", $"Speed={device.Speed}", "EX_V2 SS-capable");
                        else if (Has("SS_REQUIRED", device.Id, input.RequiredSuperSpeedDevices))
                            Set(Assessment.PROBLEM, "The separately verified functional mode requires SuperSpeed. The device is using a slower link type.", $"Speed={device.Speed}", "EX_V2 SS-capable", "SuperSpeed requirement confirmed");
                        else if (test) Set(Assessment.PRUEFEN, "The device supports SuperSpeed but is using a slower link type. Check the cable, port and desired mode. Functional impairment has not yet been established.", $"Speed={device.Speed}", "EX_V2 SS-capable", "Comparison test possible");
                        else Set(Assessment.HINWEIS, "The device supports SuperSpeed but is currently using a slower link type. Whether SuperSpeed is required and a comparison test is possible remains unknown.", $"Speed={device.Speed}", "EX_V2 SS-capable");
                        sources = [UsbEvidenceRegistry.Sources["MS_CONNV2"]]; scope = "USB_SPEED"; break;
                    case "R01" when highPolling && graph.HubChain(device).Any(h => h.Kind == NodeKind.Hub) && test:
                        Set(Assessment.PRUEFEN, "The verified high-polling mode runs behind a hub. A direct connection is a possible comparison; additional lag has not been established.", mode!.Source, "Additional hub", "Comparison test possible"); break;
                    case "R03" when highPolling && test && peers.Any(p => ValidMode(p)?.ReportsPerSecond >= 2000):
                        Set(Assessment.PRUEFEN, "Two verified high-polling modes share a hub connection. Separate upstream paths can be compared under controlled conditions.", "Shared upstream", "Verified modes", "Comparison test possible"); break;
                    case "R05" when interrupt.Length > 0 && device.Speed is LinkSpeed.Low or LinkSpeed.Full or LinkSpeed.High && test && peers.Any(p => Has("ACTIVE_ISO", p.Id, input.ActiveIsoDevices)):
                        Set(Assessment.PRUEFEN, "The input device shares a USB 2 hub connection with separately verified isochronous transfers. Test a different upstream path.", "Shared USB 2 upstream", "Active isochronous load", "Comparison test possible"); sources = [UsbEvidenceRegistry.Sources["MS_BW"]]; break;
                    case "R06" when device.Speed == LinkSpeed.Full && interrupt.Length > 0 && test && graph.TransactionTranslator(device) is string tt &&
                        graph.HubChain(device).Any(h => h.Tt == TtMode.Single) && peers.Any(p => p.Speed == LinkSpeed.Full && Has("ACTIVE_ISO", p.Id, input.ActiveIsoDevices) && graph.TransactionTranslator(p) == tt):
                        Set(Assessment.PRUEFEN, "Full-Speed input and separately confirmed isochronous load share a Single-TT. A comparison can test the suspected resource effect.", tt, "Single-TT confirmed", "Active FS isochronous load", "Comparison test possible"); sources = [UsbEvidenceRegistry.Sources["MS_TT"], UsbEvidenceRegistry.Sources["MS_BW"]]; break;
                    case "R07" when device.Role == DeviceRole.Camera && device.Speed == LinkSpeed.High && iso && test && peers.Any(p => p.Role == DeviceRole.Camera && p.Speed == LinkSpeed.High && Has("ACTIVE_ISO", p.Id, input.ActiveIsoDevices)):
                        Set(Assessment.PRUEFEN, "Two active isochronous High-Speed cameras share a hub upstream. Compare separate connections.", "Shared HS upstream", "Two active isochronous streams", "Comparison test possible"); break;
                    case "R28" when device.Speed >= LinkSpeed.Super && Has("SUSTAINED_LOAD", device.Id, input.ActiveSustainedDevices) && test && peers.Any(p => p.Speed >= LinkSpeed.Super && Has("SUSTAINED_LOAD", p.Id, input.ActiveSustainedDevices)):
                        Set(Assessment.PRUEFEN, "Two verified sustained SuperSpeed loads share a hub connection. Separate upstream paths provide a testable comparison.", "Shared SS upstream", "Two active sustained loads", "Comparison test possible"); break;
                    case "R08" when device.Role == DeviceRole.Receiver && Has("RF_24GHZ_EXPOSURE", device.Id, input.ConfirmedRfExposedDevices) && test:
                        Set(Assessment.PRUEFEN, "Physical exposure to SuperSpeed was confirmed for this 2.4 GHz receiver path. Separation distance can be compared under controlled conditions.", "2.4 GHz path and RF exposure confirmed separately", "Comparison test possible"); sources = [UsbEvidenceRegistry.Sources["INTEL_RF"]]; scope = "RF"; break;
                    case "R15":
                        Set(Assessment.HINWEIS, "Controller membership is captured. Verified CPU/chipset placement is unknown without a validated mapping; no performance difference is inferred.", device.ControllerId ?? "Controller unknown"); break;
                    case "R17" when (device.DeviceClass == 3 || device.InterfaceClasses.Contains((byte)3)) && interrupt.Length > 0:
                        Set(Assessment.HINWEIS, "A shared host controller alone does not establish a conflict between mouse and keyboard.", device.ControllerId ?? "Controller unknown"); sources = [UsbEvidenceRegistry.Sources["MS_BW"]]; break;
                }
                if (status is Assessment.PROBLEM or Assessment.PRUEFEN)
                    sources = sources.Concat(usedSources).Distinct().ToArray();
                findings.Add(new(id, device.Id, status, definition.Criterion, explanation, observed.ToArray(), missing.ToArray(), sources, scope,
                    "No end-to-end measurement. Descriptors show offered capabilities; open pipes do not prove ongoing load. Historical research sources have not all been independently reverified."));
            }
        }
        foreach (var port in snapshot.Ports.Where(p => p.State == PortState.Error))
            findings.Add(new("SCAN", port.Id, Assessment.PROBLEM, "Windows reports a connection error", $"Observed USB_CONNECTION_STATUS={port.ConnectionStatus}.",
                [$"Status={port.ConnectionStatus}"], [], [UsbEvidenceRegistry.Sources["MS_USBVIEW"]], "ENUMERATION", "The error code establishes neither its cause nor a latency change."));
        return findings.ToArray();
    }

    public Finding[] EvaluateChanges(UsbSnapshot before, UsbSnapshot after, SnapshotComparison comparison)
    {
        if (comparison.BeforeRevision != before.Revision || comparison.AfterRevision != after.Revision)
            throw new ArgumentException("Comparison belongs to different revisions.");
        return comparison.Devices.Where(d => d.BeforeId != null && d.AfterId != null && d.BeforePort != d.AfterPort
            && d.IdentityConfidence is "UNIQUE_SERIAL" or "SAME_PATH_SERIAL" or "USER_CONFIRMED")
            .Select(d => new Finding("R30", d.AfterId!, Assessment.HINWEIS, "Port change captured",
                "The same physical device is assigned to a different logical port. No performance difference was measured.",
                [$"Before={d.BeforePort}", $"After={d.AfterPort}", $"Identity={d.IdentityConfidence}"], [],
                [UsbEvidenceRegistry.Sources["MS_LOC"]], "TOPOLOGY", "A port change establishes neither physical location nor a latency change.")).ToArray();
    }

    public static string[] Required(string rule) => rule switch
    {
        "R01" or "R02" or "R03" or "R04" => ["Verified high-polling mode", "Verified hub/root-port chain", "Comparison test possible"],
        "R05" or "R06" or "R07" or "R28" or "R32" => ["Shared resource domain", "Active relevant load", "TT status at FS", "Comparison test possible"],
        "R08" or "R09" or "R10" or "R11" or "R31" or "R40" => ["Confirmed 2.4 GHz receiver path", "Physically confirmed exposure", "Comparison test possible"],
        "R13" => ["Validated 8 kHz contract for the ordinary interrupt endpoint", "Matched endpoint", "Active speed"],
        "R29" => ["High-Speed", "Open interrupt-IN endpoint with bInterval=1"],
        "R30" => ["Identity and port change in a snapshot comparison"],
        "R44" => ["UVC-Interface", "Active transfer mode and ongoing transfers"],
        "R45" => ["EX_V2 SuperSpeed capability", "Active speed", "Functional requirement / feasible comparison"],
        "R37" or "R38" or "R39" => ["Assigned interrupt/interrupter/IMOD properties; research only, not observable by this scanner"],
        "R19" or "R20" or "R21" or "R22" or "R23" or "R24" or "R25" or "R26" or "R27" or "R42" => ["Device-specific power/driver state", "Specific requirement or resume observation"],
        "R43" => ["Separate measurement channel with foreground/queue/load data (P3)"],
        _ => ["Verified device/transport/driver-specific profile", "Observed rule preconditions"]
    };
}
