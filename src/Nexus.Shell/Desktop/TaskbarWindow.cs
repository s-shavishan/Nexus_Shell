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
    private readonly DispatcherTimer _visibilityTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly DockVisibility _visibility = new();
    private bool _queued, _disposed, _shown = true, _arranging, _visibilityReported;
    private IntPtr _lastForeground;
    internal TaskbarWindow(DesktopEnvironment environment)
    {
        _environment = environment; View = new(environment);
        var canvas = new Grid(); canvas.Children.Add(View); Content = canvas;
        Handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(Handle));
        NativeWindow.Title = "Nexus Taskbar"; NativeWindow.IsShownInSwitchers = false;
        if (NativeWindow.Presenter is OverlappedPresenter presenter)
        { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMinimizable = presenter.IsMaximizable = false; }
        ShellLayerInterop.ToolWindow(Handle, noActivate: true);
        _chrome = new(Handle, customClip: true);
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
        _environment.PositionQuickSettings();
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
            _environment.UpdateDockPreviewPointer(fullscreen);
            var revealArea = _environment.IsManagedDesktop ? monitor : _registration.WorkArea;
            bool pointer = NativeMethods.GetCursorPos(out var point) && DockVisibility.InRevealArea(revealArea, BarBounds, point.X, point.Y, _shown, ShellLayerInterop.Scale(Handle));
            bool show = !_arranging && _visibility.Update(_environment.Session.State.FloatingTaskbar, maximized, fullscreen,
                _environment.DockInteraction || View.ContextMenuOpen, pointer, Environment.TickCount64);
            // Hide/show only on transitions. Native Show Desktop can hide this
            // tool window independently, so recover visibility when needed.
            if (show && (!_shown || !NativeMethods.Visible(Handle)))
            { bool reveal = !_shown; NativeWindow.Show(false); _shown = true; if (reveal) View.Reveal(); }
            else if (!show && _shown) { NativeWindow.Hide(); _shown = false; }
            _visibilityReported = false;
        }
        catch (Exception ex)
        { if (!_visibilityReported) { _visibilityReported = true; _environment.Report("Could not update dock visibility", ex, false); } }
    }
    internal void ApplyAppearance() { if (_disposed) return; View.ApplyAppearance(); Position(); }
    public void Dispose() { if (_disposed) return; _disposed = true; _visibilityTimer.Stop(); View.PreferredWidthChanged -= QueuePosition; _registration.Dispose(); _chrome.Dispose(); View.Release(); }
}
