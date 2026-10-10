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
    private readonly TextBlock _subtitle = new() { Text = "Nexus alert history", FontSize = 12 };
    private readonly WindowChrome _chrome;
    private readonly WindowMaterial _material = new();
    private readonly SurfaceMotion _motion;
    private readonly WindowTransition _transition;
    private readonly Button _all, _issues, _clear;
    private IReadOnlyList<DesktopNotice> _shown = [];
    private string _appearance = "";
    private bool _issuesOnly;
    private int _refreshQueued;
    private bool _closed;
    internal bool IsOpen { get; private set; }
    internal NotificationWindow(DesktopEnvironment environment)
    {
        _environment = environment;
        var root = new Grid { Padding = new Thickness(22), RowSpacing = 16, KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden };
        root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var header = new Grid(); header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var titles = new StackPanel { Spacing = 5 }; titles.Children.Add(_title); titles.Children.Add(_subtitle); header.Children.Add(titles);
        var close = ShellControls.IconButton("\uE8BB", "Close notifications", Hide); Grid.SetColumn(close, 1); header.Children.Add(close); root.Children.Add(header);
        var filters = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        _all = Button("All", () => { _issuesOnly = false; _appearance = ""; Refresh(); }); _issues = Button("Warnings & errors", () => { _issuesOnly = true; _appearance = ""; Refresh(); }); filters.Children.Add(_all); filters.Children.Add(_issues); Grid.SetRow(filters, 1); root.Children.Add(filters);
        var scroll = new ScrollViewer { Content = _items, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetRow(scroll, 2); root.Children.Add(scroll);
        var footer = new Grid { ColumnSpacing = 10 }; footer.ColumnDefinitions.Add(new()); footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var settings = Button("Windows app notifications…", () => environment.OpenTarget("ms-settings:notifications")); footer.Children.Add(settings);
        _clear = Button("Clear all", environment.Notifications.Clear); Grid.SetColumn(_clear, 1); footer.Children.Add(_clear); Grid.SetRow(footer, 3); root.Children.Add(footer);
        _frame = new Border { Child = root, CornerRadius = new CornerRadius(22), BorderThickness = new Thickness(1) }; Content = _frame;
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(this); _window = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_handle)); _window.Title = "Nexus notifications";
        WindowSwitcherPolicy.Request(() => _window.IsShownInSwitchers = false, error => Log.Write("Notification switcher fallback", error));
        if (_window.Presenter is OverlappedPresenter presenter) { presenter.SetBorderAndTitleBar(false, false); presenter.IsResizable = presenter.IsMinimizable = presenter.IsMaximizable = false; }
        ShellLayerInterop.ToolWindow(_handle); _chrome = new(_handle, closeRequested: Hide); _chrome.SetCorners(environment.Theme.HighContrast, 22);
        _motion = new(_frame, () => environment.Theme.Animations && !environment.Theme.HighContrast && !environment.Session.State.ReducedEffects);
        _transition = new(_frame, () => environment.Theme.Animations && !environment.Theme.HighContrast && !environment.Session.State.ReducedEffects);
        environment.Notifications.Changed += QueueRefresh;
        var escape = new KeyboardAccelerator { Key = VirtualKey.Escape }; escape.Invoked += (_, e) => { Hide(); e.Handled = true; }; root.KeyboardAccelerators.Add(escape);
        Activated += (_, e) => { if (e.WindowActivationState == WindowActivationState.Deactivated) Hide(); };
        Closed += (_, _) => { _closed = true; IsOpen = false; environment.Notifications.Changed -= QueueRefresh; _transition.Dispose(); _chrome.Dispose(); _motion.Dispose(); environment.NotificationClosed(this); };
    }
    private static Button Button(string label, Action action)
    { var button = new Button { Content = label, Padding = new Thickness(9, 6, 9, 6), Style = (Style)Application.Current.Resources["QuietButton"] }; button.Click += (_, _) => action(); return button; }
    private void QueueRefresh()
    {
        if (_closed || !IsOpen || Interlocked.Exchange(ref _refreshQueued, 1) != 0) return;
        if (!DispatcherQueue.TryEnqueue(() => { Interlocked.Exchange(ref _refreshQueued, 0); Refresh(); })) Interlocked.Exchange(ref _refreshQueued, 0);
    }
    private void Refresh()
    {
        if (_closed || !IsOpen) return;
        _environment.Notifications.MarkRead(); var theme = _environment.Theme;
        var notices = _environment.Notifications.Items.Where(item => !_issuesOnly || item.Kind != NoticeKind.Information).ToArray();
        string appearance = theme.Palette.Name + "|" + theme.HighContrast + "|" + theme.Simple + "|" + _environment.Session.State.NativeGlass + "|" + _issuesOnly + "|" + DateTime.Now.Date;
        _all.Background = theme.Brush(_issuesOnly ? "NexusInput" : "NexusSelection"); _issues.Background = theme.Brush(_issuesOnly ? "NexusSelection" : "NexusInput");
        _subtitle.Text = notices.Length == 0 ? "A little quieter here." : notices.Length + (notices.Length == 1 ? " saved Nexus alert" : " saved Nexus alerts"); _clear.IsEnabled = _environment.Notifications.Items.Count != 0;
        if (_appearance == appearance && _shown.SequenceEqual(notices)) return; _appearance = appearance; _shown = notices; _items.Children.Clear();
        if (notices.Length == 0)
        {
            var empty = new StackPanel { Spacing = 15, Margin = new Thickness(10, 65, 10, 10), HorizontalAlignment = HorizontalAlignment.Center };
            empty.Children.Add(ShellControls.Badge("\uE73E", theme, 54));
            empty.Children.Add(new TextBlock { Text = _issuesOnly ? "Everything looks quiet" : "You’re all caught up", FontSize = 18, Foreground = theme.Brush("NexusText"), TextAlignment = TextAlignment.Center });
            empty.Children.Add(new TextBlock { Text = "Nexus alerts and recovery messages\nwill be saved here.", FontSize = 12, Foreground = theme.Brush("NexusMuted"), TextAlignment = TextAlignment.Center }); _items.Children.Add(empty);
        }
        string group = ""; var now = DateTimeOffset.Now;
        foreach (var notice in notices)
        {
            string next = DesktopPresentation.AlertGroup(notice.Created, now);
            if (next != group) { group = next; _items.Children.Add(new TextBlock { Text = group, FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = theme.Brush("NexusMuted"), Margin = new Thickness(3, 8, 0, 0) }); }
            var body = new StackPanel { Spacing = 9 };
            var row = new Grid { ColumnSpacing = 9 }; row.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.Children.Add(ShellControls.Badge(notice.Kind == NoticeKind.Error ? "\uEA39" : notice.Kind == NoticeKind.Warning ? "\uE7BA" : "\uE946", theme, 27));
            var title = new TextBlock { Text = notice.Title, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = theme.Brush("NexusText"), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(title, 1); row.Children.Add(title);
            var dismiss = ShellControls.IconButton("\uE8BB", "Dismiss " + notice.Title, () => _environment.Notifications.Dismiss(notice.Id)); Grid.SetColumn(dismiss, 2); row.Children.Add(dismiss); body.Children.Add(row);
            body.Children.Add(new TextBlock { Text = notice.Message, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = theme.Brush("NexusText") });
            body.Children.Add(new TextBlock { Text = notice.Created.ToLocalTime().ToString("ddd, HH:mm") + " · " + notice.Kind, FontSize = 11, Foreground = theme.Brush("NexusMuted") });
            _items.Children.Add(new Border { Child = body, Padding = new Thickness(14), CornerRadius = new CornerRadius(theme.HighContrast ? 0 : 15), Background = theme.Material("Card", _environment.Session.State.NativeGlass), BorderBrush = theme.Edge, BorderThickness = new Thickness(1) });
        }
    }
    internal void Show()
    { if (_closed) return; ApplyAppearance(); Position(); _transition.Cancel(); _frame.IsHitTestVisible = true; IsOpen = true; Refresh(); Activate(); _motion.Open(); }
    internal async void Hide() { if (_closed || !IsOpen) return; IsOpen = false; _frame.IsHitTestVisible = false; await _transition.CloseAsync(() => { _motion.Hide(); _window.Hide(); }); }
    internal void Position()
    {
        var rect = DesktopLayout.PanelBounds(ShellLayerInterop.Monitor(_environment.Desktop.Handle).Monitor.Bounds, ShellLayerInterop.Scale(_handle), 400, 680);
        ShellLayerInterop.SetWindowPos(_handle, ShellLayerInterop.Topmost, rect.X, rect.Y, rect.Width, rect.Height, 0x0010);
    }
    internal void ApplyAppearance()
    { _material.Apply(this, _frame, _environment); _frame.BorderBrush = _environment.Theme.Edge; _frame.CornerRadius = new CornerRadius(_environment.Theme.HighContrast ? 0 : 22); _title.Foreground = _environment.Theme.Brush("NexusText"); _subtitle.Foreground = _environment.Theme.Brush("NexusMuted"); _chrome.SetCorners(_environment.Theme.HighContrast, 22); _motion.Refresh(); if (IsOpen) Refresh(); }
}
