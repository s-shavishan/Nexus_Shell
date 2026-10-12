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
    private readonly ForegroundRepairCadence _foregroundRepairCadence = new();
    private readonly CoalescedUiDispatch _positionDispatch = new();
    private bool _disposed, _shown = true, _arranging, _visibilityReported;
    private IntPtr _lastForeground;
    private DockPlacementSnapshot? _lastPlacement;
    private DockFrameGeometry? _lastFrame;
    internal TaskbarWindow(DesktopEnvironment environment)
    {
        _environment = environment; View = new(environment);
        _transition = new(View.Frame, () => environment.Theme.Animations && environment.Session.State.SurfaceAnimations && !environment.Session.State.ReducedEffects, dock: true);
        var canvas = new Grid(); canvas.Children.Add(View); Content = canvas;
        Handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(Handle));
        NativeWindow.Title = "Nexus Taskbar"; WindowSwitcherPolicy.Request(() => NativeWindow.IsShownInSwitchers = false, error => Log.Write("Switcher API unavailable; using native tool-window styling", error));
        if (NativeWindow.Presenter is OverlappedPresenter presenter)
        { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMinimizable = presenter.IsMaximizable = false; }
        ShellLayerInterop.ToolWindow(Handle, noActivate: true);
        _chrome = new(Handle, customClip: true, role: "Dock");
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
            RefreshVisibility(foreground);
        };
        _visibilityTimer.Start();
        Closed += (_, _) => { Dispose(); if (!environment.IsStopping) environment.Shutdown(); };
        Position();
    }
    internal void Position()
    {
        if (_disposed) return;
        _registration.Position(_environment.Session.State.CompactDock, _environment.Session.State.FloatingTaskbar, View.PreferredWidthDip);
        var reservation = _registration.Reservation; var bar = BarBounds;
        var monitor = ShellLayerInterop.Monitor(Handle).Monitor.Bounds;
        double scale = ShellLayerInterop.Scale(Handle);
        var placement = new DockPlacementSnapshot(bar, reservation, _registration.WorkArea, monitor, _environment.Session.State.FloatingTaskbar, scale);
        bool relayout = DockPlacementPolicy.RequiresDesktopRelayout(_lastPlacement, placement);
        if (DockPlacementPolicy.RequiresPreviewDismissal(_lastPlacement, placement))
            _environment.HideDockPreview();
        _lastPlacement = placement;
        _chrome.Trace("dock-position", bar.Width, bar.Height);
        var frame = new DockFrameGeometry(bar, reservation, scale,
            _environment.Session.State.FloatingTaskbar, _environment.Theme.HighContrast);
        if (DockFrameLayoutPolicy.RequiresLayout(_lastFrame, frame))
        {
            using var measurement = UiTrace.Measure("dock-frame-layout", "Dock");
            View.Width = bar.Width / scale; View.Height = bar.Height / scale;
            View.HorizontalAlignment = HorizontalAlignment.Left; View.VerticalAlignment = VerticalAlignment.Top;
            View.Margin = new Thickness((bar.X - reservation.X) / scale, (bar.Y - reservation.Y) / scale, 0, 0);
            View.SetAvailableWidth(bar.Width / scale);
            _chrome.SetClip(DockFrameLayoutPolicy.Clip(frame), DockFrameLayoutPolicy.CornerRadius(frame));
            _lastFrame = frame;
        }
        if (relayout)
        {
            using var measurement = UiTrace.Measure("dock-desktop-relayout", "Dock");
            _environment.ReconcileMaximizedWindows(monitor, _registration.WorkArea, scale);
            _environment.Desktop.Surface.SetWorkArea((monitor.Bottom - _registration.WorkArea.Bottom) / scale);
            _environment.PositionDesktopPanels();
        }
        RefreshVisibility();
    }
    private void QueuePosition()
    {
        if (_disposed) return;
        _positionDispatch.Request(
            callback => DispatcherQueue.TryEnqueue(() => callback()),
            () => { if (!_disposed) Position(); });
    }
    internal void ShowBar() { Position(); RefreshVisibility(); }
    internal void RefreshVisibility(IntPtr? observedForeground = null)
    {
        if (_disposed) return;
        try
        {
            var monitorInfo = ShellLayerInterop.Monitor(Handle);
            var monitor = monitorInfo.Monitor.Bounds;
            double scale = ShellLayerInterop.Scale(Handle);
            var foreground = observedForeground ?? NativeMethods.ForegroundTaskWindow();
            _environment.ObserveForeground(foreground);
            bool application = foreground != IntPtr.Zero && foreground != Handle && foreground != _environment.Desktop.Handle
                && !ShellLayerInterop.IsExplorerDesktopWindow(foreground);
            bool sameMonitor = application && ShellLayerInterop.Monitor(foreground).Monitor.Bounds == monitor;
            bool maximized = sameMonitor && NativeMethods.IsZoomed(foreground);
            // The event-driven window reconciliation still runs immediately.
            // Only throttle this extra check from the 80 ms visibility poll.
            if (_foregroundRepairCadence.ShouldSample(foreground, maximized, Environment.TickCount64))
                _environment.ReconcileForeground(monitor, _registration.WorkArea, scale);
            // Query the foreground rectangle just once per visibility tick.
            // Both fullscreen classification and title-control avoidance use
            // exactly the same native geometry snapshot.
            ShellLayerInterop.Rect rect = default;
            bool hasBounds = sameMonitor && NativeMethods.GetWindowRect(foreground, out rect);
            // Preview must not cover maximized title controls or borderless
            // fullscreen applications. Both decisions use the same HWND bounds.
            var presentation = DockForegroundGeometryPolicy.Classify(
                hasBounds, rect.Bounds, monitor, monitorInfo.Work.Bounds,
                maximized, hasBounds && NativeMethods.IsMinimized(foreground),
                hasBounds ? NativeMethods.WindowStyle(foreground) : 0, scale);
            bool fullscreen = presentation.Fullscreen;
            bool attached = presentation.Attached;
            if (_applicationMonitor != monitor) { _applicationMonitor = monitor; _applicationState = default; }
            bool overlay = foreground != _environment.Desktop.Handle && (NativeMethods.IsOwnToolWindow(foreground) || _environment.IsControlCenterWindow(foreground));
            if (overlay) (maximized, fullscreen, attached) = _applicationState;
            else _applicationState = (maximized, fullscreen, attached);
            _environment.SetDesktopFullscreen(fullscreen || (!_environment.IsManagedDesktop && (maximized || attached)));
            _environment.SetMenuBarAttached(attached);
            _environment.UpdateDockPreviewPointer(fullscreen);
            bool floating = _environment.Session.State.FloatingTaskbar;
            bool interacting = _environment.DockInteraction || View.ContextMenuOpen;
            int pollingMs = DockRefreshCadence.Interval(floating, maximized || attached, interacting);
            var interval = TimeSpan.FromMilliseconds(pollingMs);
            if (_visibilityTimer.Interval != interval) _visibilityTimer.Interval = interval;
            var revealArea = _environment.IsManagedDesktop ? monitor : _registration.WorkArea;
            bool pointer = NativeMethods.GetCursorPos(out var point) && DockVisibility.InRevealArea(revealArea, BarBounds, point.X, point.Y, _shown, scale);
            bool show = !_arranging && _visibility.Update(floating, maximized || attached, fullscreen,
                interacting, pointer, Environment.TickCount64);
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
