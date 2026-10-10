using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Nexus.Shell.UI;
using Windows.Graphics;

namespace Nexus.Shell.Desktop;

internal sealed class UtilityWindow : Window
{
    internal IntPtr Handle { get; }
    internal AppWindow NativeWindow { get; }
    internal string UtilityTitle { get; }
    internal FrameworkElement View { get; }
    private readonly DesktopEnvironment _environment;
    private readonly Border _frame;
    private readonly TextBlock _title;
    private readonly WindowChrome _chrome;
    private readonly SurfaceMotion _motion;
    private readonly WindowMaterial _material = new();
    private readonly Action _apply;
    internal UtilityWindow(DesktopEnvironment environment, string title, FrameworkElement view, int width, int height, Action apply, Action release)
    {
        _environment = environment; UtilityTitle = title; View = view; _apply = apply;
        var grid = new Grid(); grid.RowDefinitions.Add(new() { Height = new GridLength(44) }); grid.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var chrome = new Grid { Padding = new Thickness(10, 0, 16, 0), ColumnSpacing = 12 };
        chrome.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); chrome.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        var controls = new StackPanel { Orientation = Orientation.Horizontal };
        void Control(string name, string color, Action action)
        {
            var button = new Button { Width = 26, Height = 34, Padding = new Thickness(0), Style = (Style)Application.Current.Resources["QuietButton"], Content = new Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush(ShellTheme.Color(color)) } };
            button.Click += (_, _) => action(); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, name); ToolTipService.SetToolTip(button, name); controls.Children.Add(button);
        }
        Control("Close", "FFFF6058", Close); Control("Minimize", "FFFFBD2D", Minimize); Control("Maximize or restore", "FF28C840", ToggleMaximize);
        chrome.Children.Add(controls); _title = new() { Text = title, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch, TextAlignment = TextAlignment.Center };
        _title.PointerPressed += (_, e) => { if (e.GetCurrentPoint(_title).Properties.IsLeftButtonPressed) NativeMethods.BeginDrag(Handle); };
        _title.DoubleTapped += (_, _) => ToggleMaximize(); Grid.SetColumn(_title, 1); chrome.Children.Add(_title); grid.Children.Add(chrome); Grid.SetRow(view, 1); grid.Children.Add(view);
        _frame = new() { Child = grid, CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1) }; Content = _frame;
        Handle = WinRT.Interop.WindowNative.GetWindowHandle(this); NativeWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(Handle)); NativeWindow.Title = title + " · Nexus";
        if (NativeWindow.Presenter is OverlappedPresenter presenter) presenter.SetBorderAndTitleBar(false, false);
        _chrome = new(Handle, resizable: true, minimumWidth: Math.Min(width, 480), minimumHeight: Math.Min(height, 360)); _motion = new(view, () => environment.Theme.Animations && !environment.Theme.HighContrast && !environment.Session.State.ReducedEffects);
        var work = ShellLayerInterop.Monitor(Handle).Work.Bounds; double scale = ShellLayerInterop.Scale(Handle);
        int w = Math.Min(work.Width, (int)(width * scale)), h = Math.Min(work.Height, (int)(height * scale));
        NativeWindow.MoveAndResize(new RectInt32(work.X + (work.Width - w) / 2, work.Y + (work.Height - h) / 2, w, h));
        Closed += (_, _) => { release(); _motion.Dispose(); _chrome.Dispose(); };
        ApplyAppearance(); Activate(); _motion.Open();
    }
    internal void Restore() { NativeWindow.Show(); NativeMethods.Activate(Handle); _environment.UpdateTaskbar(); }
    internal void Minimize() { _motion.Hide(); if (NativeWindow.Presenter is OverlappedPresenter presenter) presenter.Minimize(); _environment.UpdateTaskbar(); }
    internal void Toggle()
    { if (NativeMethods.ForegroundTaskWindow() == Handle && !NativeMethods.IsMinimized(Handle)) Minimize(); else Restore(); }
    private void ToggleMaximize()
    { if (NativeWindow.Presenter is OverlappedPresenter presenter) { if (presenter.State == OverlappedPresenterState.Maximized) presenter.Restore(); else presenter.Maximize(); } }
    internal void ApplyAppearance()
    { _frame.RequestedTheme = _environment.Theme.ElementTheme; _frame.BorderBrush = _environment.Theme.Brush("NexusBorder"); _title.Foreground = _environment.Theme.Brush("NexusText"); _material.Apply(this, _frame, _environment); _chrome.SetCorners(_environment.Theme.HighContrast); _motion.Refresh(); _apply(); }
}
