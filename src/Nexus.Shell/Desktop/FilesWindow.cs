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
    internal FilesView View { get; }
    private readonly Grid _frame = new();
    private readonly Border _surface;
    private readonly WindowChrome _chrome;
    private readonly UI.SurfaceMotion _motion;
    private bool _tucked;
    private readonly TextBlock _title;
    private readonly DesktopEnvironment _environment;
    private readonly TaskCompletionSource<IReadOnlyList<string>> _selection = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task<IReadOnlyList<string>> Selection => _selection.Task;
    internal FilesWindow(DesktopEnvironment environment, FileSelectionRequest request, string folder)
    {
        _environment = environment; View = new(environment, request, Complete);
        _frame.RowDefinitions.Add(new() { Height = new GridLength(48) }); _frame.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var chrome = new Grid { Padding = new Thickness(8, 0, 12, 0) }; chrome.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); chrome.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        var controls = new StackPanel { Orientation = Orientation.Horizontal };
        Button Control(string label, Windows.UI.Color color, Action action)
        {
            var button = new Button { Content = new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(color) }, Width = 36, Height = 44, Style = (Style)Application.Current.Resources["QuietButton"] };
            button.Click += (_, _) => action(); ToolTipService.SetToolTip(button, label); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, label); return button;
        }
        controls.Children.Add(Control("Close", Windows.UI.Color.FromArgb(255, 220, 76, 71), Close));
        controls.Children.Add(Control("Minimize", Windows.UI.Color.FromArgb(255, 202, 145, 27), Minimize));
        controls.Children.Add(Control("Maximize or restore", Windows.UI.Color.FromArgb(255, 37, 152, 87), ToggleMaximize)); chrome.Children.Add(controls);
        _title = new TextBlock { Text = request.Title + " · Nexus", FontSize = 14, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch, TextTrimming = TextTrimming.CharacterEllipsis };
        _title.PointerPressed += (_, e) => { if (e.GetCurrentPoint(_title).Properties.IsLeftButtonPressed) NativeMethods.BeginDrag(Handle); };
        _title.DoubleTapped += (_, _) => ToggleMaximize(); Grid.SetColumn(_title, 1); chrome.Children.Add(_title);
        _frame.Children.Add(chrome); Grid.SetRow(View, 1); _frame.Children.Add(View);
        _surface = new Border { Child = _frame, CornerRadius = new CornerRadius(18) }; Content = _surface;
        Handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        NativeWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(Handle));
        NativeWindow.Title = request.Title + " · Nexus";
        if (NativeWindow.Presenter is OverlappedPresenter overlapped) overlapped.SetBorderAndTitleBar(false, false);
        _chrome = new(Handle, resizable: true, tuck: Minimize);
        _motion = new(View, () => environment.Theme.Animations && !environment.Theme.HighContrast && !environment.Session.State.ReducedEffects);
        var work = ShellLayerInterop.Monitor(Handle).Work.Bounds; double scale = ShellLayerInterop.Scale(Handle);
        int width = Math.Min(work.Width, (int)(1000 * scale)), height = Math.Min(work.Height, (int)(680 * scale));
        NativeWindow.MoveAndResize(new RectInt32(work.X + (work.Width - width) / 2, work.Y + (work.Height - height) / 2, width, height));
        Closed += (_, _) => { _motion.Dispose(); _chrome.Dispose(); View.Release(); _selection.TrySetResult([]); };
        ApplyAppearance(); Activate(); _motion.Open(); _ = View.NavigateAsync(folder);
    }
    private void Complete(IReadOnlyList<string> paths) { _selection.TrySetResult(paths); Close(); }
    internal void ShowFolder(string folder) { ReturnToWindow(); _ = View.NavigateAsync(folder); }
    internal void ReturnToWindow() { bool hidden = _tucked; _tucked = false; NativeWindow.Show(); NativeMethods.Activate(Handle); if (hidden) _motion.Open(); }
    private void ToggleMaximize()
    {
        if (NativeWindow.Presenter is not OverlappedPresenter presenter) return;
        if (presenter.State == OverlappedPresenterState.Maximized) presenter.Restore(); else presenter.Maximize();
    }
    private void Minimize() { _tucked = true; _motion.Hide(); NativeWindow.Hide(); }
    internal void ApplyAppearance()
    {
        var theme = _environment.Theme;
        _frame.RequestedTheme = theme.ElementTheme; _surface.Background = _frame.Background = theme.Brush("NexusSidebar");
        _surface.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : 18); _title.Foreground = theme.Brush("NexusText");
        _chrome.SetCorners(theme.HighContrast); _motion.Refresh(); View.ApplyAppearance();
    }
}
