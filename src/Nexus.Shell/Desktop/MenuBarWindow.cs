using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
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
    private bool _closed, _visible, _attached;
    private ShellRect? _bounds;
    private double _scale;
    internal MenuBarWindow(DesktopEnvironment environment)
    {
        _environment = environment; _view = new(environment); _frame = new Border { Child = _view, BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top }; var root = new Grid(); root.Children.Add(_frame); Content = root;
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(this); _window = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_handle)); _window.Title = "Nexus menu bar";
        WindowSwitcherPolicy.Request(() => _window.IsShownInSwitchers = false, error => Log.Write("Menu bar switcher fallback", error));
        if (_window.Presenter is OverlappedPresenter presenter) { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMaximizable = presenter.IsMinimizable = false; }
        ShellLayerInterop.ToolWindow(_handle, noActivate: true); _chrome = new(_handle, customClip: true, role: "MenuBar");
        Closed += (_, _) => { _closed = true; ResetMotion(); _view.Dispose(); _chrome.Dispose(); };
        ApplyAppearance(); Position();
    }
    internal void Position()
    {
        if (_closed) return;
        var monitor = ShellLayerInterop.Monitor(_environment.Desktop.Handle).Monitor.Bounds; double scale = ShellLayerInterop.Scale(_environment.Desktop.Handle);
        int height = Math.Min(monitor.Height, Math.Max(1, (int)Math.Round(DesktopLayout.MenuBarHeight * scale)));
        var destination = new ShellRect(monitor.X, monitor.Y, monitor.Width, height);
        bool hasCurrent = NativeMethods.GetWindowRect(_handle, out var current);
        uint flags = MenuBarNativePlacement.Flags(hasCurrent, current.Bounds, destination);
        ShellLayerInterop.SetWindowPos(_handle, ShellLayerInterop.Topmost,
            destination.X, destination.Y, destination.Width, destination.Height, flags);
        var target = MenuBarLayout.Bounds(monitor, scale, _attached);
        var local = target with { X = target.X - monitor.X, Y = target.Y - monitor.Y };
        if (_bounds == local && _scale == scale) return;
        var before = _bounds; double oldScale = _scale; _bounds = local; _scale = scale;
        _frame.Width = local.Width / scale; _frame.Height = local.Height / scale;
        _frame.Margin = new Thickness(local.X / scale, local.Y / scale, 0, 0);
        _frame.CornerRadius = new CornerRadius(_attached || _environment.Theme.HighContrast ? 0 : 10);
        _frame.BorderThickness = _attached ? new Thickness(0, 0, 0, 1) : new Thickness(1);
        bool animate = before is not null && oldScale == scale && _frame.IsLoaded && _visible && _environment.Theme.Animations && !_environment.Theme.HighContrast && _environment.Session.State.SurfaceAnimations && !_environment.Session.State.ReducedEffects;
        // Commit native and XAML bounds together. The old animation expanded the
        // HWND region across transparent, unpainted root margins until completion.
        ResetMotion(); Clip(local); _chrome.Trace("menu-position", local.Width, local.Height);
        if (animate) FadeContents();
    }
    internal void SetAttached(bool value) { if (_closed || _attached == value) return; _attached = value; Position(); }
    internal Nexus.Runtime.PanelAnchor? AnchorFor(FrameworkElement element)
    {
        if (_closed || _bounds is not { } bounds || !element.IsLoaded) return null;
        try
        {
            var point = element.TransformToVisual(_frame).TransformPoint(new(0, 0));
            var monitor = ShellLayerInterop.Monitor(_handle).Monitor.Bounds;
            return new(monitor.X + bounds.X + (int)Math.Round(point.X * _scale), monitor.Y + bounds.Y + (int)Math.Round(point.Y * _scale),
                Math.Max(1, (int)Math.Round(element.ActualWidth * _scale)), Math.Max(1, (int)Math.Round(element.ActualHeight * _scale)));
        }
        catch (Exception error) { Log.Write("Control anchor unavailable; using the menu-bar fallback", error); return null; }
    }
    private void FadeContents()
    {
        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(_view); var compositor = visual.Compositor;
            using var ease = compositor.CreateCubicBezierEasingFunction(new(.2f, .8f), new(.2f, 1));
            using var opacity = compositor.CreateScalarKeyFrameAnimation(); opacity.Duration = TimeSpan.FromMilliseconds(150);
            opacity.InsertKeyFrame(0, .94f); opacity.InsertKeyFrame(1, 1, ease); visual.StartAnimation("Opacity", opacity);
        }
        catch (Exception error) { if (!_closed) Log.Write("Menu bar motion used immediate placement", error); }
    }
    private void Clip(ShellRect rect) => _chrome.SetClip(rect, _attached || _environment.Theme.HighContrast ? 0 : 10);
    private void ResetMotion()
    { try { var visual = ElementCompositionPreview.GetElementVisual(_view); visual.StopAnimation("Opacity"); visual.Opacity = 1; } catch { } }
    internal void SetFullscreen(bool fullscreen)
    {
        if (_closed) return;
        if (fullscreen && _visible) { ResetMotion(); if (_bounds is { } bounds) Clip(bounds); _window.Hide(); _visible = false; }
        else if (!fullscreen && (!_visible || !NativeMethods.Visible(_handle))) { _window.Show(false); _visible = true; }
    }
    internal void ApplyAppearance() { if (_closed) return; _material.Apply(this, _frame, _environment, "MenuBar"); GlassPaintRetention.Edge(_frame, _environment.Theme.Edge); _view.ApplyAppearance(); _bounds = null; Position(); }
}
