using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
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
    private readonly DispatcherTimer _stacking = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool _queued, _disposed;
    internal TaskbarWindow(DesktopEnvironment environment)
    {
        _environment = environment; View = new(environment); Content = View;
        Handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(Handle));
        NativeWindow.Title = "Nexus Taskbar"; NativeWindow.IsShownInSwitchers = false;
        if (NativeWindow.Presenter is OverlappedPresenter presenter)
        { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMinimizable = presenter.IsMaximizable = false; }
        ShellLayerInterop.ToolWindow(Handle, noActivate: true);
        _registration = environment.IsManagedDesktop
            ? new ExclusiveTaskbarRegistration(Handle, environment.Desktop.Handle, QueuePosition)
            : new TaskbarRegistration(Handle, QueuePosition, hide => { if (hide) NativeWindow.Hide(); else NativeWindow.Show(false); });
        _stacking.Tick += (_, _) => { if (!_disposed) _registration.RefreshStacking(); };
        if (environment.IsManagedDesktop) _stacking.Start();
        Closed += (_, _) => { Dispose(); if (!environment.IsStopping) environment.Shutdown(); };
        Position();
    }
    internal void Position()
    {
        if (_disposed) return;
        _registration.Position(_environment.Session.State.CompactDock);
        var monitor = ShellLayerInterop.Monitor(Handle).Monitor.Bounds;
        _environment.Desktop.Surface.SetWorkArea((monitor.Bottom - BarBounds.Y) / ShellLayerInterop.Scale(Handle));
        _environment.PositionQuickSettings();
    }
    private void QueuePosition()
    { if (_queued || _disposed) return; _queued = true; DispatcherQueue.TryEnqueue(() => { _queued = false; Position(); }); }
    internal void ShowBar() { NativeWindow.Show(false); Position(); }
    public void Dispose() { if (_disposed) return; _disposed = true; _stacking.Stop(); _registration.Dispose(); View.Release(); }
}
