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
    private bool _closed;
    internal MenuWindow(DesktopEnvironment environment)
    {
        _view = new(environment); _frame = new Border { Child = _view, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1) }; Content = _frame;
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_handle));
        NativeWindow.Title = "Nexus Start"; NativeWindow.IsShownInSwitchers = false;
        if (NativeWindow.Presenter is OverlappedPresenter presenter)
        { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMinimizable = presenter.IsMaximizable = false; }
        ShellLayerInterop.ToolWindow(_handle);
        Activated += (_, args) => { if (args.WindowActivationState == WindowActivationState.Deactivated) HideMenu(); };
        Closed += (_, _) => { _closed = true; IsOpen = false; environment.MenuClosed(this); };
        ApplyAppearance(environment);
    }
    internal void ShowMenu(DesktopEnvironment environment, ShellRect taskbar, bool search)
    {
        if (_closed) return;
        ApplyAppearance(environment);
        var rect = DesktopLayout.MenuBounds(ShellLayerInterop.Monitor(environment.Taskbar.Handle).Monitor.Bounds, taskbar, ShellLayerInterop.Scale(environment.Taskbar.Handle));
        ShellLayerInterop.SetWindowPos(_handle, ShellLayerInterop.Topmost, rect.X, rect.Y, rect.Width, rect.Height, 0x0010);
        IsOpen = true; Activate(); _ = _view.OpenAsync(search);
    }
    internal void HideMenu() { if (_closed || !IsOpen) return; IsOpen = false; NativeWindow.Hide(); }
    internal void ApplyAppearance(DesktopEnvironment environment)
    { _frame.RequestedTheme = environment.Theme.ElementTheme; _frame.Background = environment.Theme.Brush("NexusPanel"); _frame.BorderBrush = environment.Theme.Brush("NexusBorder"); _view.ApplyAppearance(); }
}
