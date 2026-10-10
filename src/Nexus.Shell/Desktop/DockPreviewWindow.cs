using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Interop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Nexus.Shell.UI;
using Windows.Graphics;
using Windows.System;

namespace Nexus.Shell.Desktop;

// Independent, non-activating hover surface. Keyboard/context-menu invocation
// explicitly activates it. Only the open card can own a live DWM thumbnail.
internal sealed class DockPreviewWindow : Window
{
    private readonly DesktopEnvironment _environment;
    private readonly IntPtr _handle;
    private readonly AppWindow _native;
    private readonly WindowChrome _chrome;
    private readonly Border _frame;
    private readonly Grid _root = new() { Padding = new Thickness(14), RowSpacing = 8 };
    private readonly Grid _header = new() { ColumnSpacing = 10 };
    private readonly Grid _actions = new() { ColumnSpacing = 6 };
    private readonly Grid _fallback = new();
    private readonly TextBlock _title = new() { FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _detail = new() { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _status = new() { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Button _viewport = new() { Padding = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly Button _restore, _minimize, _maximize, _close;
    private readonly FontIcon _maximizeIcon;
    private MotionController? _motion;
    private WindowThumbnail? _thumbnail;
    private RunningWindow? _window;
    private ShellRect _anchor, _bounds;
    private long? _leaveSince;
    private (bool Valid, bool Minimized, bool Cloaked, bool Maximized, bool Fast)? _state;
    private bool _keyboardOpen, _closed, _maximized;
    internal bool IsOpen { get; private set; }

    internal DockPreviewWindow(DesktopEnvironment environment)
    {
        _environment = environment; _frame = new Border { Child = _root }; Content = _frame;
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _native = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_handle));
        _native.Title = "Nexus window preview"; WindowSwitcherPolicy.Request(() => _native.IsShownInSwitchers = false, error => Log.Write("Switcher API unavailable; using native tool-window styling", error));
        if (_native.Presenter is OverlappedPresenter presenter)
        { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMaximizable = presenter.IsMinimizable = false; presenter.IsAlwaysOnTop = true; }
        ShellLayerInterop.ToolWindow(_handle, noActivate: true); _chrome = new(_handle);
        _root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        _root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _header.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); _header.ColumnDefinitions.Add(new());
        var names = new StackPanel { Spacing = 2 }; names.Children.Add(_title); names.Children.Add(_detail);
        Grid.SetColumn(names, 1); _header.Children.Add(names); _root.Children.Add(_header);
        _viewport.Style = (Style)Application.Current.Resources["QuietButton"]; _viewport.Content = _fallback;
        _viewport.Click += (_, _) => Perform(_environment.RestoreDockWindow);
        Grid.SetRow(_viewport, 1); _root.Children.Add(_viewport);
        _restore = ActionButton("Restore / switch to window", "\uE8A7", _environment.RestoreDockWindow);
        _minimize = ActionButton("Minimize window", "\uE921", _environment.MinimizeDockWindow);
        _maximize = ActionButton("Maximize window", "\uE922", _environment.MaximizeDockWindow);
        _maximizeIcon = (FontIcon)_maximize.Content;
        _close = ActionButton("Close window", "\uE8BB", _environment.CloseDockWindow);
        var buttons = new[] { _restore, _minimize, _maximize, _close };
        for (int i = 0; i < buttons.Length; i++) { _actions.ColumnDefinitions.Add(new()); Grid.SetColumn(buttons[i], i); _actions.Children.Add(buttons[i]); }
        Grid.SetRow(_actions, 2); _root.Children.Add(_actions); Grid.SetRow(_status, 3); _root.Children.Add(_status);
        _root.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
        var escape = new KeyboardAccelerator { Key = VirtualKey.Escape };
        escape.Invoked += (_, e) => { Hide(); e.Handled = true; }; _root.KeyboardAccelerators.Add(escape);
        _viewport.SizeChanged += (_, _) => UpdateThumbnail();
        _root.Loaded += (_, _) =>
        {
            if (_closed) return;
            try { _motion ??= new(_root); foreach (var button in buttons) _motion.AttachHover(button); }
            catch (Exception ex) { Log.Write("Preview motion unavailable", ex); }
            if (IsOpen) { UpdateThumbnail(); Enter(); if (_keyboardOpen) _restore.Focus(FocusState.Programmatic); }
        };
        Activated += (_, e) => { if (_keyboardOpen && IsOpen && e.WindowActivationState == WindowActivationState.Deactivated) Hide(); };
        Closed += (_, _) =>
        { _closed = true; IsOpen = false; ReleaseThumbnail(); _motion?.Dispose(); _chrome.Dispose(); environment.DockPreviewClosed(this); };
        ApplyAppearance();
    }
    private Button ActionButton(string name, string glyph, Action<RunningWindow> action)
    {
        var button = new Button { Style = (Style)Application.Current.Resources["QuietButton"], Height = 38, Padding = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = new FontIcon { FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, Glyph = glyph } };
        AutomationProperties.SetName(button, name); ToolTipService.SetToolTip(button, name);
        button.Click += (_, _) => Perform(action); return button;
    }
    private void Perform(Action<RunningWindow> action)
    { var window = _window; Hide(); if (window is not null && !_environment.IsStopping) action(window); }
    internal void Show(RunningWindow window, ShellRect anchor, bool keyboard)
    {
        if (_closed || !NativeMethods.OwnsWindow(window)) return;
        ReleaseThumbnail(); _window = window; _anchor = anchor; _keyboardOpen = keyboard; _leaveSince = null; _state = null;
        _title.Text = window.Title; _detail.Text = window.ProcessId == Environment.ProcessId ? "Nexus" : window.ProcessName;
        AutomationProperties.SetName(_viewport, "Switch to " + window.Title);
        if (_header.Children.Count > 1) _header.Children.RemoveAt(1);
        string icon = window.ProcessId == Environment.ProcessId ? window.Title == "Sections" ? "Apps" : "Files" : NexusIcons.ForProcess(window.ProcessName);
        _header.Children.Add(NexusIcons.Image(icon, 28)); _fallback.Children.Clear(); _fallback.Children.Add(NexusIcons.Image(icon, 48));
        _bounds = DesktopLayout.PreviewBounds(ShellLayerInterop.Monitor(_environment.Taskbar.Handle).Monitor.Bounds,
            _environment.Taskbar.BarBounds, anchor, ShellLayerInterop.Scale(_environment.Taskbar.Handle));
        _native.MoveAndResize(new RectInt32(_bounds.X, _bounds.Y, _bounds.Width, _bounds.Height));
        IsOpen = true; ApplyAppearance(); _native.Show(false); RefreshState();
        if (!_environment.Session.State.ReducedEffects && !_environment.Theme.HighContrast)
        {
            try { _thumbnail = WindowThumbnail.TryCreate(_handle, window); UpdateThumbnail(); }
            catch (Exception ex) { ReleaseThumbnail(); Log.Write("Live window preview unavailable", ex); }
        }
        if (_root.IsLoaded) Enter();
        if (keyboard) { Activate(); if (_root.IsLoaded) _restore.Focus(FocusState.Programmatic); }
    }
    private void Enter()
    {
        _motion?.SetEnabled(_environment.Theme.Animations && !_environment.Theme.HighContrast && !_environment.Session.State.ReducedEffects);
        // Native thumbnail coordinates stay fixed; animate the surrounding UI.
        _motion?.Enter(_header); _motion?.Enter(_actions); if (_thumbnail is null) _motion?.Enter(_fallback);
    }
    private void UpdateThumbnail()
    {
        if (!IsOpen || _thumbnail is null || !_viewport.IsLoaded || _viewport.ActualWidth < 1 || _viewport.ActualHeight < 1) return;
        try
        {
            var point = _viewport.TransformToVisual(_frame).TransformPoint(new Windows.Foundation.Point(0, 0));
            double scale = _frame.XamlRoot?.RasterizationScale ?? ShellLayerInterop.Scale(_handle);
            var rectangle = new ShellRect((int)Math.Round(point.X * scale), (int)Math.Round(point.Y * scale),
                (int)Math.Round(_viewport.ActualWidth * scale), (int)Math.Round(_viewport.ActualHeight * scale));
            if (!_thumbnail.Update(rectangle)) ReleaseThumbnail();
            _fallback.Visibility = _thumbnail is null ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex) { ReleaseThumbnail(); Log.Write("Window preview layout unavailable", ex); }
    }
    private void RefreshState()
    {
        if (_window is null) return;
        bool valid = NativeMethods.OwnsWindow(_window);
        bool minimized = valid && (NativeMethods.IsMinimized(_window.Handle) || !NativeMethods.Visible(_window.Handle));
        bool maximized = valid && NativeMethods.IsZoomed(_window.Handle);
        bool cloaked = valid && NativeMethods.IsCloaked(_window.Handle);
        var state = (valid, minimized, cloaked, maximized, _environment.Session.State.ReducedEffects);
        if (_state == state) return; _state = state;
        _restore.IsEnabled = _viewport.IsEnabled = _close.IsEnabled = valid;
        _minimize.IsEnabled = valid && !minimized; _maximize.IsEnabled = valid;
        if (_maximized != maximized)
        { _maximized = maximized; _maximizeIcon.Glyph = maximized ? "\uE923" : "\uE922"; string name = maximized ? "Restore window size" : "Maximize window"; AutomationProperties.SetName(_maximize, name); ToolTipService.SetToolTip(_maximize, name); }
        if (minimized || !valid || cloaked) ReleaseThumbnail();
        _status.Text = minimized ? "Minimized · restore to see the window" : _environment.Session.State.ReducedEffects ? "Fast mode · window card" : "Click the preview to switch";
    }
    internal void UpdatePointer(bool fullscreen)
    {
        if (!IsOpen || _window is null) return;
        if (fullscreen || !_environment.Session.State.DockPreviews || !NativeMethods.OwnsWindow(_window)) { Hide(); return; }
        RefreshState();
        if (_keyboardOpen && NativeMethods.GetForegroundWindow() == _handle) return;
        bool inside = NativeMethods.GetCursorPos(out var point) && DockPreviewPolicy.InInteractionArea(_anchor, _bounds, point.X, point.Y);
        if (inside) _leaveSince = null;
        else { _leaveSince ??= Environment.TickCount64; if (Environment.TickCount64 - _leaveSince.Value >= DockPreviewPolicy.LeaveDelayMilliseconds) Hide(); }
    }
    internal void RefreshWindows(IReadOnlyList<RunningWindow> windows)
    {
        if (!IsOpen || _window is null) return;
        var current = windows.FirstOrDefault(w => DockWindowIdentity.Of(w) == DockWindowIdentity.Of(_window));
        if (current is null) { Hide(); return; }
        if (current == _window) return;
        _window = current; _title.Text = current.Title; AutomationProperties.SetName(_viewport, "Switch to " + current.Title);
    }
    private void ReleaseThumbnail()
    { _thumbnail?.Dispose(); _thumbnail = null; _fallback.Visibility = Visibility.Visible; }
    internal void Hide()
    {
        if (_closed || !IsOpen) return; IsOpen = _keyboardOpen = false; _leaveSince = null;
        ReleaseThumbnail(); _motion?.SetEnabled(false); _native.Hide();
    }
    internal void ApplyAppearance()
    {
        var theme = _environment.Theme; _root.RequestedTheme = theme.ElementTheme;
        _frame.Background = theme.Surface("Panel"); _frame.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : 18);
        _frame.BorderBrush = theme.Brush("NexusBorder"); _frame.BorderThickness = new Thickness(theme.HighContrast ? 1 : 0);
        _viewport.Background = theme.Brush("NexusInput"); _title.Foreground = theme.Brush("NexusText"); _detail.Foreground = _status.Foreground = theme.Brush("NexusMuted");
        foreach (var button in new[] { _restore, _minimize, _maximize, _close }) { button.Background = theme.Brush("NexusCard"); button.Foreground = theme.Brush("NexusText"); }
        _chrome.SetCorners(theme.HighContrast, 18);
        _motion?.SetEnabled(theme.Animations && !theme.HighContrast && !_environment.Session.State.ReducedEffects);
    }
}
