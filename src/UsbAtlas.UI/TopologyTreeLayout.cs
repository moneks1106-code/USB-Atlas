using UsbAtlas.Core;

namespace UsbAtlas.UI;

internal sealed record TreeNodePlacement(UsbNode Node, double X, double Y, double Width, double Height, bool IsCompact = false);
internal readonly record struct TreePoint(double X, double Y);
internal sealed record TreeLine(string ColorNodeId, TreePoint[] Points);
internal sealed record TreeLayoutResult(TreeNodePlacement[] Nodes, double Width, double Height,
    double NodeWidth, double NodeHeight, TreeLine[] CompactLines);

// Presentation only: the observed ParentId edges are preserved, without inventing a common USB root.
internal static class TopologyTreeLayout
{
    private const double Padding = 24, SiblingGap = 24, ControllerGap = 32, LevelGap = 44, RowGap = 40;
    private const double EmptyWidth = 112, EmptyHeight = 48, EmptyGap = 12, BusGutter = 20;
    internal const double CardHeight = 104;
    private sealed record Branch(UsbNode Node, Branch[] Children)
    {
        internal double Width { get; set; }
        internal double Height { get; set; }
        internal UsbNode[] EmptyPorts { get; init; } = [];
        internal int EmptyColumns { get; set; }
        internal double EmptyTop { get; set; }
        internal int Leaves => Children.Length == 0 ? 1 : Children.Sum(c => c.Leaves);
        internal int Levels => 1 + Children.Select(c => c.Levels).DefaultIfEmpty(0).Max();
        internal void Measure(double cardWidth)
        {
            foreach (var child in Children) child.Measure(cardWidth);
            Width = Math.Max(cardWidth, Children.Sum(c => c.Width) + Math.Max(0, Children.Length - 1) * SiblingGap);
        }
        internal void MeasureCompact(double cardWidth, double levelGap)
        {
            foreach (var child in Children) child.MeasureCompact(cardWidth, levelGap);
            Width = Math.Max(cardWidth, Children.Sum(c => c.Width) + Math.Max(0, Children.Length - 1) * SiblingGap)
                + (EmptyPorts.Length > 0 ? BusGutter : 0);
            Height = CardHeight + (Children.Length > 0 ? levelGap + Children.Max(c => c.Height) : 0);
            if (EmptyPorts.Length == 0) return;
            EmptyColumns = Math.Max(1, (int)Math.Floor((Width - BusGutter + EmptyGap) / (EmptyWidth + EmptyGap)));
            EmptyTop = Height + (Children.Length == 0 ? levelGap : 24);
            int rows = (EmptyPorts.Length + EmptyColumns - 1) / EmptyColumns;
            Height = EmptyTop + rows * EmptyHeight + (rows - 1) * EmptyGap;
        }
    }

