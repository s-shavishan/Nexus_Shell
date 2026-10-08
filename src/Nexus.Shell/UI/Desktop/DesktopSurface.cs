using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Desktop;
using Nexus.Shell.Services;
using Windows.System;
using Shapes = Microsoft.UI.Xaml.Shapes;

namespace Nexus.Shell.UI.Desktop;

internal sealed class DesktopSurface : Grid
{
    private readonly DesktopEnvironment _environment;
    private readonly Viewbox _wallpaper = new() { Stretch = Stretch.UniformToFill, IsHitTestVisible = false };
    private readonly GridView _icons;
    private readonly InfoBar _status = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(20), MaxWidth = 540 };
    private readonly List<Shapes.Path> _ribbons = [];
    private bool _refreshing;
    internal DesktopSurface(DesktopEnvironment environment)
    {
        _environment = environment;
        var canvas = new Canvas { Width = 1600, Height = 1000 };
        foreach (string path in new[] {
            "M -200,-150 H 1800 V 1150 H -200 Z",
            "M -200,-120 H 1800 V 280 C 1300,120 980,580 610,320 C 330,100 80,500 -200,370 Z",
            "M -180,730 C 130,470 190,110 620,350 C 1000,560 1120,890 1780,330 L 1780,1120 H -180 Z",
            "M -200,890 C 410,1000 590,510 1040,600 C 1410,675 1540,530 1780,680 L 1780,1150 H -200 Z" })
        {
            var shape = (Shapes.Path)XamlReader.Load("<Path xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Data='" + path + "'/>");
            _ribbons.Add(shape); canvas.Children.Add(shape);
        }
        _wallpaper.Child = canvas; Children.Add(_wallpaper);
        _icons = new GridView { Margin = new Thickness(20), SelectionMode = ListViewSelectionMode.Single,
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
        Children.Add(_icons); Children.Add(_status);
        var enter = new KeyboardAccelerator { Key = VirtualKey.K, Modifiers = VirtualKeyModifiers.Control };
        enter.Invoked += (_, args) => { environment.ShowMenu(true); args.Handled = true; }; KeyboardAccelerators.Add(enter);
        ApplyAppearance();
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
        _wallpaper.Visibility = theme.HighContrast || _environment.Session.State.ReducedEffects ? Visibility.Collapsed : Visibility.Visible;
        Background = theme.HighContrast ? theme.Brush("NexusPanel") : ShellTheme.Gradient(theme.Palette.Canvas, theme.Palette.WallpaperEnd);
        string[] colors = WallpaperColors(theme.Palette.Name);
        _ribbons[0].Fill = ShellTheme.Gradient(colors[0], colors[3]);
        for (int i = 1; i < 4; i++) _ribbons[i].Fill = ShellTheme.Gradient(colors[i - 1], colors[i]);
    }
    internal static string[] WallpaperColors(string name) => name switch
    {
        "Solstice" => ["FFFFD3AD", "FFD68267", "FF9E5B71", "FF493251"],
        "Ember" => ["FFCD683C", "FFAF3546", "FF682637", "FF271F3E"],
        "Opal" => ["FFECD9F8", "FFB0B7EA", "FF9BBACF", "FF576CAB"],
        "Lagoon" => ["FF547C82", "FF315F66", "FF28425D", "FF102D37"],
        "Graphite" => ["FF6A7690", "FF414C6A", "FF293B51", "FF171E36"],
        _ => ["FF7D668D", "FF515A84", "FF37526D", "FF1F2B48"]
    };
    internal void Report(string message) { _status.Message = message; _status.Severity = InfoBarSeverity.Error; _status.IsOpen = true; }
}
