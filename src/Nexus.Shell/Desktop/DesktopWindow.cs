using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Nexus.Shell.UI.Desktop;
using Windows.Graphics;

namespace Nexus.Shell.Desktop;

internal sealed class DesktopWindow : Window, IDisposable
{
    internal const string NativeWindowTitle = "White Dreams Nexus Desktop";
    internal IntPtr Handle { get; }
    internal AppWindow NativeWindow { get; }
    internal DesktopSurface Surface { get; }
    private readonly DesktopLayerHook _hook;
    private bool _positionQueued, _disposed;
    internal DesktopWindow(DesktopEnvironment environment)
    {
        Surface = new(environment); Content = Surface;
        Handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(Handle));
        NativeWindow.Title = NativeWindowTitle; NativeWindow.IsShownInSwitchers = false;
        if (NativeWindow.Presenter is OverlappedPresenter presenter)
        { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMinimizable = presenter.IsMaximizable = false; }
        ShellLayerInterop.ToolWindow(Handle);
        _hook = new(Handle, QueuePosition);
        Closed += (_, _) => { Dispose(); if (!environment.IsStopping) environment.Shutdown(); };
        Activated += (_, args) => { if (args.WindowActivationState != WindowActivationState.Deactivated) ShellLayerInterop.AnchorDesktop(Handle); };
        Position();
    }
    internal void ShowSurface() { NativeWindow.Show(false); ShellLayerInterop.AnchorDesktop(Handle); }
    private void QueuePosition()
    {
        if (_positionQueued || _disposed) return; _positionQueued = true;
        DispatcherQueue.TryEnqueue(() => { _positionQueued = false; if (!_disposed) Position(); });
    }
    private void Position()
    { var b = ShellLayerInterop.Monitor(Handle).Monitor.Bounds; NativeWindow.MoveAndResize(new RectInt32(b.X, b.Y, b.Width, b.Height)); ShellLayerInterop.AnchorDesktop(Handle); }
    public void Dispose() { if (_disposed) return; _disposed = true; _hook.Dispose(); }
}
