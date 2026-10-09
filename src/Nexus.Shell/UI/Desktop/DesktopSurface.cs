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
    private readonly Border _menuBar = new() { Height = 40, VerticalAlignment = VerticalAlignment.Top, BorderThickness = new Thickness(0, 0, 0, 1) };
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
        _icons = new GridView { Margin = new Thickness(20, 58, 20, 20), SelectionMode = ListViewSelectionMode.Single,
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
        Children.Add(_icons); Children.Add(_menuBar); Children.Add(_status);
        _clockTimer.Tick += (_, _) => RefreshClock();
        Loaded += (_, _) => { RefreshClock(); _clockTimer.Start(); };
        Unloaded += (_, _) => _clockTimer.Stop();
        var enter = new KeyboardAccelerator { Key = VirtualKey.K, Modifiers = VirtualKeyModifiers.Control };
        enter.Invoked += (_, args) => { environment.ShowMenu(true); args.Handled = true; }; KeyboardAccelerators.Add(enter);
        ApplyAppearance();
    }
    private void RefreshClock()
    {
        _menuClock.Text = DateTime.Now.ToString(_environment.Session.State.Clock24Hour ? "ddd, d MMM  HH:mm" : "ddd, d MMM  h:mm tt");
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
        Add("◈  NEXUS", "Open Nexus launcher", () => _environment.ShowMenu(), true);
        Add("Files", "Open file browser", () => _environment.ShowFiles());
        Add("Explore", "Open Explore workspace", () => _environment.ShowSections("Explore"));
        Add("Windows", "Show open windows", _environment.ShowWindowOverview);
        Add("View", "Personalize desktop", () => _environment.ShowSections("Personalize"));
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
        _wallpaper.Visibility = theme.HighContrast || _environment.Session.State.ReducedEffects ? Visibility.Collapsed : Visibility.Visible;
        Background = theme.Surface("Canvas");
        if (_wallpaperName != theme.Palette.Name)
        {
            _wallpaper.Source = new BitmapImage(new Uri("ms-appx:///Assets/Wallpapers/" + theme.Palette.Name + ".png"));
            _wallpaperName = theme.Palette.Name;
        }
    }
    internal void Report(string message) { _status.Message = message; _status.Severity = InfoBarSeverity.Error; _status.IsOpen = true; }
}
