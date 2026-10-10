using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
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
    private int _generation;
    internal MenuBarWindow(DesktopEnvironment environment)
    {
        _environment = environment; _view = new(environment); _frame = new Border { Child = _view, BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top }; var root = new Grid(); root.Children.Add(_frame); Content = root;
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(this); _window = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_handle)); _window.Title = "Nexus menu bar";
        WindowSwitcherPolicy.Request(() => _window.IsShownInSwitchers = false, error => Log.Write("Menu bar switcher fallback", error));
        if (_window.Presenter is OverlappedPresenter presenter) { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMaximizable = presenter.IsMinimizable = false; }
        ShellLayerInterop.ToolWindow(_handle, noActivate: true); _chrome = new(_handle, customClip: true);
        Closed += (_, _) => { _closed = true; ++_generation; _view.Dispose(); _chrome.Dispose(); };
        ApplyAppearance(); Position();
    }
    internal void Position()
    {
        if (_closed) return;
        var monitor = ShellLayerInterop.Monitor(_environment.Desktop.Handle).Monitor.Bounds; double scale = ShellLayerInterop.Scale(_environment.Desktop.Handle);
        int height = Math.Min(monitor.Height, Math.Max(1, (int)Math.Round(DesktopLayout.MenuBarHeight * scale)));
        ShellLayerInterop.SetWindowPos(_handle, ShellLayerInterop.Topmost, monitor.X, monitor.Y, monitor.Width, height, 0x0010);
        var target = MenuBarLayout.Bounds(monitor, scale, _attached);
        var local = target with { X = target.X - monitor.X, Y = target.Y - monitor.Y };
        if (_bounds == local && _scale == scale) return;
        var before = _bounds; double oldScale = _scale; _bounds = local; _scale = scale;
        _frame.Width = local.Width / scale; _frame.Height = local.Height / scale;
        _frame.Margin = new Thickness(local.X / scale, local.Y / scale, 0, 0);
        _frame.CornerRadius = new CornerRadius(_attached || _environment.Theme.HighContrast ? 0 : 10);
        _frame.BorderThickness = _attached ? new Thickness(0, 0, 0, 1) : new Thickness(1);
        int generation = ++_generation;
        bool animate = before is not null && oldScale == scale && _frame.IsLoaded && _visible && _environment.Theme.Animations && !_environment.Theme.HighContrast && !_environment.Session.State.ReducedEffects;
        if (animate) { _chrome.SetClip(new(0, 0, monitor.Width, height), 0); _ = MoveAsync(before!.Value, local, scale, generation); }
        else { ResetMotion(); Clip(local); }
    }
    internal void SetAttached(bool value) { if (_closed || _attached == value) return; _attached = value; Position(); }
    private async Task MoveAsync(ShellRect before, ShellRect after, double scale, int generation)
    {
        try
        {
            ElementCompositionPreview.SetIsTranslationEnabled(_frame, true);
            var visual = ElementCompositionPreview.GetElementVisual(_frame); var compositor = visual.Compositor; visual.CenterPoint = Vector3.Zero;
            using var ease = compositor.CreateCubicBezierEasingFunction(new(.2f, .8f), new(.2f, 1));
            using var move = compositor.CreateVector3KeyFrameAnimation(); move.Duration = TimeSpan.FromMilliseconds(220);
            move.InsertExpressionKeyFrame(0, FormattableString.Invariant($"this.StartingValue + Vector3({(before.X - after.X) / scale}, {(before.Y - after.Y) / scale}, 0)")); move.InsertKeyFrame(1, Vector3.Zero, ease);
            using var resize = compositor.CreateVector3KeyFrameAnimation(); resize.Duration = move.Duration;
            resize.InsertExpressionKeyFrame(0, FormattableString.Invariant($"this.StartingValue * Vector3({(double)before.Width / after.Width}, {(double)before.Height / after.Height}, 1)")); resize.InsertKeyFrame(1, Vector3.One, ease);
            using var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); batch.Completed += (_, _) => done.TrySetResult();
            visual.StartAnimation("Translation", move); visual.StartAnimation("Scale", resize); batch.End();
            await done.Task.WaitAsync(TimeSpan.FromMilliseconds(450));
        }
        catch (Exception error) { if (!_closed) Log.Write("Menu bar motion used immediate placement", error); }
        if (!_closed && generation == _generation) { ResetMotion(); Clip(after); }
    }
    private void Clip(ShellRect rect) => _chrome.SetClip(rect, _attached || _environment.Theme.HighContrast ? 0 : 10);
    private void ResetMotion()
    { try { var visual = ElementCompositionPreview.GetElementVisual(_frame); visual.StopAnimation("Scale"); visual.StopAnimation("Translation"); visual.Scale = Vector3.One; visual.Properties.InsertVector3("Translation", Vector3.Zero); } catch { } }
    internal void SetFullscreen(bool fullscreen)
    {
        if (_closed) return;
        if (fullscreen && _visible) { ++_generation; ResetMotion(); if (_bounds is { } bounds) Clip(bounds); _window.Hide(); _visible = false; }
        else if (!fullscreen && (!_visible || !NativeMethods.Visible(_handle))) { _window.Show(false); _visible = true; }
    }
    internal void ApplyAppearance() { if (_closed) return; _material.Apply(this, _frame, _environment, "MenuBar"); _frame.BorderBrush = _environment.Theme.Edge; _view.ApplyAppearance(); _bounds = null; Position(); }
}
