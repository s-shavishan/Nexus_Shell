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
    private readonly ControlCenterEnvironment _environment;
    private readonly IntPtr _handle;
    private readonly AppWindow _window;
    private readonly QuickSettingsView _view;
    private readonly Border _frame;
    private readonly WindowChrome _chrome;
    private readonly UI.SurfaceMotion _motion;
    private readonly UI.WindowMaterial _material = new();
    private bool _closed;
    internal bool IsOpen { get; private set; }
    internal IntPtr Handle => _handle;
    internal QuickSettingsView View => _view;
    private bool _compact;
    internal QuickSettingsWindow(ControlCenterEnvironment environment)
    {
        _environment = environment; _view = new(environment, environment.Hide);
        _frame = new Border { Child = _view, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1) }; Content = _frame;
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _window = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_handle));
        _window.Title = "Nexus Control Center"; WindowSwitcherPolicy.Request(() => _window.IsShownInSwitchers = false, error => Log.Write("Switcher API unavailable; using native tool-window styling", error));
        if (_window.Presenter is OverlappedPresenter presenter)
        { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMinimizable = presenter.IsMaximizable = false; }
        ShellLayerInterop.ToolWindow(_handle);
        _chrome = new(_handle, closeRequested: environment.Hide);
        _motion = new(_frame, () => environment.Theme.Animations && !environment.Theme.HighContrast && environment.State.SurfaceAnimations && !environment.State.ReducedEffects);
        Activated += (_, args) => { if (IsOpen && args.WindowActivationState == WindowActivationState.Deactivated) environment.Hide(); };
        Closed += (_, _) => { _closed = true; IsOpen = false; _motion.Dispose(); _chrome.Dispose(); _view.Dispose(); environment.Stop(); };
        ApplyAppearance();
    }
    internal void Show(string? section = null, bool compact = false)
    {
        if (_closed) return; _compact = compact; ApplyAppearance(); Position();
        bool wasOpen = IsOpen; _frame.IsHitTestVisible = true; IsOpen = true; _view.Open(section, compact); Activate(); if (!wasOpen) _motion.Open();
    }
    internal void Position()
    {
        if (_closed) return;
        var monitor = _environment.Snapshot.Desired.Monitor;
        var bounds = new ShellRect(monitor.X, monitor.Y, monitor.Width, monitor.Height);
        var rect = _compact ? DesktopLayout.QuickControlBounds(bounds, monitor.Scale, _environment.Snapshot.Desired.Section) : DesktopLayout.PanelBounds(bounds, monitor.Scale, 540, 740);
        ShellLayerInterop.SetWindowPos(_handle, ShellLayerInterop.Topmost, rect.X, rect.Y, rect.Width, rect.Height, 0x0010);
    }
    internal void HideSurface() { if (_closed || !IsOpen) return; IsOpen = false; _frame.IsHitTestVisible = false; _view.Hide(); _window.Hide(); _motion.Hide(); }
    internal void ApplyAppearance()
    {
        var theme = _environment.Theme;
        _frame.RequestedTheme = theme.ElementTheme; _material.Apply(this, _frame, theme, _environment.State); _frame.BorderBrush = theme.Edge;
        _frame.BorderThickness = new Thickness(1); _frame.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : 22);
        _chrome.SetCorners(theme.HighContrast, 22); _motion.Refresh(); _view.ApplyAppearance();
    }
    internal void RefreshPreferences() => _view.RefreshPreferences();
}
