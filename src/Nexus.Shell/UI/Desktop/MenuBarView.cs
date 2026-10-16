// නමෝ බුද්ධාය 🤍
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Nexus.Shell.Desktop;
using Nexus.Shell.Services;

namespace Nexus.Shell.UI.Desktop;
internal sealed class MenuBarView : Grid, IDisposable
{
    private readonly DesktopEnvironment _environment;
    private readonly TextBlock _clock = new()
    {
        FontSize = 12.5,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly Button _notification;
    private readonly Button _bluetooth;
    private readonly TextBlock _inputLabel = new()
    {
        FontSize = 12,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly Button _inputLanguage, _awake;
    private readonly DispatcherTimer _timer = new()
    {
        Interval = TimeSpan.FromSeconds(15)
    };
    private readonly Grid _controls = new()
    {
        Width = 17,
        Height = 17
    };
    private readonly List<Button> _buttons = [];
    private readonly TextBlock _activeTitle = new()
    {
        Text = "Desktop",
        FontSize = 13,
        MaxWidth = 210,
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly Button _activeApp;
    private readonly StackPanel _windowControls = new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 3,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(8, 0, 8, 0)
    };
    private readonly Button _minimize, _maximize, _close;
    private bool _disposed;
    private int _refreshQueued;
    internal MenuBarView(DesktopEnvironment environment)
    {
        _environment = environment;
        Padding = new Thickness(18, 0, 16, 0);
        ColumnSpacing = 12;
        KeyboardAcceleratorPlacementMode = Microsoft.UI.Xaml.Input.KeyboardAcceleratorPlacementMode.Hidden;
        ColumnDefinitions.Add(new());
        ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center
        };
        void Menu(string text, MenuFlyout flyout, bool bold = false)
        {
            var button = Basic(text, text + " menu");
            button.FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
            button.Flyout = flyout;
            left.Children.Add(button);
        }

        var nexus = Basic(NexusIcons.Image("Launchpad", 19), "Nexus menu");
        nexus.Width = 34;
        nexus.Padding = new Thickness(5, 0, 5, 0);
        nexus.Flyout = environment.Menus.NexusMenu();
        left.Children.Add(nexus);
        _activeApp = Basic(_activeTitle, "Active window");
        _activeApp.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        _activeTitle.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        var activeMenu = environment.Menus.ActiveWindowMenu();
        _activeApp.Flyout = activeMenu;
        left.Children.Add(_activeApp);
        Menu("File", environment.Menus.FileMenu());
        Menu("View", environment.Menus.ViewMenu());
        Menu("Window", environment.Menus.WindowMenu());
        var help = Basic("Help", "Nexus desktop shortcuts and tools");
        help.Click += (_, _) => environment.ShowDesktopGuide();
        left.Children.Add(help);
        Children.Add(left);
        Button WindowControl(string glyph, string name, Action action)
        {
            var button = Basic(ShellControls.Icon(glyph, 10), name);
            button.Width = 26;
            button.Padding = new Thickness(0);
            button.Height = button.MinHeight = 24;
            button.Click += (_, _) => action();
            _windowControls.Children.Add(button);
            return button;
        }

        _minimize = WindowControl("\uE921", "Minimize active window", () =>
        {
            if (environment.ActiveWindow is { } window)
                environment.MinimizeDockWindow(window);
        });
        _maximize = WindowControl("\uE922", "Maximize or restore active window", () =>
        {
            if (environment.ActiveWindow is { } window)
                environment.MaximizeDockWindow(window);
        });
        _close = WindowControl("\uE8BB", "Close active window", () =>
        {
            if (environment.ActiveWindow is { } window)
                environment.CloseDockWindow(window);
        });
        left.Children.Add(_windowControls);
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center
        };
        Button Icon(string glyph, string name, Action action)
        {
            var button = Basic(new FontIcon { Glyph = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 }, name);
            button.Width = 31;
            button.Click += (_, _) => action();
            right.Children.Add(button);
            return button;
        }

        Button QuickControl(string glyph, string section)
        {
            Button? button = null;
            button = Icon(glyph, section + " controls", () => environment.ShowQuickControl(section, environment.MenuControlAnchor(button!)));
            button.ContextFlyout = environment.Menus.SystemControlMenu(section);
            return button;
        }

        QuickControl("\uE701", "Network");
        QuickControl("\uE767", "Sound");
        Icon("\uE721", "Nexus Search · Ctrl+Alt+Space", environment.ShowSearch);
        _bluetooth = QuickControl("\uE702", "Bluetooth");
        var display = QuickControl("\uE7F4", "Display");
        var controls = _controls;
        for (int i = 0; i < 2; i++)
        {
            controls.Children.Add(new Border { Height = 2, Background = new SolidColorBrush(Microsoft.UI.Colors.White), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, i * 8 + 4, 0, 0), CornerRadius = new CornerRadius(1) });
            controls.Children.Add(new Ellipse { Width = 5, Height = 5, Fill = new SolidColorBrush(Microsoft.UI.Colors.White), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(i == 0 ? 3 : 10, i * 8 + 2.5, 0, 0) });
        }

