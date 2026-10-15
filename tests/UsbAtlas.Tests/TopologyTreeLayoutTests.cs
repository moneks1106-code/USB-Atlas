using UsbAtlas.Core;
using UsbAtlas.UI;

namespace UsbAtlas.Tests;

public class TopologyTreeLayoutTests
{
    private static UsbNode Node(string id, NodeKind kind, string? parent = null, PortState state = PortState.Unknown) => new()
    { Id = id, Name = id, Kind = kind, ParentId = parent, State = state };
    private static UsbNode[] Forest() =>
    [
        Node("c1", NodeKind.Controller), Node("r1", NodeKind.RootHub, "c1"),
        Node("p1", NodeKind.Port, "r1"), Node("d1", NodeKind.Device, "p1"),
        Node("p2", NodeKind.Port, "r1"), Node("d2", NodeKind.Device, "p2"),
        Node("c2", NodeKind.Controller), Node("r2", NodeKind.RootHub, "c2"),
        Node("p3", NodeKind.Port, "r2"), Node("d3", NodeKind.Device, "p3"),
        Node("c3", NodeKind.Controller), Node("r3", NodeKind.RootHub, "c3"),
        Node("p4", NodeKind.Port, "r3"), Node("d4", NodeKind.Device, "p4"),
        Node("c4", NodeKind.Controller), Node("r4", NodeKind.RootHub, "c4"),
        Node("p5", NodeKind.Port, "r4"), Node("d5", NodeKind.Device, "p5")
    ];

    [Fact] public void ParentsAreAboveChildrenAndBranchIsCentered()
    {
        var layout = TopologyTreeLayout.Create(Forest(), new HashSet<string>(), false, 1800);
        var byId = layout.Nodes.ToDictionary(p => p.Node.Id);
        foreach (var node in layout.Nodes.Where(p => p.Node.ParentId != null))
            Assert.True(byId[node.Node.ParentId!].Y + layout.NodeHeight < node.Y);
        Assert.Equal((byId["p1"].X + byId["p2"].X) / 2, byId["r1"].X, 5);
        Assert.Equal(byId["p1"].Y, byId["p2"].Y);
    }
    [Theory] [InlineData(1200)] [InlineData(1800)] [InlineData(2300)]
    public void TypicalFourControllerSetupFitsWholeViewportWithoutShrinkingText(double width)
    {
        var layout = TopologyTreeLayout.Create(Forest(), new HashSet<string>(), false, width);
        Assert.Equal(Forest().Length, layout.Nodes.Length);
        Assert.True(layout.Width <= width);
        Assert.True(layout.Height <= 600);
        Assert.InRange(layout.NodeWidth, 200, 280);
        Assert.Equal(104, layout.NodeHeight);
        Assert.Single(layout.Nodes.Where(p => p.Node.Kind == NodeKind.Controller).Select(p => p.Y).Distinct());
    }
    [Theory] [InlineData(500)] [InlineData(1200)] [InlineData(2300)]
    public void CardsDoNotOverlapAndRemainInsideCanvas(double width)
    {
        var layout = TopologyTreeLayout.Create(Forest(), new HashSet<string>(), false, width);
        for (int i = 0; i < layout.Nodes.Length; i++)
        {
            var a = layout.Nodes[i];
            Assert.InRange(a.X, 0, layout.Width - layout.NodeWidth + 0.001);
            Assert.InRange(a.Y, 0, layout.Height - layout.NodeHeight + 0.001);
            foreach (var b in layout.Nodes.Skip(i + 1))
                Assert.True(a.X + layout.NodeWidth <= b.X || b.X + layout.NodeWidth <= a.X
                    || a.Y + layout.NodeHeight <= b.Y || b.Y + layout.NodeHeight <= a.Y);
        }
    }
    [Fact] public void NarrowWindowWrapsWholeControllerTreesInsteadOfSeparatingParents()
    {
        var layout = TopologyTreeLayout.Create(Forest(), new HashSet<string>(), false, 600);
        Assert.True(layout.Nodes.Where(p => p.Node.Kind == NodeKind.Controller).Select(p => p.Y).Distinct().Count() > 1);
        Assert.Equal(Forest().Length, layout.Nodes.Length);
        Assert.True(layout.NodeWidth >= 200);
    }
    [Theory] [InlineData(1800)] [InlineData(2300)]
    public void SavedSixDeviceSetupFitsMaximizedViewport(double width)
    {
        var nodes = Forest().Concat([Node("p6", NodeKind.Port, "r1"), Node("d6", NodeKind.Device, "p6")]).ToArray();
        var layout = TopologyTreeLayout.Create(nodes, new HashSet<string>(), false, width);
        Assert.Equal(nodes.Length, layout.Nodes.Length);
        Assert.True(layout.Width <= width);
        Assert.True(layout.Height <= 600);
    }
    [Fact] public void CollapsedHubHidesOnlyItsOwnDescendants()
    {
        var layout = TopologyTreeLayout.Create(Forest(), new HashSet<string> { "r1" }, false, 1800);
        Assert.Contains(layout.Nodes, p => p.Node.Id == "r1");
        Assert.DoesNotContain(layout.Nodes, p => p.Node.Id is "p1" or "p2" or "d1" or "d2");
        Assert.Contains(layout.Nodes, p => p.Node.Id == "d5");
    }
    [Fact] public void OptionalEmptyPortsAreNotLostOrInvented()
    {
        var nodes = Forest().Append(Node("empty", NodeKind.Port, "r1", PortState.Empty)).ToArray();
        Assert.DoesNotContain(TopologyTreeLayout.Create(nodes, new HashSet<string>(), false, 1800).Nodes, p => p.Node.Id == "empty");
        Assert.Contains(TopologyTreeLayout.Create(nodes, new HashSet<string>(), true, 1800).Nodes, p => p.Node.Id == "empty");
    }
    [Fact] public void NestedHubPreservesAllObservedEdges()
    {
        UsbNode[] nodes = [Node("c", NodeKind.Controller), Node("r", NodeKind.RootHub, "c"), Node("p", NodeKind.Port, "r"),
            Node("h", NodeKind.Hub, "p"), Node("q", NodeKind.Port, "h"), Node("d", NodeKind.Device, "q")];
        var layout = TopologyTreeLayout.Create(nodes, new HashSet<string>(), false, 1200);
        Assert.Equal(nodes.Select(n => n.Id), layout.Nodes.Select(p => p.Node.Id));
        Assert.Equal(6, layout.Nodes.Select(p => p.Y).Distinct().Count());
    }
    [Fact] public void EmptyScanProducesAnEmptyCanvas()
    { Assert.Empty(TopologyTreeLayout.Create([], new HashSet<string>(), false, 1200).Nodes); }

