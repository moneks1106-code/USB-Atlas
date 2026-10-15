using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using UsbAtlas.Core;

namespace UsbAtlas.UI;

/// <summary>State of the single tree page. Free of WinUI types so that it can be unit-tested.</summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IUsbTopologyScanner scanner;
    private readonly UsbSnapshotManager manager;
    private readonly UsbRuleEngine rules;
    private readonly UsbObservationStore observations;
    private UsbSnapshot? current;
    private RuleObservations? currentObservations;
    private Finding[] findings = [];
    private UsbNode? selectedNode;
    private bool busy, expert, showEmptyPorts;
    private string message = "Ready for your first USB scan.";
    private string error = "";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? TopologyChanged;

    public MainViewModel(IUsbTopologyScanner scanner, UsbSnapshotManager manager, UsbRuleEngine rules, UsbObservationStore observations)
    {
        this.scanner = scanner;
        this.manager = manager;
        this.rules = rules;
        this.observations = observations;
        RefreshCommand = new AsyncCommand(RefreshAsync, ex => Error = ex.Message);
    }

    public ICommand RefreshCommand { get; }

    public async Task InitializeAsync()
    {
        try
        {
            var saved = await manager.LoadLatestAsync();
            if (saved != null)
            {
                await ApplyAsync(saved);
                Message = "Last saved snapshot. Refresh to capture the current state." + ObservationNote;
            }
            else
            {
                await RefreshAsync();
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    public bool Busy
    {
        get => busy;
        private set { busy = value; Changed(); Changed(nameof(WorkEnabled)); }
    }
    public bool WorkEnabled => !busy;

    public string Message
    {
        get => message;
        private set { message = value; Changed(); }
    }

    public string Error
    {
        get => error;
        private set { error = value; Changed(); Changed(nameof(HasError)); }
    }
    public bool HasError => error.Length > 0;

    public bool Expert
    {
        get => expert;
        set { expert = value; Changed(); Changed(nameof(SelectedFindings)); }
    }

    public bool ShowEmptyPorts
    {
        get => showEmptyPorts;
        set { showEmptyPorts = value; Changed(); TopologyChanged?.Invoke(this, EventArgs.Empty); }
    }

    public UsbSnapshot? Snapshot => current;
    public IReadOnlyList<Finding> Findings => findings;
    public UsbNode? SelectedNode => selectedNode;

    public string ScanTime => current == null
        ? "No snapshot yet"
        : $"Snapshot {current.CapturedUtc.ToLocalTime():yyyy-MM-dd · HH:mm:ss}";

    public string StatusTitle => ConfigurationSummary.Create(current, findings).Title;
    public string StatusBody => ConfigurationSummary.Create(current, findings).Body;

    public string AssessmentCounts
    {
        get
        {
            int problems = findings.Count(f => f.Status == Assessment.Problem);
            int checks = findings.Count(f => f.Status == Assessment.Check);
            int notes = findings.Count(f => f.Status == Assessment.Note);
            int unknown = findings.Count(f => f.Status == Assessment.NotAssessable);
            return $"{problems} confirmed problems · {checks} comparison tests · {notes} notes · {unknown} rule/device cases cannot be assessed (missing observations, not errors)";
        }
    }

    public string Coverage => $"{findings.Select(f => f.RuleId).Distinct().Count()} rule contracts · only supported preconditions are evaluated · not a latency benchmark";

    public string FindingsText
    {
        get
        {
            var shown = findings.Where(f => f.Status != Assessment.NotAssessable)
                .OrderBy(f => f.Status switch { Assessment.Problem => 0, Assessment.Check => 1, Assessment.Note => 2, _ => 3 })
                .ToArray();
            return shown.Length == 0 ? "No findings. The observed properties give no reason for a note." : string.Join("\n\n", shown.Select(Format));
        }
    }

    /// <summary>Rules whose preconditions this scanner did not observe, grouped by rule.</summary>
    public string UnknownText => string.Join("\n\n", findings.Where(f => f.Status == Assessment.NotAssessable).GroupBy(f => f.RuleId)
        .Select(g => $"{g.Key} · {g.First().Title}\nMissing: {string.Join("; ", g.First().Missing)}"));

    public string ScanWarnings
    {
        get
        {
            if (current == null) return "";
            var lines = current.Warnings.Concat(current.Nodes
                .Where(n => n.Unknowns.Length > 0)
                .Select(n => NodeName(n) + ": " + string.Join("; ", n.Unknowns)));
            var text = string.Join('\n', lines);
            return text.Length == 0 ? "All queried properties were read." : text;
        }
    }

    public string SelectedName => selectedNode == null ? "Select a node" : NodeName(selectedNode);

    public string SelectedSummary
    {
        get
        {
            if (selectedNode == null) return "Click a node in the USB tree.";
            var n = selectedNode;
            return n.Kind switch
            {
                NodeKind.Controller => $"Host controller\nPCI {n.VendorId ?? "????"}:{n.ProductId ?? "????"}\n{n.LocationPath ?? "Location path unknown"}",
                NodeKind.RootHub => $"Root hub of {ControllerLabel(n.ControllerId)}\n{PortCount(n)} logical ports",
                NodeKind.Port => $"{StateText(n.State)}\n{PortLabel(n.Id)}\n{(n.ConnectorTypeC == true ? "USB-C connector" : n.UserConnectable == false ? "Not user-connectable (internal)" : "User-connectable")}",
                _ => $"{RoleDescription(n)} · {SpeedText(n.Speed)}\nPort: {PortLabel(n.ParentId)}\n{(n.HubDepth == 0 ? "Connected directly to the root hub" : $"Behind {n.HubDepth} intermediate hub(s)")}\nManufacturer: {n.Manufacturer ?? "unknown"}"
            };
        }
    }

    public string SelectedDetails
    {
        get
        {
            if (selectedNode == null || current == null) return "No selection.";
            var n = selectedNode;
            var graph = new UsbDomainGraph(current);
            var text = new StringBuilder();
            text.AppendLine($"VID/PID: {n.VendorId ?? "?"}:{n.ProductId ?? "?"}");
            text.AppendLine($"Instance: {n.InstanceId ?? "UNKNOWN"}");
            text.AppendLine($"Container: {n.ContainerId ?? "UNKNOWN"}");
            text.AppendLine($"LocationPath: {n.LocationPath ?? "UNKNOWN"}");
            text.AppendLine($"Controller: {n.ControllerId ?? "UNKNOWN"}");
            text.AppendLine($"Model hint: {ControllerPlacementHint.For(current, n).Label}");
            text.AppendLine($"Hub chain: {string.Join(" → ", graph.HubChain(n).Select(NodeName))}");
            text.AppendLine($"TT: {n.Tt}; Multi-TT capable: {n.MultiTtCapable?.ToString() ?? "UNKNOWN"}");
            text.AppendLine($"SuperSpeed capable: {n.SuperSpeedCapable?.ToString() ?? "UNKNOWN"}");
            text.AppendLine($"Logical interfaces: {n.LogicalInterfaces.Length}");
            text.AppendLine($"Device descriptor: {n.DeviceDescriptorHex ?? "UNKNOWN"}");
            text.AppendLine($"Configuration descriptor: {n.ConfigurationDescriptorHex ?? "UNKNOWN"}");
            text.AppendLine();
            text.AppendLine("Open pipes (not a load measurement):");
            foreach (var p in n.OpenPipes)
                text.AppendLine($"0x{p.Address:X2} Type={p.TransferType} bInterval={p.Interval} MaxPacket={p.MaxPacketSize} Service={p.ServiceIntervalUs(n.Speed)?.ToString() ?? "UNKNOWN"} µs");
            text.AppendLine();
            text.AppendLine("Offered endpoints (not proof of activity):");
            foreach (var p in n.OfferedEndpoints)
                text.AppendLine($"Interface {p.InterfaceNumber} Alt {p.AlternateSetting} 0x{p.Address:X2} Type={p.TransferType} bInterval={p.Interval}");
            text.AppendLine();
            text.AppendLine("Data gaps:");
            text.Append(n.Unknowns.Length == 0 ? "none" : string.Join('\n', n.Unknowns));
            return text.ToString();
        }
    }

    public string SelectedFindings
    {
        get
        {
            if (selectedNode == null) return "";
            var own = findings.Where(f => f.NodeId == selectedNode.Id && f.Status != Assessment.NotAssessable).ToArray();
            if (expert) return own.Length == 0 ? "No findings for this node." : string.Join("\n\n", own.Select(Format));
            var actionable = own.Where(f => f.Status is Assessment.Problem or Assessment.Check).Select(f => f.Explanation).ToArray();
            return actionable.Length > 0
                ? string.Join("\n\n", actionable)
                : "No configuration problem is established in the observed properties. Other properties cannot yet be assessed. Expert mode shows the confirmed properties.";
        }
    }

    public void SelectNode(UsbNode node)
    {
        selectedNode = node;
        Changed(nameof(SelectedNode));
        Changed(nameof(SelectedName));
        Changed(nameof(SelectedSummary));
        Changed(nameof(SelectedDetails));
        Changed(nameof(SelectedFindings));
        if (node.Kind == NodeKind.Port)
            Message = $"{PortLabel(node.Id)} · {StateText(node.State)}. A logical port number does not identify a physical socket.";
        else if (node.Kind is NodeKind.Controller or NodeKind.RootHub && current != null)
            Message = $"{NodeName(node)} · {ControllerPlacementHint.For(current, node).Label} · open the details for the reasoning.";
    }

    /// <summary>Human-readable position of a logical port, e.g. "Port 3 on root hub of controller 2".</summary>
    public string PortLabel(string? portId)
    {
        var port = current?.Find(portId);
        if (port == null) return "Port unknown";
        var hub = current!.Find(port.ParentId);
        string on = hub == null ? "unknown hub"
            : hub.Kind == NodeKind.RootHub ? "root hub of " + ControllerLabel(hub.ControllerId)
            : "hub \"" + NodeName(hub) + "\"";
        return $"{NodeName(port)} on {on}";
    }

    private string ControllerLabel(string? controllerId)
    {
        if (current == null) return "controller";
        int index = current.Controllers.ToList().FindIndex(c => c.Id == controllerId);
        return index < 0 ? "unknown controller" : $"controller {index + 1}";
    }

    private int PortCount(UsbNode hub) => current?.Ports.Count(p => p.ParentId == hub.Id) ?? 0;

    private async Task RefreshAsync()
    {
        if (busy) return;
        Busy = true;
        Error = "";
        Message = "Reading USB connections and running a verification scan…";
        try
        {
            var scan = await scanner.ScanAsync();
            await manager.SaveLatestAsync(scan);
            await ApplyAsync(scan);
            Message = "Snapshot saved. Click a node for details. Colors mark the tree level and path, not a performance ranking." + ObservationNote;
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>Text appended to the status message when an external observation file was applied.</summary>
    public string ObservationNote => currentObservations == null ? "" : " External observations applied from " + observations.PathFor(currentObservations.Revision) + ".";
    public bool HasObservations => currentObservations != null;

    private async Task ApplyAsync(UsbSnapshot snapshot)
    {
        current = snapshot;
        try
        {
            currentObservations = await observations.LoadAsync(snapshot.Revision);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            currentObservations = null;
            Error = "Observation file ignored: " + ex.Message;
        }
        findings = rules.Evaluate(snapshot, currentObservations);
        selectedNode = selectedNode == null ? null : snapshot.Find(selectedNode.Id);
        ChangedAll();
        TopologyChanged?.Invoke(this, EventArgs.Empty);
    }

    private string Format(Finding f)
    {
        string where = current?.Find(f.NodeId) is { } node ? NodeName(node) : f.NodeId;
        return $"{StatusText(f.Status)} · {where}\n{f.Title}\n{f.Explanation}\nObserved: {string.Join("; ", f.Observed)}\nSource: {string.Join("; ", f.Sources)}\nLimitation: {f.Limitations}";
    }

    private static string RoleDescription(UsbNode d) => d.FunctionRoles.Length > 0
        ? string.Join(" / ", d.FunctionRoles.Select(RoleText)) + " (one device)"
        : RoleText(d.Role);

    public static string NodeName(UsbNode node) => node.Kind switch
    {
        NodeKind.Port when node.PortNumber is { } number => $"Port {number}",
        NodeKind.RootHub => "Root hub",
        _ => node.Name
    };

    public static string SpeedText(LinkSpeed speed) => speed switch
    {
        LinkSpeed.Low => "1.5 Mbit/s",
        LinkSpeed.Full => "12 Mbit/s",
        LinkSpeed.High => "480 Mbit/s",
        LinkSpeed.Super => "SuperSpeed",
        LinkSpeed.SuperPlus => "SuperSpeedPlus",
        _ => "Speed unknown"
    };

    public static string StatusText(Assessment status) => status switch
    {
        Assessment.Ok => "OK",
        Assessment.Note => "NOTE",
        Assessment.Check => "CHECK",
        Assessment.Problem => "PROBLEM",
        _ => "NOT ASSESSABLE"
    };

    public static string StateText(PortState state) => state switch
    {
        PortState.Empty => "confirmed empty",
        PortState.Connected => "connected",
        PortState.Error => "Connection error",
        _ => "State unknown"
    };

    public static string RoleText(DeviceRole role) => role switch
    {
        DeviceRole.Mouse => "Mouse",
        DeviceRole.Keyboard => "Keyboard",
        DeviceRole.Gamepad => "Gamepad",
        DeviceRole.Receiver => "Receiver",
        DeviceRole.Camera => "Camera",
        DeviceRole.Audio => "Audio",
        DeviceRole.Storage => "Storage",
        DeviceRole.Hub => "Hub",
        DeviceRole.Capture => "Capture",
        DeviceRole.InternalControl => "Internal control",
        DeviceRole.Other => "Multiple / other functions",
        _ => "Role unknown"
    };

    public static string Glyph(DeviceRole role) => role switch
    {
        DeviceRole.Mouse => "",
        DeviceRole.Keyboard => "",
        DeviceRole.Gamepad => "",
        DeviceRole.Receiver => "",
        DeviceRole.Camera => "",
        DeviceRole.Audio => "",
        DeviceRole.Storage => "",
        DeviceRole.Hub => "",
        DeviceRole.Capture => "",
        _ => ""
    };

    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    private void ChangedAll() => PropertyChanged?.Invoke(this, new(null));
}
