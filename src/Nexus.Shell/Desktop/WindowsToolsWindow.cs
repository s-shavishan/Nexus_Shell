using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Services;

namespace Nexus.Shell.Desktop;

internal sealed class WindowsToolsWindow : DesktopPanelWindow
{
    // A usable utility window must not auto-hide when it opens a Windows tool.
    protected override bool DismissOnDeactivate => false;
    private readonly TextBox _search = new() { PlaceholderText = "Find a Windows tool…" };
    private readonly StackPanel _items = new() { Spacing = 10 };
    private readonly TextBlock _awakeStatus = new() { FontSize = 13, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _awakeDetail = new() { Text = "Temporarily request that Windows stays awake. Your power plan stays the same. Locking, disconnecting or sleeping ends the timer.", FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly ComboBox _duration = new() { Width = 115 };
    private readonly CheckBox _display = new() { Content = "Keep display on" };
    private readonly Button _start, _stop;
    private readonly Border _awakeCard;
    private readonly TextBlock _awakeTitle = new() { Text = "Keep awake", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private int _columns = 2;

    internal WindowsToolsWindow(DesktopEnvironment environment) : base(environment, "Windows tools", new Grid(), 600, 740, center: true)
    {
        var body = (Grid)Root.Children[1]; body.RowSpacing = 14;
        body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        body.RowDefinitions.Add(new() { Height = GridLength.Auto }); body.RowDefinitions.Add(new());
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_search, "Find Windows tools");
        _search.TextChanged += (_, _) => RenderTools(); body.Children.Add(_search);
        var awake = new StackPanel { Spacing = 7 };
        awake.Children.Add(_awakeTitle); awake.Children.Add(_awakeStatus); awake.Children.Add(_awakeDetail);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (int minutes in KeepAwakeSession.Durations) _duration.Items.Add(new ComboBoxItem { Content = minutes + " minutes", Tag = minutes });
        _duration.SelectedIndex = 1;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_duration, "Keep awake duration");
        _start = ActionButton("Start", () => { if (_duration.SelectedItem is ComboBoxItem { Tag: int minutes }) Environment.StartKeepAwake(minutes, _display.IsChecked == true); });
        _stop = ActionButton("Stop", () => Environment.StopKeepAwake());
        actions.Children.Add(_duration); actions.Children.Add(_start); actions.Children.Add(_stop);
        awake.Children.Add(_display); awake.Children.Add(actions);
        _awakeCard = new Border { Child = awake, Padding = new Thickness(14), BorderThickness = new Thickness(1) };
        Grid.SetRow(_awakeCard, 1); body.Children.Add(_awakeCard);
        var scroll = new ScrollViewer { Content = _items, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 2); body.Children.Add(scroll);
        scroll.SizeChanged += (_, _) => { int columns = scroll.ActualWidth < 430 ? 1 : 2; if (columns != _columns) { _columns = columns; RenderTools(); } };
        ApplyAppearance();
        Environment.SystemStatusChanged += RefreshStatus;
    }
    internal void Show() { ShowPanel(); RefreshStatus(); _search.Focus(FocusState.Programmatic); }
    private void RefreshStatus()
    {
        if (IsClosed) return;
        string status = Environment.KeepAwake.Status;
        if (_awakeStatus.Text != status) _awakeStatus.Text = status;
        bool active = Environment.KeepAwake.Active;
        if (_stop.IsEnabled != active) _stop.IsEnabled = active;
        string start = active ? "Restart timer" : "Start";
        if (!Equals(_start.Content, start)) _start.Content = start;
    }
    private void RenderTools()
    {
        if (IsClosed) return;
        var theme = Environment.Theme; _items.Children.Clear();
        foreach (var group in WindowsToolCatalog.Search(_search.Text).GroupBy(tool => tool.Group))
        {
            _items.Children.Add(new TextBlock { Text = group.Key, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = theme.Brush("NexusText"), Margin = new Thickness(0, 4, 0, 0) });
            var grid = new Grid { RowSpacing = 8, ColumnSpacing = 8 };
            for (int column = 0; column < _columns; column++) grid.ColumnDefinitions.Add(new());
            int index = 0;
            foreach (var tool in group)
            {
                if (index % _columns == 0) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
                var content = new StackPanel { Spacing = 5 };
                content.Children.Add(new TextBlock { Text = tool.Name, FontSize = 14, TextWrapping = TextWrapping.Wrap, Foreground = theme.Brush("NexusText") });
                content.Children.Add(new TextBlock { Text = tool.Detail, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = theme.Brush("NexusMuted") });
                var button = ActionButton(tool.Name, () => Environment.RunWindowsTool(tool.Id));
                button.Content = content; button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                button.Padding = new Thickness(12); button.MinHeight = 78;
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, tool.Name + ". " + tool.Detail);
                Grid.SetColumn(button, index % _columns); Grid.SetRow(button, index / _columns); grid.Children.Add(button); index++;
            }
            _items.Children.Add(grid);
        }
        if (_items.Children.Count == 0) _items.Children.Add(new TextBlock { Text = "No Windows tools match your search.", TextWrapping = TextWrapping.Wrap, Foreground = theme.Brush("NexusMuted") });
    }
    internal override void ApplyAppearance()
    {
        base.ApplyAppearance(); if (_awakeCard is null) return;
        var theme = Environment.Theme;
        _awakeCard.Background = theme.Material("Card", Environment.Session.State.NativeGlass);
        _awakeCard.BorderBrush = theme.Edge; _awakeCard.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : MidnightGlassMetrics.CardRadius);
        _awakeTitle.Foreground = theme.Brush("NexusText"); _awakeStatus.Foreground = theme.Brush("NexusText");
        _awakeDetail.Foreground = theme.Brush("NexusMuted");
        RefreshStatus(); RenderTools();
    }
    protected override void OnClosed() { Environment.SystemStatusChanged -= RefreshStatus; Environment.WindowsToolsClosed(this); }
}
