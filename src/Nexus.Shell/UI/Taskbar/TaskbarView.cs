using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
using Nexus.Shell.Desktop;
using Nexus.Shell.Interop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Windows.System;

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
    private readonly DockWindowSet _windowSet = new();
    private readonly Dictionary<DockWindowIdentity, DockItem> _items = [];
    private readonly HashSet<MenuFlyout> _openContextMenus = [];
    private readonly Dictionary<Button, Image> _pinImages = [];
    private readonly DispatcherTimer _hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(DockPreviewPolicy.HoverDelayMilliseconds) };
    private readonly EventHandler<object> _hoverTick;
    private DockItem? _hoverItem;
    private bool _baselineReconciled, _released;
    internal bool ContextMenuOpen => _openContextMenus.Count > 0;
    private sealed class DockItem
    {
        internal RunningWindow Window;
        internal Button Button = null!;
        internal Border Indicator = null!;
        internal Image Icon = null!;
        internal DockPresence? Presence;
        internal DockMotionCue PendingCue;
        internal bool NewlyOpened;
        internal bool? PreviewsEnabled;
        internal (bool Active, bool Minimized)? State;
        internal DockItem(RunningWindow window) => Window = window;
    }
    private readonly Button _start, _search, _overview, _clockButton, _desktopButton;
    private readonly Border _separator;
    private double _reportedWidth;
    internal event Action? PreferredWidthChanged;
    internal double PreferredWidthDip => 416 + (_pins.Children.Count + _windows.Children.Count) * (_environment.Session.State.CompactDock ? 44 : 52);
    private MotionController? _motion;
    private DockMotionController? _dockMotion;
    internal TaskbarView(DesktopEnvironment environment)
    {
        _environment = environment; _frame.Child = _body; Children.Add(_frame);
        _body.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); _body.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); _body.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var start = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        _start = IconButton("Nexus", "Start", () => environment.ShowMenu(), true); start.Children.Add(_start);
        _search = IconButton("Search", "Search apps", () => environment.ShowMenu(true), true); start.Children.Add(_search);
        _body.Children.Add(start);
        var middle = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        _separator = new Border { Width = 1, Height = 22, VerticalAlignment = VerticalAlignment.Center };
        middle.Children.Add(_pins); middle.Children.Add(_separator); middle.Children.Add(_windows);
        var scroll = new ScrollViewer { Content = middle, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Disabled, VerticalAlignment = VerticalAlignment.Center };
        scroll.ViewChanged += (_, _) => _environment.HideDockPreview();
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
        _hoverTick = (_, _) =>
        {
            _hoverTimer.Stop(); var item = _hoverItem; _hoverItem = null;
            if (_released || item is null || !_environment.CanShowDockPreview || ContextMenuOpen || !NativeMethods.Visible(_environment.Taskbar.Handle)) return;
            if (TryGetAnchor(item.Button, out var anchor) && NativeMethods.GetCursorPos(out var point)
                && DockPreviewPolicy.Contains(anchor, point.X, point.Y)) _environment.ShowDockPreview(item.Window, anchor);
        };
        _hoverTimer.Tick += _hoverTick;
        Loaded += (_, _) =>
        {
            if (_released) return;
            try { _motion ??= new MotionController(_body); foreach (var b in _iconButtons.Concat(_pins.Children.OfType<Button>()).Concat(_windows.Children.OfType<Button>())) _motion.AttachHover(b); }
            catch (Exception ex) { environment.Report("Taskbar motion unavailable", ex, false); }
            try { _dockMotion ??= new(_body); }
            catch (Exception ex) { environment.Report("Dock state motion unavailable", ex, false); }
            ApplyAppearance(); foreach (var item in _items.Values) PrepareMotion(item); _motion?.Enter(_body);
        };
        ApplyAppearance();
    }
    private Button IconButton(string icon, string title, Action action, bool retain = false)
    {
        string glyph = icon switch { "Nexus" => "\uE80F", "Search" => "\uE721", "Windows" => "\uE7F4", "Settings" => "\uE713", _ => "" };
        UIElement face = retain && glyph.Length > 0 ? new FontIcon { FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = glyph, FontSize = 22 } : NexusIcons.Image(icon, 36);
        var button = new Button { Content = face, Style = (Style)Application.Current.Resources["DockButton"], Width = 48, Height = 52 };
        button.Loaded += (_, _) => _motion?.AttachHover(button);
        button.Click += (_, _) => action(); ToolTipService.SetToolTip(button, title); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, title);
        if (retain) _iconButtons.Add(button); _motion?.AttachHover(button); return button;
    }
    private Button PinButton(AppEntry app)
    {
        var icon = NexusIcons.Image(NexusIcons.ForApp(app), 32);
        var host = new Grid(); host.Children.Add(icon);
        var button = new Button { Content = host, Tag = host, Style = (Style)Application.Current.Resources["DockButton"], Width = 48, Height = 52 };
        _pinImages.Add(button, icon); bool arrive = _baselineReconciled;
        button.Loaded += (_, _) =>
        { if (_released) return; _motion?.AttachHover(button); _dockMotion?.Play(icon, arrive ? DockMotionCue.Arrive : DockMotionCue.None); arrive = false; };
        button.Unloaded += (_, _) => _dockMotion?.Detach(icon);
        button.Click += (_, _) => { if (_environment.TryLaunch(app)) _dockMotion?.Play(icon, DockMotionCue.Launch); };
        ToolTipService.SetToolTip(button, "Open " + app.Name); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, "Open " + app.Name);
        _motion?.AttachHover(button); return button;
    }
    internal void Refresh(IReadOnlyList<RunningWindow> windows)
    {
        if (_released) return;
        var pins = _environment.Session.State.PinnedApps.ToArray();
        if (!_shownPins.SequenceEqual(pins))
        {
            foreach (var button in _pins.Children.OfType<Button>()) { _motion?.DetachHover(button); if (_pinImages.TryGetValue(button, out var icon)) _dockMotion?.Detach(icon); }
            _pinImages.Clear(); _pins.Children.Clear(); foreach (var pin in pins) _pins.Children.Add(PinButton(pin)); _shownPins = pins;
        }
        _windowSet.Reconcile(windows);
        var live = _windowSet.Windows.Select(DockWindowIdentity.Of).ToHashSet();
        _environment.RefreshDockPreviewWindows(_windowSet.Windows);
        foreach (var key in _items.Keys.Where(k => !live.Contains(k)).ToArray())
        {
            var removed = _items[key]; var button = removed.Button;
            if (ReferenceEquals(_hoverItem, removed)) CancelPreviewRequest();
            if (button.ContextFlyout is MenuFlyout menu) { menu.Hide(); _openContextMenus.Remove(menu); }
            _motion?.DetachHover(button); _dockMotion?.Detach(removed.Icon, removed.Indicator); _windows.Children.Remove(button); _items.Remove(key);
        }
        foreach (var window in _windowSet.Windows)
        {
            var key = DockWindowIdentity.Of(window);
            if (!_items.TryGetValue(key, out var item))
            {
                item = CreateWindowButton(window); _items.Add(key, item); _windows.Children.Add(item.Button);
            }
            if (item.Window != window) { item.Window = window; item.State = null; }
        }
        RefreshWindowStates();
        ApplyDensity(); RefreshClock(); _baselineReconciled = true;
    }
    private DockItem CreateWindowButton(RunningWindow window)
    {
        var item = new DockItem(window) { NewlyOpened = _baselineReconciled };
        string icon = window.ProcessId == Environment.ProcessId ? window.Title == "Sections" ? "Apps" : "Files" : NexusIcons.ForProcess(window.ProcessName);
        var face = new Grid(); var iconHost = new Grid(); item.Icon = NexusIcons.Image(icon, 32); iconHost.Children.Add(item.Icon); face.Children.Add(iconHost);
        item.Indicator = new Border { Width = 22, Height = 3, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center };
        face.Children.Add(item.Indicator);
        item.Button = new Button { Content = face, Tag = iconHost, Width = 46, Height = 52, Style = (Style)Application.Current.Resources["DockButton"] };
        item.Button.Click += (_, _) => _environment.ToggleDockWindow(item.Window);
        var menu = new MenuFlyout();
        var restore = new MenuFlyoutItem { Text = "Restore / switch to window" };
        restore.Click += (_, _) => _environment.RestoreDockWindow(item.Window);
        var minimize = new MenuFlyoutItem { Text = "Minimize window" };
        minimize.Click += (_, _) => _environment.MinimizeDockWindow(item.Window);
        var maximize = new MenuFlyoutItem { Text = "Maximize window" };
        maximize.Click += (_, _) => { _environment.HideDockPreview(); _environment.MaximizeDockWindow(item.Window); };
        var preview = new MenuFlyoutItem { Text = "Window preview" };
        preview.Click += (_, _) =>
        {
            // Let the context menu close before activating the preview.
            item.Button.DispatcherQueue.TryEnqueue(() => { if (!_released && TryGetAnchor(item.Button, out var anchor)) _environment.ShowDockPreview(item.Window, anchor, true); });
        };
        var close = new MenuFlyoutItem { Text = "Close window" };
        close.Click += (_, _) => { _environment.HideDockPreview(); _environment.CloseDockWindow(item.Window); };
        menu.Items.Add(restore); menu.Items.Add(minimize); menu.Items.Add(maximize); menu.Items.Add(preview); menu.Items.Add(new MenuFlyoutSeparator()); menu.Items.Add(close);
        menu.Opened += (_, _) =>
        {
            _environment.HideDockPreview(); _openContextMenus.Add(menu);
            bool valid = NativeMethods.OwnsWindow(item.Window);
            restore.IsEnabled = maximize.IsEnabled = close.IsEnabled = valid; preview.IsEnabled = valid && _environment.Session.State.DockPreviews;
            minimize.IsEnabled = valid && NativeMethods.Visible(item.Window.Handle) && !NativeMethods.IsMinimized(item.Window.Handle);
            maximize.Text = valid && NativeMethods.IsZoomed(item.Window.Handle) ? "Restore window size" : "Maximize window";
            _environment.Taskbar.RefreshVisibility();
        };
        menu.Closed += (_, _) => { _openContextMenus.Remove(menu); _environment.Taskbar.RefreshVisibility(); };
        item.Button.ContextFlyout = menu;
        item.Button.Loaded += (_, _) => { if (!_released) { _motion?.AttachHover(item.Button); PrepareMotion(item); } };
        item.Button.Unloaded += (_, _) => _dockMotion?.Detach(item.Icon, item.Indicator);
        item.Button.PointerEntered += (_, e) =>
        {
            if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse) return;
            if (!_environment.CanShowDockPreview || ContextMenuOpen || _released) return;
            _hoverTimer.Stop(); _hoverItem = item; _hoverTimer.Start();
        };
        item.Button.PointerExited += (_, _) => { if (ReferenceEquals(_hoverItem, item)) CancelPreviewRequest(); };
        item.Button.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => _environment.HideDockPreview()), true);
        item.Button.KeyDown += (_, e) =>
        { if (e.Key == VirtualKey.Down && _environment.CanShowDockPreview && TryGetAnchor(item.Button, out var anchor)) { CancelPreviewRequest(); _environment.ShowDockPreview(item.Window, anchor, true); e.Handled = true; } };
        _motion?.AttachHover(item.Button); return item;
    }
    internal void RefreshWindowStates()
    {
        if (_released) return;
        var foreground = NativeMethods.ForegroundTaskWindow();
        foreach (var item in _items.Values)
        {
            bool valid = NativeMethods.OwnsWindow(item.Window);
            bool minimized = valid && (NativeMethods.IsMinimized(item.Window.Handle) || !NativeMethods.Visible(item.Window.Handle));
            bool active = valid && !minimized && foreground == item.Window.Handle;
            var state = (active, minimized);
            var presence = minimized ? DockPresence.Minimized : active ? DockPresence.Active : DockPresence.Running;
            var cue = valid ? DockMotionPolicy.Transition(item.Presence, presence, item.NewlyOpened) : DockMotionCue.None;
            bool transitioned = item.Presence != presence;
            bool initialized = item.Presence is not null; item.Presence = valid ? presence : null; item.NewlyOpened = false;
            if (cue != DockMotionCue.None) { if (item.Icon.IsLoaded && _dockMotion is not null) { _dockMotion.Play(item.Icon, cue); item.PendingCue = DockMotionCue.None; } else item.PendingCue = cue; }
            if (_dockMotion is not null && transitioned) { item.Indicator.Width = 22; item.Indicator.Opacity = 1; _dockMotion.SetIndicator(item.Indicator, presence, initialized); }
            if (item.State == state && item.Button.IsEnabled == valid && item.PreviewsEnabled == _environment.Session.State.DockPreviews) continue;
            item.State = state; item.Button.IsEnabled = valid; item.PreviewsEnabled = _environment.Session.State.DockPreviews;
            item.Button.Background = active ? _environment.Theme.Surface("Accent") : _environment.Theme.Brush(minimized ? "NexusInput" : "NexusCard");
            item.Indicator.Background = _environment.Theme.Brush(active ? "NexusAccent" : "NexusMuted");
            if (active) item.Indicator.Background = _environment.Theme.Brush("NexusAccentText");
            if (_dockMotion is null) { item.Indicator.Width = active ? 22 : minimized ? 5 : 10; item.Indicator.Opacity = minimized ? .45 : 1; }
            string action = minimized ? "Restore " : active ? "Minimize " : "Switch to ";
            ToolTipService.SetToolTip(item.Button, _environment.Session.State.DockPreviews ? null : action + item.Window.Title);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item.Button, action + item.Window.Title);
        }
    }
    private void PrepareMotion(DockItem item)
    {
        if (_dockMotion is null) return;
        item.Indicator.Width = 22; item.Indicator.Opacity = 1;
        _dockMotion.SetIndicator(item.Indicator, item.Presence ?? DockPresence.Running, false);
        _dockMotion.Play(item.Icon, item.PendingCue); item.PendingCue = DockMotionCue.None;
    }
    internal void CancelPreviewRequest() { _hoverTimer.Stop(); _hoverItem = null; }
    private bool TryGetAnchor(Button button, out ShellRect anchor)
    {
        anchor = default;
        if (!button.IsLoaded || !NativeMethods.GetWindowRect(_environment.Taskbar.Handle, out var window) || _environment.Taskbar.Content is not FrameworkElement root) return false;
        try
        {
            var point = button.TransformToVisual(root).TransformPoint(new Windows.Foundation.Point(0, 0));
            double scale = button.XamlRoot?.RasterizationScale ?? ShellLayerInterop.Scale(_environment.Taskbar.Handle);
            anchor = new(window.Left + (int)Math.Round(point.X * scale), window.Top + (int)Math.Round(point.Y * scale),
                Math.Max(1, (int)Math.Round(button.ActualWidth * scale)), Math.Max(1, (int)Math.Round(button.ActualHeight * scale)));
            return true;
        }
        catch { return false; }
    }
    private void ApplyDensity()
    {
        bool compact = _environment.Session.State.CompactDock;
        foreach (var button in _iconButtons.Concat(_pins.Children.OfType<Button>()).Concat(_windows.Children.OfType<Button>())) { button.Width = compact ? 40 : 48; button.Height = compact ? 44 : 52; if (button.Content is Image i) i.Width = i.Height = compact ? 30 : 36; }
        foreach (var icon in _pinImages.Values.Concat(_items.Values.Select(i => i.Icon))) icon.Width = icon.Height = compact ? 28 : 32;
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
    internal void Reveal() => _motion?.Enter(_body);
    internal void ApplyAppearance()
    {
        if (_released) return;
        var theme = _environment.Theme; RequestedTheme = theme.ElementTheme; _frame.Background = theme.Surface("Dock");
        bool motionEnabled = !theme.HighContrast && theme.Animations && !_environment.Session.State.ReducedEffects;
        _motion?.SetEnabled(motionEnabled); _dockMotion?.SetEnabled(motionEnabled);
        _frame.CornerRadius = new CornerRadius(_environment.Session.State.FloatingTaskbar && !theme.HighContrast ? 22 : 0);
        _frame.BorderBrush = theme.Brush("NexusBorder"); _frame.BorderThickness = new Thickness(theme.HighContrast ? 1 : 0);
        _separator.Background = theme.Brush("NexusBorder"); _time.Foreground = theme.Brush("NexusText"); _date.Foreground = theme.Brush("NexusMuted");
        _clockButton.Background = theme.Brush("NexusCard");
        foreach (var button in _iconButtons) { button.Background = theme.Brush("NexusCard"); button.Foreground = theme.Brush("NexusText"); }
        _start.Background = theme.Surface("Accent"); _start.Foreground = theme.Brush("NexusAccentText");
        foreach (var item in _items.Values) item.State = null;
        RefreshWindowStates();
        ApplyDensity(); RefreshClock();
    }
    internal void Release()
    {
        if (_released) return; _released = true; CancelPreviewRequest(); _hoverTimer.Tick -= _hoverTick;
        foreach (var menu in _openContextMenus.ToArray()) menu.Hide();
        _openContextMenus.Clear(); _motion?.Dispose(); _dockMotion?.Dispose(); _pinImages.Clear(); _iconButtons.Clear(); _items.Clear();
    }
}