        var control = Basic(controls, "Control Center");
        control.Width = 32;
        control.Click += (_, _) => environment.ShowQuickSettings();
        right.Children.Add(control);
        _inputLanguage = Basic(_inputLabel, "Input language");
        _inputLanguage.Flyout = environment.Menus.InputLanguageMenu();
        right.Children.Add(_inputLanguage);
        _awake = Basic(new FontIcon { Glyph = "\uE708", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 }, "Keep awake");
        _awake.Width = 31;
        _awake.Click += (_, _) => environment.ShowWindowsTools();
        right.Children.Add(_awake);
        _notification = Basic(new FontIcon { Glyph = "\uE7E7", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 }, "Windows notifications");
        _notification.ContextFlyout = environment.Menus.NotificationsMenu();
        _notification.Click += (_, _) => environment.OpenWindowsSurface(WindowsShellSurface.Notifications);
        right.Children.Add(_notification);
        var date = Basic(_clock, "Windows date and time");
        date.ContextFlyout = environment.Menus.ClockMenu();
        date.Click += (_, _) => environment.OpenWindowsSurface(WindowsShellSurface.Calendar);
        right.Children.Add(date);
        ContextFlyout = environment.Menus.MenuBarMenu();
        Grid.SetColumn(right, 1);
        Children.Add(right);
        environment.ActiveWindowChanged += QueueRefresh;
        environment.SystemStatusChanged += QueueRefresh;
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        ApplyAppearance();
        SizeChanged += (_, _) =>
        {
            int shown = ActualWidth < 480 ? 1 : ActualWidth < 950 ? 2 : int.MaxValue;
            foreach (var(button, i)in left.Children.OfType<Button>().Select((button, i) => (button, i)))
                SetVisible(button, i < shown);
            foreach (var button in right.Children.OfType<Button>().Take(2))
                SetVisible(button, ActualWidth >= 650);
            SetVisible(_bluetooth, ActualWidth >= 900 && BluetoothRadioAvailability.ShowControls(BluetoothRadioAvailability.Detect()));
            SetVisible(display, ActualWidth >= 900);
            Refresh();
        };
    }

    private static void SetVisible(UIElement element, bool visible)
    {
        var next = visible ? Visibility.Visible : Visibility.Collapsed;
        if (element.Visibility != next)
            element.Visibility = next;
    }

    private static void SetText(TextBlock label, string value)
    {
        if (label.Text != value)
            label.Text = value;
    }

    private static void SetName(DependencyObject element, string value)
    {
        if (Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(element) != value)
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(element, value);
    }

    private Button Basic(object content, string name)
    {
        var button = new Button
        {
            Content = content,
            FontSize = 13,
            MinHeight = 30,
            Height = 30,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 0, 10, 0),
            Style = (Style)Application.Current.Resources["QuietButton"]
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, name);
        ToolTipService.SetToolTip(button, name);
        _buttons.Add(button);
        return button;
    }

    private void QueueRefresh()
    {
        if (_disposed || Interlocked.Exchange(ref _refreshQueued, 1) != 0)
            return;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            Interlocked.Exchange(ref _refreshQueued, 0);
            if (!_disposed)
                Refresh();
        }))
            Interlocked.Exchange(ref _refreshQueued, 0);
    }

    internal void Refresh(DateTime? clockOverride = null)
    {
        if (_disposed)
            return;
        var window = _environment.ActiveWindow;
        SetText(_activeTitle, window is null ? "Desktop" : window.Title);
        string name = window is null ? "Nexus desktop menu" : "Window controls for " + window.Title;
        SetName(_activeApp, name);
        if (!Equals(ToolTipService.GetToolTip(_activeApp), name))
            ToolTipService.SetToolTip(_activeApp, name);
        SetVisible(_windowControls, window is not null && ActualWidth >= 1050);
        bool hasWindow = window is not null;
        if (_minimize.IsEnabled != hasWindow)
            _minimize.IsEnabled = hasWindow;
        if (_maximize.IsEnabled != hasWindow)
            _maximize.IsEnabled = hasWindow;
        if (_close.IsEnabled != hasWindow)
            _close.IsEnabled = hasWindow;
        var icon = (FontIcon)_maximize.Content;
        string glyph = _environment.ActiveWindowMaximized ? "\uE923" : "\uE922";
        if (icon.Glyph != glyph)
            icon.Glyph = glyph;
        SetText(_clock, (clockOverride ?? DateTime.Now).ToString(DesktopPresentation.ClockFormat(ActualWidth < 650, _environment.Session.State.Clock24Hour)));
        SetName(_notification, _environment.WindowsRoute(WindowsShellSurface.Notifications).Name);
        SetVisible(_inputLanguage, ActualWidth >= 750);
        try
        {
            var input = _environment.CurrentInputLanguage();
            SetText(_inputLabel, input.Code);
            SetName(_inputLanguage, "Input language · " + input.Name);
            if (!Equals(ToolTipService.GetToolTip(_inputLanguage), input.Name))
                ToolTipService.SetToolTip(_inputLanguage, input.Name);
        }
        catch
        {
            SetText(_inputLabel, "—");
        }

        SetVisible(_bluetooth, ActualWidth >= 900 && BluetoothRadioAvailability.ShowControls(BluetoothRadioAvailability.Detect()));
        SetVisible(_awake, _environment.KeepAwake.Active && ActualWidth >= 650);
        string awake = _environment.KeepAwake.Status;
        SetName(_awake, awake);
        if (!Equals(ToolTipService.GetToolTip(_awake), awake))
            ToolTipService.SetToolTip(_awake, awake);
    }

    internal void ApplyAppearance()
    {
        if (_disposed)
            return;
        var theme = _environment.Theme;
        if (RequestedTheme != theme.ElementTheme)
            RequestedTheme = theme.ElementTheme;
        var foreground = theme.Brush("NexusText");
        foreach (var button in _buttons)
            if (!ReferenceEquals(button.Foreground, foreground))
                button.Foreground = foreground;
        foreach (var line in _controls.Children.OfType<Border>())
            if (!ReferenceEquals(line.Background, foreground))
                line.Background = foreground;
        foreach (var dot in _controls.Children.OfType<Ellipse>())
            if (!ReferenceEquals(dot.Fill, foreground))
                dot.Fill = foreground;
        if (!ReferenceEquals(_clock.Foreground, foreground))
            _clock.Foreground = foreground;
        if (!ReferenceEquals(_inputLabel.Foreground, foreground))
            _inputLabel.Foreground = foreground;
        Refresh();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _timer.Stop();
        _environment.ActiveWindowChanged -= QueueRefresh;
        _environment.SystemStatusChanged -= QueueRefresh;
    }
}
