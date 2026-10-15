using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using UsbAtlas.Core;
using Windows.Foundation;
using Windows.UI;

namespace UsbAtlas.UI;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    private readonly HashSet<string> collapsed = [];
    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        CreateColorLegend();
        Root.DataContext = ViewModel;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        SystemBackdrop = new MicaBackdrop();
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1500, 1000));
        ViewModel.TopologyChanged += (_, _) => DrawTopology();
        Root.Loaded += async (_, _) => await ViewModel.InitializeAsync();
    }
    private void MapViewport_SizeChanged(object sender, SizeChangedEventArgs args)
    { if (MapSurface != null && MapViewport.ActualWidth > 0) DrawTopology(); }
    private void DrawTopology()
    {
        if (MapSurface == null || ViewModel.Snapshot == null) return;
        MapSurface.Children.Clear();
        var snapshot = ViewModel.Snapshot;
        var layout = TopologyTreeLayout.Create(snapshot.Nodes, collapsed, ViewModel.ShowEmptyPorts,
            MapViewport.ActualWidth - 20, MapViewport.ActualHeight - 20);
        var positions = layout.Nodes.ToDictionary(p => p.Node.Id);
        var colors = TopologyColorScheme.Create(snapshot.Nodes);
        var parents = snapshot.Nodes.Where(n => n.ParentId != null).Select(n => n.ParentId!).ToHashSet();
        // Visible, explicit edges; companion metadata never creates a physical-neighbour edge.
        void AddLine(string hex, params Point[] points)
        {
            var line = new Polyline { Stroke = Brush(hex), StrokeThickness = 3, IsHitTestVisible = false };
            foreach (var point in points) line.Points.Add(point);
            MapSurface.Children.Add(line);
        }
        foreach (var branch in layout.Nodes.Where(p => !p.IsCompact && p.Node.ParentId != null && positions.ContainsKey(p.Node.ParentId))
            .GroupBy(p => p.Node.ParentId!))
        {
            var parent = positions[branch.Key];
            double parentX = parent.X + parent.Width / 2;
            double parentBottom = parent.Y + parent.Height;
            double branchY = (parentBottom + branch.Min(p => p.Y)) / 2;
            string trunkColor = colors[parent.Node.Id].PathColor;
            AddLine(trunkColor, new(parentX, parentBottom), new(parentX, branchY));
            double left = branch.Min(p => p.X + layout.NodeWidth / 2), right = branch.Max(p => p.X + layout.NodeWidth / 2);
            if (left < right) AddLine(trunkColor, new(left, branchY), new(right, branchY));
            foreach (var child in branch)
            {
                double childX = child.X + layout.NodeWidth / 2;
                AddLine(colors[child.Node.Id].PathColor, new(childX, branchY), new(childX, child.Y));
                var junction = new Ellipse { Width = 7, Height = 7, Fill = Brush(colors[child.Node.Id].PathColor), IsHitTestVisible = false };
                Canvas.SetLeft(junction, childX - 3.5); Canvas.SetTop(junction, branchY - 3.5); MapSurface.Children.Add(junction);
            }
        }
        foreach (var line in layout.CompactLines)
            AddLine(colors[line.ColorNodeId].PathColor, line.Points.Select(p => new Point(p.X, p.Y)).ToArray());
        foreach (var placement in layout.Nodes)
        {
            var node = placement.Node;
            string nodeName = MainViewModel.NodeName(node);
            var style = colors[node.Id];
            if (placement.IsCompact)
            {
                var portText = new StackPanel { Spacing = 0 };
                portText.Children.Add(new TextBlock { Text = nodeName, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
                portText.Children.Add(new TextBlock { Text = "empty · " + style.PathLabel, FontSize = 12, Foreground = Brush(style.LevelColor) });
                var portButton = new Button { Content = portText, Width = placement.Width, Height = placement.Height,
                    Padding = new Thickness(6), CornerRadius = new CornerRadius(8), Background = Brush(style.Background),
                    BorderBrush = Brush(style.PathColor), BorderThickness = new Thickness(1.5) };
                ToolTipService.SetToolTip(portButton, nodeName + " · confirmed empty · " + style.PathLabel + "\n" + ViewModel.PortLabel(node.Id));
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(portButton, nodeName + ", confirmed empty, " + style.PathLabel);
                portButton.Click += (_, _) => ShowNodeDetails(node, portButton, style);
                Canvas.SetLeft(portButton, placement.X); Canvas.SetTop(portButton, placement.Y); MapSurface.Children.Add(portButton);
                continue;
            }
            var panel = new Grid { Width = layout.NodeWidth - 28, ColumnSpacing = 10 };
            panel.ColumnDefinitions.Add(new() { Width = new GridLength(24) }); panel.ColumnDefinitions.Add(new());
            var icon = new FontIcon { Glyph = node.Kind switch { NodeKind.Controller => "\uE950", NodeKind.Hub or NodeKind.RootHub => "\uE8CE", NodeKind.Port => "\uE88E", _ => MainViewModel.Glyph(node.Role) }, FontSize = 22, Foreground = Brush(style.LevelColor) };
            panel.Children.Add(icon);
            var text = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            var placementHint = ControllerPlacementHint.For(snapshot, node);
            string title = node.Kind == NodeKind.RootHub ? "Root hub\n" + placementHint.Label : nodeName;
            text.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap, MaxLines = 3, TextTrimming = TextTrimming.CharacterEllipsis });
            var subtitle = node.Kind switch
            {
                NodeKind.Port => MainViewModel.StateText(node.State), NodeKind.Controller => "Host controller",
                NodeKind.RootHub => "Root hub", _ => MainViewModel.SpeedText(node.Speed)
            };
            text.Children.Add(new TextBlock { Text = style.PathLabel + " · " + subtitle, FontSize = 12,
                TextWrapping = TextWrapping.NoWrap, MaxLines = 1, TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = Brush("#BDC9D4") });
            Grid.SetColumn(text, 1); panel.Children.Add(text);
            var button = new Button { Content = panel, Width = layout.NodeWidth, Height = layout.NodeHeight,
                Padding = new Thickness(12), CornerRadius = new CornerRadius(12),
                Background = Brush(style.Background), BorderBrush = Brush(style.PathColor), BorderThickness = new Thickness(1.5) };
            ToolTipService.SetToolTip(button, style.LevelLabel + " · " + style.PathLabel + " · " + subtitle + "\n" + nodeName + "\n"
                + (node.Kind is NodeKind.Controller or NodeKind.RootHub ? placementHint.Label + "\n" : "")
                + (node.Kind == NodeKind.Port ? ViewModel.PortLabel(node.Id) : node.LocationPath ?? node.Id));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, title + ", " + style.LevelLabel + ", " + style.PathLabel);
            button.Click += (_, _) => ShowNodeDetails(node, button, style);
            Canvas.SetLeft(button, placement.X); Canvas.SetTop(button, placement.Y); MapSurface.Children.Add(button);
            var accent = new Border { Width = 5, Height = layout.NodeHeight - 28, CornerRadius = new CornerRadius(2),
                Background = Brush(style.PathColor), IsHitTestVisible = false };
            Canvas.SetLeft(accent, placement.X + 1); Canvas.SetTop(accent, placement.Y + 14); MapSurface.Children.Add(accent);
            var levelBadge = new Border { Background = Brush(style.Background), BorderBrush = Brush(style.LevelColor),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 2, 6, 2),
                IsHitTestVisible = false, Child = new TextBlock { Text = style.LevelLabel, Foreground = Brush(style.LevelColor),
                    FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, CharacterSpacing = 70 } };
            Canvas.SetLeft(levelBadge, placement.X + 12); Canvas.SetTop(levelBadge, placement.Y - 9); MapSurface.Children.Add(levelBadge);
            if (parents.Contains(node.Id))
            {
                var toggle = new Button { Content = collapsed.Contains(node.Id) ? "+" : "−", Width = 28, Height = 28, FontSize = 14,
                    Padding = new Thickness(0), CornerRadius = new CornerRadius(8), Foreground = Brush(style.PathColor),
                    Background = Brush(style.Background), BorderBrush = Brush(style.PathColor) };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle, nodeName + " expand/collapse branch");
                toggle.Click += (_, _) => { if (!collapsed.Add(node.Id)) collapsed.Remove(node.Id); DrawTopology(); };
                Canvas.SetLeft(toggle, placement.X + layout.NodeWidth - 20); Canvas.SetTop(toggle, placement.Y - 10); MapSurface.Children.Add(toggle);
            }
        }
        MapSurface.Width = layout.Width;
        MapSurface.Height = Math.Max(layout.Height, MapViewport.ActualHeight - 20);
    }
    private static SolidColorBrush Brush(string hex) => new(Color.FromArgb(255,
        Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16)));
    private void CreateColorLegend()
    {
        foreach (var level in TopologyColorScheme.Levels)
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            item.Children.Add(new Ellipse { Width = 9, Height = 9, Fill = Brush(level.Color) });
            item.Children.Add(new TextBlock { Text = level.Label, FontSize = 12, Foreground = Brush(level.Color) });
            TopologyColorLegend.Children.Add(item);
        }
    }
    private void ShowNodeDetails(UsbNode node, Button target, TopologyNodeColors colors)
    {
        ViewModel.SelectNode(node);
        var details = new StackPanel { Width = 380, Spacing = 12 };
        details.Children.Add(new TextBlock { Text = colors.LevelLabel + " · " + colors.PathLabel,
            Foreground = Brush(colors.PathColor), FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        details.Children.Add(new TextBlock { Text = ViewModel.SelectedName, FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        details.Children.Add(new TextBlock { Text = ViewModel.SelectedSummary, FontSize = 14, TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true });
        if (node.Kind is NodeKind.Controller or NodeKind.RootHub && ViewModel.Snapshot is { } snapshot)
        {
            var hint = ControllerPlacementHint.For(snapshot, node);
            details.Children.Add(new TextBlock { Text = hint.Label, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            details.Children.Add(new TextBlock { Text = hint.Explanation, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
            if (hint.Source != null) details.Children.Add(new TextBlock { Text = "Source: " + hint.Source, FontSize = 12,
                TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
        }
        if (node.IsPhysical) details.Children.Add(new TextBlock { Text = ViewModel.SelectedFindings, FontSize = 13,
            TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
        if (ViewModel.Expert)
            details.Children.Add(new Expander { Header = "Technical node details", HorizontalAlignment = HorizontalAlignment.Stretch,
                Content = new TextBlock { Text = ViewModel.SelectedDetails, FontFamily = new FontFamily("Consolas"), FontSize = 12,
                    TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true } });
        new Flyout { Content = new ScrollViewer { Content = details, MaxHeight = 480,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } }.ShowAt(target);
    }
    private void AssessmentDetails_Click(object sender, RoutedEventArgs args)
    {
        if (sender is not Button target) return;
        var details = new StackPanel { Width = 380, Spacing = 12 };
        details.Children.Add(new TextBlock { Text = ViewModel.StatusTitle, FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        details.Children.Add(new TextBlock { Text = ViewModel.AssessmentCounts, TextWrapping = TextWrapping.Wrap });
        details.Children.Add(new TextBlock { Text = ViewModel.FindingsText, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
        foreach (var section in new[] { ("Scan data gaps", ViewModel.ScanWarnings), ("Properties that cannot be assessed", ViewModel.UnknownText) })
            details.Children.Add(new Expander { Header = section.Item1, HorizontalAlignment = HorizontalAlignment.Stretch,
                Content = new TextBlock { Text = section.Item2, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true } });
        new Flyout { Content = new ScrollViewer { Content = details, MaxHeight = 560,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } }.ShowAt(target);
    }
}
