using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Nexus.Shell.Desktop;
using Nexus.Shell.Services;
using Windows.System;

namespace Nexus.Shell.UI.Desktop;

internal sealed class DesktopSurface : Grid
{
    private readonly DesktopEnvironment _environment;
    private readonly Image _wallpaper = new() { Stretch = Stretch.UniformToFill, IsHitTestVisible = false };
    private readonly GridView _icons;
    private readonly Border _menuBar = new() { Height = 32, VerticalAlignment = VerticalAlignment.Top, BorderThickness = new Thickness(0, 0, 0, 1) };
    private readonly Border _calendar = new() { Width = 322, Padding = new Thickness(16), CornerRadius = new CornerRadius(21), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(20, 20, 20, 106) };
    private readonly TextBlock _weekday = new() { FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly TextBlock _day = new() { FontSize = 42 };
    private readonly TextBlock _month = new() { FontSize = 12 };
    private readonly StackPanel _tasks = new() { Spacing = 10 };
    private (string Palette, bool Contrast, bool Focus, bool Visible, string Tasks)? _calendarContent;
    private readonly TextBlock _menuClock = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly InfoBar _status = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(20), MaxWidth = 540 };
    private string? _wallpaperName;
    private bool _refreshing;
    internal DesktopSurface(DesktopEnvironment environment)
    {
        _environment = environment;
        Children.Add(_wallpaper);
        BuildMenuBar();
        BuildCalendar();
        _icons = new GridView { Margin = new Thickness(20, 48, 20, 100), SelectionMode = ListViewSelectionMode.Single,
            IsItemClickEnabled = false, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Stretch };
        _icons.ItemsPanel = (ItemsPanelTemplate)XamlReader.Load("<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><ItemsWrapGrid Orientation='Vertical' ItemWidth='108' ItemHeight='112'/></ItemsPanelTemplate>");
        _icons.ItemTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <StackPanel Width="96" Padding="4,8" Spacing="7" AutomationProperties.Name="{Binding Name}">
                <Image Width="58" Height="58" Source="{Binding IconUri}" HorizontalAlignment="Center"/>
                <Border Background="{ThemeResource NexusDesktopLabel}" Padding="5,3,5,3" CornerRadius="5">
                  <TextBlock Text="{Binding Name}" Foreground="{ThemeResource NexusDesktopText}" FontSize="12" TextAlignment="Center" TextWrapping="Wrap" MaxLines="2" TextTrimming="CharacterEllipsis"/>
                </Border>
              </StackPanel>
            </DataTemplate>
            """);
        _icons.DoubleTapped += OpenDoubleTapped;
        _icons.KeyDown += (_, args) => { if (args.Key == VirtualKey.Enter && _icons.SelectedItem is DesktopShortcut item) { environment.OpenDesktopItem(item); args.Handled = true; } };
        _icons.RightTapped += (_, args) =>
        {
            var item = ItemAt(args.OriginalSource);
            if (item is null) return;
            environment.Menus.DesktopItemMenu(item).ShowAt(_icons, new FlyoutShowOptions { Position = args.GetPosition(_icons) }); args.Handled = true;
        };
        RightTapped += (_, args) =>
        {
            if (ItemAt(args.OriginalSource) is not null) return;
            _icons.SelectedItem = null;
            environment.Menus.DesktopMenu().ShowAt(this, new FlyoutShowOptions { Position = args.GetPosition(this) }); args.Handled = true;
        };
        Children.Add(_icons); Children.Add(_calendar); Children.Add(_menuBar); Children.Add(_status);
        _clockTimer.Tick += (_, _) => RefreshClock();
        Loaded += (_, _) => { RefreshClock(); _clockTimer.Start(); };
        Unloaded += (_, _) => _clockTimer.Stop();
        var enter = new KeyboardAccelerator { Key = VirtualKey.K, Modifiers = VirtualKeyModifiers.Control };
        enter.Invoked += (_, args) => { environment.ShowMenu(true); args.Handled = true; }; KeyboardAccelerators.Add(enter);
        ApplyAppearance();
        RefreshContent();
    }
    private void RefreshClock()
    {
        _menuClock.Text = DateTime.Now.ToString(_environment.Session.State.Clock24Hour ? "ddd, d MMM  HH:mm" : "ddd, d MMM  h:mm tt");
        _weekday.Text = DateTime.Now.ToString("dddd"); _day.Text = DateTime.Now.ToString("d"); _month.Text = DateTime.Now.ToString("MMM yyyy");
    }
    private void BuildMenuBar()
    {
        var menu = new Grid { Padding = new Thickness(19, 0, 21, 0) };
        menu.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        menu.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        void Add(string label, string accessible, Action action, bool bold = false)
        {
            var button = new Button { Content = label, Style = (Style)Application.Current.Resources["QuietButton"],
                FontWeight = bold ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                FontSize = 12, CornerRadius = new CornerRadius(9), Padding = new Thickness(11, 5, 11, 5), MinHeight = 28 };
            button.Click += (_, _) => action();
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, accessible);
            left.Children.Add(button);
        }
        void Menu(string label, MenuFlyout flyout)
        {
            var button = new Button { Content = label, Flyout = flyout, FontSize = 12, Padding = new Thickness(9, 3, 9, 3), MinHeight = 26, CornerRadius = new CornerRadius(6), Style = (Style)Application.Current.Resources["QuietButton"] };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, label + " menu"); left.Children.Add(button);
        }
        Menu("◈", _environment.Menus.NexusMenu());
        Add("Desktop", "Show Nexus desktop", _environment.ShowDesktop, true);
        Menu("File", _environment.Menus.FileMenu());
        Menu("View", _environment.Menus.ViewMenu());
        Menu("Window", _environment.Menus.WindowMenu());
        Add("Help", "Open Nexus settings and help", () => _environment.ShowSections("Personalize"));
        menu.Children.Add(left);
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, VerticalAlignment = VerticalAlignment.Center };
        var settings = new Button { Content = new FontIcon { Glyph = "\uE713", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 15 },
            Width = 30, Height = 29, Style = (Style)Application.Current.Resources["QuietButton"], Padding = new Thickness(0) };
        settings.Click += (_, _) => _environment.ShowQuickSettings();
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(settings, "Nexus Control Center");
        right.Children.Add(settings); right.Children.Add(_menuClock);
        Grid.SetColumn(right, 1); menu.Children.Add(right);
        _menuBar.Child = menu;
    }
    private void BuildCalendar()
    {
        var body = new Grid { ColumnSpacing = 16 }; body.ColumnDefinitions.Add(new() { Width = new GridLength(86) }); body.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        var date = new StackPanel(); date.Children.Add(_weekday); date.Children.Add(_day); date.Children.Add(_month); body.Children.Add(date);
        Grid.SetColumn(_tasks, 1); body.Children.Add(_tasks); _calendar.Child = body;
        _calendar.Tapped += (_, _) => _environment.ShowSections("Study");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_calendar, "Today's date and tasks. Open Study.");
    }
    internal void RefreshContent()
    {
        var tasks = _environment.Session.State.Tasks.Where(t => !t.Completed).Take(3).ToArray();
        var content = (_environment.Theme.Palette.Name, _environment.Theme.HighContrast, _environment.Session.State.FocusMode, _environment.Session.State.ShowClockWidget, string.Join("\0", tasks.Select(t => t.Id + ":" + t.Title)));
        if (_calendarContent == content) return; _calendarContent = content;
        _icons.Visibility = _environment.Session.State.FocusMode ? Visibility.Collapsed : Visibility.Visible;
        _calendar.Visibility = _environment.Session.State.ShowClockWidget && !_environment.Session.State.FocusMode ? Visibility.Visible : Visibility.Collapsed;
        _tasks.Children.Clear();
        if (tasks.Length == 0) _tasks.Children.Add(new TextBlock { Text = "A little space for today.\nOpen Study to add a task.", FontSize = 12, Foreground = _environment.Theme.Brush("NexusMuted"), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
        foreach (var task in tasks)
        {
            var line = new Grid { ColumnSpacing = 10 }; line.ColumnDefinitions.Add(new() { Width = new GridLength(4) }); line.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            line.Children.Add(new Border { Width = 4, Height = 25, CornerRadius = new CornerRadius(2), Background = _environment.Theme.Brush("NexusAccent") });
            var text = new TextBlock { Text = task.Title, FontSize = 12, Foreground = _environment.Theme.Brush("NexusText"), MaxLines = 2, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis }; Grid.SetColumn(text, 1); line.Children.Add(text); _tasks.Children.Add(line);
        }
        RefreshClock();
    }
    private DesktopShortcut? ItemAt(object source)
    {
        var element = source as DependencyObject;
        while (element is not null && !ReferenceEquals(element, _icons))
        { if (element is GridViewItem container) return container.Content as DesktopShortcut; element = VisualTreeHelper.GetParent(element); }
        return null;
    }
    private void OpenDoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
    { if (ItemAt(args.OriginalSource) is DesktopShortcut item) { _environment.OpenDesktopItem(item); args.Handled = true; } }
    internal void SetWorkArea(double bottomInset) => Padding = new Thickness(0, 0, 0, Math.Max(0, bottomInset));
    internal async Task RefreshAsync()
    {
        if (_refreshing || _environment.IsStopping) return; _refreshing = true;
        try
        {
            var items = await Task.Run(DesktopCatalog.Read);
            if (_environment.IsStopping) return;
            string? selected = (_icons.SelectedItem as DesktopShortcut)?.Id;
            _icons.ItemsSource = items; _icons.SelectedItem = items.FirstOrDefault(i => i.Id == selected);
        }
        catch (Exception ex) { _environment.Report("Could not refresh desktop icons", ex); }
        finally { _refreshing = false; }
    }
    internal void ApplyAppearance()
    {
        var theme = _environment.Theme; RequestedTheme = theme.ElementTheme;
        _menuBar.Background = theme.Surface("Dock"); _menuBar.BorderBrush = theme.Brush("NexusBorder");
        _menuClock.Foreground = theme.Brush("NexusText");
        _calendar.Background = theme.Surface("Sidebar"); _calendar.BorderBrush = theme.Brush("NexusBorder"); _weekday.Foreground = theme.Brush("NexusAccent"); _day.Foreground = theme.Brush("NexusText"); _month.Foreground = theme.Brush("NexusMuted");
        _wallpaper.Visibility = theme.HighContrast || _environment.Session.State.ReducedEffects ? Visibility.Collapsed : Visibility.Visible;
        Background = theme.Surface("Canvas");
        if (_wallpaperName != theme.Palette.Name)
        {
            _wallpaper.Source = new BitmapImage(new Uri("ms-appx:///Assets/Wallpapers/" + theme.Palette.Name + ".png"));
            _wallpaperName = theme.Palette.Name;
        }
        RefreshContent();
    }
    internal void Report(string message) { _status.Message = message; _status.Severity = InfoBarSeverity.Error; _status.IsOpen = true; }
}
