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
    private readonly TextBlock _clock = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _badge = new() { FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _notification;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(15) };
    private readonly Grid _controls = new() { Width = 17, Height = 17 };
    private readonly List<Button> _buttons = [];
    internal MenuBarView(DesktopEnvironment environment)
    {
        _environment = environment; Padding = new Thickness(15, 0, 15, 0); ColumnSpacing = 8;
        ColumnDefinitions.Add(new()); ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        void Menu(string text, MenuFlyout flyout, bool bold = false)
        { var button = Basic(text, text + " menu"); button.FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal; button.Flyout = flyout; left.Children.Add(button); }
        Menu("◈", environment.Menus.NexusMenu()); Menu("Desktop", environment.Menus.NexusMenu(), true);
        Menu("File", environment.Menus.FileMenu()); Menu("View", environment.Menus.ViewMenu()); Menu("Window", environment.Menus.WindowMenu());
        var help = Basic("Help", "Nexus help and personalization"); help.Click += (_, _) => environment.ShowSections("Personalize"); left.Children.Add(help); Children.Add(left);
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        Button Icon(string glyph, string name, Action action)
        { var button = Basic(new FontIcon { Glyph = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 }, name); button.Width = 31; button.Click += (_, _) => action(); right.Children.Add(button); return button; }
        Icon("\uE701", "Network connections", () => environment.OpenTarget("ms-settings:network-status"));
        Icon("\uE767", "Sound and Control Center", environment.ShowQuickSettings);
        Icon("\uE721", "Search apps", () => environment.ShowMenu(true));
        var controls = _controls;
        for (int i = 0; i < 2; i++)
        { controls.Children.Add(new Border { Height = 2, Background = new SolidColorBrush(Microsoft.UI.Colors.White), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, i * 8 + 4, 0, 0), CornerRadius = new CornerRadius(1) }); controls.Children.Add(new Ellipse { Width = 5, Height = 5, Fill = new SolidColorBrush(Microsoft.UI.Colors.White), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(i == 0 ? 3 : 10, i * 8 + 2.5, 0, 0) }); }
        var control = Basic(controls, "Control Center"); control.Width = 32; control.Click += (_, _) => environment.ShowQuickSettings(); right.Children.Add(control);
        var bell = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 }; bell.Children.Add(new FontIcon { Glyph = "\uE7E7", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 }); bell.Children.Add(_badge);
        _notification = Basic(bell, "Nexus notifications"); _notification.Click += (_, _) => environment.ShowNotifications(); right.Children.Add(_notification);
        var date = Basic(_clock, "Date and notifications"); date.Click += (_, _) => environment.ShowNotifications(); right.Children.Add(date);
        Grid.SetColumn(right, 1); Children.Add(right);
        environment.Notifications.Changed += QueueRefresh; _timer.Tick += (_, _) => Refresh(); _timer.Start(); ApplyAppearance();
        SizeChanged += (_, _) =>
        {
            int shown = ActualWidth < 480 ? 1 : ActualWidth < 820 ? 2 : int.MaxValue;
            foreach (var (button, i) in left.Children.OfType<Button>().Select((button, i) => (button, i))) button.Visibility = i < shown ? Visibility.Visible : Visibility.Collapsed;
            foreach (var button in right.Children.OfType<Button>().Take(2)) button.Visibility = ActualWidth < 650 ? Visibility.Collapsed : Visibility.Visible;
            Refresh();
        };
    }
    private Button Basic(object content, string name)
    {
        var button = new Button { Content = content, FontSize = 13, MinHeight = 28, Height = 30, CornerRadius = new CornerRadius(5), Padding = new Thickness(9, 0, 9, 0), Style = (Style)Application.Current.Resources["QuietButton"] };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, name); ToolTipService.SetToolTip(button, name); _buttons.Add(button); return button;
    }
    private void QueueRefresh() => DispatcherQueue.TryEnqueue(Refresh);
    internal void Refresh()
    {
        _clock.Text = DateTime.Now.ToString(ActualWidth < 650 ? "HH:mm" : _environment.Session.State.Clock24Hour ? "ddd d MMM  HH:mm" : "ddd d MMM  h:mm tt");
        int unread = _environment.Notifications.Unread; _badge.Text = unread > 0 ? unread.ToString() : "";
        _badge.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_notification, unread + " unread Nexus notifications");
    }
    internal void ApplyAppearance()
    { RequestedTheme = _environment.Theme.ElementTheme; foreach (var button in _buttons) button.Foreground = _environment.Theme.Brush("NexusText"); foreach (var line in _controls.Children.OfType<Border>()) line.Background = _environment.Theme.Brush("NexusText"); foreach (var dot in _controls.Children.OfType<Ellipse>()) dot.Fill = _environment.Theme.Brush("NexusText"); _clock.Foreground = _environment.Theme.Brush("NexusText"); _badge.Foreground = _environment.Theme.Brush("NexusAccent"); Refresh(); }
    public void Dispose() { _timer.Stop(); _environment.Notifications.Changed -= QueueRefresh; }
}
