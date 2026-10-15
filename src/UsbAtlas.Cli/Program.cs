using System.Text.Json;
using System.Text.Json.Serialization;
using UsbAtlas.Core;
using UsbAtlas.Windows;

var scanner = new UsbTopologyScanner(new DiagnosticActivityGate());
var snapshot = await scanner.ScanAsync();
var options = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
if (args.Length == 2 && args[0] == "--output")
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
    await File.WriteAllTextAsync(Path.GetFullPath(args[1]), JsonSerializer.Serialize(snapshot, options));
    Console.WriteLine($"Snapshot saved. Controllers={snapshot.Nodes.Count(n => n.Kind == NodeKind.Controller)}, Ports={snapshot.Ports.Count()}, Devices={snapshot.Devices.Count()}, consistent={snapshot.Consistent}");
}
else
    Console.WriteLine(JsonSerializer.Serialize(snapshot, options));
