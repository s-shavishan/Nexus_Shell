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
    private readonly WindowChrome _chrome;
    private readonly CoalescedUiDispatch _positionDispatch = new();
    private bool _disposed;
    internal DesktopWindow(DesktopEnvironment environment)
    {
        Surface = new(environment); Content = Surface;
        Handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(Handle));
        NativeWindow.Title = NativeWindowTitle; WindowSwitcherPolicy.Request(() => NativeWindow.IsShownInSwitchers = false, error => Log.Write("Switcher API unavailable; using native tool-window styling", error));
        if (NativeWindow.Presenter is OverlappedPresenter presenter)
        { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMinimizable = presenter.IsMaximizable = false; }
        ShellLayerInterop.ToolWindow(Handle);
        _chrome = new(Handle); _chrome.SetFullscreen(true);
        _hook = new(Handle, QueuePosition);
        Closed += (_, _) => { Dispose(); if (!environment.IsStopping) environment.Shutdown(); };
        Activated += (_, args) => { if (args.WindowActivationState != WindowActivationState.Deactivated) ShellLayerInterop.AnchorDesktop(Handle); };
        Position();
    }
    internal void ShowSurface() { if (_disposed) return; NativeWindow.Show(false); ShellLayerInterop.AnchorDesktop(Handle); }
    private void QueuePosition()
    {
        if (_disposed) return;
        _positionDispatch.Request(
            callback => DispatcherQueue.TryEnqueue(() => callback()),
            () => { if (!_disposed) Position(); },
            error => Log.Write("Desktop reposition dispatch failed; a later native notification can retry", error));
    }
    private void Position()
    {
        if (_disposed) return;
        var target = ShellLayerInterop.Monitor(Handle).Monitor.Bounds;
        bool actualAvailable = NativeMethods.GetWindowRect(Handle, out var actual);
        if (DesktopSurfacePositionPolicy.RequiresMove(actualAvailable, actual.Bounds, target))
        {
            using var work = UiTrace.Measure("desktop-native-resize", "Desktop");
            NativeWindow.MoveAndResize(new RectInt32(target.X, target.Y, target.Width, target.Height));
        }
        // WorkerW/Progman parentage can change even without geometry changes:
        // always restore the required desktop Z-order/anchor relationship.
        ShellLayerInterop.AnchorDesktop(Handle);
    }
    public void Dispose() { if (_disposed) return; _disposed = true; _hook.Dispose(); _chrome.Dispose(); }
}
