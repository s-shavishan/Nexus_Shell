using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Nexus.Shell.UI.Taskbar;

namespace Nexus.Shell.Desktop;

internal sealed class TaskbarWindow : Window, IDisposable
{
    internal IntPtr Handle { get; }
    internal AppWindow NativeWindow { get; }
    internal TaskbarView View { get; }
    internal ShellRect BarBounds => _registration.Bounds;
    private readonly DesktopEnvironment _environment;
    private readonly ITaskbarLayer _registration;
    private readonly WindowChrome _chrome;
    private readonly UI.WindowMaterial _material = new();
    private readonly UI.WindowTransition _transition;
    private readonly DispatcherTimer _visibilityTimer = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private readonly DockVisibility _visibility = new();
    private bool _queued, _disposed, _shown = true, _arranging, _visibilityReported;
    private IntPtr _lastForeground;
    internal TaskbarWindow(DesktopEnvironment environment)
    {
        _environment = environment; View = new(environment);
        _transition = new(View.Frame, () => environment.Theme.Animations && !environment.Session.State.ReducedEffects, dock: true);
        var canvas = new Grid(); canvas.Children.Add(View); Content = canvas;
        Handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(Handle));
        NativeWindow.Title = "Nexus Taskbar"; WindowSwitcherPolicy.Request(() => NativeWindow.IsShownInSwitchers = false, error => Log.Write("Switcher API unavailable; using native tool-window styling", error));
        if (NativeWindow.Presenter is OverlappedPresenter presenter)
        { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMinimizable = presenter.IsMaximizable = false; }
        ShellLayerInterop.ToolWindow(Handle, noActivate: true);
        _chrome = new(Handle, customClip: true);
        _material.Apply(this, View.Frame, environment, "Dock");
        View.Loaded += (_, _) => { if (!_disposed) _material.Apply(this, View.Frame, environment, "Dock"); };
        _registration = environment.IsManagedDesktop
            ? new ExclusiveTaskbarRegistration(Handle, QueuePosition)
            : new TaskbarRegistration(Handle, QueuePosition, hide => { _arranging = hide; RefreshVisibility(); });
        View.PreferredWidthChanged += QueuePosition;
        _visibilityTimer.Tick += (_, _) =>
        {
            if (_disposed) return;
            var foreground = NativeMethods.ForegroundTaskWindow();
            if (foreground != _lastForeground) { _lastForeground = foreground; View.RefreshWindowStates(); }
            RefreshVisibility();
        };
        _visibilityTimer.Start();
        Closed += (_, _) => { Dispose(); if (!environment.IsStopping) environment.Shutdown(); };
        Position();
    }
    internal void Position()
    {
        if (_disposed) return;
        _environment.HideDockPreview();
        _registration.Position(_environment.Session.State.CompactDock, _environment.Session.State.FloatingTaskbar, View.PreferredWidthDip);
        var reservation = _registration.Reservation; var bar = BarBounds;
        double scale = ShellLayerInterop.Scale(Handle);
        View.Width = bar.Width / scale; View.Height = bar.Height / scale;
        View.HorizontalAlignment = HorizontalAlignment.Left; View.VerticalAlignment = VerticalAlignment.Top;
        View.Margin = new Thickness((bar.X - reservation.X) / scale, (bar.Y - reservation.Y) / scale, 0, 0);
        View.SetAvailableWidth(bar.Width / scale);
        _chrome.SetClip(new ShellRect(bar.X - reservation.X, bar.Y - reservation.Y, bar.Width, bar.Height),
            _environment.Session.State.FloatingTaskbar && !_environment.Theme.HighContrast ? 22 : 0);
        var monitor = ShellLayerInterop.Monitor(Handle).Monitor.Bounds;
        _environment.Desktop.Surface.SetWorkArea((monitor.Bottom - _registration.WorkArea.Bottom) / scale);
        _environment.PositionDesktopPanels();
        RefreshVisibility();
    }
    private void QueuePosition()
    { if (_queued || _disposed) return; _queued = true; DispatcherQueue.TryEnqueue(() => { _queued = false; Position(); }); }
    internal void ShowBar() { Position(); RefreshVisibility(); }
    internal void RefreshVisibility()
    {
        if (_disposed) return;
        try
        {
            var monitor = ShellLayerInterop.Monitor(Handle).Monitor.Bounds;
            var foreground = NativeMethods.ForegroundTaskWindow();
            bool application = foreground != IntPtr.Zero && foreground != Handle && foreground != _environment.Desktop.Handle
                && !ShellLayerInterop.IsExplorerDesktopWindow(foreground);
            bool sameMonitor = application && ShellLayerInterop.Monitor(foreground).Monitor.Bounds == monitor;
            bool maximized = sameMonitor && NativeMethods.IsZoomed(foreground);
            bool fullscreen = sameMonitor && !maximized && NativeMethods.GetWindowRect(foreground, out var rect)
                && rect.Left <= monitor.X && rect.Top <= monitor.Y && rect.Right >= monitor.Right && rect.Bottom >= monitor.Bottom;
            // Preview does not own Explorer's work area. Avoid covering a
            // maximized application's native title controls in that mode.
            bool attached = sameMonitor && !NativeMethods.IsMinimized(foreground) && NativeMethods.GetWindowRect(foreground, out var appBounds)
                && MenuBarLayout.Attached(appBounds.Bounds, ShellLayerInterop.Monitor(Handle).Work.Bounds, maximized, ShellLayerInterop.Scale(Handle));
            if (_applicationMonitor != monitor) { _applicationMonitor = monitor; _applicationState = default; }
            bool overlay = foreground != _environment.Desktop.Handle && (NativeMethods.IsOwnToolWindow(foreground) || _environment.IsControlCenterWindow(foreground));
            if (overlay) (maximized, fullscreen, attached) = _applicationState;
            else _applicationState = (maximized, fullscreen, attached);
            _environment.SetDesktopFullscreen(fullscreen || (!_environment.IsManagedDesktop && (maximized || attached)));
            _environment.SetMenuBarAttached(attached);
            _environment.UpdateDockPreviewPointer(fullscreen);
            var revealArea = _environment.IsManagedDesktop ? monitor : _registration.WorkArea;
            bool pointer = NativeMethods.GetCursorPos(out var point) && DockVisibility.InRevealArea(revealArea, BarBounds, point.X, point.Y, _shown, ShellLayerInterop.Scale(Handle));
            bool show = !_arranging && _visibility.Update(_environment.Session.State.FloatingTaskbar, maximized || attached, fullscreen,
                _environment.DockInteraction || View.ContextMenuOpen, pointer, Environment.TickCount64);
            // Hide/show only on transitions. Native Show Desktop can hide this
            // tool window independently, so recover visibility when needed.
            if (show && (!_shown || !NativeMethods.Visible(Handle)))
            { bool reveal = !_shown; _transition.Cancel(); NativeWindow.Show(false); _shown = true; if (reveal) _transition.Restore(); }
            else if (!show && (fullscreen || _arranging) && (_shown || NativeMethods.Visible(Handle)))
            { _shown = false; _transition.Cancel(); NativeWindow.Hide(); }
            else if (!show && _shown) { _shown = false; HideAnimated(); }
            _visibilityReported = false;
        }
        catch (Exception ex)
        { if (!_visibilityReported) { _visibilityReported = true; _environment.Report("Could not update dock visibility", ex, false); } }
    }
    private async void HideAnimated() => await _transition.MinimizeAsync(() => NativeWindow.Hide());
    private (bool Maximized, bool Fullscreen, bool Attached) _applicationState;
    private ShellRect? _applicationMonitor;
    internal void ApplyAppearance() { if (_disposed) return; View.ApplyAppearance(); _material.Apply(this, View.Frame, _environment, "Dock"); Position(); }
    public void Dispose() { if (_disposed) return; _disposed = true; _visibilityTimer.Stop(); _transition.Dispose(); View.PreferredWidthChanged -= QueuePosition; _registration.Dispose(); _chrome.Dispose(); View.Release(); }
}
