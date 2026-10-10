using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Nexus.Shell.UI;
using Nexus.Shell.UI.Desktop;

namespace Nexus.Shell.Desktop;

// Independent from the wallpaper HWND: maximized apps cannot cover the bar.
// True full-screen apps keep exclusive focus and hide both desktop bars.
internal sealed class MenuBarWindow : Window
{
    private readonly DesktopEnvironment _environment;
    private readonly IntPtr _handle;
    private readonly AppWindow _window;
    private readonly Border _frame;
    private readonly MenuBarView _view;
    private readonly WindowMaterial _material = new();
    private readonly WindowChrome _chrome;
    private bool _closed, _visible;
    internal MenuBarWindow(DesktopEnvironment environment)
    {
        _environment = environment; _view = new(environment); _frame = new Border { Child = _view, BorderThickness = new Thickness(0, 0, 0, 1) }; Content = _frame;
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(this); _window = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_handle)); _window.Title = "Nexus menu bar";
        WindowSwitcherPolicy.Request(() => _window.IsShownInSwitchers = false, error => Log.Write("Menu bar switcher fallback", error));
        if (_window.Presenter is OverlappedPresenter presenter) { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMaximizable = presenter.IsMinimizable = false; }
        ShellLayerInterop.ToolWindow(_handle, noActivate: true); _chrome = new(_handle, customClip: true);
        Closed += (_, _) => { _closed = true; _view.Dispose(); _chrome.Dispose(); };
        ApplyAppearance(); Position();
    }
    internal void Position()
    {
        if (_closed) return;
        var monitor = ShellLayerInterop.Monitor(_environment.Desktop.Handle).Monitor.Bounds; double scale = ShellLayerInterop.Scale(_environment.Desktop.Handle);
        int height = Math.Min(monitor.Height, (int)Math.Round(DesktopLayout.MenuBarHeight * scale));
        ShellLayerInterop.SetWindowPos(_handle, ShellLayerInterop.Topmost, monitor.X, monitor.Y, monitor.Width, height, 0x0010);
        _chrome.SetClip(new ShellRect(0, 0, monitor.Width, height), 0);
    }
    internal void SetFullscreen(bool fullscreen)
    {
        if (_closed) return;
        if (fullscreen && _visible) { _window.Hide(); _visible = false; }
        else if (!fullscreen && (!_visible || !NativeMethods.Visible(_handle))) { _window.Show(false); _visible = true; }
    }
    internal void ApplyAppearance() { if (_closed) return; _material.Apply(this, _frame, _environment, "MenuBar"); _frame.BorderBrush = _environment.Theme.Brush("NexusBorder"); _view.ApplyAppearance(); }
}
