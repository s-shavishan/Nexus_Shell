using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Nexus.Shell.UI.Controls;

namespace Nexus.Shell.Desktop;

internal sealed class QuickSettingsWindow : Window
{
    private readonly DesktopEnvironment _environment;
    private readonly IntPtr _handle;
    private readonly AppWindow _window;
    private readonly QuickSettingsView _view;
    private readonly Border _frame;
    private readonly WindowChrome _chrome;
    private readonly UI.SurfaceMotion _motion;
    private bool _closed;
    internal bool IsOpen { get; private set; }
    internal QuickSettingsWindow(DesktopEnvironment environment)
    {
        _environment = environment; _view = new(environment, Hide);
        _frame = new Border { Child = _view, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1) }; Content = _frame;
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _window = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_handle));
        _window.Title = "Nexus Quick Settings"; _window.IsShownInSwitchers = false;
        if (_window.Presenter is OverlappedPresenter presenter)
        { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMinimizable = presenter.IsMaximizable = false; }
        ShellLayerInterop.ToolWindow(_handle);
        _chrome = new(_handle);
        _motion = new(_view, () => environment.Theme.Animations && !environment.Theme.HighContrast && !environment.Session.State.ReducedEffects);
        Activated += (_, args) => { if (args.WindowActivationState == WindowActivationState.Deactivated) Hide(); };
        Closed += (_, _) => { _closed = true; IsOpen = false; _motion.Dispose(); _chrome.Dispose(); _view.Dispose(); environment.QuickSettingsClosed(this); };
        ApplyAppearance();
    }
    internal void Show()
    {
        if (_closed) return; ApplyAppearance(); Position();
        IsOpen = true; Activate(); _view.Open(); _motion.Open();
    }
    internal void Position()
    {
        if (_closed) return;
        var rect = DesktopLayout.QuickSettingsBounds(ShellLayerInterop.Monitor(_environment.Taskbar.Handle).Monitor.Bounds,
            _environment.Taskbar.BarBounds, ShellLayerInterop.Scale(_environment.Taskbar.Handle));
        ShellLayerInterop.SetWindowPos(_handle, ShellLayerInterop.Topmost, rect.X, rect.Y, rect.Width, rect.Height, 0x0010);
    }
    internal void Hide() { if (_closed || !IsOpen) return; IsOpen = false; _motion.Hide(); _view.Hide(); _window.Hide(); }
    internal void ApplyAppearance()
    {
        var theme = _environment.Theme;
        _frame.RequestedTheme = theme.ElementTheme; _frame.Background = theme.Surface("Sidebar"); _frame.BorderBrush = theme.Brush("NexusBorder");
        _frame.BorderThickness = new Thickness(theme.HighContrast ? 1 : 0); _frame.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : 22);
        _chrome.SetCorners(theme.HighContrast); _motion.Refresh(); _view.ApplyAppearance();
    }
    internal void RefreshPreferences() => _view.RefreshPreferences();
}
