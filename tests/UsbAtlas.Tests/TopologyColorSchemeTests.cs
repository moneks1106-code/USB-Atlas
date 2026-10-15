using UsbAtlas.Core;
using UsbAtlas.UI;

namespace UsbAtlas.Tests;

public class TopologyColorSchemeTests
{
    private static UsbNode Node(string id, NodeKind kind, string? parent = null) => new()
    { Id = id, Kind = kind, Name = id, ParentId = parent };
    private static UsbNode[] Tree() =>
    [Node("c", NodeKind.Controller), Node("r", NodeKind.RootHub, "c"), Node("p1", NodeKind.Port, "r"),
     Node("d1", NodeKind.Device, "p1"), Node("p2", NodeKind.Port, "r"), Node("d2", NodeKind.Device, "p2")];

    [Fact] public void PortAndDeviceSharePathButHaveDifferentLevelMarkers()
    {
        var colors = TopologyColorScheme.Create(Tree());
        Assert.Equal(colors["p1"].PathColor, colors["d1"].PathColor);
        Assert.Equal(colors["p1"].PathLabel, colors["d1"].PathLabel);
        Assert.NotEqual(colors["p1"].LevelColor, colors["d1"].LevelColor);
        Assert.NotEqual(colors["p1"].LevelLabel, colors["d1"].LevelLabel);
    }
    [Fact] public void SiblingBranchesAreDistinct()
    {
        var colors = TopologyColorScheme.Create(Tree());
        Assert.NotEqual(colors["p1"].PathColor, colors["p2"].PathColor);
        Assert.NotEqual(colors["d1"].PathLabel, colors["d2"].PathLabel);
        Assert.Equal(colors["c"].PathColor, colors["r"].PathColor);
    }
    [Fact] public void ReorderedSnapshotDoesNotRedistributeColors()
    {
        var nodes = Tree(); var a = TopologyColorScheme.Create(nodes); var b = TopologyColorScheme.Create(nodes.Reverse());
        foreach (var node in nodes) Assert.Equal(a[node.Id], b[node.Id]);
    }
    [Fact] public void NestedHubHasOwnUpstreamAndDistinctDownstreamBranches()
    {
        UsbNode[] nodes = [Node("c", NodeKind.Controller), Node("r", NodeKind.RootHub, "c"), Node("p", NodeKind.Port, "r"),
            Node("h", NodeKind.Hub, "p"), Node("q", NodeKind.Port, "h"), Node("d", NodeKind.Device, "q")];
        var colors = TopologyColorScheme.Create(nodes);
        Assert.Equal(colors["p"].PathColor, colors["h"].PathColor);
        Assert.Equal(colors["q"].PathColor, colors["d"].PathColor);
        Assert.NotEqual(colors["p"].PathColor, colors["q"].PathColor);
    }
    [Fact] public void ManyPortsHaveIndividualColorsAndTextualKeys()
    {
        var nodes = Enumerable.Range(1, 40).Select(i => Node("p" + i, NodeKind.Port, "r"))
            .Prepend(Node("r", NodeKind.RootHub, "c")).Prepend(Node("c", NodeKind.Controller)).ToArray();
        var colors = TopologyColorScheme.Create(nodes);
        Assert.Equal(40, nodes.Where(n => n.Kind == NodeKind.Port).Select(n => colors[n.Id].PathColor).Distinct().Count());
        Assert.Equal(40, nodes.Where(n => n.Kind == NodeKind.Port).Select(n => colors[n.Id].PathLabel).Distinct().Count());
    }
    [Fact] public void MissingParentDoesNotInventAControllerPath()
    {
        var colors = TopologyColorScheme.Create([Node("orphan", NodeKind.Device, "missing")]);
        Assert.Equal("Unknown path", colors["orphan"].PathLabel);
    }
    [Fact] public void EmptyScanHasNoColorAssignments()
    { Assert.Empty(TopologyColorScheme.Create([])); }
}
