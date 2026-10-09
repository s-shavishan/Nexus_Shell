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
    private readonly InfoBar _status = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(20), MaxWidth = 540 };
    private string? _wallpaperName;
    private bool _refreshing;
    internal DesktopSurface(DesktopEnvironment environment)
    {
        _environment = environment;
        Children.Add(_wallpaper);
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
        if (_wallpaperName != theme.Palette.Name)
        {
            _wallpaper.Source = new BitmapImage(new Uri("ms-appx:///Assets/Wallpapers/" + theme.Palette.Name + ".png"));
            _wallpaperName = theme.Palette.Name;
        }
    }
    internal void Report(string message) { _status.Message = message; _status.Severity = InfoBarSeverity.Error; _status.IsOpen = true; }
}
