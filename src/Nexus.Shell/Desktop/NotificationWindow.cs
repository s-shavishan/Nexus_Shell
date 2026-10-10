using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Nexus.Shell.UI;
using Windows.System;

namespace Nexus.Shell.Desktop;

internal sealed class NotificationWindow : Window
{
    private readonly DesktopEnvironment _environment;
    private readonly IntPtr _handle;
    private readonly AppWindow _window;
    private readonly Border _frame;
    private readonly StackPanel _items = new() { Spacing = 10 };
    private readonly TextBlock _title = new() { Text = "Notifications", FontSize = 23, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly TextBlock _subtitle = new() { Text = "Nexus alerts for this session", FontSize = 12 };
    private readonly WindowChrome _chrome;
    private readonly WindowMaterial _material = new();
    private readonly SurfaceMotion _motion;
    private bool _closed;
    internal bool IsOpen { get; private set; }
    internal NotificationWindow(DesktopEnvironment environment)
    {
        _environment = environment;
        var root = new Grid { Padding = new Thickness(20), RowSpacing = 15, KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden };
        root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var header = new Grid(); header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var titles = new StackPanel { Spacing = 5 }; titles.Children.Add(_title); titles.Children.Add(_subtitle); header.Children.Add(titles);
        var close = Button("×", Hide); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(close, "Close notifications"); Grid.SetColumn(close, 1); header.Children.Add(close); root.Children.Add(header);
        var scroll = new ScrollViewer { Content = _items, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetRow(scroll, 1); root.Children.Add(scroll);
        var footer = new Grid { ColumnSpacing = 10 }; footer.ColumnDefinitions.Add(new()); footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var settings = Button("Windows app notifications…", () => environment.OpenTarget("ms-settings:notifications")); footer.Children.Add(settings);
        var clear = Button("Clear", environment.Notifications.Clear); Grid.SetColumn(clear, 1); footer.Children.Add(clear); Grid.SetRow(footer, 2); root.Children.Add(footer);
        _frame = new Border { Child = root, CornerRadius = new CornerRadius(22), BorderThickness = new Thickness(1) }; Content = _frame;
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(this); _window = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_handle)); _window.Title = "Nexus notifications";
        WindowSwitcherPolicy.Request(() => _window.IsShownInSwitchers = false, error => Log.Write("Notification switcher fallback", error));
        if (_window.Presenter is OverlappedPresenter presenter) { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMinimizable = presenter.IsMaximizable = false; }
        ShellLayerInterop.ToolWindow(_handle); _chrome = new(_handle); _chrome.SetCorners(environment.Theme.HighContrast, 22);
        _motion = new(root, () => environment.Theme.Animations && !environment.Theme.HighContrast && !environment.Session.State.ReducedEffects);
        environment.Notifications.Changed += QueueRefresh;
        var escape = new KeyboardAccelerator { Key = VirtualKey.Escape }; escape.Invoked += (_, e) => { Hide(); e.Handled = true; }; root.KeyboardAccelerators.Add(escape);
        Activated += (_, e) => { if (e.WindowActivationState == WindowActivationState.Deactivated) Hide(); };
        Closed += (_, _) => { _closed = true; IsOpen = false; environment.Notifications.Changed -= QueueRefresh; _chrome.Dispose(); _motion.Dispose(); environment.NotificationClosed(this); };
    }
    private static Button Button(string label, Action action)
    { var button = new Button { Content = label, Padding = new Thickness(9, 6, 9, 6), Style = (Style)Application.Current.Resources["QuietButton"] }; button.Click += (_, _) => action(); return button; }
    private void QueueRefresh() { if (!_closed && IsOpen) DispatcherQueue.TryEnqueue(Refresh); }
    private void Refresh()
    {
        if (_closed || !IsOpen) return;
        _environment.Notifications.MarkRead(); var theme = _environment.Theme; _items.Children.Clear();
        var notices = _environment.Notifications.Items;
        if (notices.Count == 0) _items.Children.Add(new TextBlock { Text = "You’re all caught up.\nNexus alerts and recovery messages will appear here.", FontSize = 15, TextWrapping = TextWrapping.Wrap, Foreground = theme.Brush("NexusMuted"), Margin = new Thickness(8, 40, 8, 8) });
        foreach (var notice in notices)
        {
            var body = new StackPanel { Spacing = 9 };
            var row = new Grid(); row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = notice.Title, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = theme.Brush("NexusText"), TextWrapping = TextWrapping.Wrap });
            var dismiss = Button("×", () => _environment.Notifications.Dismiss(notice.Id)); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(dismiss, "Dismiss " + notice.Title); Grid.SetColumn(dismiss, 1); row.Children.Add(dismiss); body.Children.Add(row);
            body.Children.Add(new TextBlock { Text = notice.Message, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = theme.Brush("NexusText") });
            body.Children.Add(new TextBlock { Text = notice.Created.ToLocalTime().ToString("ddd, HH:mm") + " · " + notice.Kind, FontSize = 11, Foreground = theme.Brush("NexusMuted") });
            _items.Children.Add(new Border { Child = body, Padding = new Thickness(14), CornerRadius = new CornerRadius(15), Background = theme.Glass("Card"), BorderBrush = theme.Brush("NexusBorder"), BorderThickness = new Thickness(1) });
        }
    }
    internal void Show()
    { if (_closed) return; ApplyAppearance(); Position(); IsOpen = true; Refresh(); Activate(); _motion.Open(); }
    internal void Hide() { if (_closed || !IsOpen) return; IsOpen = false; _motion.Hide(); _window.Hide(); }
    internal void Position()
    {
        var rect = DesktopLayout.PanelBounds(ShellLayerInterop.Monitor(_environment.Desktop.Handle).Monitor.Bounds, ShellLayerInterop.Scale(_handle), 400, 680);
        ShellLayerInterop.SetWindowPos(_handle, ShellLayerInterop.Topmost, rect.X, rect.Y, rect.Width, rect.Height, 0x0010);
    }
    internal void ApplyAppearance()
    { _material.Apply(this, _frame, _environment); _frame.BorderBrush = _environment.Theme.Brush("NexusBorder"); _title.Foreground = _environment.Theme.Brush("NexusText"); _subtitle.Foreground = _environment.Theme.Brush("NexusMuted"); _chrome.SetCorners(_environment.Theme.HighContrast, 22); _motion.Refresh(); if (IsOpen) Refresh(); }
}
