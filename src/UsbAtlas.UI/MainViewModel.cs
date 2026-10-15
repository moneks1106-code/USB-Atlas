using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using UsbAtlas.Core;

namespace UsbAtlas.UI;

public sealed record DeviceItem(UsbNode Node, string Name, string Subtitle, string Glyph, string Location)
{ public string MatchLabel => $"{Name} · {Location}"; public override string ToString() => Name; }
public sealed record PortItem(string Id, string Label);
public sealed record HistoryItem(UsbSnapshot Snapshot, string Label);
public sealed record RoleOption(DeviceRole Value, string Label);
public sealed record ComparisonItem(string Name, string State, string Identity, string BeforePort, string AfterPort,
    string BeforeConnection, string AfterConnection, string Changes);

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IUsbTopologyScanner scanner;
    private readonly UsbSnapshotManager manager;
    private readonly UsbRuleEngine rules;
    private readonly UsbPlacementAdvisor advisor;
    private readonly UsbPortMapper mapper;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? TopologyChanged;
    private UserSettings settings = new(null, null, [], []);
    private UsbSnapshot? current, baseline;
    private Finding[] findings = [];
    private UsbSnapshot[] history = [];
    private bool busy, expert, showEmptyPorts;
    private string message = "Ready for your first USB scan.", error = "", mapLabel = "";
    private DeviceItem? selectedDevice;
    private PortItem? selectedPort;
    private HistoryItem? selectedHistory;
    private string page = "overview";
    private SnapshotComparison? comparison;
    private readonly Dictionary<string, string> manualMatches = [];
    private readonly SemaphoreSlim commandGate = new(1, 1);
    private UsbNode? selectedNode;
    public ObservableCollection<DeviceItem> Devices { get; } = [];
    public ObservableCollection<PortItem> Ports { get; } = [];
    public ObservableCollection<HistoryItem> History { get; } = [];
    public ObservableCollection<DeviceItem> BeforeDevices { get; } = [];
    public ObservableCollection<DeviceItem> AfterDevices { get; } = [];
    public ObservableCollection<ComparisonItem> Deltas { get; } = [];
    public DeviceItem? MatchBefore { get; set; }
    public DeviceItem? MatchAfter { get; set; }
    public RoleOption[] Roles { get; } = Enum.GetValues<DeviceRole>().Select(r => new RoleOption(r, RoleText(r))).ToArray();
    public DeviceRole SelectedRole { get; set; }
    public RoleOption? SelectedRoleOption
    {
        get => Roles.FirstOrDefault(r => r.Value == SelectedRole);
        set { if (value != null) { SelectedRole = value.Value; Changed(); } }
    }
    public ICommand RefreshCommand { get; }
    public ICommand BaselineCommand { get; }
    public ICommand PreferredCommand { get; }
    public ICommand MapCommand { get; }
    public ICommand RoleCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ShowHistoryCommand { get; }
    public ICommand ConfirmMatchCommand { get; }
    public ICommand GuideCommand { get; }

    public MainViewModel(IUsbTopologyScanner scanner, UsbSnapshotManager manager, UsbRuleEngine rules,
        UsbPlacementAdvisor advisor, UsbPortMapper mapper)
    {
        this.scanner = scanner; this.manager = manager; this.rules = rules; this.advisor = advisor; this.mapper = mapper;
        RefreshCommand = Command(RefreshAsync);
        BaselineCommand = Command(async () => { baseline = current ?? throw new InvalidOperationException("Run a scan first."); manualMatches.Clear(); settings = settings with { Baseline = baseline.Revision }; await manager.SaveSettingsAsync(settings); Recompare(); ChangedAll(); });
        PreferredCommand = Command(async () => { if (current == null) return; settings = settings with { Preferred = current.Revision }; await manager.SaveSettingsAsync(settings); Message = "This revision is saved as the preferred setup. Previous port labels are available in the comparison when restoring connections."; ChangedAll(); });
        MapCommand = Command(MapAsync);
        RoleCommand = Command(async () =>
        {
            if (SelectedDevice == null || current == null) return;
            var key = RoleKey(SelectedDevice.Node);
            settings = settings with { Roles = settings.Roles.Where(r => r.IdentityKey != key).Append(new RoleAssignment(key, SelectedRole)).ToArray() };
            await manager.SaveSettingsAsync(settings); Populate(); Message = "Role saved as a user assignment. No polling or radio properties are inferred from it.";
        });
        ExportCommand = Command(ExportAsync);
        ShowHistoryCommand = Command(() => { if (SelectedHistory != null) { current = SelectedHistory.Snapshot; manualMatches.Clear(); Populate(); Message = "Historical snapshot opened. Refresh to capture the current state."; } return Task.CompletedTask; });
        ConfirmMatchCommand = Command(() =>
        {
            if (MatchBefore == null || MatchAfter == null || baseline == null || current == null) throw new InvalidOperationException("Select one device before and after reconnecting it.");
            if (manualMatches.Any(p => p.Key != MatchBefore.Node.Id && p.Value == MatchAfter.Node.Id)) throw new InvalidOperationException("The new device has already been matched to another device.");
            var candidate = new Dictionary<string, string>(manualMatches) { [MatchBefore.Node.Id] = MatchAfter.Node.Id };
            manager.Compare(baseline, current, manualMatches: candidate);
            manualMatches[MatchBefore.Node.Id] = MatchAfter.Node.Id; Recompare(); ChangedAll();
            return Task.CompletedTask;
        });
        GuideCommand = Command(() => { Page = "compare"; Message = GuideText; return Task.CompletedTask; });
    }
    private AsyncCommand Command(Func<Task> action) => new(async () =>
    {
        if (busy || !await commandGate.WaitAsync(0)) return;
        try { Error = ""; await action(); } finally { commandGate.Release(); }
    }, ex => { Error = ex.Message; });
    public async Task InitializeAsync()
    {
        try
        {
            settings = await manager.LoadSettingsAsync(); history = await manager.HistoryAsync();
            baseline = history.FirstOrDefault(s => s.Revision == settings.Baseline);
            current = history.LastOrDefault();
            if (current != null) { Populate(); Message = "Last saved snapshot. Refresh to capture the current state."; }
            else await RefreshAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
    public bool Busy { get => busy; private set { busy = value; Changed(); Changed(nameof(WorkEnabled)); } }
    public bool WorkEnabled => !busy;
    public string Message { get => message; private set { message = value; Changed(); } }
    public string Error { get => error; set { error = value; Changed(); Changed(nameof(ErrorVisibility)); } }
    public Visibility ErrorVisibility => string.IsNullOrEmpty(error) ? Visibility.Collapsed : Visibility.Visible;
    public bool Expert { get => expert; set { expert = value; Changed(); Changed(nameof(ExpertVisibility)); Changed(nameof(SimpleVisibility)); Changed(nameof(SelectedFindings)); } }
    public bool ShowEmptyPorts { get => showEmptyPorts; set { showEmptyPorts = value; Changed(); TopologyChanged?.Invoke(this, EventArgs.Empty); } }
    public Visibility ExpertVisibility => expert ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SimpleVisibility => expert ? Visibility.Collapsed : Visibility.Visible;
    public string Page { get => page; set { page = value; ChangedAll(); } }
    public Visibility OverviewVisibility => page == "overview" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TopologyVisibility => page == "topology" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility StandardPageVisibility => page == "topology" ? Visibility.Collapsed : Visibility.Visible;
    public Visibility MapVisibility => page == "map" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CompareVisibility => page == "compare" ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EvidenceVisibility => page == "evidence" ? Visibility.Visible : Visibility.Collapsed;
    public UsbSnapshot? Snapshot => current;
    public DeviceItem? SelectedDevice
    {
        get => selectedDevice;
        set { selectedDevice = value; selectedNode = value?.Node; SelectedRole = value == null ? DeviceRole.Unknown : Role(value.Node); Changed(); Changed(nameof(SelectedRole)); Changed(nameof(SelectedRoleOption)); Changed(nameof(SelectedName)); Changed(nameof(SelectedSummary)); Changed(nameof(SelectedDetails)); Changed(nameof(SelectedFindings)); }
    }
    public PortItem? SelectedPort { get => selectedPort; set { selectedPort = value; Changed(); } }
    public HistoryItem? SelectedHistory { get => selectedHistory; set { selectedHistory = value; Changed(); } }
    public string MapLabel { get => mapLabel; set { mapLabel = value; Changed(); } }
    public string ControllerCount => Count(NodeKind.Controller).ToString();
    public string DeviceCount => current?.Devices.Count().ToString() ?? "–";
    public string PortCount => current?.Ports.Count().ToString() ?? "–";
    public string FreeCount => current?.Ports.Count(p => p.State == PortState.Empty).ToString() ?? "–";
    public string PortSummary => current == null ? "No scan yet" : $"{current.Ports.Count(p => p.State == PortState.Connected)} connected · {FreeCount} empty · {current.Ports.Count(p => p.State == PortState.Unknown)} unknown";
    public string InputCount => current == null ? "–" : current.Devices.Count(d => Role(d) is DeviceRole.Mouse or DeviceRole.Keyboard or DeviceRole.Gamepad or DeviceRole.Receiver || d.FunctionRoles.Any(r => r is DeviceRole.Mouse or DeviceRole.Keyboard)).ToString();
    public string ProblemCount => findings.Count(f => f.Status == Assessment.PROBLEM).ToString();
    public string TestCount => findings.Count(f => f.Status == Assessment.PRUEFEN).ToString();
    public string UnknownCount => findings.Count(f => f.Status == Assessment.NICHT_BEWERTBAR).ToString();
    public string ScanTime => current == null ? "No snapshot yet" : $"Snapshot {current.CapturedUtc.ToLocalTime():yyyy-MM-dd · HH:mm:ss} · {current.Revision.ToString()[..8]}";
    public bool Incomplete => current == null || Count(NodeKind.Controller) == 0 || current.Warnings.Length > 0 || current.Nodes.Any(n => n.Unknowns.Length > 0);
    public string StatusTitle => ConfigurationSummary.Create(current, findings).Title;
    public string StatusBody => ConfigurationSummary.Create(current, findings).Body;
    public string AssessmentCounts => $"{ProblemCount} confirmed problems · {TestCount} comparison tests · {UnknownCount} rule/device cases cannot be assessed (not an error count)";
    public string Coverage => "45 rule contracts · only supported preconditions are evaluated · not a latency benchmark";
    public string SelectedName => selectedNode == null ? "Select a device" : EnglishDisplay.NodeName(selectedNode);
    public string SelectedSummary => selectedNode == null ? "Select a device card or a node in the USB tree."
        : !selectedNode.IsPhysical ? $"{EnglishDisplay.NodeName(selectedNode)}\n{(selectedNode.Kind == NodeKind.Port ? StateText(selectedNode.State) : "Part of the captured USB topology")}\n{(selectedNode.Kind == NodeKind.Port ? PortLabel(selectedNode.Id) : "Physical location unknown")}" : $"{RoleDescription(selectedNode)} · {SpeedText(selectedNode.Speed)}\nPort: {PortLabel(selectedNode.ParentId)}\n{(selectedNode.HubDepth == 0 ? "Connected directly to the root hub" : $"Behind {selectedNode.HubDepth} intermediate hub(s)")}\nManufacturer: {EnglishDisplay.Manufacturer(selectedNode.Manufacturer)}";
    public string SelectedDetails
    {
        get
        {
            if (selectedNode == null || current == null) return "No selection.";
            var n = selectedNode; var graph = new UsbDomainGraph(current);
            return $"VID/PID: {n.VendorId}:{n.ProductId}\nInstance: {n.InstanceId ?? "UNKNOWN"}\nContainer: {n.ContainerId ?? "UNKNOWN"}\nLocationPath: {n.LocationPath ?? "UNKNOWN"}\nController: {n.ControllerId}\nVerified controller class: {current.Find(n.ControllerId)?.ControllerClass ?? n.ControllerClass}\nModel hint: {ControllerPlacementHint.For(current, n).Label}\nHub chain: {string.Join(" → ", graph.HubChain(n).Select(EnglishDisplay.NodeName))}\nUpstreams: {string.Join(", ", graph.Upstreams(n))}\nTT: {n.Tt}; Multi-TT capable: {n.MultiTtCapable?.ToString() ?? "UNKNOWN"}\nSuperSpeed capable: {n.SuperSpeedCapable?.ToString() ?? "UNKNOWN"}\nLogical interfaces: {n.LogicalInterfaces.Length}\nDevice descriptor: {n.DeviceDescriptorHex ?? "UNKNOWN"}\nConfiguration descriptor: {n.ConfigurationDescriptorHex ?? "UNKNOWN"}\n\nOpen pipes (not a load measurement):\n{string.Join('\n', n.OpenPipes.Select(p => $"0x{p.Address:X2} Type={p.TransferType} bInterval={p.Interval} MaxPacket={p.MaxPacketSize} Service={p.ServiceIntervalUs(n.Speed)?.ToString() ?? "UNKNOWN"} µs"))}\n\nOffered endpoints (not proof of activity):\n{string.Join('\n', n.OfferedEndpoints.Select(p => $"Interface {p.InterfaceNumber} Alt {p.AlternateSetting} 0x{p.Address:X2} Type={p.TransferType} bInterval={p.Interval}"))}\n\nData gaps:\n{string.Join('\n', n.Unknowns.Select(EnglishDisplay.ScanDetail))}";
        }
    }
    public string SelectedFindings => SelectedDevice == null ? "" : expert
        ? FormatFindings(findings.Where(f => f.NodeId == SelectedDevice.Node.Id && f.Status != Assessment.NICHT_BEWERTBAR))
        : string.Join("\n\n", findings.Where(f => f.NodeId == SelectedDevice.Node.Id && f.Status is Assessment.PROBLEM or Assessment.PRUEFEN).Select(f => f.Explanation)) is var actionable && actionable.Length > 0
            ? actionable : "No configuration problem is established in the observed properties. Other properties cannot yet be assessed. Assessment details above explain the limits of the check.";
    public string FindingsText => FormatFindings(findings.Where(f => f.Status != Assessment.NICHT_BEWERTBAR));
    public string UnknownText => string.Join("\n\n", findings.Where(f => f.Status == Assessment.NICHT_BEWERTBAR).GroupBy(f => f.RuleId)
        .Select(g => $"{g.Key} · {g.First().Title}\nMissing: {string.Join("; ", g.First().Missing)}"));
    public string ScanWarnings => current == null ? "" : string.Join('\n', current.Warnings.Concat(current.Nodes.Where(n => n.Unknowns.Length > 0).Select(n => EnglishDisplay.NodeName(n) + ": " + string.Join("; ", n.Unknowns))).Select(EnglishDisplay.ScanDetail));
    public string PortMapText => current == null ? "" : string.Join('\n', settings.PortMappings.Select(m => $"{m.Label} · {(mapper.Resolve(current, m.PortId, settings.PortMappings) == null ? "Mapping no longer confirmed — check again" : "Confirmed")}\n{m.PortId}"));
    public string GuideText
    {
        get
        {
            if (current == null) return "1  Refresh and select a device.\n2  Map an available port.\n3  Reconnect manually and refresh again.";
            var proposals = advisor.Propose(current, findings, settings.PortMappings);
            var p = proposals.FirstOrDefault();
            return p == null ? "No supported reconnection recommendation is available. Unnecessary port changes are not required.\n\nFor your own comparison: select a device → set a baseline → choose a known socket → reconnect → refresh."
                : $"1  Select a device\n{(current.Find(p.DeviceId) is { } proposed ? EnglishDisplay.NodeName(proposed) : "Device")}\n\n2  Check the port\n{p.Instruction}\n\n3  Check again\nRefresh after reconnecting manually.\n\nReason: {p.Reason}\nExpected observation: {p.ExpectedChange}\nLimitation: {p.Limitations}";
        }
    }
    public string ComparisonText
    {
        get
        {
            if (comparison == null || current == null || baseline == null) return "Set a starting snapshot as the baseline. Then reconnect a device and refresh.";
            var builder = new StringBuilder(comparison.Conclusion + "\n\n");
            foreach (var d in comparison.Devices)
            {
                builder.AppendLine($"{((baseline.Find(d.BeforeId) ?? current.Find(d.AfterId)) is { } compared ? EnglishDisplay.NodeName(compared) : "Device")} · {EnglishDisplay.ChangeKind(d.Kind)}");
                builder.AppendLine($"Identity: {d.IdentityConfidence}");
                builder.AppendLine($"Before: {LabelAt(baseline, d.BeforePort)}\nAfter: {LabelAt(current, d.AfterPort)}");
                builder.AppendLine($"Previous controller: {d.BeforeController ?? "not captured"}\nCurrent controller: {d.AfterController ?? "not captured"}");
                builder.AppendLine($"Previous hubs: {string.Join(" → ", d.BeforeHubs)}\nCurrent hubs: {string.Join(" → ", d.AfterHubs)}");
                foreach (var change in d.Changes) builder.AppendLine("• " + change);
                builder.AppendLine();
            }
            builder.AppendLine("Resolved problems: " + (comparison.ResolvedProblems.Length == 0 ? "none established" : string.Join(", ", comparison.ResolvedProblems)));
            builder.AppendLine("New problems: " + (comparison.NewProblems.Length == 0 ? "none established in reliably matched devices" : string.Join(", ", comparison.NewProblems)));
            builder.AppendLine($"Still cannot be assessed: {UnknownCount} rule/device combinations.");
            return builder.ToString();
        }
    }
    public string ComparisonSummary => comparison?.Conclusion ?? "Set a baseline and refresh after reconnecting.";
    public string BaselineText => baseline == null ? "No baseline" : $"Baseline: {baseline.CapturedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} · {baseline.Revision.ToString()[..8]}";
    public string PreferredText => settings.Preferred == null ? "No preferred setup yet" : "Preferred revision: " + settings.Preferred.ToString()![..8];
    public bool NodeChanged(string nodeId) => comparison?.Devices.Any(d => d.AfterId == nodeId && d.Kind == "Geändert") == true;
    private async Task RefreshAsync()
    {
        if (busy) return;
        Busy = true; Error = ""; Message = "Reading USB connections and running a verification scan…";
        try
        {
            var scan = await scanner.ScanAsync(); await manager.SaveAsync(scan);
            current = scan; manualMatches.Clear(); history = await manager.HistoryAsync();
            if (baseline == null) { baseline = scan; settings = settings with { Baseline = scan.Revision }; await manager.SaveSettingsAsync(settings); }
            Populate(); Message = "Snapshot saved. Click nodes for details. CPU/chipset hints are model-based inferences; colors indicate topology, not a performance ranking.";
        }
        finally { Busy = false; }
    }
    private void Populate()
    {
        if (current == null) return;
        var selectedId = SelectedDevice?.Node.Id;
        findings = rules.Evaluate(current, BuildRuleInputs(current));
        Devices.Clear(); foreach (var d in current.Devices) Devices.Add(Item(d));
        Ports.Clear(); foreach (var p in current.Ports.Where(p => p.UserConnectable != false)) Ports.Add(new(p.Id, $"{PortLabel(p.Id)} · {StateText(p.State)}"));
        History.Clear(); foreach (var h in history.Reverse()) History.Add(new(h, $"{h.CapturedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} · {h.Devices.Count()} devices · {h.Revision.ToString()[..8]}"));
        SelectedDevice = Devices.FirstOrDefault(d => d.Node.Id == selectedId) ?? Devices.FirstOrDefault();
        Recompare(); ChangedAll(); TopologyChanged?.Invoke(this, EventArgs.Empty);
    }
    private void Recompare()
    {
        BeforeDevices.Clear(); AfterDevices.Clear(); Deltas.Clear();
        if (baseline == null || current == null) { comparison = null; return; }
        comparison = manager.Compare(baseline, current, rules.Evaluate(baseline), findings, manualMatches);
        var changes = rules.EvaluateChanges(baseline, current, comparison);
        findings = findings.Where(f => f.RuleId != "R30" || !changes.Any(c => c.NodeId == f.NodeId)).Concat(changes).ToArray();
        foreach (var d in baseline.Devices) BeforeDevices.Add(Item(d, baseline)); foreach (var d in current.Devices) AfterDevices.Add(Item(d, current));
        foreach (var d in comparison.Devices.OrderBy(d => d.Kind == "Unverändert" ? 1 : 0))
        {
            var a = baseline.Find(d.BeforeId); var b = current.Find(d.AfterId);
            Deltas.Add(new((a ?? b) is { } shown ? EnglishDisplay.NodeName(shown) : "Device", EnglishDisplay.ChangeKind(d.Kind), d.IdentityConfidence switch
            {
                "UNIQUE_SERIAL" or "SAME_PATH_SERIAL" => "Unique serial number", "USER_CONFIRMED" => "Identity confirmed by you",
                "SAME_PATH_ONLY" => "Same path; identity uncertain if the device was replaced", "AMBIGUOUS" => "Please confirm the device identity", _ => "Identity not established yet"
            }, LabelAt(baseline, d.BeforePort), LabelAt(current, d.AfterPort),
            a == null ? "Not captured" : $"{SpeedText(a.Speed)} · {a.HubDepth} intermediate hub(s)",
            b == null ? "Not captured" : $"{SpeedText(b.Speed)} · {b.HubDepth} intermediate hub(s)",
            d.Changes.Length == 0 ? "No observable change" : string.Join(" · ", d.Changes)));
        }
    }
    private RuleObservations BuildRuleInputs(UsbSnapshot snapshot)
    {
        var evidence = new Dictionary<string, string>();
        var feasible = new List<string>();
        foreach (var device in snapshot.Devices)
        {
            var target = snapshot.Ports.FirstOrDefault(p => p.Id != device.ParentId && mapper.IsAvailableTarget(snapshot, p)
                && snapshot.Find(p.ParentId)?.Kind == NodeKind.RootHub && mapper.Resolve(snapshot, p.Id, settings.PortMappings) != null);
            if (target == null) continue;
            feasible.Add(device.Id); evidence["TEST_FEASIBLE:" + device.Id] = "USER_CONFIRMED_PORT_MAP:" + target.Id;
        }
        return RuleObservations.Empty(snapshot.Revision) with { TestFeasibleDevices = feasible.ToArray(), ValidatedEvidence = evidence };
    }
    private async Task MapAsync()
    {
        if (current == null || SelectedPort == null) throw new InvalidOperationException("Select a port first.");
        var mapping = mapper.Confirm(current, SelectedPort.Id, MapLabel, settings.PortMappings);
        settings = settings with { PortMappings = settings.PortMappings.Where(m => m.PortId != mapping.PortId).Append(mapping).ToArray() };
        await manager.SaveSettingsAsync(settings); Populate(); Message = "Socket saved as a user-confirmed mapping.";
    }
    private async Task ExportAsync()
    {
        if (current == null) throw new InvalidOperationException("Capture a snapshot first.");
        string path = Path.Combine(manager.DirectoryPath, "exports", "usb-atlas-" + DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss") + ".json");
        await manager.WriteAtomicAsync(path, new { schema = "usb-atlas-comparison/1", exportedUtc = DateTimeOffset.UtcNow, baseline, current, userAssertions = settings, findings, comparison, manualMatches });
        Message = "Export saved locally (includes device identifiers): " + path;
    }
    public void SelectNode(UsbNode node)
    {
        SelectedDevice = Devices.FirstOrDefault(d => d.Node.Id == node.Id);
        selectedNode = node;
        Changed(nameof(SelectedName)); Changed(nameof(SelectedSummary)); Changed(nameof(SelectedDetails));
        if (node.Kind == NodeKind.Port) { SelectedPort = Ports.FirstOrDefault(p => p.Id == node.Id); Message = $"{PortLabel(node.Id)} · {StateText(node.State)}. A logical port does not confirm a physical socket location."; }
        else if (node.Kind is NodeKind.Controller or NodeKind.RootHub && current != null) Message = $"{EnglishDisplay.NodeName(node)} · {ControllerPlacementHint.For(current, node).Label} · Open details for the reasoning.";
    }
    public string PortLabel(string? id) => current == null ? "Not mapped" : LabelAt(current, id);
    private string LabelAt(UsbSnapshot s, string? id)
    {
        if (id == null) return "Not captured";
        var mapped = mapper.Resolve(s, id, settings.PortMappings); if (mapped != null) return mapped.Label;
        var port = s.Find(id); int group = Array.FindIndex(s.Nodes.Where(n => n.Kind == NodeKind.Controller).ToArray(), n => n.Id == port?.ControllerId) + 1;
        var root = id.IndexOf("/root/", StringComparison.Ordinal);
        return $"Unknown socket · USB group {group} / path {(root >= 0 ? id[(root + 6)..] : id.Split('/').Last())}";
    }
    private int Count(NodeKind kind) => current?.Nodes.Count(n => n.Kind == kind) ?? 0;
    private static string RoleKey(UsbNode d) => d.SerialKey ?? "PATH:" + d.Id + ":" + d.InstanceId;
    private DeviceRole Role(UsbNode d) => settings.Roles.FirstOrDefault(r => r.IdentityKey == RoleKey(d))?.Role ?? d.Role;
    private string RoleDescription(UsbNode d) => settings.Roles.Any(r => r.IdentityKey == RoleKey(d)) || d.FunctionRoles.Length == 0
        ? RoleText(Role(d)) : string.Join(" / ", d.FunctionRoles.Select(RoleText)) + " (one device)";
    private DeviceItem Item(UsbNode d, UsbSnapshot? snapshot = null) => new(d, EnglishDisplay.NodeName(d), $"{RoleDescription(d)} · {SpeedText(d.Speed)}", Glyph(d.Role == DeviceRole.Other && d.FunctionRoles.Length > 0 ? d.FunctionRoles[0] : Role(d)), LabelAt(snapshot ?? current!, d.ParentId));
    private static string FormatFindings(IEnumerable<Finding> list) => string.Join("\n\n", list.Select(f => $"{EnglishDisplay.AssessmentName(f.Status)} · {f.RuleId}\n{f.Explanation}\nObserved: {string.Join("; ", f.Observed)}\nSource: {string.Join("; ", f.Sources)}\nLimitation: {f.Limitations}"));
    public static string SpeedText(LinkSpeed speed) => speed switch { LinkSpeed.Low => "1.5 Mbit/s", LinkSpeed.Full => "12 Mbit/s", LinkSpeed.High => "480 Mbit/s", LinkSpeed.Super => "SuperSpeed", LinkSpeed.SuperPlus => "SuperSpeedPlus", _ => "Speed unknown" };
    public static string StateText(PortState state) => state switch { PortState.Empty => "confirmed empty", PortState.Connected => "connected", PortState.Error => "Connection error", _ => "State unknown" };
    public static string RoleText(DeviceRole role) => role switch { DeviceRole.Mouse => "Mouse", DeviceRole.Keyboard => "Keyboard", DeviceRole.Gamepad => "Gamepad", DeviceRole.Receiver => "Receiver", DeviceRole.Camera => "Camera", DeviceRole.Audio => "Audio", DeviceRole.Storage => "Storage", DeviceRole.Hub => "Hub", DeviceRole.Capture => "Capture", DeviceRole.InternalControl => "Internal control", DeviceRole.Other => "Multiple / other functions", _ => "Role unknown" };
    public static string Glyph(DeviceRole role) => role switch { DeviceRole.Mouse => "\uE962", DeviceRole.Keyboard => "\uE765", DeviceRole.Gamepad => "\uE7FC", DeviceRole.Receiver => "\uE704", DeviceRole.Camera => "\uE722", DeviceRole.Audio => "\uE767", DeviceRole.Storage => "\uEDA2", DeviceRole.Hub => "\uE8CE", DeviceRole.Capture => "\uE714", _ => "\uE88E" };
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    private void ChangedAll() => PropertyChanged?.Invoke(this, new(null));
}
