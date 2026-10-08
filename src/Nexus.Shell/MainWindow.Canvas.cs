using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Services;
using Nexus.Shell.UI;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    private bool IsDesktopCanvas => _page == "Home" && _state.DesktopLayout && !_highContrast;
    private void RenderDesktopCanvas()
    {
        if (!_ready) return;
        DesktopProfileButton.Content = ActiveProfile.Name + "  ⌄";
        DesktopWorkspaceDescription.Text = ActiveProfile.Description;
        DesktopWorkspaceCount.Text = ActiveProfile.Apps.Count + " apps · " + ActiveProfile.SavedItemIds.Count + " saved items";
        DesktopOpenWorkspaceButton.Content = ActiveProfile.Apps.Count + ActiveProfile.SavedItemIds.Count == 0 ? "Set up workspace" : "Open workspace";
        DesktopItemsGrid.Children.Clear();
        AddDesktopShortcut("My files", "Files", () => Launch(AppCatalog.Defaults().First(a => a.Id == "files")));
        AddDesktopShortcut("Explore", "Explore", () => Navigate("Explore"));
        AddDesktopShortcut("Study", "Study", () => Navigate("Study"));
        AddDesktopShortcut("App Library", "Apps", () => Navigate("Apps"));
        AddDesktopShortcut("PC controls", "Settings", () => Navigate("PC controls"));
        foreach (var saved in _state.SavedItems.Where(s => s.Favorite).Take(4))
            AddDesktopShortcut(saved.Title, NexusIcons.ForKind(saved.Kind), () => SelectExploreItem(saved));
        UpdateDesktopCanvasLayout();
    }
    private void AddDesktopShortcut(string label, string icon, Action action)
    {
        var face = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        face.Children.Add(NexusIcons.Image(icon, 64));
        var caption = new TextBlock { Text = label, FontSize = 13, TextAlignment = TextAlignment.Center,
            MaxLines = 2, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Resource("NexusDesktopText") };
        face.Children.Add(new Border { Child = caption, Padding = new Thickness(6, 3, 6, 3),
            CornerRadius = new CornerRadius(7), Background = Resource("NexusDesktopLabel") });
        var button = new Button { Content = face, Style = (Style)Application.Current.Resources["DesktopIconButton"] };
        button.Click += (_, _) => action();
        var menu = new MenuFlyout();
        var open = new MenuFlyoutItem { Text = "Open " + label }; open.Click += (_, _) => action();
        menu.Items.Add(open); button.ContextFlyout = menu;
        ToolTipService.SetToolTip(button, label);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, "Open " + label);
        DesktopItemsGrid.Children.Add(button); _motion?.AttachHover(button);
    }
    private void UpdateDesktopCanvasLayout()
    {
        if (!_ready) return;
        bool narrow = PageHost.ActualWidth < 520;
        int columns = narrow ? Math.Clamp((int)(PageHost.ActualWidth / 112), 1, 2) : 2;
        DesktopItemsGrid.ColumnDefinitions.Clear(); DesktopItemsGrid.RowDefinitions.Clear();
        for (int column = 0; column < columns; column++)
            DesktopItemsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (int row = 0; row < (DesktopItemsGrid.Children.Count + columns - 1) / columns; row++)
            DesktopItemsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int i = 0; i < DesktopItemsGrid.Children.Count; i++)
        {
            var child = (FrameworkElement)DesktopItemsGrid.Children[i];
            Grid.SetRow(child, i / columns); Grid.SetColumn(child, i % columns);
        }
        DesktopItemsScroller.HorizontalAlignment = narrow ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        DesktopWorkspaceCard.Visibility = PageHost.ActualWidth >= 520 && DesktopRoot.ActualHeight >= 680 && !_state.FocusMode
            ? Visibility.Visible : Visibility.Collapsed;
    }
}
