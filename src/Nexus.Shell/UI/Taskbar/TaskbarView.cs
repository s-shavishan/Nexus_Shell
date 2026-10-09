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
    private readonly StackPanel _pins = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly StackPanel _windows = new() { Orientation = Orientation.Horizontal, Spacing = 4 };
    private readonly TextBlock _time = new() { FontSize = 12, TextAlignment = TextAlignment.Right };
    private readonly TextBlock _date = new() { FontSize = 10, TextAlignment = TextAlignment.Right };
    private readonly List<Button> _iconButtons = [];
    private IReadOnlyList<AppEntry> _shownPins = [];
    private IReadOnlyList<RunningWindow> _shownWindows = [];
    private bool _shownSections, _shownFiles;
    private MotionController? _motion;
    internal TaskbarView(DesktopEnvironment environment)
    {
        _environment = environment; ColumnSpacing = 12; Padding = new Thickness(12, 4, 12, 4);
        ColumnDefinitions.Add(new() { Width = GridLength.Auto }); ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var start = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        start.Children.Add(IconButton("Nexus", "Start", () => environment.ShowMenu(), true));
        start.Children.Add(IconButton("Search", "Search apps", () => environment.ShowMenu(true), true));
        Children.Add(start);
        var middle = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        middle.Children.Add(_pins); middle.Children.Add(new Border { Width = 1, Height = 24, Background = environment.Theme.Brush("NexusBorder") }); middle.Children.Add(_windows);
        var scroll = new ScrollViewer { Content = middle, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(scroll, 1); Children.Add(scroll);
        var status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        status.Children.Add(IconButton("Windows", "Switch windows", environment.ShowWindowOverview, true));
        status.Children.Add(IconButton("Settings", "Quick settings", environment.ShowQuickSettings, true));
        var clock = new StackPanel { Spacing = 2 }; clock.Children.Add(_time); clock.Children.Add(_date);
        var clockButton = new Button { Content = clock, Style = (Style)Application.Current.Resources["QuietButton"], Padding = new Thickness(6) };
        clockButton.Click += (_, _) => environment.ShowMenu(); status.Children.Add(clockButton);
        var desktop = new Button { Width = 22, Height = 42, Content = new Border { Width = 2, Height = 28, Background = environment.Theme.Brush("NexusBorder") }, Style = (Style)Application.Current.Resources["QuietButton"] };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(desktop, "Show Nexus desktop"); ToolTipService.SetToolTip(desktop, "Show Nexus desktop");
        desktop.Click += (_, _) => environment.ShowDesktop(); status.Children.Add(desktop);
        Grid.SetColumn(status, 2); Children.Add(status);
        Loaded += (_, _) =>
        { try { _motion ??= new MotionController(this); foreach (var b in _iconButtons.Concat(_pins.Children.OfType<Button>()).Concat(_windows.Children.OfType<Button>())) _motion.AttachHover(b); ApplyAppearance(); } catch (Exception ex) { environment.Report("Taskbar motion unavailable", ex, false); } };
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
    }
    internal void RefreshClock()
    { var now = DateTime.Now; _time.Text = now.ToString(_environment.Session.State.Clock24Hour ? "HH:mm" : "h:mm tt"); _date.Text = now.ToString("ddd, d MMM"); }
    internal void ApplyAppearance()
    { var theme = _environment.Theme; RequestedTheme = theme.ElementTheme; Background = theme.Brush("NexusShell"); _time.Foreground = theme.Brush("NexusText"); _date.Foreground = theme.Brush("NexusMuted"); _motion?.SetEnabled(!theme.HighContrast && theme.Animations && !_environment.Session.State.ReducedEffects); ApplyDensity(); RefreshClock(); }
    internal void Release() { _motion?.Dispose(); _iconButtons.Clear(); }
}
