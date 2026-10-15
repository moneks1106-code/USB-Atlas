using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using UsbAtlas.Core;
using UsbAtlas.Windows;

namespace UsbAtlas.UI;

public partial class App : Application
{
    private Window? window;

    public App()
    {
        UnhandledException += (_, e) => WriteCrash(new Exception(e.Message, e.Exception));
        DebugSettings.XamlResourceReferenceFailed += (_, e) => WriteCrash(new Exception("XAML resource: " + e.Message));
        AppDomain.CurrentDomain.UnhandledException += (_, e) => WriteCrash(e.ExceptionObject as Exception);
        Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en-US";
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        InitializeComponent();
    }

    private static void WriteCrash(Exception? exception)
    {
        try
        {
            var folder = DataDirectory();
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "last-error.txt"), exception?.ToString() ?? "Unknown startup error");
        }
        catch
        {
            // No recursive failure while reporting a fatal startup error.
        }
    }

    private static string DataDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UsbAtlas");

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var services = new ServiceCollection()
            .AddSingleton<DiagnosticActivityGate>()
            .AddSingleton<IUsbTopologyScanner, UsbTopologyScanner>()
            .AddSingleton<UsbRuleEngine>()
            .AddSingleton(new UsbSnapshotManager(DataDirectory()))
            .AddSingleton<MainViewModel>()
            .BuildServiceProvider();
        window = new MainWindow(services.GetRequiredService<MainViewModel>());
        window.Closed += (_, _) => services.Dispose();
        window.Activate();
    }
}
