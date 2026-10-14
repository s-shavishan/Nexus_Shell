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
    private readonly Grid _body = new() { ColumnSpacing = 14, Padding = new Thickness(18, 10, 18, 10) };
    // Each inset island is *inside* the window-owned material surface. Never
    // repaint Frame here: native acrylic and its opaque fallback remain owned
    // exclusively by TaskbarWindow / WindowMaterial.
    private readonly Border _launchIsland = new() { CornerRadius = new CornerRadius(17), BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _appsIsland = new() { CornerRadius = new CornerRadius(19), BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _utilityIsland = new() { CornerRadius = new CornerRadius(17), BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _topGlint = new() { Height = 1, Opacity = .28, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8, 0, 8, 0) };
    private bool _islandsEmphasized;
    private readonly StackPanel _pins = new() { Orientation = Orientation.Horizontal, Spacing = 7 };
    private readonly StackPanel _windows = new() { Orientation = Orientation.Horizontal, Spacing = 7 };
    private readonly TextBlock _time = new() { FontSize = 12, TextAlignment = TextAlignment.Right };
    private readonly TextBlock _date = new() { FontSize = 10, TextAlignment = TextAlignment.Right };
    private readonly List<Button> _iconButtons = [];
    private IReadOnlyList<AppEntry> _shownPins = [];
    private readonly DockWindowSet _windowSet = new();
    private readonly Dictionary<DockWindowIdentity, DockItem> _items = [];
    private readonly HashSet<MenuFlyout> _openContextMenus = [];
    private readonly Dictionary<Button, Image> _pinImages = [];
    private readonly Dictionary<Button, (AppEntry App, Border Indicator)> _pinMarkers = [];
    private readonly Dictionary<string, string> _shortcutTargets = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pendingShortcuts = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _hoverTimer = new() { Interval = TimeSpan.FromMilliseconds(DockPreviewPolicy.HoverDelayMilliseconds) };
    private readonly EventHandler<object> _hoverTick;
    private DockItem? _hoverItem;
    private (Button Button, AppEntry App)? _hoverPin;
    private bool _baselineReconciled, _released, _refreshIncomplete;
    private bool? _appliedCompactDock;
    internal bool ContextMenuOpen => _openContextMenus.Count > 0;

    // A failed settings-triggered Refresh can leave a partially updated UI
    // even when the next native snapshot is unchanged. The environment uses
    // this invariant to force recovery without waiting for a new window event.
    internal bool NeedsRecovery
    {
        get
        {
            if (_released) return false;
            if (_refreshIncomplete
                || !DockVisualRecoveryPolicy.HasConsistentIdentities(_windowSet.Windows, _items.Keys)
                || _items.Count != _windows.Children.Count) return true;
            for (int i = 0; i < _windowSet.Windows.Count; i++)
            {
                var key = DockWindowIdentity.Of(_windowSet.Windows[i]);
                if (!_items.TryGetValue(key, out var item)
                    || !ReferenceEquals(_windows.Children[i], item.Button))
                    return true;
            }
            return false;
        }
    }
    private sealed class DockItem
    {
        internal RunningWindow Window;
        internal Button Button = null!;
        internal Border Indicator = null!;
        internal Image Icon = null!;
        internal DockPresence? Presence;
        internal DockMotionCue PendingCue;
        internal bool NewlyOpened;
        internal bool EverLoaded;
        internal bool? PreviewsEnabled;
        internal (bool Active, bool Minimized)? State;
        internal DockItem(RunningWindow window) => Window = window;
    }
    private readonly Button _start, _overview, _clockButton, _desktopButton;
    private readonly Border _separator;
    private double _reportedWidth;
    internal event Action? PreferredWidthChanged;
    internal double PreferredWidthDip => (_environment.Session.State.FloatingTaskbar ? 212 : 344)
        + (_pins.Children.Count + _windows.Children.OfType<Button>().Count(button => button.Visibility == Visibility.Visible))
        * (_environment.Session.State.CompactDock ? 44 : 72);
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
        _launchIsland.Child = start;
        _body.Children.Add(_launchIsland);
        var middle = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16, VerticalAlignment = VerticalAlignment.Center };
        _separator = new Border { Width = 1, Height = 34, VerticalAlignment = VerticalAlignment.Center };
        middle.Children.Add(_pins); middle.Children.Add(_separator); middle.Children.Add(_windows);
        var scroll = new ScrollViewer { Content = middle, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Enabled, VerticalScrollMode = ScrollMode.Disabled, VerticalAlignment = VerticalAlignment.Center };
        scroll.ViewChanged += (_, _) => _environment.HideDockPreview();
        _appsIsland.Child = scroll;
        Grid.SetColumn(_appsIsland, 1); _body.Children.Add(_appsIsland);
        var status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        _overview = IconButton("Windows", "Switch windows", environment.ShowWindowOverview, true); status.Children.Add(_overview);
        var clock = new StackPanel { Spacing = 2 }; clock.Children.Add(_time); clock.Children.Add(_date);
        _clockButton = new Button { Content = clock, Style = (Style)Application.Current.Resources["QuietButton"], Padding = new Thickness(10, 5, 10, 5), CornerRadius = new CornerRadius(12) };
        _clockButton.Click += (_, _) => environment.OpenWindowsSurface(WindowsShellSurface.Calendar); ToolTipService.SetToolTip(_clockButton, "Windows date and time"); status.Children.Add(_clockButton);
        var clockMenu = environment.Menus.ClockMenu(); _clockButton.ContextFlyout = clockMenu;
        var dockMenu = environment.Menus.DockMenu(); _frame.ContextFlyout = dockMenu;
        foreach (var context in new[] { clockMenu, dockMenu })
        {
            context.Opened += (_, _) => { _environment.HideDockPreview(); _openContextMenus.Add(context); _environment.Taskbar.RefreshVisibility(); };
            context.Closed += (_, _) => { _openContextMenus.Remove(context); if (!_released) _environment.Taskbar.RefreshVisibility(); };
        }
        _desktopButton = new Button { Width = 24, Height = 40, Content = new FontIcon { FontFamily = new FontFamily("Segoe MDL2 Assets"), Glyph = "\uE740", FontSize = 13 }, Style = (Style)Application.Current.Resources["QuietButton"], Padding = new Thickness(0) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_desktopButton, "Show Nexus desktop"); ToolTipService.SetToolTip(_desktopButton, "Show Nexus desktop");
        _desktopButton.Click += (_, _) => environment.ShowDesktop(); status.Children.Add(_desktopButton);
        _utilityIsland.Child = status;
        Grid.SetColumn(_utilityIsland, 2); _body.Children.Add(_utilityIsland);
        Grid.SetColumnSpan(_topGlint, 3); _body.Children.Add(_topGlint);
        _hoverTick = (_, _) =>
        {
            _hoverTimer.Stop();
            var item = _hoverItem; var pin = _hoverPin;
            _hoverItem = null; _hoverPin = null;
            if (_released || !_environment.CanShowDockPreview || ContextMenuOpen
                || !NativeMethods.Visible(_environment.Taskbar.Handle)) return;
            // The representative running icon is hidden when paired with a
            // pin; resolve the live HWND on hover instead of showing nothing.
            var button = item?.Button ?? pin?.Button;
            var target = item?.Window ?? (pin is { } current ? FindPinnedWindow(current.App) : null);
            if (button is not null && target is not null && NativeMethods.OwnsWindow(target)
                && TryGetAnchor(button, out var anchor) && NativeMethods.GetCursorPos(out var point)
                && DockPreviewPolicy.Contains(anchor, point.X, point.Y))
                _environment.ShowDockPreview(target, anchor);
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
        UIElement face = NexusIcons.Image(icon, 46);
        var button = new Button { Content = face, Style = (Style)Application.Current.Resources["DockButton"], Width = 64, Height = 68 };
        button.Loaded += (_, _) => _motion?.AttachHover(button);
        button.Click += (_, _) => action(); ToolTipService.SetToolTip(button, title); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, title);
        if (retain) _iconButtons.Add(button); _motion?.AttachHover(button); return button;
    }
    private Button PinButton(AppEntry app)
    {
        QueueShortcutIdentity(app.Target);
        var icon = NexusIcons.Image(NexusIcons.ForApp(app), 46);
        _ = AppIconCache.ApplyAsync(icon, app);
        var host = new Grid(); host.Children.Add(icon);
        var marker = new Border { Width = 18, Height = 3, CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
            Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        host.Children.Add(marker);
        var button = new Button { Content = host, Tag = host, Style = (Style)Application.Current.Resources["DockButton"], Width = 64, Height = 68 };
        _pinImages.Add(button, icon); _pinMarkers.Add(button, (app, marker));
        bool arrive = _baselineReconciled;
        void PlayArrival()
        {
            if (!arrive || _released || _dockMotion is null || !icon.IsLoaded) return;
            _dockMotion.Play(icon, DockMotionCue.Arrive);
            arrive = false;
        }
        // The image may load after its button. Play the arrival once when
        // both the visual and retained compositor controller are ready.
        icon.Loaded += (_, _) => PlayArrival();
        button.Loaded += (_, _) =>
        {
            if (_released) return;
            _motion?.AttachHover(button);
            PlayArrival();
        };
        button.Unloaded += (_, _) =>
        {
            // An unloaded pin is no longer newly arrived: never replay stale
            // creation motion on later density/theme reparenting.
            arrive = false;
            _dockMotion?.Detach(icon);
        };
        void OpenPinned()
        {
            var running = FindPinnedWindow(app);
            if (running is not null) _environment.ToggleDockWindow(running);
            else if (_environment.TryLaunch(app)) _dockMotion?.Play(icon, DockMotionCue.Launch);
        }
        button.Click += (_, _) => OpenPinned();
        button.PointerEntered += (_, e) =>
        {
            if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse
                || _released || !_environment.CanShowDockPreview || ContextMenuOpen
                || FindPinnedWindow(app) is null) return;
            _hoverTimer.Stop(); _hoverItem = null;
            _hoverPin = (button, app); _hoverTimer.Start();
        };
        button.PointerExited += (_, _) =>
        {
            if (_hoverPin is { } pending && ReferenceEquals(pending.Button, button))
                CancelPreviewRequest();
        };
        button.AddHandler(UIElement.PointerPressedEvent,
            new PointerEventHandler((_, _) => _environment.HideDockPreview()), true);
        button.KeyDown += (_, e) =>
        {
            if (e.Key != VirtualKey.Down || _released || !_environment.CanShowDockPreview) return;
            var target = FindPinnedWindow(app);
            if (target is null || !TryGetAnchor(button, out var anchor)) return;
            CancelPreviewRequest();
            _environment.ShowDockPreview(target, anchor, true); e.Handled = true;
        };
        var menu = Menus.ShellMenu.Create();
        menu.Items.Add(Menus.ShellMenu.Action("Open or switch", OpenPinned, "\uE768"));
        // Keep every real document reachable even though the dock merges
        // only a representative running icon into its pinned artwork.
        var openWindows = new MenuFlyoutSubItem { Text = "Open windows" };
        menu.Items.Add(openWindows);
        var remove = new MenuFlyoutItem { Text = "Remove from dock" }; remove.Click += (_, _) =>
        { if (_released) return; _environment.Session.State.PinnedApps.RemoveAll(pin => string.Equals(pin.Target, app.Target, StringComparison.OrdinalIgnoreCase)); _environment.SaveState(); }; menu.Items.Add(remove);
        var position = Menus.ShellMenu.Group("Position");
        foreach (var (label, direction) in new[] { ("Move left", -1), ("Move right", 1) })
        { position.Items.Add(Menus.ShellMenu.Action(label, () => { if (!_released && DesktopOrganization.MovePin(_environment.Session.State.PinnedApps, app.Target, direction)) _environment.SaveState(); })); } menu.Items.Add(position);
        if (Path.IsPathFullyQualified(app.Target)) menu.Items.Add(Menus.ShellMenu.Action("Windows file options…", () => _environment.WindowsFileAction(app.Target), "\uE713"));
        menu.Opened += (_, _) =>
        {
            _environment.HideDockPreview(); _openContextMenus.Add(menu);
            openWindows.Items.Clear();
            string target = _shortcutTargets.TryGetValue(app.Target, out var resolved)
                ? resolved : app.Target;
            var matching = _windowSet.Windows.Where(w =>
                DockPinnedWindowPolicy.Matches(target, app.Name, w.Title, w.ProcessName)
                && NativeMethods.OwnsWindow(w)).Take(12).ToArray();
            openWindows.Visibility = matching.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            foreach (var window in matching)
            {
                var chosen = window;
                string label = string.IsNullOrWhiteSpace(window.Title)
                    ? window.ProcessName : window.Title;
                openWindows.Items.Add(Menus.ShellMenu.Action(label,
                    () => _environment.RestoreDockWindow(chosen), "\uE8A7"));
            }
            _environment.Taskbar.RefreshVisibility();
        };
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

    internal void Refresh(IReadOnlyList<RunningWindow> windows, IReadOnlySet<IntPtr>? destroyedHandles = null)
    {
        if (_released) return;
        using var work = UiTrace.Measure("dock-refresh", "Dock", windows.Count);
        // Reconcile may have failed after updating the retained DockWindowSet,
        // but before completing the WinUI child list. A later identical native
        // snapshot must not bypass the repair path.
        bool recovering = NeedsRecovery;
        _refreshIncomplete = true;
        var pins = _environment.Session.State.PinnedApps.ToArray();
        bool pinsChanged = !_shownPins.SequenceEqual(pins);
        if (pinsChanged)
        {
            // Old pinned buttons may still hold a delayed hover request.
            CancelPreviewRequest();
            foreach (var button in _pins.Children.OfType<Button>()) { if (button.ContextFlyout is MenuFlyout menu) { menu.Hide(); _openContextMenus.Remove(menu); } _motion?.DetachHover(button); if (_pinImages.TryGetValue(button, out var icon)) _dockMotion?.Detach(icon); }
            _pinImages.Clear(); _pinMarkers.Clear();
            _pins.Children.Clear(); foreach (var pin in pins) _pins.Children.Add(PinButton(pin)); _shownPins = pins;
        }
        bool windowsChanged = _windowSet.Reconcile(windows, destroyedHandles);
        if (!DockVisualRecoveryPolicy.RequiresFullReconcile(
                _baselineReconciled, pinsChanged, windowsChanged, recovering,
                _windowSet.Windows.Count, _items.Count, _windows.Children.Count))
        {
            // A stable layout needs only a state update; a previous partial
            // failure or missing visual forces a full recovery instead.
            RefreshWindowStates();
            if (_appliedCompactDock != _environment.Session.State.CompactDock) ApplyDensity();
            _refreshIncomplete = false;
            return;
        }
        var live = _windowSet.Windows.Select(DockWindowIdentity.Of).ToHashSet();
        _environment.RefreshDockPreviewWindows(_windowSet.Windows);
        foreach (var key in _items.Keys.Where(k => !live.Contains(k)
            || destroyedHandles?.Contains(k.Handle) == true).ToArray())
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
        if (DockVisualRecoveryPolicy.RequiresVisualRebuild(
                recovering, _windowSet.Windows.Count, _windows.Children.Count))
        {
            // Reattach the existing live controls in the exact retained order.
            // This only runs following an interrupted refresh or a broken
            // child count, not on normal foreground/window notifications.
            CancelPreviewRequest();
            _windows.Children.Clear();
            foreach (var window in _windowSet.Windows)
                _windows.Children.Add(_items[DockWindowIdentity.Of(window)].Button);
        }
        RefreshWindowStates();
        ApplyDensity(); RefreshClock(); _baselineReconciled = true;
        _refreshIncomplete = false;
    }
    private void QueueShortcutIdentity(string target)
    {
        if (!target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
            || !Path.IsPathFullyQualified(target) || target.StartsWith(@"\\", StringComparison.Ordinal)
            || _shortcutTargets.ContainsKey(target) || !_pendingShortcuts.Add(target)) return;
        // IShellLink uses STA COM. Resolve only once per new pin, away from the
        // dock's frame/foreground/scroll lanes; no blocking file or COM calls
        // during a hover or native window event.
        var thread = new Thread(() =>
        {
            string resolved = "";
            try
            {
                var link = ShortcutResolver.Read(target);
                if (link.Target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    resolved = link.Target;
            }
            catch (Exception error) { Log.Write("Pinned shortcut identity unavailable: " + target, error); }
            DispatcherQueue.TryEnqueue(() =>
            {
                _pendingShortcuts.Remove(target);
                if (_released) return;
                _shortcutTargets[target] = resolved;
                // Window visibility is controlled by ApplyDensity, not the
                // indicator refresh. Reconcile both after a shortcut resolves:
                // otherwise a pinned app can still have a duplicate running
                // button until a later native structural event.
                ApplyDensity();
                RefreshWindowStates();
            });
        }) { IsBackground = true, Name = "Nexus dock shortcut identity" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }
    private RunningWindow? FindPinnedWindow(AppEntry pin)
    {
        string target = _shortcutTargets.TryGetValue(pin.Target, out var resolved) ? resolved : pin.Target;
        return _windowSet.Windows.FirstOrDefault(window =>
            DockPinnedWindowPolicy.Matches(target, pin.Name, window.Title, window.ProcessName));
    }

    private bool HasPinnedArtwork(RunningWindow window)
        => _shownPins.Any(pin =>
        {
            // Only the representative window is integrated into the pin.
            // Additional document/windows from the same process remain
            // individually reachable as running entries.
            var representative = FindPinnedWindow(pin);
            return representative is not null
                && DockWindowIdentity.Of(representative) == DockWindowIdentity.Of(window);
        });

    private void RefreshPinnedIndicators(IntPtr? observedForeground = null)
    {
        // Use the caller's already-observed foreground HWND to avoid a
        // second user32 query on every WinEvent-driven dock repaint.
        var foreground = observedForeground ?? NativeMethods.ForegroundTaskWindow();
        foreach (var (button, marker) in _pinMarkers)
        {
            var running = FindPinnedWindow(marker.App);
            bool active = running is not null;
            var visibility = active ? Visibility.Visible : Visibility.Collapsed;
            if (marker.Indicator.Visibility != visibility) marker.Indicator.Visibility = visibility;
            if (active)
            {
                var brush = _environment.Theme.Brush(running!.Handle == foreground ? "NexusAccent" : "NexusMuted");
                if (!ReferenceEquals(marker.Indicator.Background, brush)) marker.Indicator.Background = brush;
            }
            string tooltip = active ? "Switch to " + marker.App.Name : "Open " + marker.App.Name;
            if (!Equals(ToolTipService.GetToolTip(button), tooltip)) ToolTipService.SetToolTip(button, tooltip);
            if (Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(button) != tooltip)
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, tooltip);
        }
    }

    private DockItem CreateWindowButton(RunningWindow window)
    {
        var item = new DockItem(window) { NewlyOpened = _baselineReconciled };
        string icon = window.ProcessId == Environment.ProcessId ? window.Title switch { "File jobs" => "Jobs", "Nexus Controls" => "Controls", "Notes" => "Note", "Calculator" => "Calculator", _ => "Files" } : NexusIcons.ForProcess(window.ProcessName);
        var face = new Grid(); var iconHost = new Grid(); item.Icon = NexusIcons.Image(icon, 46); iconHost.Children.Add(item.Icon); face.Children.Add(iconHost);
        item.Indicator = new Border { Width = 22, Height = 3, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center };
        face.Children.Add(item.Indicator);
        item.Button = new Button { Content = face, Tag = iconHost, Width = 64, Height = 68, Style = (Style)Application.Current.Resources["DockButton"] };
        item.Button.Click += (_, _) => _environment.ToggleDockWindow(item.Window);
        var menu = Menus.ShellMenu.Create(); RunningWindow target = item.Window;
        var restore = new MenuFlyoutItem { Text = "Activate", Icon = new FontIcon { Glyph = "\uE8A7" } };
        restore.Click += (_, _) => _environment.RestoreDockWindow(target);
        var minimize = new MenuFlyoutItem { Text = "Minimize window" };
        minimize.Click += (_, _) => _environment.MinimizeDockWindow(target);
        var maximize = new MenuFlyoutItem { Text = "Maximize window" };
        maximize.Click += (_, _) => { _environment.HideDockPreview(); _environment.MaximizeDockWindow(target); };
        var preview = new MenuFlyoutItem { Text = "Window preview" };
        preview.Click += (_, _) =>
        {
            // Let the context menu close before activating the preview.
            var selected = target; item.Button.DispatcherQueue.TryEnqueue(() => { if (!_released && TryGetAnchor(item.Button, out var anchor)) _environment.ShowDockPreview(selected, anchor, true); });
        };
        var close = new MenuFlyoutItem { Text = "Close window" };
        close.Click += (_, _) => { _environment.HideDockPreview(); _environment.CloseDockWindow(target); };
        var arrange = Menus.ShellMenu.Group("Arrange", "\uE9A6"); foreach (string placement in WindowPlacementPlan.Choices) arrange.Items.Add(Menus.ShellMenu.Action(placement, () => _environment.PlaceWindow(target, placement)));
        menu.Items.Add(restore); menu.Items.Add(minimize); menu.Items.Add(maximize); menu.Items.Add(arrange); menu.Items.Add(preview); menu.Items.Add(new MenuFlyoutSeparator()); menu.Items.Add(close);
        menu.Opened += (_, _) =>
        {
            _environment.HideDockPreview(); _openContextMenus.Add(menu);
            target = item.Window; bool valid = NativeMethods.OwnsWindow(target); arrange.IsEnabled = valid;
            restore.IsEnabled = maximize.IsEnabled = close.IsEnabled = valid; preview.IsEnabled = valid && _environment.Session.State.DockPreviews;
            minimize.IsEnabled = valid && NativeMethods.Visible(target.Handle) && !NativeMethods.IsMinimized(target.Handle);
            maximize.Text = valid && NativeMethods.IsZoomed(target.Handle) ? "Restore window size" : "Maximize window";
            _environment.Taskbar.RefreshVisibility();
        };
        menu.Closed += (_, _) => { _openContextMenus.Remove(menu); _environment.Taskbar.RefreshVisibility(); };
        item.Button.ContextFlyout = menu;
        // The child indicator and the button can receive Loaded in either
        // order. Settle on the indicator's own Loaded event so deferred
        // compositor work cannot be lost when the parent loads first.
        item.Indicator.Loaded += (_, _) =>
        {
            if (!_released && _dockMotion is not null)
                _dockMotion.SetIndicator(item.Indicator, item.Presence ?? DockPresence.Running, false);
        };
        // Preserve an arrival cue when the button loads before its image.
        // Never replay it after unload, when it has already become stale.
        item.Icon.Loaded += (_, _) =>
        {
            if (_released || _dockMotion is null || item.PendingCue != DockMotionCue.Arrive) return;
            _dockMotion.Play(item.Icon, DockMotionCue.Arrive);
            item.PendingCue = DockMotionCue.None;
        };
        item.Button.Loaded += (_, _) =>
        {
            if (_released) return;
            _motion?.AttachHover(item.Button);
            PrepareMotion(item);
            item.EverLoaded = true;
        };
        item.Button.Unloaded += (_, _) =>
        {
            item.EverLoaded = true;
            item.PendingCue = DockMotionCue.None;
            _dockMotion?.Detach(item.Icon, item.Indicator);
        };
        item.Button.PointerEntered += (_, e) =>
        {
            if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse) return;
            if (!_environment.CanShowDockPreview || ContextMenuOpen || _released) return;
            _hoverTimer.Stop(); _hoverPin = null; _hoverItem = item; _hoverTimer.Start();
        };
        item.Button.PointerExited += (_, _) => { if (ReferenceEquals(_hoverItem, item)) CancelPreviewRequest(); };
        item.Button.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => _environment.HideDockPreview()), true);
        item.Button.KeyDown += (_, e) =>
        { if (e.Key == VirtualKey.Down && _environment.CanShowDockPreview && TryGetAnchor(item.Button, out var anchor)) { CancelPreviewRequest(); _environment.ShowDockPreview(item.Window, anchor, true); e.Handled = true; } };
        _motion?.AttachHover(item.Button); return item;
    }
    // Used for ordinary Alt+Tab/foreground transitions. The item dictionary
    // is scanned in managed memory; only two HWNDs require native state probes.
    internal void RefreshForegroundStates(IntPtr previous, IntPtr foreground)
    {
        if (_released) return;
        foreach (var item in _items.Values)
            if (DockStateRefreshPolicy.ShouldInspect(item.Window.Handle, previous, foreground))
                RefreshWindowState(item, foreground);
        // Pinned representative windows are hidden from the running icon
        // strip. The foreground-only path MUST update their indicators too,
        // otherwise the dock appears to stop following active applications.
        RefreshPinnedIndicators(foreground);
    }

    internal void RefreshWindowStates(IntPtr? observedForeground = null)
    {
        if (_released) return;
        var foreground = observedForeground ?? NativeMethods.ForegroundTaskWindow();
        foreach (var item in _items.Values) RefreshWindowState(item, foreground);
        RefreshPinnedIndicators(foreground);
    }

    private void RefreshWindowState(DockItem item, IntPtr foreground)
    {
        bool valid = NativeMethods.OwnsWindow(item.Window);
        bool minimized = valid && (NativeMethods.IsMinimized(item.Window.Handle) || !NativeMethods.Visible(item.Window.Handle));
        bool active = valid && !minimized && foreground == item.Window.Handle;
        var state = (active, minimized);
        var presence = minimized ? DockPresence.Minimized : active ? DockPresence.Active : DockPresence.Running;
        var cue = valid ? DockMotionPolicy.Transition(item.Presence, presence, item.NewlyOpened) : DockMotionCue.None;
        bool transitioned = DockIndicatorUpdatePolicy.ShouldTransition(valid, item.Presence, presence);
        bool initialized = item.Presence is not null; item.Presence = valid ? presence : null; item.NewlyOpened = false;
        if (!valid) item.PendingCue = DockMotionCue.None;
        switch (DockCueDeliveryPolicy.Decide(cue, item.Icon.IsLoaded, item.EverLoaded, _dockMotion is not null))
        {
            case DockCueDelivery.Play:
                _dockMotion!.Play(item.Icon, cue);
                item.PendingCue = DockMotionCue.None;
                break;
            case DockCueDelivery.BufferArrival:
                item.PendingCue = DockMotionCue.Arrive;
                break;
            case DockCueDelivery.Discard:
                item.PendingCue = DockMotionCue.None;
                break;
        }
        if (_dockMotion is not null && transitioned)
        {
            SetIndicatorLayout(item.Indicator, 22, 1);
            _dockMotion.SetIndicator(item.Indicator, presence, initialized);
        }
        if (item.State == state && item.Button.IsEnabled == valid && item.PreviewsEnabled == _environment.Session.State.DockPreviews) return;
        item.State = state;
        if (item.Button.IsEnabled != valid) item.Button.IsEnabled = valid;
        item.PreviewsEnabled = _environment.Session.State.DockPreviews;
        var background = active ? _environment.Theme.Brush("NexusSelection") : _transparent;
        if (!ReferenceEquals(item.Button.Background, background)) item.Button.Background = background;
        var marker = _environment.Theme.Brush(active ? "NexusAccent" : "NexusMuted");
        if (!ReferenceEquals(item.Indicator.Background, marker)) item.Indicator.Background = marker;
        // Indicator brush already reflects the active state above.
        if (_dockMotion is null) SetIndicatorLayout(item.Indicator, active ? 22 : minimized ? 5 : 10, minimized ? .45 : 1);
        string action = minimized ? "Restore " : active ? "Minimize " : "Switch to ";
        ToolTipService.SetToolTip(item.Button, _environment.Session.State.DockPreviews ? null : action + item.Window.Title);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item.Button, action + item.Window.Title);
    }
    private static void SetIndicatorLayout(
        Border indicator, double width, double opacity, Action? onPropertyWrite = null)
    {
        // The optional observer is used only by the native UI probe. It counts
        // actual assignments (not WinUI DP change callbacks, which may be
        // coalesced for an unattached element). Regular UI calls pass null.
        if (indicator.Width != width)
        {
            indicator.Width = width;
            onPropertyWrite?.Invoke();
        }
        // WinUI stores Opacity using single-precision backing values on
        // some Windows App SDK builds. Reading 0.45 back can produce
        // 0.44999998807907104. Compare at the effective precision to
        // avoid redundant XAML layout writes on every steady-state poll.
        if (!SameNativeOpacity(indicator.Opacity, opacity))
        {
            indicator.Opacity = opacity;
            onPropertyWrite?.Invoke();
        }
    }

    private static bool SameNativeOpacity(double actual, double requested)
        => (float)actual == (float)requested;

    private void PrepareMotion(DockItem item)
    {
        if (_dockMotion is null) return;
        SetIndicatorLayout(item.Indicator, 22, 1);
        _dockMotion.SetIndicator(item.Indicator, item.Presence ?? DockPresence.Running, false);
        if (item.PendingCue == DockMotionCue.Arrive && item.Icon.IsLoaded)
        {
            _dockMotion.Play(item.Icon, DockMotionCue.Arrive);
            item.PendingCue = DockMotionCue.None;
        }
    }
    internal void CancelPreviewRequest()
    {
        _hoverTimer.Stop(); _hoverItem = null; _hoverPin = null;
    }
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
            Size(button, compact ? MidnightGlassMetrics.DockCompactButtonWidth : MidnightGlassMetrics.DockButtonWidth,
                compact ? MidnightGlassMetrics.DockCompactButtonHeight : MidnightGlassMetrics.DockButtonHeight);
            if (button.Content is Image icon) Size(icon, compact ? 30 : 46, compact ? 30 : 46);
        }
        foreach (var icon in _pinImages.Values.Concat(_items.Values.Select(i => i.Icon)))
            Size(icon, compact ? 30 : 46, compact ? 30 : 46);
        foreach (var item in _items.Values)
        {
            var state = HasPinnedArtwork(item.Window) ? Visibility.Collapsed : Visibility.Visible;
            if (item.Button.Visibility != state) item.Button.Visibility = state;
        }
        var separator = _windows.Children.OfType<Button>().Any(button => button.Visibility == Visibility.Visible)
            ? Visibility.Visible : Visibility.Collapsed;
        if (_separator.Visibility != separator) _separator.Visibility = separator;
        double width = PreferredWidthDip;
        if (_reportedWidth != width) { _reportedWidth = width; PreferredWidthChanged?.Invoke(); }
        _appliedCompactDock = compact;
    }
    internal void VerifyRetainedIndicatorLayout()
    {
        var indicator = new Border { Width = 22, Opacity = 1 };
        int writes = 0;
        Action observeWrite = () => writes++;

        // Count real assignments instead of relying on detached-element
        // dependency-property callbacks. Validate values at the precision
        // WinUI actually retains, not exact double equality.
        for (int i = 0; i < 100; i++)
            SetIndicatorLayout(indicator, 22, 1, observeWrite);
        if (writes != 0 || indicator.Width != 22 || !SameNativeOpacity(indicator.Opacity, 1))
            throw new InvalidOperationException(
                $"Unchanged dock indicator rewrote layout (writes={writes}, width={indicator.Width}, opacity={indicator.Opacity}).");

        SetIndicatorLayout(indicator, 10, .45, observeWrite);
        if (indicator.Width != 10 || !SameNativeOpacity(indicator.Opacity, .45) || writes != 2)
            throw new InvalidOperationException(
                $"Dock indicator transition was not applied (writes={writes}, width={indicator.Width}, opacity={indicator.Opacity}).");

        // This is a real production regression case: the value is returned
        // as a rounded float on AppVeyor, so 100 subsequent identical
        // requests must NOT keep rewriting the same opacity.
        for (int i = 0; i < 100; i++)
            SetIndicatorLayout(indicator, 10, .45, observeWrite);
        if (writes != 2 || indicator.Width != 10 || !SameNativeOpacity(indicator.Opacity, .45))
            throw new InvalidOperationException(
                $"Stable fractional opacity caused repeat layout writes (writes={writes}, width={indicator.Width}, opacity={indicator.Opacity}).");

        // Distinct fractional opacity must still update on a real change.
        SetIndicatorLayout(indicator, 10, .65, observeWrite);
        if (writes != 3 || !SameNativeOpacity(indicator.Opacity, .65))
            throw new InvalidOperationException(
                $"Real opacity-only transition was suppressed (writes={writes}, opacity={indicator.Opacity}).");

        SetIndicatorLayout(indicator, 22, 1, observeWrite);
        if (indicator.Width != 22 || !SameNativeOpacity(indicator.Opacity, 1) || writes != 5)
            throw new InvalidOperationException(
                $"Dock indicator geometry was not restored (writes={writes}, width={indicator.Width}, opacity={indicator.Opacity}).");
    }

    internal void VerifyRetainedAppearance()
    {
        ApplyAppearance();
        int writes = 0;
        long clock = _time.RegisterPropertyChangedCallback(TextBlock.ForegroundProperty, (_, _) => writes++);
        long launcher = _start.RegisterPropertyChangedCallback(Control.ForegroundProperty, (_, _) => writes++);
        try
        {
            for (int i = 0; i < 100; i++) ApplyAppearance();
            if (writes != 0)
                throw new InvalidOperationException("Stable dock appearance rewrote clock or launcher brushes.");
        }
        finally
        {
            _time.UnregisterPropertyChangedCallback(TextBlock.ForegroundProperty, clock);
            _start.UnregisterPropertyChangedCallback(Control.ForegroundProperty, launcher);
        }
    }

    internal void VerifyRetainedDensity()
    {
        bool original = _environment.Session.State.CompactDock;
        var button = _iconButtons[0]; var icon = (Image)button.Content;
        try
        {
            _environment.Session.State.CompactDock = false; Refresh([]);
            if (button.Width != 64 || button.Height != 68 || icon.Width != 46 || icon.Height != 46)
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
                if (button.Width != 64 || button.Height != 68 || icon.Width != 46 || icon.Height != 46)
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
        var corners = new CornerRadius(_environment.Session.State.FloatingTaskbar && !_environment.Theme.HighContrast ? MidnightGlassMetrics.DockRadius : 0);
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
        // Avoid a blank decorative plate when all utility controls are hidden
        // by narrow desktop widths. The running-app zone remains scrollable.
        SetVisible(_utilityIsland, _overview.Visibility == Visibility.Visible
            || _clockButton.Visibility == Visibility.Visible || _desktopButton.Visibility == Visibility.Visible);
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
        var theme = _environment.Theme;
        if (RequestedTheme != theme.ElementTheme) RequestedTheme = theme.ElementTheme;
        // In isolation (native resource probe) render a readable solid dock.
        // In the real dock, WindowMaterial owns both material and fallback.
        if (!MaterialOwnedByWindow) GlassPaintRetention.Background(_frame, theme.Material("Dock", false));
        bool motionEnabled = !theme.HighContrast && theme.Animations && _environment.Session.State.SurfaceAnimations && !_environment.Session.State.ReducedEffects;
        _motion?.SetEnabled(motionEnabled); _dockMotion?.SetEnabled(motionEnabled);
        ApplyFrameCorners();
        if (!MaterialOwnedByWindow) GlassPaintRetention.Edge(_frame, theme.Edge);
        if (!_frame.BorderThickness.Equals(new Thickness(theme.HighContrast ? 1 : 0)))
            _frame.BorderThickness = new Thickness(theme.HighContrast ? 1 : 0);
        ApplyVisualIslands();
        var border = theme.Brush("NexusBorder"); var text = theme.Brush("NexusText"); var muted = theme.Brush("NexusMuted");
        if (!ReferenceEquals(_separator.Background, border)) _separator.Background = border;
        if (!ReferenceEquals(_time.Foreground, text)) _time.Foreground = text;
        if (!ReferenceEquals(_date.Foreground, muted)) _date.Foreground = muted;
        var selection = theme.Brush("NexusSelection");
        if (!ReferenceEquals(_clockButton.Background, selection)) _clockButton.Background = selection;
        foreach (var button in _iconButtons)
        {
            if (!ReferenceEquals(button.Background, _transparent)) button.Background = _transparent;
            if (!ReferenceEquals(button.Foreground, text)) button.Foreground = text;
        }
        foreach (var item in _items.Values) item.State = null;
        RefreshWindowStates();
        ApplyDensity(); RefreshClock();
    }
    private void ApplyVisualIslands()
    {
        var theme = _environment.Theme;
        bool emphasize = MidnightGlassMetrics.ReferenceEnabled(theme.Palette, theme.HighContrast)
            && _environment.Session.State.FloatingTaskbar
            && !theme.Simple;
        // All materials are cached in ShellTheme. Applying the same look
        // never restarts an animation, re-enumerates HWNDs or edits Frame.
        var edge = emphasize ? theme.Edge : _transparent;
        PaintIsland(_launchIsland, emphasize ? theme.Surface("Hero") : _transparent, edge, emphasize, 14);
        PaintIsland(_appsIsland, emphasize ? theme.Surface("Sidebar") : _transparent, edge, emphasize, 15);
        PaintIsland(_utilityIsland, emphasize ? theme.Surface("Sidebar") : _transparent, edge, emphasize, 14);
        var glintVisibility = emphasize ? Visibility.Visible : Visibility.Collapsed;
        if (_topGlint.Visibility != glintVisibility) _topGlint.Visibility = glintVisibility;
        if (emphasize && !ReferenceEquals(_topGlint.Background, edge))
            _topGlint.Background = edge;
        _islandsEmphasized = emphasize;
    }

    private static void PaintIsland(Border island, Brush background, Brush edge, bool emphasized, double radius)
    {
        if (!ReferenceEquals(island.Background, background)) island.Background = background;
        if (!ReferenceEquals(island.BorderBrush, edge)) island.BorderBrush = edge;
        // The outer dock already owns the sole glass rim. Inset islands use
        // tonal separation, not a second hard border around every icon.
        var thickness = new Thickness(0);
        if (!island.BorderThickness.Equals(thickness)) island.BorderThickness = thickness;
        var corners = new CornerRadius(radius);
        if (!island.CornerRadius.Equals(corners)) island.CornerRadius = corners;
    }

    // This runs in the real native UI preflight against production WinUI
    // elements, checking hierarchy, stable paints and accessibility fallback.
    internal void VerifyVisualIslands()
    {
        ApplyVisualIslands();
        if (_appsIsland.Child is not ScrollViewer overflow
            || overflow.HorizontalScrollBarVisibility != ScrollBarVisibility.Hidden
            || overflow.HorizontalScrollMode != ScrollMode.Enabled)
            throw new InvalidOperationException("Dock overflow must stay scrollable without a visible scrollbar.");
        if (_galleryInvalid())
            throw new InvalidOperationException("The floating dock lost its three functional glass zones.");
        var originalFloating = _environment.Session.State.FloatingTaskbar;
        try
        {
            _environment.Session.State.FloatingTaskbar = true;
            ApplyVisualIslands();
            bool expected = MidnightGlassMetrics.ReferenceEnabled(
                _environment.Theme.Palette, _environment.Theme.HighContrast)
                && !_environment.Theme.Simple;
            if (_islandsEmphasized != expected
                || _topGlint.Visibility != (expected ? Visibility.Visible : Visibility.Collapsed))
                throw new InvalidOperationException("Floating Midnight materials did not honor accessibility mode.");
            var launcher = _launchIsland.Background;
            var apps = _appsIsland.Background;
            var utility = _utilityIsland.Background;
            var edge = _appsIsland.BorderBrush;
            for (int n = 0; n < 100; n++) ApplyVisualIslands();
            if (!ReferenceEquals(launcher, _launchIsland.Background)
                || !ReferenceEquals(apps, _appsIsland.Background)
                || !ReferenceEquals(utility, _utilityIsland.Background)
                || !ReferenceEquals(edge, _appsIsland.BorderBrush))
                throw new InvalidOperationException("Stable dock composition recreated inset material paints.");
            _environment.Session.State.FloatingTaskbar = false;
            ApplyVisualIslands();
            if (_islandsEmphasized || _topGlint.Visibility != Visibility.Collapsed
                || _launchIsland.BorderThickness.Left != 0)
                throw new InvalidOperationException("Attached dock retained floating-only visual decoration.");
        }
        finally
        {
            _environment.Session.State.FloatingTaskbar = originalFloating;
            ApplyVisualIslands();
        }
    }

    private bool _galleryInvalid()
        => _body.Children.Count != 4
            || !ReferenceEquals(_body.Children[0], _launchIsland)
            || !ReferenceEquals(_body.Children[1], _appsIsland)
            || !ReferenceEquals(_body.Children[2], _utilityIsland)
            || _launchIsland.Child is not StackPanel launcher
            || launcher.Children.Count != 1 || !ReferenceEquals(launcher.Children[0], _start)
            || _appsIsland.Child is not ScrollViewer
            || _utilityIsland.Child is not StackPanel
            || _topGlint.IsHitTestVisible || _topGlint.Height != 1;

    internal void Release()
    {
        if (_released) return; _released = true; CancelPreviewRequest(); _hoverTimer.Tick -= _hoverTick;
        foreach (var menu in _openContextMenus.ToArray()) menu.Hide();
        _openContextMenus.Clear(); _motion?.Dispose(); _dockMotion?.Dispose(); _pinImages.Clear(); _pinMarkers.Clear(); _iconButtons.Clear(); _items.Clear();
    }
}
