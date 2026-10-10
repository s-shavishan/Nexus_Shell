using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Nexus.Shell.UI.Menus;
using Windows.Graphics;

namespace Nexus.Shell.Desktop;

internal sealed class MenuWindow : Window
{
    internal AppWindow NativeWindow { get; }
    internal bool IsOpen { get; private set; }
    private readonly IntPtr _handle;
    private readonly StartMenuView _view;
    private readonly Border _frame;
    private readonly WindowChrome _chrome;
    private readonly UI.SurfaceMotion _motion;
    private readonly UI.WindowMaterial _material = new();
    private bool _closed;
    internal MenuWindow(DesktopEnvironment environment)
    {
        _view = new(environment); _frame = new Border { Child = _view, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1) }; Content = _frame;
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_handle));
        NativeWindow.Title = "Nexus Launchpad"; WindowSwitcherPolicy.Request(() => NativeWindow.IsShownInSwitchers = false, error => Log.Write("Switcher API unavailable; using native tool-window styling", error));
        if (NativeWindow.Presenter is OverlappedPresenter presenter)
        { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMinimizable = presenter.IsMaximizable = false; }
        ShellLayerInterop.ToolWindow(_handle);
        _chrome = new(_handle, closeRequested: HideMenu);
        _motion = new(_frame, () => environment.Theme.Animations && !environment.Theme.HighContrast && environment.Session.State.SurfaceAnimations && !environment.Session.State.ReducedEffects);
        Activated += (_, args) => { if (args.WindowActivationState == WindowActivationState.Deactivated) HideMenu(); };
        Closed += (_, _) => { _closed = true; IsOpen = false; _motion.Dispose(); _view.Dispose(); _chrome.Dispose(); environment.MenuClosed(this); };
        ApplyAppearance(environment);
    }
    internal void ShowMenu(DesktopEnvironment environment, ShellRect taskbar, bool search)
    {
        if (_closed) return;
        ApplyAppearance(environment);
        var monitor = ShellLayerInterop.Monitor(environment.Taskbar.Handle).Monitor.Bounds;
        var rect = DesktopLayout.LaunchpadBounds(monitor, ShellLayerInterop.Scale(environment.Taskbar.Handle));
        ShellLayerInterop.SetWindowPos(_handle, ShellLayerInterop.Topmost, rect.X, rect.Y, rect.Width, rect.Height, 0x0010);
        _frame.IsHitTestVisible = true; IsOpen = true;
        _ = _view.OpenAsync(search); Activate(); _view.FocusEntry(search); _motion.Open();
    }
    internal void HideMenu() { if (_closed || !IsOpen) return; IsOpen = false; _frame.IsHitTestVisible = false; _view.Hide(); NativeWindow.Hide(); _motion.Hide(); }
    internal void ApplyAppearance(DesktopEnvironment environment)
    {
        _frame.RequestedTheme = environment.Theme.ElementTheme; _material.Apply(this, _frame, environment); _frame.BorderBrush = environment.Theme.Edge;
        _frame.BorderThickness = new Thickness(1); _frame.CornerRadius = new CornerRadius(environment.Theme.HighContrast ? 0 : 22);
        _chrome.SetCorners(environment.Theme.HighContrast, 22); _motion.Refresh(); _view.ApplyAppearance();
    }
}
