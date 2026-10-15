using UsbAtlas.Core;

namespace UsbAtlas.UI;

internal sealed record TopologyNodeColors(string PathColor, string LevelColor, string Background,
    string PathLabel, string LevelLabel);
internal sealed record TopologyLevelColor(string Label, string Color);

// Presentation only. A path color identifies an observed logical branch, not a physical device.
internal static class TopologyColorScheme
{
    internal static readonly TopologyLevelColor[] Levels =
    [new("Controller", "#79B8FF"), new("Hubs", "#BEA1FF"), new("Ports", "#77D8E3"), new("Devices", "#E7C785")];
    private static readonly string[] PathPalette =
    ["#79B8FF", "#BEA1FF", "#F2A5D3", "#F5B974", "#D7DC77", "#7ED8B4",
     "#77D8E3", "#A3B4FF", "#E8AE99", "#B5D393", "#E7C785", "#C5B3E6"];

    internal static Dictionary<string, TopologyNodeColors> Create(IEnumerable<UsbNode> nodes)
    {
        var all = nodes.GroupBy(n => n.Id).Select(g => g.First()).ToDictionary(n => n.Id);
        var controllers = all.Values.Where(n => n.Kind == NodeKind.Controller).OrderBy(n => n.Id, StringComparer.Ordinal).ToArray();
        var ports = all.Values.Where(n => n.Kind == NodeKind.Port).OrderBy(n => n.Id, StringComparer.Ordinal).ToArray();
        var colors = new Dictionary<string, (string Color, string Label)>();
        var usedColors = new HashSet<string>();
        void Assign(UsbNode node, string label)
        {
            uint hash = StableHash(node.Id);
            int first = (int)(hash % (uint)PathPalette.Length);
            string? color = Enumerable.Range(0, PathPalette.Length).Select(i => PathPalette[(first + i) % PathPalette.Length])
                .FirstOrDefault(c => !usedColors.Contains(c));
            for (int step = 0; color == null; step++)
            {
                string candidate = FromHue((hash % 36000) / 100d + step * 137.507764);
                if (!usedColors.Contains(candidate)) color = candidate;
            }
            usedColors.Add(color); colors[node.Id] = (color, label);
        }
        for (int i = 0; i < controllers.Length; i++) Assign(controllers[i], $"Branch {i + 1:00}");
        for (int i = 0; i < ports.Length; i++) Assign(ports[i], $"Path {i + 1:00}");

        (string Color, string Label) Path(UsbNode node)
        {
            var visited = new HashSet<string>();
            var current = node;
            while (visited.Add(current.Id))
            {
                if (colors.TryGetValue(current.Id, out var found)) return found;
                if (current.ParentId == null || !all.TryGetValue(current.ParentId, out var parent)) break;
                current = parent;
            }
            return ("#A9B4BF", "Unknown path");
        }
        var result = new Dictionary<string, TopologyNodeColors>();
        foreach (var node in all.Values)
        {
            var path = Path(node);
            var (level, label) = node.Kind switch
            {
                NodeKind.Controller => (Levels[0].Color, "CONTROLLER"),
                NodeKind.RootHub => (Levels[1].Color, "ROOT-HUB"),
                NodeKind.Hub => (Levels[1].Color, "HUB"),
                NodeKind.Port => (Levels[2].Color, "PORT"),
                _ => (Levels[3].Color, "DEVICE")
            };
            result[node.Id] = new(path.Color, level, Tint(level), path.Label, label);
        }
        return result;
    }

    private static uint StableHash(string value)
    {
        uint hash = 2166136261;
        foreach (char c in value) { hash ^= c; hash = unchecked(hash * 16777619); }
        return hash;
    }
    private static string Tint(string hex)
    {
        int Channel(int start, int background) => (int)Math.Round(background * 0.88 + Convert.ToInt32(hex.Substring(start, 2), 16) * 0.12);
        return $"#{Channel(1, 25):X2}{Channel(3, 32):X2}{Channel(5, 40):X2}";
    }
    private static string FromHue(double degrees)
    {
        double hue = (degrees % 360) / 60, chroma = 0.48;
        double x = chroma * (1 - Math.Abs(hue % 2 - 1));
        var (r, g, b) = hue switch
        {
            < 1 => (chroma, x, 0d), < 2 => (x, chroma, 0d), < 3 => (0d, chroma, x),
            < 4 => (0d, x, chroma), < 5 => (x, 0d, chroma), _ => (chroma, 0d, x)
        };
        return $"#{(int)Math.Round((r + 0.46) * 255):X2}{(int)Math.Round((g + 0.46) * 255):X2}{(int)Math.Round((b + 0.46) * 255):X2}";
    }
}