    internal static TreeLayoutResult Create(IEnumerable<UsbNode> nodes, IReadOnlySet<string> collapsed,
        bool showEmptyPorts, double availableWidth, double availableHeight = double.PositiveInfinity)
    {
        var all = nodes.ToArray();
        if (showEmptyPorts && all.Any(n => n.Kind == NodeKind.Port && n.State == PortState.Empty))
            return CreateWithEmptyPorts(all, collapsed, availableWidth, availableHeight);
        var children = all.Where(n => n.ParentId != null).GroupBy(n => n.ParentId!)
            .ToDictionary(g => g.Key, g => g.ToArray());
        var visited = new HashSet<string>();
        Branch? Build(UsbNode node)
        {
            if (!visited.Add(node.Id) || (!showEmptyPorts && node.Kind == NodeKind.Port && node.State == PortState.Empty)) return null;
            var descendants = collapsed.Contains(node.Id) || !children.TryGetValue(node.Id, out var list)
                ? [] : list.Select(Build).OfType<Branch>().ToArray();
            return new(node, descendants);
        }
        var roots = all.Where(n => n.Kind == NodeKind.Controller).Select(Build).OfType<Branch>().ToArray();
        double viewport = double.IsFinite(availableWidth) && availableWidth > 0 ? Math.Max(320, availableWidth) : 1100;
        foreach (var root in roots) root.Measure(200);
        int leaves = roots.Sum(r => r.Leaves);
        double minimumWidth = roots.Sum(r => r.Width) + Math.Max(0, roots.Length - 1) * ControllerGap + 2 * Padding;
        // Fit card widths to the viewport; text size is never scaled down.
        double cardWidth = leaves == 0 ? 240 : Math.Clamp(200 + (viewport - minimumWidth) / leaves, 200, 280);
        foreach (var root in roots) root.Measure(cardWidth);

        var rows = new List<List<Branch>>();
        var row = new List<Branch>();
        double rowWidth = 0;
        foreach (var root in roots)
        {
            double nextWidth = rowWidth + (row.Count == 0 ? 0 : ControllerGap) + root.Width;
            if (row.Count > 0 && nextWidth + 2 * Padding > viewport)
            { rows.Add(row); row = []; rowWidth = 0; }
            rowWidth += (row.Count == 0 ? 0 : ControllerGap) + root.Width;
            row.Add(root);
        }
        if (row.Count > 0) rows.Add(row);
        double WidthOf(List<Branch> items) => items.Sum(r => r.Width) + Math.Max(0, items.Count - 1) * ControllerGap;
        double canvasWidth = Math.Max(viewport, rows.Select(WidthOf).DefaultIfEmpty(0).Max() + 2 * Padding);
        var result = new List<TreeNodePlacement>();
        void Place(Branch branch, double x, double y)
        {
            result.Add(new(branch.Node, x + (branch.Width - cardWidth) / 2, y, cardWidth, CardHeight));
            double childrenWidth = branch.Children.Sum(c => c.Width) + Math.Max(0, branch.Children.Length - 1) * SiblingGap;
            double childX = x + (branch.Width - childrenWidth) / 2;
            foreach (var child in branch.Children)
            { Place(child, childX, y + CardHeight + LevelGap); childX += child.Width + SiblingGap; }
        }
        double rowY = Padding;
        foreach (var items in rows)
        {
            double x = (canvasWidth - WidthOf(items)) / 2;
            foreach (var root in items) { Place(root, x, rowY); x += root.Width + ControllerGap; }
            rowY += items.Max(r => r.Levels) * (CardHeight + LevelGap) - LevelGap + RowGap;
        }
        double height = result.Select(p => p.Y + CardHeight + Padding).DefaultIfEmpty(2 * Padding).Max();
        return new(result.ToArray(), canvasWidth, height, cardWidth, CardHeight, []);
    }

