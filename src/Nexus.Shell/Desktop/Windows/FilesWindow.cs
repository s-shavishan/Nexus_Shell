// නමෝ බුද්ධාය 🤍
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Nexus.Shell.UI.Files;
using Windows.Graphics;

namespace Nexus.Shell.Desktop;

internal sealed class FilesWindow : Window
{
    internal IntPtr Handle { get; }
    internal AppWindow NativeWindow { get; }
    private readonly FilesView? _single;
    private readonly FilesWorkspaceView? _workspace;
    internal FilesView View => _workspace?.ActiveView ?? _single!;
    private readonly Grid _frame = new();
    private readonly Border _surface;
    private readonly WindowChrome _chrome;
    private readonly UI.NativeCaptionRegions _caption;
    private readonly UI.SurfaceMotion _motion;
    private readonly UI.WindowMaterial _material = new();
    private readonly UI.WindowTransition _transition;
    private bool _tucked;
    private readonly TextBlock _title;
    private readonly Grid _titleBar;
    private readonly FilesEnvironment _environment;
    private readonly TaskCompletionSource<IReadOnlyList<string>> _selection = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task<IReadOnlyList<string>> Selection => _selection.Task;
    internal FilesWindow(FilesEnvironment environment, FileSelectionRequest request, string folder)
    {
        _environment = environment;
        FrameworkElement content;
        if (request.Kind == FileSelectionKind.Browse)
            content = _workspace = new(environment, folder, Complete);
        else content = _single = new(environment, request, Complete);
        _frame.RowDefinitions.Add(new() { Height = new GridLength(50) }); _frame.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var chrome = _titleBar = new Grid { Padding = new Thickness(12, 0, 12, 0), BorderThickness = new Thickness(0, 0, 0, 1) }; chrome.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); chrome.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        var controls = new StackPanel { Orientation = Orientation.Horizontal };
        Button Control(string label, Windows.UI.Color color, Action action)
        {
            var button = new Button { Content = new Ellipse { Width = 13, Height = 13, Fill = new SolidColorBrush(color) }, Width = 28, Height = 38, Padding = new Thickness(0), Style = (Style)Application.Current.Resources["QuietButton"] };
            button.Click += (_, _) => action(); ToolTipService.SetToolTip(button, label); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, label); return button;
        }
        controls.Children.Add(Control("Close", Windows.UI.Color.FromArgb(255, 255, 96, 88), RequestClose));
        controls.Children.Add(Control("Minimize", Windows.UI.Color.FromArgb(255, 255, 189, 45), Minimize));
        controls.Children.Add(Control("Maximize or restore", Windows.UI.Color.FromArgb(255, 40, 200, 64), ToggleMaximize)); chrome.Children.Add(controls);
        _title = new TextBlock { Text = request.Title, FontSize = 13.5, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Left, Margin = new Thickness(28, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch, TextTrimming = TextTrimming.CharacterEllipsis };
        var dragArea = new Border { Child = _title, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        Grid.SetColumn(dragArea, 1); chrome.Children.Add(dragArea);
        _frame.Children.Add(chrome); Grid.SetRow(content, 1); _frame.Children.Add(content);
        _surface = new Border { Child = _frame, CornerRadius = new CornerRadius(MidnightGlassMetrics.FrameRadius), BorderThickness = new Thickness(1) }; Content = _surface;
        Handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(Handle));
        NativeWindow.Title = request.Title + " · Nexus";
        if (_workspace is not null) _workspace.TitleChanged += title => { _title.Text = title; NativeWindow.Title = title + " · Nexus Files"; };
        if (NativeWindow.Presenter is OverlappedPresenter overlapped) overlapped.SetBorderAndTitleBar(false, false);
        _transition = new(_surface, () => environment.Theme.Animations && !environment.Theme.HighContrast && environment.State.SurfaceAnimations && !environment.State.ReducedEffects);
        _chrome = new(Handle, resizable: true, stateChanged: maximized => { _surface.CornerRadius = new CornerRadius(maximized || environment.Theme.HighContrast ? 0 : MidnightGlassMetrics.FrameRadius); _surface.BorderThickness = new Thickness(maximized || !environment.Theme.HighContrast ? 0 : 1); }, minimizeRequested: Minimize, closeRequested: RequestClose, role: "Files");
        _transition.BindRestore(_chrome);
        _caption = new(NativeWindow, _chrome, dragArea);
        _motion = new(_surface, () => environment.Theme.Animations && !environment.Theme.HighContrast && environment.State.SurfaceAnimations && !environment.State.ReducedEffects);
        var work = ShellLayerInterop.Monitor(Handle).Work.Bounds; double scale = ShellLayerInterop.Scale(Handle);
        int width = Math.Min(work.Width, (int)(1130 * scale)), height = Math.Min(work.Height, (int)(740 * scale));
        NativeWindow.MoveAndResize(new RectInt32(work.X + (work.Width - width) / 2, work.Y + (work.Height - height) / 2, width, height));
        Closed += (_, _) => { _caption.Dispose(); _transition.Dispose(); _motion.Dispose(); _chrome.Dispose(); if (_workspace is not null) _workspace.Release(); else _single!.Release(); _selection.TrySetResult([]); };
        ApplyAppearance(); Activate(); _motion.Open(); if (_single is not null) _ = _single.NavigateAsync(folder);
    }
    private void Complete(IReadOnlyList<string> paths) { _selection.TrySetResult(paths); RequestClose(); }
    private async void RequestClose() => await _transition.CloseAsync(Close);
    internal void ShowFolder(string folder) { ReturnToWindow(); if (_workspace is not null) _workspace.OpenLocation(folder); else _ = View.NavigateAsync(folder); }
    internal void ReturnToWindow() { bool hidden = _tucked; _tucked = false; _transition.Restore(); NativeWindow.Show(); NativeMethods.Activate(Handle); if (hidden) _motion.Open(); }
    internal void ToggleFromDock()
    {
        if (!_tucked && NativeMethods.GetForegroundWindow() == Handle && NativeMethods.Visible(Handle) && !NativeMethods.IsMinimized(Handle)) Minimize();
        else ReturnToWindow();
    }
    internal void MinimizeFromDock() => Minimize();
    private void ToggleMaximize()
    {
        if (NativeWindow.Presenter is not OverlappedPresenter presenter) return;
        _transition.Restore();
        if (presenter.State == OverlappedPresenterState.Maximized) presenter.Restore(); else presenter.Maximize();
    }
    private async void Minimize()
    {
        if (_transition.IsMinimizing) { _transition.Restore(); return; }
        await _transition.MinimizeAsync(() => { _tucked = true; _motion.Hide(); if (NativeWindow.Presenter is OverlappedPresenter presenter) presenter.Minimize(); });
    }
    internal void ApplyAppearance()
    {
        var theme = _environment.Theme;
        _frame.RequestedTheme = theme.ElementTheme; _frame.Background = null; _material.Apply(this, _surface, theme, _environment.State);
        _surface.CornerRadius = new CornerRadius(theme.HighContrast || _chrome.IsAttached ? 0 : MidnightGlassMetrics.FrameRadius);
        _surface.BorderBrush = theme.Edge; _surface.BorderThickness = new Thickness(theme.HighContrast && !_chrome.IsAttached ? 1 : 0); _title.Foreground = theme.Brush("NexusText"); _titleBar.Background = theme.Material("MenuBar", _environment.State.NativeGlass); _titleBar.BorderBrush = theme.Edge;
        _chrome.SetCorners(theme.HighContrast); _motion.Refresh(); if (_workspace is not null) _workspace.ApplyAppearance(); else _single!.ApplyAppearance();
    }
}