    private static UsbNode[] With27Ports()
    {
        var nodes = new List<UsbNode>();
        int[] ports = [18, 4, 1, 4], occupied = [3, 1, 1, 1];
        for (int i = 0; i < ports.Length; i++)
        {
            nodes.Add(Node($"c{i}", NodeKind.Controller)); nodes.Add(Node($"r{i}", NodeKind.RootHub, $"c{i}"));
            for (int p = 0; p < ports[i]; p++)
            {
                string id = $"p{i}-{p}";
                nodes.Add(Node(id, NodeKind.Port, $"r{i}", p < occupied[i] ? PortState.Connected : PortState.Empty));
                if (p < occupied[i]) nodes.Add(Node($"d{i}-{p}", NodeKind.Device, id));
            }
        }
        return nodes.ToArray();
    }
    [Theory] [InlineData(1860, 760)] [InlineData(2480, 1120)]
    public void All27PortsAndSixDevicesFitMaximizedViewport(double width, double height)
    {
        var nodes = With27Ports();
        var layout = TopologyTreeLayout.Create(nodes, new HashSet<string>(), true, width, height);
        Assert.Equal(nodes.Length, layout.Nodes.Length);
        Assert.Equal(21, layout.Nodes.Count(p => p.IsCompact));
        Assert.InRange(layout.Width, 0, width + 0.001); Assert.InRange(layout.Height, 0, height);
        Assert.Equal(6, layout.Nodes.Count(p => p.Node.Kind == NodeKind.Device));
        Assert.All(layout.Nodes.Where(p => !p.IsCompact), p => Assert.Equal(104, p.Height));
        Assert.Equal(nodes.Select(n => n.Id).Order(), layout.Nodes.Select(p => p.Node.Id).Order());
    }
    [Theory] [InlineData(600, 500)] [InlineData(1860, 760)] [InlineData(2480, 1120)]
    public void CompactAndFullCardsDoNotOverlapOrLeaveCanvas(double width, double height)
    {
        var layout = TopologyTreeLayout.Create(With27Ports(), new HashSet<string>(), true, width, height);
        for (int i = 0; i < layout.Nodes.Length; i++)
        {
            var a = layout.Nodes[i];
            Assert.InRange(a.X, 0, layout.Width - a.Width + 0.001);
            Assert.InRange(a.Y, 0, layout.Height - a.Height + 0.001);
            foreach (var b in layout.Nodes.Skip(i + 1))
                Assert.True(a.X + a.Width <= b.X + 0.001 || b.X + b.Width <= a.X + 0.001
                    || a.Y + a.Height <= b.Y || b.Y + b.Height <= a.Y);
        }
    }
    [Fact] public void EveryCompactPortHasAColoredConnectionAndItsActualParentAbove()
    {
        var layout = TopologyTreeLayout.Create(With27Ports(), new HashSet<string>(), true, 1860, 760);
        var byId = layout.Nodes.ToDictionary(p => p.Node.Id);
        foreach (var port in layout.Nodes.Where(p => p.IsCompact))
        {
            var line = Assert.Single(layout.CompactLines, l => l.ColorNodeId == port.Node.Id);
            Assert.Equal(new TreePoint(port.X + port.Width / 2, port.Y), line.Points.Last());
            Assert.True(byId[port.Node.ParentId!].Y + 104 < port.Y);
            Assert.Contains(layout.CompactLines, l => l.ColorNodeId == port.Node.ParentId);
        }
    }
    [Fact] public void EmptyPortLinesDoNotRunThroughOtherCards()
    {
        var layout = TopologyTreeLayout.Create(With27Ports(), new HashSet<string>(), true, 1860, 760);
        foreach (var line in layout.CompactLines)
        for (int i = 1; i < line.Points.Length; i++)
        {
            var a = line.Points[i - 1]; var b = line.Points[i];
            foreach (var card in layout.Nodes)
                Assert.False(a.X == b.X
                    ? a.X > card.X && a.X < card.X + card.Width && Math.Max(a.Y, b.Y) > card.Y && Math.Min(a.Y, b.Y) < card.Y + card.Height
                    : a.Y > card.Y && a.Y < card.Y + card.Height && Math.Max(a.X, b.X) > card.X && Math.Min(a.X, b.X) < card.X + card.Width);
        }
    }
    [Fact] public void AvailableHeightTightensSpacingWithoutScalingCards()
    {
        var roomy = TopologyTreeLayout.Create(With27Ports(), new HashSet<string>(), true, 1860, 1000);
        var tighter = TopologyTreeLayout.Create(With27Ports(), new HashSet<string>(), true, 1860, 735);
        Assert.True(tighter.Height < roomy.Height); Assert.True(tighter.Height <= 735);
        Assert.Equal(roomy.NodeWidth, tighter.NodeWidth); Assert.Equal(roomy.NodeHeight, tighter.NodeHeight);
        Assert.Equal(roomy.Nodes.Length, tighter.Nodes.Length);
    }
    [Fact] public void CollapsingRootHidesItsEmptyPortsWithoutChangingOtherTrees()
    {
        var layout = TopologyTreeLayout.Create(With27Ports(), new HashSet<string> { "r0" }, true, 1860, 760);
        Assert.Contains(layout.Nodes, p => p.Node.Id == "r0");
        Assert.DoesNotContain(layout.Nodes, p => p.Node.ParentId == "r0");
        Assert.Equal(6, layout.Nodes.Count(p => p.IsCompact));
        Assert.DoesNotContain(layout.CompactLines, l => l.ColorNodeId == "r0");
    }
    [Fact] public void OnlyConfirmedEmptyLeafPortsAreCompact()
    {
        UsbNode[] nodes = [Node("c", NodeKind.Controller), Node("r", NodeKind.RootHub, "c"),
            Node("empty", NodeKind.Port, "r", PortState.Empty), Node("unknown", NodeKind.Port, "r"),
            Node("error", NodeKind.Port, "r", PortState.Error), Node("contradictory", NodeKind.Port, "r", PortState.Empty),
            Node("d", NodeKind.Device, "contradictory")];
        var layout = TopologyTreeLayout.Create(nodes, new HashSet<string>(), true, 1860, 760);
        Assert.Equal("empty", Assert.Single(layout.Nodes, p => p.IsCompact).Node.Id);
        Assert.Equal(nodes.Length, layout.Nodes.Length);
    }
    [Fact] public void NestedHubsKeepTheirEmptyPortsAndSeparateBuses()
    {
        UsbNode[] nodes = [Node("c", NodeKind.Controller), Node("r", NodeKind.RootHub, "c"),
            Node("p", NodeKind.Port, "r", PortState.Connected), Node("h", NodeKind.Hub, "p"),
            Node("q", NodeKind.Port, "h", PortState.Empty), Node("e", NodeKind.Port, "r", PortState.Empty)];
        var layout = TopologyTreeLayout.Create(nodes, new HashSet<string>(), true, 1200, 1000);
        Assert.Equal(nodes.Length, layout.Nodes.Length);
        Assert.Equal(2, layout.Nodes.Count(p => p.IsCompact));
        var nested = layout.Nodes.Single(p => p.Node.Id == "q"); var root = layout.Nodes.Single(p => p.Node.Id == "e");
        Assert.True(nested.Y < root.Y); Assert.Equal("h", nested.Node.ParentId); Assert.Equal("r", root.Node.ParentId);
    }
}