    private static TreeLayoutResult CreateWithEmptyPorts(UsbNode[] all, IReadOnlySet<string> collapsed,
        double availableWidth, double availableHeight)
    {
        var children = all.Where(n => n.ParentId != null).GroupBy(n => n.ParentId!)
            .ToDictionary(g => g.Key, g => g.ToArray());
        // A reported-empty port with descendants is contradictory and keeps its full branch visible.
        bool Compact(UsbNode n) => n.Kind == NodeKind.Port && n.State == PortState.Empty && !children.ContainsKey(n.Id);
        var visited = new HashSet<string>();
        Branch? Build(UsbNode node)
        {
            if (!visited.Add(node.Id)) return null;
            var list = collapsed.Contains(node.Id) || !children.TryGetValue(node.Id, out var found) ? [] : found;
            return new(node, list.Where(n => !Compact(n)).Select(Build).OfType<Branch>().ToArray())
                { EmptyPorts = list.Where(Compact).ToArray() };
        }
        var roots = all.Where(n => n.Kind == NodeKind.Controller).Select(Build).OfType<Branch>().ToArray();
        double viewport = double.IsFinite(availableWidth) && availableWidth > 0 ? Math.Max(320, availableWidth) : 1100;
        foreach (var root in roots) root.MeasureCompact(200, 32);
        int leaves = roots.Sum(r => r.Leaves);
        double minimumWidth = roots.Sum(r => r.Width) + Math.Max(0, roots.Length - 1) * ControllerGap + 2 * Padding;
        double cardWidth = leaves == 0 ? 240 : Math.Clamp(200 + (viewport - minimumWidth) / leaves, 200, 280);
        foreach (var root in roots) root.MeasureCompact(cardWidth, 32);
        var rows = new List<List<Branch>>();
        var row = new List<Branch>();
        double rowWidth = 0;
        foreach (var root in roots)
        {
            double next = rowWidth + (row.Count == 0 ? 0 : ControllerGap) + root.Width;
            if (row.Count > 0 && next + 2 * Padding > viewport)
            { rows.Add(row); row = []; rowWidth = 0; }
            rowWidth += (row.Count == 0 ? 0 : ControllerGap) + root.Width; row.Add(root);
        }
        if (row.Count > 0) rows.Add(row);
        double TotalHeight() => 2 * Padding + rows.Sum(r => r.Max(b => b.Height)) + Math.Max(0, rows.Count - 1) * RowGap;
        // Use the actual viewport height before tightening gaps; fonts and card contents are not scaled.
        double levelGap = 32;
        if (double.IsFinite(availableHeight) && availableHeight > 0 && TotalHeight() > availableHeight)
        { levelGap = 24; foreach (var root in roots) root.MeasureCompact(cardWidth, levelGap); }
        double WidthOf(List<Branch> items) => items.Sum(r => r.Width) + Math.Max(0, items.Count - 1) * ControllerGap;
        double canvasWidth = Math.Max(viewport, rows.Select(WidthOf).DefaultIfEmpty(0).Max() + 2 * Padding);
        var result = new List<TreeNodePlacement>();
        var lines = new List<TreeLine>();
        void Place(Branch branch, double x, double y)
        {
            double cardX = x + (branch.Width - cardWidth) / 2;
            result.Add(new(branch.Node, cardX, y, cardWidth, CardHeight));
            double childrenWidth = branch.Children.Sum(c => c.Width) + Math.Max(0, branch.Children.Length - 1) * SiblingGap;
            double gutter = branch.EmptyPorts.Length > 0 ? BusGutter : 0;
            double childX = x + gutter + (branch.Width - gutter - childrenWidth) / 2;
            foreach (var child in branch.Children)
            { Place(child, childX, y + CardHeight + levelGap); childX += child.Width + SiblingGap; }
            if (branch.EmptyPorts.Length == 0) return;
            double busX = x + 4, top = y + branch.EmptyTop;
            int lastRow = (branch.EmptyPorts.Length - 1) / branch.EmptyColumns;
            lines.Add(new(branch.Node.Id, [new(cardX + cardWidth / 2, y + CardHeight),
                new(cardX + cardWidth / 2, y + CardHeight + levelGap / 2), new(busX, y + CardHeight + levelGap / 2),
                new(busX, top + lastRow * (EmptyHeight + EmptyGap) - 8)]));
            for (int i = 0; i < branch.EmptyPorts.Length; i++)
            {
                int column = i % branch.EmptyColumns, rowIndex = i / branch.EmptyColumns;
                double px = x + BusGutter + column * (EmptyWidth + EmptyGap), py = top + rowIndex * (EmptyHeight + EmptyGap);
                var port = branch.EmptyPorts[i];
                result.Add(new(port, px, py, EmptyWidth, EmptyHeight, true));
                if (column == 0)
                {
                    int columns = Math.Min(branch.EmptyColumns, branch.EmptyPorts.Length - i);
                    lines.Add(new(branch.Node.Id, [new(busX, py - 8), new(x + BusGutter + (columns - 1) * (EmptyWidth + EmptyGap) + EmptyWidth / 2, py - 8)]));
                }
                lines.Add(new(port.Id, [new(px + EmptyWidth / 2, py - 8), new(px + EmptyWidth / 2, py)]));
            }
        }
        double rowY = Padding;
        foreach (var items in rows)
        {
            double x = (canvasWidth - WidthOf(items)) / 2;
            foreach (var root in items) { Place(root, x, rowY); x += root.Width + ControllerGap; }
            rowY += items.Max(r => r.Height) + RowGap;
        }
        return new(result.ToArray(), canvasWidth, TotalHeight(), cardWidth, CardHeight, lines.ToArray());
    }
}
