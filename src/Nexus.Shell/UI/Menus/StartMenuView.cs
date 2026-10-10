using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Desktop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Windows.System;
using System.ComponentModel;

namespace Nexus.Shell.UI.Menus;

internal sealed class LaunchpadTile(AppEntry app) : INotifyPropertyChanged
{
    internal AppEntry App => app;
    public string Name => app.Name;
    private ImageSource _icon = NexusIcons.Source(NexusIcons.ForApp(app));
    public ImageSource Icon { get => _icon; set { if (ReferenceEquals(_icon, value)) return; _icon = value; PropertyChanged?.Invoke(this, new(nameof(Icon))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

// All app entry points use Launchpad and its embedded search. Only one bounded
// page is realized, and icon completion never rebuilds the grid.
internal sealed class StartMenuView : Grid, IDisposable
{
    private readonly DesktopEnvironment _environment;
    private readonly TextBox _search = new() { PlaceholderText = "Find an app", FontSize = 15, CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, 14, 10) };
    private readonly GridView _apps = new() { IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.Single, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _title = new() { Text = "Launchpad", FontSize = 27, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly TextBlock _subtitle = new() { Text = "Your apps, ready when you are.", FontSize = 12 };
    private readonly StackPanel _dots = new() { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _empty = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _searchFrame = new() { CornerRadius = new CornerRadius(13), BorderThickness = new Thickness(1) };
    private readonly FontIcon _searchIcon = ShellControls.Icon("\uE721", 18);
    private readonly TextBlock _hint = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _previous = new() { Content = "‹", Width = 38 }, _next = new() { Content = "›", Width = 38 };
    private readonly Dictionary<string, Button> _categories = [];
    private readonly Button _refresh;
    private readonly DispatcherTimer _filterTimer = new() { Interval = TimeSpan.FromMilliseconds(45) };
    private IReadOnlyList<AppEntry> _catalog = [], _filtered = [];
    private int _page, _epoch, _renderVersion;
    private bool _disposed, _open, _refreshing;
    private IReadOnlyList<AppEntry> _shown = [];
    private string _category = "All";
    internal StartMenuView(DesktopEnvironment environment)
    {
        _environment = environment; Padding = new Thickness(26); RowSpacing = 16;
        KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) RowDefinitions.Add(new() { Height = height });
        var heading = new Grid(); heading.ColumnDefinitions.Add(new()); heading.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var identity = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 13 }; identity.Children.Add(NexusIcons.Image("Launchpad", 42));
        var titles = new StackPanel { Spacing = 3 }; titles.Children.Add(_title); titles.Children.Add(_subtitle); identity.Children.Add(titles); heading.Children.Add(identity);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        _refresh = ShellControls.IconButton("\uE72C", "Refresh installed apps", () => _ = RefreshCatalogAsync(refresh: true)); actions.Children.Add(_refresh);
        var close = ShellControls.IconButton("\uE8BB", "Close Launchpad", environment.HideMenu); actions.Children.Add(close); Grid.SetColumn(actions, 1); heading.Children.Add(actions); Children.Add(heading);
        var searchRow = new Grid(); searchRow.ColumnDefinitions.Add(new() { Width = new GridLength(42) }); searchRow.ColumnDefinitions.Add(new());
        searchRow.Children.Add(_searchIcon);
        _search.BorderThickness = new Thickness(0); _search.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent); Grid.SetColumn(_search, 1); searchRow.Children.Add(_search);
        _searchFrame.Child = searchRow;
        _search.GotFocus += (_, _) => _searchFrame.BorderBrush = environment.Theme.Brush("NexusAccent");
        _search.LostFocus += (_, _) => _searchFrame.BorderBrush = environment.Theme.Edge;
        Grid.SetRow(_searchFrame, 1); Children.Add(_searchFrame); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_search, "Find an app in Launchpad");
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        foreach (string category in new[] { "All", "Pinned", "Recent", "System", "Tools", "Development", "Games" })
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
              <StackPanel Spacing="11" Padding="4,10" HorizontalAlignment="Stretch" AutomationProperties.Name="{Binding Name}">
                <Image Source="{Binding Icon}" Width="60" Height="60" />
                <TextBlock Text="{Binding Name}" FontSize="12" TextAlignment="Center" TextTrimming="CharacterEllipsis" TextWrapping="Wrap" MaxLines="2" />
              </StackPanel>
            </DataTemplate>
            """);
        _apps.ItemClick += (_, e) => Open(((LaunchpadTile)e.ClickedItem).App);
        _apps.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter && _apps.SelectedItem is LaunchpadTile tile) { Open(tile.App); e.Handled = true; } };
        var context = new MenuFlyout(); var pin = new MenuFlyoutItem { Text = "Pin to dock" };
        context.Items.Add(pin); _apps.ContextFlyout = context;
        context.Opening += (_, _) => { pin.IsEnabled = _apps.SelectedItem is LaunchpadTile; if (_apps.SelectedItem is LaunchpadTile tile) pin.Text = LaunchpadCatalog.IsPinned(environment.Session.State.PinnedApps, tile.App) ? "Remove from dock" : "Pin to dock"; };
        pin.Click += (_, _) => { if (_apps.SelectedItem is LaunchpadTile tile) { try { LaunchpadCatalog.TogglePin(environment.Session.State.PinnedApps, tile.App); environment.SaveState(); Render(); } catch (Exception error) { environment.Report("The dock pin could not be changed", error); } } };
        _apps.RightTapped += (_, args) => { DependencyObject? node = args.OriginalSource as DependencyObject; while (node is not null && node is not GridViewItem && !ReferenceEquals(node, _apps)) node = VisualTreeHelper.GetParent(node); if (node is GridViewItem item) _apps.SelectedItem = item.Content; };
        Grid.SetRow(_apps, 3); Children.Add(_apps);
        var empty = new StackPanel { Spacing = 14, HorizontalAlignment = HorizontalAlignment.Center }; var emptyIcon = ShellControls.Icon("\uE721", 32); emptyIcon.Foreground = environment.Theme.Brush("NexusMuted"); empty.Children.Add(emptyIcon);
        empty.Children.Add(new TextBlock { Text = "Nothing here yet", FontSize = 18, Foreground = environment.Theme.Brush("NexusText"), TextAlignment = TextAlignment.Center });
        empty.Children.Add(new TextBlock { Text = "Try another category or search.\nRight-click an app to pin it to your dock.", FontSize = 12, Foreground = environment.Theme.Brush("NexusMuted"), TextAlignment = TextAlignment.Center }); _empty.Child = empty; Grid.SetRow(_empty, 3); Children.Add(_empty);
        var footer = new Grid(); footer.ColumnDefinitions.Add(new()); footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); footer.Children.Add(_hint);
        var pages = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9 }; pages.Children.Add(_previous); pages.Children.Add(_dots); pages.Children.Add(_next); Grid.SetColumn(pages, 1); footer.Children.Add(pages); Grid.SetRow(footer, 4); Children.Add(footer);
        _previous.Style = _next.Style = (Style)Application.Current.Resources["QuietButton"]; _previous.Padding = _next.Padding = new Thickness(0); _previous.Height = _next.Height = 32;
        _previous.Click += (_, _) => { _page--; Render(); }; _next.Click += (_, _) => { _page++; Render(); };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_previous, "Previous app page"); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_next, "Next app page");
        _filterTimer.Tick += (_, _) => { _filterTimer.Stop(); if (_open) Render(); };
        _search.TextChanged += (_, _) => { _page = 0; _filterTimer.Stop(); if (_open) _filterTimer.Start(); };
        _search.KeyDown += (_, e) => { if (e.Key == VirtualKey.Down) { _filterTimer.Stop(); Render(); _apps.Focus(FocusState.Programmatic); e.Handled = true; } else if (e.Key == VirtualKey.Enter) { _filterTimer.Stop(); Render(); if (_filtered.Count > 0) Open(_filtered[0]); e.Handled = true; } };
        var escape = new KeyboardAccelerator { Key = VirtualKey.Escape }; escape.Invoked += (_, e) => { environment.HideMenu(); e.Handled = true; }; KeyboardAccelerators.Add(escape);
        ApplyAppearance();
    }
    private IEnumerable<AppEntry> Builtins() => [new("sections", "Sections", "nexus:sections", "\uE80F", "System"), new("files", "Files", "nexus:files", "\uE8B7", "System"), new("settings", "Settings", "ms-settings:", "\uE713", "System"), new("notes", "Notes", "nexus:notes", "\uE70B", "Utility"), new("calculator", "Calculator", "nexus:calculator", "\uE8EF", "Utility")];
    internal Task OpenAsync(bool search)
    {
        ++_epoch; _open = true; _title.Text = "Launchpad";
        _category = "All"; _page = 0; _search.Text = ""; _filterTimer.Stop();
        _catalog = LaunchpadCatalog.Build(Builtins(), _environment.Session.State.PinnedApps, _catalog); Render();
        return RefreshCatalogAsync();
    }
    internal void FocusEntry(bool search) { if (search) _search.Focus(FocusState.Programmatic); else _apps.Focus(FocusState.Programmatic); }
    private async Task RefreshCatalogAsync(bool refresh = false)
    {
        int epoch = _epoch; if (_refreshing || _disposed || !_open) return;
        if (refresh) { AppIconCache.RetryFailures(); _shown = []; }
        _refreshing = true; _refresh.IsEnabled = false;
        try { var discovered = await _environment.GetCatalogAsync(refresh); if (epoch != _epoch || _environment.IsStopping || !_open || _disposed) return; _catalog = LaunchpadCatalog.Build(Builtins(), _environment.Session.State.PinnedApps, discovered); Render(); }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (_open && !_disposed) _environment.Report("Launchpad could not refresh installed apps", error); }
        finally { _refreshing = false; if (!_disposed) { _refresh.IsEnabled = true; if (_open && epoch != _epoch) _ = RefreshCatalogAsync(); } }
    }
    internal void Hide() { ++_epoch; _open = false; _filterTimer.Stop(); }
    private void Render()
    {
        if (_disposed) return;
        var input = _category == "Recent" ? (_environment.Session.State.RememberRecentItems ? _environment.Session.State.RecentApps : []) : _category == "Pinned" ? _catalog.Where(app => LaunchpadCatalog.IsPinned(_environment.Session.State.PinnedApps, app)) : _catalog;
        _filtered = LaunchpadCatalog.Filter(input, _search.Text.Trim(), _category is "Pinned" or "Recent" ? "All" : _category);
        int pages = Math.Max(1, (_filtered.Count + LaunchpadCatalog.PageSize - 1) / LaunchpadCatalog.PageSize); _page = Math.Clamp(_page, 0, pages - 1);
        var shown = LaunchpadCatalog.Page(_filtered, _page);
        if (!_shown.SequenceEqual(shown))
        {
            _shown = shown.ToArray(); var tiles = shown.Select(app => new LaunchpadTile(app)).ToArray(); _apps.ItemsSource = tiles;
            _ = RefreshIconsAsync(tiles, ++_renderVersion);
        }
        _previous.IsEnabled = _page > 0; _next.IsEnabled = _page + 1 < pages;
        _empty.Visibility = _filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _dots.Children.Clear();
        for (int index = Math.Max(0, _page - 2); index < Math.Min(pages, _page + 3); index++)
        { int target = index; var dot = new Button { Content = new Border { Width = target == _page ? 15 : 5, Height = 5, CornerRadius = new CornerRadius(3), Background = _environment.Theme.Brush(target == _page ? "NexusAccent" : "NexusMuted") }, Width = 22, Height = 28, Padding = new Thickness(0), Style = (Style)Application.Current.Resources["QuietButton"] }; dot.Click += (_, _) => { _page = target; Render(); }; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(dot, "App page " + (index + 1)); _dots.Children.Add(dot); }
        _hint.Text = _filtered.Count == 0 ? "No apps match. Try another category or search." : $"{_filtered.Count} apps   ·   Page {_page + 1} of {pages}";
        foreach (var (category, button) in _categories) { button.Background = category == _category ? _environment.Theme.Brush("NexusSelection") : _environment.Theme.Material("Card", _environment.Session.State.NativeGlass); button.Foreground = _environment.Theme.Brush("NexusText"); }
    }
    private async Task RefreshIconsAsync(LaunchpadTile[] tiles, int render)
    {
        async Task Load(LaunchpadTile tile) { var icon = await AppIconCache.GetAsync(tile.App); if (!_disposed && render == _renderVersion && icon is not null) tile.Icon = icon; }
        await Task.WhenAll(tiles.Select(Load));
    }
    private void Open(AppEntry app) { _environment.HideMenu(); _environment.Launch(app); }
    internal void ApplyAppearance()
    {
        var theme = _environment.Theme; RequestedTheme = theme.ElementTheme;
        _title.Foreground = _apps.Foreground = _search.Foreground = theme.Brush("NexusText"); _hint.Foreground = _subtitle.Foreground = _searchIcon.Foreground = theme.Brush("NexusMuted");
        _searchFrame.Background = theme.Material("Input", _environment.Session.State.NativeGlass); _searchFrame.BorderBrush = _search.FocusState == FocusState.Unfocused ? theme.Edge : theme.Brush("NexusAccent"); _searchFrame.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : 13);
        foreach (var (category, button) in _categories) { button.Background = category == _category ? theme.Brush("NexusSelection") : theme.Material("Card", _environment.Session.State.NativeGlass); button.Foreground = theme.Brush("NexusText"); button.BorderBrush = theme.Edge; button.BorderThickness = new Thickness(1); button.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : 10); }
        if (_empty.Child is StackPanel empty) { foreach (var label in empty.Children.OfType<TextBlock>()) label.Foreground = theme.Brush(label.FontSize > 15 ? "NexusText" : "NexusMuted"); foreach (var icon in empty.Children.OfType<FontIcon>()) icon.Foreground = theme.Brush("NexusMuted"); }
    }
    public void Dispose() { if (_disposed) return; Hide(); _disposed = true; ++_renderVersion; _filterTimer.Stop(); }
}
