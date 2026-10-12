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
    private readonly SolidColorBrush _transparent = new(Microsoft.UI.Colors.Transparent);
    private readonly Grid _body = new() { ColumnSpacing = 10, Padding = new Thickness(13, 7, 13, 7) };
    private readonly StackPanel _pins = new() { Orientation = Orientation.Horizontal, Spacing = 3 };
    private readonly StackPanel _windows = new() { Orientation = Orientation.Horizontal, Spacing = 3 };
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
    private bool? _appliedCompactDock;
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
    private readonly Button _start, _overview, _clockButton, _desktopButton;
    private readonly Border _separator;
    private double _reportedWidth;
    internal event Action? PreferredWidthChanged;
    internal double PreferredWidthDip => (_environment.Session.State.FloatingTaskbar ? 168 : 304) + (_pins.Children.Count + _windows.Children.Count) * (_environment.Session.State.CompactDock ? 44 : 52);
    internal Border Frame => _frame;
    // The real HWND uses WindowMaterial to control native acrylic, opaque
    // preparation and fallback. Its view must not repaint that same surface.
    internal bool MaterialOwnedByWindow { get; set; }
    private MotionController? _motion;
    private DockMotionController? _dockMotion;
    internal TaskbarView(DesktopEnvironment environment)
    {
        _environment = environment; _frame.Child = _body; Children.Add(_frame);
        _body.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); _body.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); _body.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var start = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        _start = IconButton("Launchpad", "Launchpad", () => environment.ShowMenu(), true); start.Children.Add(_start);
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
        var clock = new StackPanel { Spacing = 2 }; clock.Children.Add(_time); clock.Children.Add(_date);
        _clockButton = new Button { Content = clock, Style = (Style)Application.Current.Resources["QuietButton"], Padding = new Thickness(10, 5, 10, 5), CornerRadius = new CornerRadius(12) };
        _clockButton.Click += (_, _) => environment.ShowNotifications(); ToolTipService.SetToolTip(_clockButton, "Date and notifications"); status.Children.Add(_clockButton);
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
        UIElement face = NexusIcons.Image(icon, 36);
        var button = new Button { Content = face, Style = (Style)Application.Current.Resources["DockButton"], Width = 48, Height = 52 };
        button.Loaded += (_, _) => _motion?.AttachHover(button);
        button.Click += (_, _) => action(); ToolTipService.SetToolTip(button, title); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, title);
        if (retain) _iconButtons.Add(button); _motion?.AttachHover(button); return button;
    }
    private Button PinButton(AppEntry app)
    {
        var icon = NexusIcons.Image(NexusIcons.ForApp(app), 32);
        _ = AppIconCache.ApplyAsync(icon, app);
        var host = new Grid(); host.Children.Add(icon);
        var button = new Button { Content = host, Tag = host, Style = (Style)Application.Current.Resources["DockButton"], Width = 48, Height = 52 };
        _pinImages.Add(button, icon); bool arrive = _baselineReconciled;
        button.Loaded += (_, _) =>
        { if (_released) return; _motion?.AttachHover(button); _dockMotion?.Play(icon, arrive ? DockMotionCue.Arrive : DockMotionCue.None); arrive = false; };
        button.Unloaded += (_, _) => _dockMotion?.Detach(icon);
        button.Click += (_, _) => { if (_environment.TryLaunch(app)) _dockMotion?.Play(icon, DockMotionCue.Launch); };
        var menu = new MenuFlyout(); var open = new MenuFlyoutItem { Text = "Open " + app.Name }; open.Click += (_, _) => { if (_environment.TryLaunch(app)) _dockMotion?.Play(icon, DockMotionCue.Launch); }; menu.Items.Add(open);
        var remove = new MenuFlyoutItem { Text = "Remove from dock" }; remove.Click += (_, _) =>
        { if (_released) return; _environment.Session.State.PinnedApps.RemoveAll(pin => string.Equals(pin.Target, app.Target, StringComparison.OrdinalIgnoreCase)); _environment.SaveState(); }; menu.Items.Add(remove);
        foreach (var (label, direction) in new[] { ("Move pin left", -1), ("Move pin right", 1) })
        { var move = new MenuFlyoutItem { Text = label }; move.Click += (_, _) => { if (!_released && DesktopOrganization.MovePin(_environment.Session.State.PinnedApps, app.Target, direction)) _environment.SaveState(); }; menu.Items.Add(move); }
        menu.Opened += (_, _) => { _environment.HideDockPreview(); _openContextMenus.Add(menu); _environment.Taskbar.RefreshVisibility(); };
        menu.Closed += (_, _) => { _openContextMenus.Remove(menu); if (!_released) _environment.Taskbar.RefreshVisibility(); }; button.ContextFlyout = menu;
        ToolTipService.SetToolTip(button, "Open " + app.Name); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, "Open " + app.Name);
        _motion?.AttachHover(button); return button;
    }
    // Explicit Launchpad rescan invalidates the shared icon cache. Recreate
    // pinned icon surfaces once, without losing the current running-window
    // entries or turning normal dock polling into a visual rebuild.
    internal void RefreshPinnedAppIcons()
    {
        if (_released) return;
        _shownPins = [];
        Refresh(_windowSet.Windows.ToArray());
    }

    internal void VerifyPinnedIconRefresh()
    {
        var saved = _environment.Session.State.PinnedApps.ToArray();
        try
        {
            _environment.Session.State.PinnedApps.Clear();
            _environment.Session.State.PinnedApps.Add(
                new AppEntry("files", "Files", "nexus:files", "", "System"));
            Refresh([]);
            var oldButton = (Button)_pins.Children[0];
            var oldIcon = _pinImages[oldButton];
            RefreshPinnedAppIcons();
            var newButton = (Button)_pins.Children[0];
            if (ReferenceEquals(oldButton, newButton) || !_pinImages.TryGetValue(newButton, out var newIcon)
                || ReferenceEquals(oldIcon, newIcon))
                throw new InvalidOperationException("Manual installed-app refresh retained the old pinned dock icon.");
            var stableButton = newButton;
            for (int i = 0; i < 20; i++) Refresh([]);
            if (!ReferenceEquals(stableButton, _pins.Children[0]))
                throw new InvalidOperationException("Ordinary dock polling rebuilt a refreshed pin.");
        }
        finally
        {
            _environment.Session.State.PinnedApps.Clear();
            _environment.Session.State.PinnedApps.AddRange(saved);
            Refresh([]);
        }
    }

    internal void Refresh(IReadOnlyList<RunningWindow> windows)
    {
        if (_released) return;
        using var work = UiTrace.Measure("dock-refresh", "Dock", windows.Count);
        var pins = _environment.Session.State.PinnedApps.ToArray();
        bool pinsChanged = !_shownPins.SequenceEqual(pins);
        if (pinsChanged)
        {
            foreach (var button in _pins.Children.OfType<Button>()) { if (button.ContextFlyout is MenuFlyout menu) { menu.Hide(); _openContextMenus.Remove(menu); } _motion?.DetachHover(button); if (_pinImages.TryGetValue(button, out var icon)) _dockMotion?.Detach(icon); }
            _pinImages.Clear(); _pins.Children.Clear(); foreach (var pin in pins) _pins.Children.Add(PinButton(pin)); _shownPins = pins;
        }
        bool windowsChanged = _windowSet.Reconcile(windows);
        if (_baselineReconciled && !pinsChanged && !windowsChanged)
        {
            // A foreground/minimize change still needs state icons and actions,
            // but a stable snapshot does not need XAML layout reconciliation.
            RefreshWindowStates();
            if (_appliedCompactDock != _environment.Session.State.CompactDock) ApplyDensity();
            return;
        }
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
        string icon = window.ProcessId == Environment.ProcessId ? window.Title switch { "Sections" => "Apps", "Notes" => "Note", "Calculator" => "Calculator", _ => "Files" } : NexusIcons.ForProcess(window.ProcessName);
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
    internal void RefreshWindowStates(IntPtr? observedForeground = null)
    {
        if (_released) return;
        var foreground = observedForeground ?? NativeMethods.ForegroundTaskWindow();
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
            item.State = state;
            if (item.Button.IsEnabled != valid) item.Button.IsEnabled = valid;
            item.PreviewsEnabled = _environment.Session.State.DockPreviews;
        item.Button.Background = active ? _environment.Theme.Brush("NexusSelection") : _transparent;
            item.Indicator.Background = _environment.Theme.Brush(active ? "NexusAccent" : "NexusMuted");
            // Indicator brush already reflects the active state above.
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
        static void Size(FrameworkElement target, double width, double height)
        {
            if (target.Width != width) target.Width = width;
            if (target.Height != height) target.Height = height;
        }
        foreach (var button in _iconButtons.Concat(_pins.Children.OfType<Button>()).Concat(_windows.Children.OfType<Button>()))
        {
            Size(button, compact ? 40 : 48, compact ? 44 : 52);
            if (button.Content is Image icon) Size(icon, compact ? 30 : 36, compact ? 30 : 36);
        }
        foreach (var icon in _pinImages.Values.Concat(_items.Values.Select(i => i.Icon)))
            Size(icon, compact ? 30 : 40, compact ? 30 : 40);
        var separator = _windows.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (_separator.Visibility != separator) _separator.Visibility = separator;
        double width = PreferredWidthDip;
        if (_reportedWidth != width) { _reportedWidth = width; PreferredWidthChanged?.Invoke(); }
        _appliedCompactDock = compact;
    }
    internal void VerifyRetainedDensity()
    {
        bool original = _environment.Session.State.CompactDock;
        var button = _iconButtons[0]; var icon = (Image)button.Content;
        try
        {
            _environment.Session.State.CompactDock = false; Refresh([]);
            if (button.Width != 48 || button.Height != 52 || icon.Width != 36 || icon.Height != 36)
                throw new InvalidOperationException("The normal dock did not apply its production icon geometry.");
            _environment.Session.State.CompactDock = true; Refresh([]);
            if (button.Width != 40 || button.Height != 44 || icon.Width != 30 || icon.Height != 30 || !ReferenceEquals(button, _iconButtons[0]))
                throw new InvalidOperationException("Compact dock changes were lost behind unchanged window/pin snapshots.");
            int writes = 0;
            long token = button.RegisterPropertyChangedCallback(WidthProperty, (_, _) => writes++);
            try
            {
                for (int i = 0; i < 20; i++) Refresh([]);
                if (writes != 0) throw new InvalidOperationException("Unchanged dock snapshots rewrote icon sizing.");
                _environment.Session.State.CompactDock = false; Refresh([]);
                if (button.Width != 48 || button.Height != 52 || icon.Width != 36 || icon.Height != 36)
                    throw new InvalidOperationException("The dock did not restore normal geometry after compact mode.");
            }
            finally { button.UnregisterPropertyChangedCallback(WidthProperty, token); }

            // Even a changed running-window snapshot may call ApplyDensity.
            // Stable item sizes and the separator must not invalidate layout.
            int densityWrites = 0, widthNotifications = 0;
            Action notifyWidth = () => widthNotifications++;
            PreferredWidthChanged += notifyWidth;
            long iconSize = icon.RegisterPropertyChangedCallback(WidthProperty, (_, _) => densityWrites++);
            long buttonSize = button.RegisterPropertyChangedCallback(HeightProperty, (_, _) => densityWrites++);
            long separatorVisibility = _separator.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => densityWrites++);
            try
            {
                for (int i = 0; i < 100; i++) ApplyDensity();
                if (densityWrites != 0 || widthNotifications != 0)
                    throw new InvalidOperationException("Stable dock density rewrote item geometry or emitted spurious width changes.");
            }
            finally
            {
                icon.UnregisterPropertyChangedCallback(WidthProperty, iconSize);
                button.UnregisterPropertyChangedCallback(HeightProperty, buttonSize);
                _separator.UnregisterPropertyChangedCallback(UIElement.VisibilityProperty, separatorVisibility);
                PreferredWidthChanged -= notifyWidth;
            }

            SetAvailableWidth(700);
            int frameWrites = 0;
            long corner = _frame.RegisterPropertyChangedCallback(Border.CornerRadiusProperty, (_, _) => frameWrites++);
            long overview = _overview.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => frameWrites++);
            long clock = _clockButton.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => frameWrites++);
            long desktop = _desktopButton.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => frameWrites++);
            long date = _date.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => frameWrites++);
            try
            {
                for (int i = 0; i < 100; i++) SetAvailableWidth(700);
                if (frameWrites != 0)
                    throw new InvalidOperationException("Unchanged floating dock geometry rewrote frame corners or responsive control visibility.");
                SetAvailableWidth(400);
                SetAvailableWidth(700);
                if (frameWrites == 0)
                    throw new InvalidOperationException("Real dock width changes failed to reconcile native responsive controls.");
            }
            finally
            {
                _frame.UnregisterPropertyChangedCallback(Border.CornerRadiusProperty, corner);
                _overview.UnregisterPropertyChangedCallback(UIElement.VisibilityProperty, overview);
                _clockButton.UnregisterPropertyChangedCallback(UIElement.VisibilityProperty, clock);
                _desktopButton.UnregisterPropertyChangedCallback(UIElement.VisibilityProperty, desktop);
                _date.UnregisterPropertyChangedCallback(UIElement.VisibilityProperty, date);
            }
        }
        finally { _environment.Session.State.CompactDock = original; ApplyDensity(); SetAvailableWidth(PreferredWidthDip); }
    }
    private void ApplyFrameCorners()
    {
        var corners = new CornerRadius(_environment.Session.State.FloatingTaskbar && !_environment.Theme.HighContrast ? 22 : 0);
        if (!_frame.CornerRadius.Equals(corners)) _frame.CornerRadius = corners;
    }

    internal void SetAvailableWidth(double widthDip)
    {
        ApplyFrameCorners();
        // Each visibility assignment can invalidate XAML measure/arrange.
        // Preserve reachability while avoiding writes for a stable dock frame.
        static void SetVisible(UIElement element, bool visible)
        {
            var next = visible ? Visibility.Visible : Visibility.Collapsed;
            if (element.Visibility != next) element.Visibility = next;
        }
        SetVisible(_overview, widthDip >= 440);
        SetVisible(_clockButton, widthDip >= 480 && !_environment.Session.State.FloatingTaskbar);
        SetVisible(_desktopButton, widthDip >= 600);
        SetVisible(_date, widthDip >= 620);
    }
    internal void RefreshClock()
    {
        var now = DateTime.Now;
        string time = now.ToString(_environment.Session.State.Clock24Hour ? "HH:mm" : "h:mm tt");
        string date = now.ToString("ddd, d MMM");
        // The desktop heartbeat is more frequent than the displayed minute.
        // Do not invalidate XAML text layout when the clock has not changed.
        if (_time.Text != time) _time.Text = time;
        if (_date.Text != date) _date.Text = date;
    }
    internal void ApplyAppearance()
    {
        if (_released) return;
        var theme = _environment.Theme; RequestedTheme = theme.ElementTheme;
        // In isolation (native resource probe) render a readable solid dock.
        // In the real dock, WindowMaterial owns both material and fallback.
        if (!MaterialOwnedByWindow) GlassPaintRetention.Background(_frame, theme.Material("Dock", false));
        bool motionEnabled = !theme.HighContrast && theme.Animations && _environment.Session.State.SurfaceAnimations && !_environment.Session.State.ReducedEffects;
        _motion?.SetEnabled(motionEnabled); _dockMotion?.SetEnabled(motionEnabled);
        ApplyFrameCorners();
        if (!MaterialOwnedByWindow) GlassPaintRetention.Edge(_frame, theme.Edge);
        if (!_frame.BorderThickness.Equals(new Thickness(1))) _frame.BorderThickness = new Thickness(1);
        _separator.Background = theme.Brush("NexusBorder"); _time.Foreground = theme.Brush("NexusText"); _date.Foreground = theme.Brush("NexusMuted");
        _clockButton.Background = theme.Brush("NexusSelection");
        foreach (var button in _iconButtons) { button.Background = _transparent; button.Foreground = theme.Brush("NexusText"); }
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
