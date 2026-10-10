using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Nexus.Shell.Desktop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Windows.System;

namespace Nexus.Shell.UI.Menus;

internal sealed record LaunchpadTile(string Name, string IconUri, AppEntry App);

// Start opens the app grid. Search is a separate entry into the same catalogue,
// never a full-screen keystroke hook. Only one bounded page is realized.
internal sealed class StartMenuView : Grid
{
    private readonly DesktopEnvironment _environment;
    private readonly TextBox _search = new() { PlaceholderText = "Find an app", FontSize = 15, CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, 14, 10) };
    private readonly GridView _apps = new() { IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.Single, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _title = new() { Text = "Launchpad", FontSize = 27, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly TextBlock _hint = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _previous = new() { Content = "‹", Width = 38 }, _next = new() { Content = "›", Width = 38 };
    private readonly Dictionary<string, Button> _categories = [];
    private IReadOnlyList<AppEntry> _catalog = [], _filtered = [];
    private int _page, _epoch;
    private string _category = "All";
    internal StartMenuView(DesktopEnvironment environment)
    {
        _environment = environment; Padding = new Thickness(26); RowSpacing = 16;
        KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) RowDefinitions.Add(new() { Height = height });
        var heading = new Grid(); heading.ColumnDefinitions.Add(new()); heading.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); heading.Children.Add(_title);
        var close = new Button { Content = "×", Width = 34, Height = 34, Style = (Style)Application.Current.Resources["QuietButton"] };
        close.Click += (_, _) => environment.HideMenu(); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(close, "Close Launchpad"); Grid.SetColumn(close, 1); heading.Children.Add(close); Children.Add(heading);
        Grid.SetRow(_search, 1); Children.Add(_search); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_search, "Find an app in Launchpad");
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        foreach (string category in new[] { "All", "System", "Tools", "Development", "Games", "Apps" })
        {
            var button = new Button { Content = category, CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 6, 12, 6), Style = (Style)Application.Current.Resources["QuietButton"] };
            button.Click += (_, _) => { _category = category; _page = 0; Render(); }; _categories.Add(category, button); tabs.Children.Add(button);
        }
        var tabScroll = new ScrollViewer { Content = tabs, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(tabScroll, 2); Children.Add(tabScroll);
        _apps.ItemsPanel = (ItemsPanelTemplate)XamlReader.Load("""
            <ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><ItemsWrapGrid Orientation="Horizontal" ItemWidth="110" ItemHeight="116" /></ItemsPanelTemplate>
            """);
        _apps.ItemTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <StackPanel Spacing="10" Padding="4,8" HorizontalAlignment="Stretch">
                <Image Source="{Binding IconUri}" Width="54" Height="54" />
                <TextBlock Text="{Binding Name}" FontSize="12" TextAlignment="Center" TextTrimming="CharacterEllipsis" TextWrapping="Wrap" MaxLines="2" />
              </StackPanel>
            </DataTemplate>
            """);
        _apps.ItemClick += (_, e) => Open(((LaunchpadTile)e.ClickedItem).App);
        _apps.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter && _apps.SelectedItem is LaunchpadTile tile) { Open(tile.App); e.Handled = true; } };
        Grid.SetRow(_apps, 3); Children.Add(_apps);
        var footer = new Grid(); footer.ColumnDefinitions.Add(new()); footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); footer.Children.Add(_hint);
        var pages = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 }; pages.Children.Add(_previous); pages.Children.Add(_next); Grid.SetColumn(pages, 1); footer.Children.Add(pages); Grid.SetRow(footer, 4); Children.Add(footer);
        _previous.Click += (_, _) => { _page--; Render(); }; _next.Click += (_, _) => { _page++; Render(); };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_previous, "Previous app page"); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_next, "Next app page");
        _search.TextChanged += (_, _) => { _page = 0; Render(); };
        _search.KeyDown += (_, e) => { if (e.Key == VirtualKey.Down) { _apps.Focus(FocusState.Programmatic); e.Handled = true; } else if (e.Key == VirtualKey.Enter && _filtered.Count > 0) { Open(_filtered[0]); e.Handled = true; } };
        var escape = new KeyboardAccelerator { Key = VirtualKey.Escape }; escape.Invoked += (_, e) => { environment.HideMenu(); e.Handled = true; }; KeyboardAccelerators.Add(escape);
        ApplyAppearance();
    }
    private IEnumerable<AppEntry> Builtins() => [new("sections", "Sections", "nexus:sections", "\uE80F", "System"), new("files", "Files", "nexus:files", "\uE8B7", "System"), new("settings", "Settings", "ms-settings:", "\uE713", "System"), new("notes", "Notes", "nexus:notes", "\uE70B", "Utility"), new("calculator", "Calculator", "nexus:calculator", "\uE8EF", "Utility")];
    internal async Task OpenAsync(bool search)
    {
        int epoch = ++_epoch; _title.Text = search ? "Search apps" : "Launchpad";
        _category = "All"; _page = 0; _search.Text = "";
        _catalog = LaunchpadCatalog.Build(Builtins(), _environment.Session.State.PinnedApps, _catalog); Render();
        if (search) _search.Focus(FocusState.Programmatic); else _apps.Focus(FocusState.Programmatic);
        try { var discovered = await _environment.GetCatalogAsync(); if (epoch != _epoch || _environment.IsStopping) return; _catalog = LaunchpadCatalog.Build(Builtins(), _environment.Session.State.PinnedApps, discovered); Render(); }
        catch (OperationCanceledException) { }
        catch (Exception error) { _environment.Report("Launchpad could not refresh installed apps", error); }
    }
    internal void Hide() => _epoch++;
    private void Render()
    {
        _filtered = LaunchpadCatalog.Filter(_catalog, _search.Text.Trim(), _category);
        int pages = Math.Max(1, (_filtered.Count + LaunchpadCatalog.PageSize - 1) / LaunchpadCatalog.PageSize); _page = Math.Clamp(_page, 0, pages - 1);
        _apps.ItemsSource = LaunchpadCatalog.Page(_filtered, _page).Select(app => new LaunchpadTile(app.Name, "ms-appx:///Assets/Icons/" + NexusIcons.ForApp(app) + ".svg", app)).ToArray();
        _previous.IsEnabled = _page > 0; _next.IsEnabled = _page + 1 < pages;
        _hint.Text = _filtered.Count == 0 ? "No apps match. Try another category or search." : $"{_filtered.Count} apps   ·   Page {_page + 1} of {pages}";
        foreach (var (category, button) in _categories) button.Background = _environment.Theme.Brush(category == _category ? "NexusSelection" : "NexusInput");
    }
    private void Open(AppEntry app) { _environment.HideMenu(); _environment.Launch(app); }
    internal void ApplyAppearance()
    { var theme = _environment.Theme; RequestedTheme = theme.ElementTheme; _title.Foreground = _apps.Foreground = _search.Foreground = theme.Brush("NexusText"); _hint.Foreground = theme.Brush("NexusMuted"); _search.Background = theme.Glass("Panel"); }
}
