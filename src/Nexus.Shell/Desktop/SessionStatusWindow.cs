using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Services;

namespace Nexus.Shell.Desktop;

internal sealed class SessionStatusWindow : DesktopPanelWindow
{
    private readonly StackPanel _items = new() { Spacing = 11 };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly ResourceSampler _sampler = new();
    private ResourceSnapshot? _resources;
    internal SessionStatusWindow(DesktopEnvironment environment) : base(environment, "Session status", new Grid(), 450, 710)
    {
        var body = (Grid)Root.Children[1]; body.RowSpacing = 14; body.RowDefinitions.Add(new()); body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var scroll = new ScrollViewer { Content = _items, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; body.Children.Add(scroll);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        actions.Children.Add(ActionButton("Check services", environment.CheckDesktopServices)); actions.Children.Add(ActionButton("Repair work area", environment.RepairDesktopLayout)); Grid.SetRow(actions, 1); body.Children.Add(actions);
        _timer.Tick += (_, _) => Sample(); ApplyAppearance();
    }
    internal void Show() { ShowPanel(); _sampler.Reset(); Sample(); _timer.Start(); Environment.CheckDesktopServices(); }
    private void Sample()
    {
        if (!IsOpen || IsClosed) return;
        try { _resources = _sampler.Sample(); } catch { _resources = null; }
        Render();
    }
    private void Render()
    {
        if (IsClosed) return; _items.Children.Clear(); var theme = Environment.Theme;
        foreach (var status in Environment.DesktopServiceStatus())
        {
            var content = new StackPanel { Spacing = 6 };
            content.Children.Add(new TextBlock { Text = status.Title, FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = theme.Brush("NexusText") });
            content.Children.Add(new TextBlock { Text = status.Detail, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = theme.Brush("NexusMuted") });
            _items.Children.Add(new Border { Child = content, Padding = new Thickness(14), CornerRadius = new CornerRadius(theme.HighContrast ? 0 : 14), Background = theme.Material("Card", Environment.Session.State.NativeGlass), BorderBrush = theme.Edge, BorderThickness = new Thickness(1) });
        }
        string resources = _resources is null ? "Process measurements are unavailable." : $"Desktop process · {_resources.WorkingSetMB:F0} MB working set · " + (_resources.CpuPercent is { } cpu ? $"{cpu:F1}% CPU across all processors" : "CPU sample preparing");
        _items.Children.Add(new TextBlock { Text = resources + "\n" + MaterialStatus + "\nService checks report observed status; CPU here covers the desktop process only.", FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = theme.Brush("NexusMuted") });
    }
    internal override void ApplyAppearance() { base.ApplyAppearance(); Render(); }
    protected override void OnHidden() { _timer.Stop(); _sampler.Reset(); }
    protected override void OnClosed() { _timer.Stop(); _sampler.Dispose(); Environment.SessionStatusClosed(this); }
}
