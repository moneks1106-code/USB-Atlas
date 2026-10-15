using System.ComponentModel;
using UsbAtlas.Core;
using UsbAtlas.UI;

namespace UsbAtlas.Tests;

public class MainViewModelTests : IDisposable
{
    private sealed class FakeScanner(Func<UsbSnapshot> produce) : IUsbTopologyScanner
    {
        public int Calls;
        public Task<UsbSnapshot> ScanAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(produce());
        }
    }

    private readonly string directory = Path.Combine(Path.GetTempPath(), "UsbAtlasTests", Guid.NewGuid().ToString());

    private MainViewModel Create(FakeScanner scanner) => new(scanner, new UsbSnapshotManager(directory), new UsbRuleEngine(new UsbEvidenceRegistry()), new UsbObservationStore(directory));

    private async Task WriteObservations(RuleObservations observations)
    {
        var store = new UsbObservationStore(directory);
        Directory.CreateDirectory(store.FolderPath);
        await File.WriteAllTextAsync(store.PathFor(observations.Revision), System.Text.Json.JsonSerializer.Serialize(observations, UsbSnapshotManager.JsonOptions));
    }

    [Fact]
    public async Task ExternalModeContractTurnsFullSpeedLinkIntoAProblem()
    {
        var snapshot = Fixture.Simple();
        snapshot = snapshot with { Nodes = snapshot.Nodes.Select(n => n.Id == "c/root/1/device" ? n with { Speed = LinkSpeed.Full } : n).ToArray() };
        await WriteObservations(RuleObservations.Empty(snapshot.Revision) with
        {
            Modes = [new("c/root/1/device", 0x81, 8000, true, "mouse-tester session 42", true, 0)]
        });
        var vm = Create(new FakeScanner(() => snapshot));
        await vm.InitializeAsync();
        Assert.True(vm.HasObservations);
        Assert.Contains("External observations applied", vm.Message);
        Assert.Equal("Your USB configuration has one detected problem.", vm.StatusTitle);
        Assert.Contains("mouse-tester session 42", vm.FindingsText);
    }

    [Fact]
    public async Task ObservationFileForAnotherRevisionIsIgnoredWithAnError()
    {
        var snapshot = Fixture.Simple();
        var store = new UsbObservationStore(directory);
        Directory.CreateDirectory(store.FolderPath);
        await File.WriteAllTextAsync(store.PathFor(snapshot.Revision),
            System.Text.Json.JsonSerializer.Serialize(RuleObservations.Empty(Guid.NewGuid()), UsbSnapshotManager.JsonOptions));
        var vm = Create(new FakeScanner(() => snapshot));
        await vm.InitializeAsync();
        Assert.False(vm.HasObservations);
        Assert.StartsWith("Observation file ignored", vm.Error);
        Assert.NotNull(vm.Snapshot);
    }

    [Fact]
    public async Task NoObservationFileMeansNoObservations()
    {
        var vm = Create(new FakeScanner(Fixture.Simple));
        await vm.InitializeAsync();
        Assert.False(vm.HasObservations);
        Assert.Null(await new UsbObservationStore(directory).LoadAsync(vm.Snapshot!.Revision));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Fact]
    public async Task FirstStartScansAndSavesWhenNothingIsStored()
    {
        var scanner = new FakeScanner(Fixture.Simple);
        var vm = Create(scanner);
        await vm.InitializeAsync();
        Assert.Equal(1, scanner.Calls);
        Assert.NotNull(vm.Snapshot);
        Assert.False(vm.Busy);
        Assert.False(vm.HasError);
        Assert.StartsWith("Snapshot saved", vm.Message);
        Assert.True(File.Exists(Path.Combine(directory, "latest.json")));
        Assert.Equal("Your observed USB configuration looks healthy.", vm.StatusTitle);
    }

    [Fact]
    public async Task SecondStartShowsTheSavedSnapshotWithoutScanning()
    {
        var saved = Fixture.Simple();
        await new UsbSnapshotManager(directory).SaveLatestAsync(saved);
        var scanner = new FakeScanner(Fixture.Simple);
        var vm = Create(scanner);
        await vm.InitializeAsync();
        Assert.Equal(0, scanner.Calls);
        Assert.Equal(saved.Revision, vm.Snapshot!.Revision);
        Assert.StartsWith("Last saved snapshot", vm.Message);
    }

    [Fact]
    public async Task ScannerFailureIsShownAndLeavesTheViewUsable()
    {
        var scanner = new FakeScanner(() => throw new IOException("USB topology changed during capture."));
        var vm = Create(scanner);
        await vm.InitializeAsync();
        Assert.True(vm.HasError);
        Assert.Equal("USB topology changed during capture.", vm.Error);
        Assert.False(vm.Busy);
        Assert.True(vm.WorkEnabled);
        Assert.Null(vm.Snapshot);
    }

    [Fact]
    public async Task RefreshCommandClearsEarlierErrorAndRedrawsTopology()
    {
        int calls = 0;
        var scanner = new FakeScanner(() => calls++ == 0 ? throw new IOException("boom") : Fixture.Simple());
        var vm = Create(scanner);
        await vm.InitializeAsync();
        Assert.True(vm.HasError);
        int redraws = 0;
        vm.TopologyChanged += (_, _) => redraws++;
        vm.RefreshCommand.Execute(null);
        await WaitUntil(() => !vm.Busy && vm.Snapshot != null);
        Assert.False(vm.HasError);
        Assert.Equal(1, redraws);
    }

    [Fact]
    public async Task SelectionTextsDescribeTheNode()
    {
        var vm = Create(new FakeScanner(Fixture.Simple));
        await vm.InitializeAsync();
        var snapshot = vm.Snapshot!;
        vm.SelectNode(snapshot.Find("c/root/1/device")!);
        Assert.Equal("Gaming Mouse", vm.SelectedName);
        Assert.Contains("Mouse · 480 Mbit/s", vm.SelectedSummary);
        Assert.Contains("Port: Port 1 on root hub of controller 1", vm.SelectedSummary);
        Assert.Contains("Connected directly to the root hub", vm.SelectedSummary);
        Assert.Contains("Expert mode shows", vm.SelectedFindings);
        vm.Expert = true;
        Assert.Contains("OK · Gaming Mouse", vm.SelectedFindings);
        Assert.Contains("125 µs", vm.SelectedFindings);
        Assert.Contains("Hub chain: Root hub", vm.SelectedDetails);

        vm.SelectNode(snapshot.Find("c/root/2")!);
        Assert.Equal("Port 2", vm.SelectedName);
        Assert.StartsWith("confirmed empty", vm.SelectedSummary);
        Assert.StartsWith("Port 2 on root hub of controller 1", vm.Message);

        vm.SelectNode(snapshot.Find("c/root")!);
        Assert.Equal("Root hub", vm.SelectedName);
        Assert.Contains("2 logical ports", vm.SelectedSummary);
        Assert.Contains("Likely CPU/SoC", vm.Message);
    }

    [Fact]
    public async Task FindingsTextOrdersProblemsFirstAndNamesTheNode()
    {
        var broken = Fixture.Port("c/root", 3, PortState.Error) with { ConnectionStatus = 2 };
        var ssd = Fixture.Device("c/root/2", "External SSD", LinkSpeed.High, hid: false) with { SuperSpeedCapable = true };
        var snapshot = Fixture.Snapshot(Fixture.Controller(), Fixture.RootHub(), Fixture.Port("c/root", 1), Fixture.Device("c/root/1"),
            Fixture.Port("c/root", 2), ssd, broken);
        var vm = Create(new FakeScanner(() => snapshot));
        await vm.InitializeAsync();
        Assert.Equal("Your USB configuration has one detected problem.", vm.StatusTitle);
        Assert.StartsWith("1 confirmed problems · 0 comparison tests", vm.AssessmentCounts);
        Assert.Contains("R44 · ", vm.UnknownText);
        Assert.DoesNotContain("NOT ASSESSABLE", vm.FindingsText);
        Assert.StartsWith("PROBLEM · Port 3", vm.FindingsText);
        Assert.Contains("NOTE · External SSD", vm.FindingsText);
        Assert.Contains("OK · Gaming Mouse", vm.FindingsText);
        Assert.Equal("All queried properties were read.", vm.ScanWarnings);
    }

    [Fact]
    public async Task ScanWarningsListSnapshotAndNodeGaps()
    {
        var snapshot = Fixture.Simple() with { Warnings = ["Hotplug notifications unavailable."] };
        snapshot = snapshot with { Nodes = snapshot.Nodes.Select(n => n.Id == "c/root/1/device" ? n with { Unknowns = ["String/serial descriptor: timeout"] } : n).ToArray() };
        var vm = Create(new FakeScanner(() => snapshot));
        await vm.InitializeAsync();
        Assert.Contains("Hotplug notifications unavailable.", vm.ScanWarnings);
        Assert.Contains("Gaming Mouse: String/serial descriptor: timeout", vm.ScanWarnings);
        Assert.Contains("Missing data limits the assessment", vm.StatusBody);
    }

    [Fact]
    public async Task PortLabelNamesExternalHubs()
    {
        var hub = Fixture.Hub("c/root/1", "Desk Hub");
        var snapshot = Fixture.Snapshot(Fixture.Controller(), Fixture.RootHub(), Fixture.Port("c/root", 1), hub, Fixture.Port(hub.Id, 4), Fixture.Device(hub.Id + "/4") with { HubDepth = 1 });
        var vm = Create(new FakeScanner(() => snapshot));
        await vm.InitializeAsync();
        Assert.Equal("Port 4 on hub \"Desk Hub\"", vm.PortLabel(hub.Id + "/4"));
        Assert.Equal("Port unknown", vm.PortLabel("nope"));
        vm.SelectNode(snapshot.Find(hub.Id + "/4/device")!);
        Assert.Contains("Behind 1 intermediate hub(s)", vm.SelectedSummary);
    }

    [Fact]
    public async Task TogglesRaisePropertyChangesAndRedraws()
    {
        var vm = Create(new FakeScanner(Fixture.Simple));
        await vm.InitializeAsync();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        int redraws = 0;
        vm.TopologyChanged += (_, _) => redraws++;
        vm.ShowEmptyPorts = true;
        vm.Expert = true;
        Assert.Contains(nameof(MainViewModel.ShowEmptyPorts), changed);
        Assert.Contains(nameof(MainViewModel.Expert), changed);
        Assert.Contains(nameof(MainViewModel.SelectedFindings), changed);
        Assert.Equal(1, redraws);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition());
    }
}
