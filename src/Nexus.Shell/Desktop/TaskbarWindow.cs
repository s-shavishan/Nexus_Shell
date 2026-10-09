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
    private readonly DispatcherTimer _stacking = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool _queued, _disposed;
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
            ? new ExclusiveTaskbarRegistration(Handle, environment.Desktop.Handle, QueuePosition)
            : new TaskbarRegistration(Handle, QueuePosition, hide => { if (hide) NativeWindow.Hide(); else NativeWindow.Show(false); });
        View.PreferredWidthChanged += QueuePosition;
        _stacking.Tick += (_, _) => { if (!_disposed) _registration.RefreshStacking(); };
        if (environment.IsManagedDesktop) _stacking.Start();
        Closed += (_, _) => { Dispose(); if (!environment.IsStopping) environment.Shutdown(); };
        Position();
    }
    internal void Position()
    {
        if (_disposed) return;
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
        _environment.Desktop.Surface.SetWorkArea((monitor.Bottom - reservation.Y) / scale);
        _environment.PositionQuickSettings();
    }
    private void QueuePosition()
    { if (_queued || _disposed) return; _queued = true; DispatcherQueue.TryEnqueue(() => { _queued = false; Position(); }); }
    internal void ShowBar() { NativeWindow.Show(false); Position(); }
    internal void ApplyAppearance() { if (_disposed) return; View.ApplyAppearance(); Position(); }
    public void Dispose() { if (_disposed) return; _disposed = true; _stacking.Stop(); View.PreferredWidthChanged -= QueuePosition; _registration.Dispose(); _chrome.Dispose(); View.Release(); }
}
