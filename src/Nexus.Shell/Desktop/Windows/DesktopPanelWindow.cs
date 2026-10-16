// නමෝ බුද්ධාය 🤍
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Nexus.Shell.UI;
using Windows.Graphics;
using Windows.System;

namespace Nexus.Shell.Desktop;
// Panels own their HWND and release active work when hidden.
internal abstract class DesktopPanelWindow : Window
{
    protected readonly DesktopEnvironment Environment;
    protected readonly Border Frame;
    protected readonly Grid Root;
    protected readonly IntPtr Handle;
    protected readonly AppWindow Native;
    private readonly TextBlock _title;
    private readonly WindowChrome _chrome;
    private readonly WindowMaterial _material = new();
    private readonly SurfaceMotion _motion;
    private readonly int _width, _height;
    private readonly bool _center;
    private IntPtr _monitorSource;
    protected bool IsClosed { get; private set; }
    internal bool IsOpen { get; private set; }
    protected virtual bool DismissOnDeactivate => true;
    protected virtual bool IsImmersive => false;
    protected virtual bool AllowUserResize => !IsImmersive;
    internal string MaterialStatus => _material.Status;

    protected DesktopPanelWindow(DesktopEnvironment environment, string title, FrameworkElement body, int width, int height, bool center = false, bool animateBody = true)
    {
        Environment = environment;
        _width = width;
        _height = height;
        _center = center;
        Root = new Grid
        {
            Padding = new Thickness(MidnightGlassMetrics.ContentPadding),
            RowSpacing = 14,
            KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden
        };
        Root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new());
        var header = new Grid
        {
            ColumnSpacing = 12
        };
        header.ColumnDefinitions.Add(new());
        header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _title = new TextBlock
        {
            Text = title,
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(_title);
        var close = ShellControls.IconButton("\uE8BB", "Close " + title, Hide);
        Grid.SetColumn(close, 1);
        header.Children.Add(close);
        Root.Children.Add(header);
        Grid.SetRow(body, 1);
        Root.Children.Add(body);
        Frame = new Border
        {
            Child = Root,
            CornerRadius = new CornerRadius(MidnightGlassMetrics.FrameRadius),
            BorderThickness = new Thickness(1)
        };
        Content = Frame;
        Handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Native = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(Handle));
        Native.Title = "Nexus " + title;
        WindowSwitcherPolicy.Request(() => Native.IsShownInSwitchers = false, error => Log.Write("Desktop panel switcher fallback", error));
        if (Native.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = AllowUserResize;
            presenter.IsMinimizable = presenter.IsMaximizable = false;
        }

        ShellLayerInterop.ToolWindow(Handle);
        _chrome = new(Handle, resizable: AllowUserResize, minimumWidth: Math.Min(420, _width), minimumHeight: Math.Min(300, _height), closeRequested: Hide);
        _motion = new(animateBody ? Root : header, () => environment.Theme.Animations && !environment.Theme.HighContrast && environment.Session.State.SurfaceAnimations && !environment.Session.State.ReducedEffects);
        var escape = new KeyboardAccelerator
        {
            Key = VirtualKey.Escape
        };
        escape.Invoked += (_, e) =>
        {
            Hide();
            e.Handled = true;
        };
        Root.KeyboardAccelerators.Add(escape);
        Activated += (_, args) =>
        {
            if (DismissOnDeactivate && IsOpen && args.WindowActivationState == WindowActivationState.Deactivated)
                Hide();
        };
        Closed += (_, _) =>
        {
            IsClosed = true;
            IsOpen = false;
            OnHidden();
            _chrome.Dispose();
            _motion.Dispose();
            OnClosed();
        };
    }

    protected void ShowPanel(IntPtr monitorSource = default)
    {
        if (IsClosed || Environment.IsStopping)
            return;
        _monitorSource = monitorSource;
        ApplyAppearance();
        Position(monitorSource);
        IsOpen = true;
        Frame.IsHitTestVisible = true;
        Native.Show();
        Activate();
        _motion.Open();
        OnShown();
    }

    internal void Hide()
    {
        if (!IsOpen || IsClosed)
            return;
        IsOpen = false;
        OnHidden();
        Frame.IsHitTestVisible = false;
        _motion.Hide();
        Native.Hide();
    }

    internal void Reposition()
    {
        if (IsOpen && !IsClosed)
            Position(_monitorSource);
    }

    private void Position(IntPtr source)
    {
        if (source == IntPtr.Zero)
            source = Environment.Desktop.Handle;
        var monitor = ShellLayerInterop.Monitor(source);
        double scale = ShellLayerInterop.Scale(source);
        ShellRect bounds = ResolveBounds(monitor.Work.Bounds, monitor.Monitor.Bounds, scale);
        Native.MoveAndResize(new RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        ShellLayerInterop.SetWindowPos(Handle, ShellLayerInterop.Topmost, 0, 0, 0, 0, 0x0013);
    }

    // Keep ordinary panels' native placement intact. Specialized floating
    // surfaces may choose a compact/expanded position without manipulating
    // Win32 HWND geometry from every input event.
    protected virtual ShellRect ResolveBounds(ShellRect work, ShellRect monitor, double scale) => _center ? DesktopLayout.CenteredPanelBounds(work, scale, _width, _height) : DesktopLayout.PanelBounds(monitor, scale, _width, _height);
    internal virtual void ApplyAppearance()
    {
        var theme = Environment.Theme;
        Root.RequestedTheme = theme.ElementTheme;
        _material.Apply(this, Frame, Environment);
        Frame.BorderBrush = theme.Edge;
        Frame.BorderThickness = new Thickness(theme.HighContrast ? 1 : 0);
        Frame.CornerRadius = new CornerRadius(theme.HighContrast || IsImmersive ? 0 : MidnightGlassMetrics.FrameRadius);
        _title.Foreground = theme.Brush("NexusText");
        _chrome.SetCorners(theme.HighContrast || IsImmersive, IsImmersive ? 0 : MidnightGlassMetrics.FrameRadius);
        _motion.Refresh();
    }

    protected virtual void OnHidden()
    {
    }

    protected virtual void OnShown()
    {
    }

    protected virtual void OnClosed()
    {
    }

    protected static Button ActionButton(string text, Action action)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(10, 6, 10, 6),
            Style = (Style)Application.Current.Resources["QuietButton"]
        };
        button.Click += (_, _) => action();
        return button;
    }
}
