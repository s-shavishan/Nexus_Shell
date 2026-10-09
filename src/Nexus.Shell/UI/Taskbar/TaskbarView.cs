using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Desktop;
using Nexus.Shell.Interop;
using Nexus.Shell.Models;

namespace Nexus.Shell.UI.Taskbar;

internal sealed class TaskbarView : Grid
{
    private readonly DesktopEnvironment _environment;
    private readonly Border _frame = new();
    private readonly Grid _body = new() { ColumnSpacing = 12, Padding = new Thickness(14, 6, 14, 6) };
    private readonly StackPanel _pins = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly StackPanel _windows = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly TextBlock _time = new() { FontSize = 12, TextAlignment = TextAlignment.Right };
    private readonly TextBlock _date = new() { FontSize = 10, TextAlignment = TextAlignment.Right };
    private readonly List<Button> _iconButtons = [];
    private IReadOnlyList<AppEntry> _shownPins = [];
    private IReadOnlyList<RunningWindow> _shownWindows = [];
    private bool _shownSections, _shownFiles;
    private readonly Button _search, _overview, _clockButton, _desktopButton;
    private readonly Border _separator;
    private double _reportedWidth;
    internal event Action? PreferredWidthChanged;
    internal double PreferredWidthDip => 416 + (_pins.Children.Count + _windows.Children.Count) * (_environment.Session.State.CompactDock ? 44 : 52);
    private MotionController? _motion;
    internal TaskbarView(DesktopEnvironment environment)
    {
        _environment = environment; _frame.Child = _body; Children.Add(_frame);
        _body.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); _body.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); _body.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var start = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        start.Children.Add(IconButton("Nexus", "Start", () => environment.ShowMenu(), true));
        _search = IconButton("Search", "Search apps", () => environment.ShowMenu(true), true); start.Children.Add(_search);
        _body.Children.Add(start);
        var middle = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        _separator = new Border { Width = 1, Height = 22, VerticalAlignment = VerticalAlignment.Center };
        middle.Children.Add(_pins); middle.Children.Add(_separator); middle.Children.Add(_windows);
        var scroll = new ScrollViewer { Content = middle, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(scroll, 1); _body.Children.Add(scroll);
        var status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        _overview = IconButton("Windows", "Switch windows", environment.ShowWindowOverview, true); status.Children.Add(_overview);
        status.Children.Add(IconButton("Settings", "Quick settings", environment.ShowQuickSettings, true));
        var clock = new StackPanel { Spacing = 2 }; clock.Children.Add(_time); clock.Children.Add(_date);
        _clockButton = new Button { Content = clock, Style = (Style)Application.Current.Resources["QuietButton"], Padding = new Thickness(10, 5, 10, 5), CornerRadius = new CornerRadius(12) };
        _clockButton.Click += (_, _) => environment.ShowMenu(); ToolTipService.SetToolTip(_clockButton, "Your clock · open Start"); status.Children.Add(_clockButton);
        _desktopButton = new Button { Width = 24, Height = 40, Content = new FontIcon { FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = "\uE740", FontSize = 13 }, Style = (Style)Application.Current.Resources["QuietButton"], Padding = new Thickness(0) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_desktopButton, "Show Nexus desktop"); ToolTipService.SetToolTip(_desktopButton, "Show Nexus desktop");
        _desktopButton.Click += (_, _) => environment.ShowDesktop(); status.Children.Add(_desktopButton);
        Grid.SetColumn(status, 2); _body.Children.Add(status);
        Loaded += (_, _) =>
        { try { _motion ??= new MotionController(_body); foreach (var b in _iconButtons.Concat(_pins.Children.OfType<Button>()).Concat(_windows.Children.OfType<Button>())) _motion.AttachHover(b); ApplyAppearance(); _motion.Enter(_body); } catch (Exception ex) { environment.Report("Taskbar motion unavailable", ex, false); } };
        ApplyAppearance();
    }
    private Button IconButton(string icon, string title, Action action, bool retain = false)
    {
        var button = new Button { Content = NexusIcons.Image(icon, 36), Style = (Style)Application.Current.Resources["DockButton"], Width = 48, Height = 52 };
        button.Click += (_, _) => action(); ToolTipService.SetToolTip(button, title); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, title);
        if (retain) _iconButtons.Add(button); _motion?.AttachHover(button); return button;
    }
    internal void Refresh(IReadOnlyList<RunningWindow> windows)
    {
        var pins = _environment.Session.State.PinnedApps.Take(5).ToArray();
        if (!_shownPins.SequenceEqual(pins))
        { _pins.Children.Clear(); foreach (var pin in pins) _pins.Children.Add(IconButton(NexusIcons.ForApp(pin), pin.Name, () => _environment.Launch(pin))); _shownPins = pins; }
        var list = windows.Take(20).ToArray(); bool sections = _environment.SectionsOpen;
        bool files = _environment.FilesOpen;
        if (!_shownWindows.SequenceEqual(list) || _shownSections != sections || _shownFiles != files)
        {
            _windows.Children.Clear();
            if (sections) _windows.Children.Add(WindowButton("Apps", "Sections", () => _environment.ShowSections()));
            if (files) _windows.Children.Add(WindowButton("Files", "Files", _environment.ReturnToFiles));
            foreach (var window in list) _windows.Children.Add(WindowButton(NexusIcons.ForProcess(window.ProcessName), window.Title,
                () => { if (!NativeMethods.Activate(window)) _environment.Report("This window is no longer available."); }));
            _shownWindows = list; _shownSections = sections; _shownFiles = files;
        }
        ApplyDensity(); RefreshClock();
    }
    private Button WindowButton(string icon, string title, Action action)
    {
        var face = new Grid(); face.Children.Add(NexusIcons.Image(icon, 32));
        face.Children.Add(new Border { Width = 8, Height = 3, CornerRadius = new CornerRadius(2), Background = _environment.Theme.Brush("NexusAccent"), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center });
        var button = new Button { Content = face, Width = 46, Height = 52, Style = (Style)Application.Current.Resources["DockButton"] };
        button.Click += (_, _) => action(); ToolTipService.SetToolTip(button, title); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, "Return to " + title);
        _motion?.AttachHover(button); return button;
    }
    private void ApplyDensity()
    {
        bool compact = _environment.Session.State.CompactDock;
        foreach (var button in _iconButtons.Concat(_pins.Children.OfType<Button>()).Concat(_windows.Children.OfType<Button>())) { button.Width = compact ? 40 : 48; button.Height = compact ? 44 : 52; if (button.Content is Image i) i.Width = i.Height = compact ? 30 : 36; }
        _separator.Visibility = _windows.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        double width = PreferredWidthDip;
        if (_reportedWidth != width) { _reportedWidth = width; PreferredWidthChanged?.Invoke(); }
    }
    internal void SetAvailableWidth(double widthDip)
    {
        _frame.CornerRadius = new CornerRadius(_environment.Session.State.FloatingTaskbar && !_environment.Theme.HighContrast ? 22 : 0);
        // Keep Start and Quick Settings reachable on small/scaled displays.
        _search.Visibility = widthDip < 400 ? Visibility.Collapsed : Visibility.Visible;
        _overview.Visibility = widthDip < 440 ? Visibility.Collapsed : Visibility.Visible;
        _clockButton.Visibility = widthDip < 480 ? Visibility.Collapsed : Visibility.Visible;
        _desktopButton.Visibility = widthDip < 600 ? Visibility.Collapsed : Visibility.Visible;
        _date.Visibility = widthDip < 620 ? Visibility.Collapsed : Visibility.Visible;
    }
    internal void RefreshClock()
    { var now = DateTime.Now; _time.Text = now.ToString(_environment.Session.State.Clock24Hour ? "HH:mm" : "h:mm tt"); _date.Text = now.ToString("ddd, d MMM"); }
    internal void ApplyAppearance()
    {
        var theme = _environment.Theme; RequestedTheme = theme.ElementTheme; _frame.Background = theme.Brush("NexusPanel");
        _frame.CornerRadius = new CornerRadius(_environment.Session.State.FloatingTaskbar && !theme.HighContrast ? 22 : 0);
        _frame.BorderBrush = theme.Brush("NexusBorder"); _frame.BorderThickness = new Thickness(theme.HighContrast ? 1 : 0);
        _separator.Background = theme.Brush("NexusBorder"); _time.Foreground = theme.Brush("NexusText"); _date.Foreground = theme.Brush("NexusMuted");
        _clockButton.Background = theme.Brush("NexusCard");
        _motion?.SetEnabled(!theme.HighContrast && theme.Animations && !_environment.Session.State.ReducedEffects); ApplyDensity(); RefreshClock();
    }
    internal void Release() { _motion?.Dispose(); _iconButtons.Clear(); }
}
