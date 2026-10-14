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

internal sealed class LaunchpadTile(AppEntry app, bool highContrast) : INotifyPropertyChanged
{
    internal AppEntry App => app;
    public string Name => app.Name;
    private ImageSource? _icon = LaunchpadArtworkPolicy.UsesNexusArtwork(app.Id)
        ? NexusIcons.Source(NexusIcons.ForApp(app)) : null;
    private Visibility _placeholder = LaunchpadArtworkPolicy.UsesNexusArtwork(app.Id)
        ? Visibility.Collapsed : Visibility.Visible;
    public string Monogram => LaunchpadArtworkPolicy.Monogram(app.Name);
    public SolidColorBrush TileBrush { get; } =
        new(ShellTheme.Color(LaunchpadArtworkPolicy.BackgroundForContrast(app.Name, highContrast)));
    internal void UpdateContrast(bool highContrast)
    {
        var color = ShellTheme.Color(LaunchpadArtworkPolicy.BackgroundForContrast(app.Name, highContrast));
        if (TileBrush.Color != color) TileBrush.Color = color;
    }
    public Visibility PlaceholderVisibility { get => _placeholder; private set
        { if (_placeholder == value) return; _placeholder = value; PropertyChanged?.Invoke(this, new(nameof(PlaceholderVisibility))); } }
    public ImageSource? Icon
    {
        get => _icon;
        set
        {
            if (ReferenceEquals(_icon, value)) return;
            _icon = value;
            PlaceholderVisibility = value is null ? Visibility.Visible : Visibility.Collapsed;
            PropertyChanged?.Invoke(this, new(nameof(Icon)));
        }
    }
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
    private readonly TextBlock _subtitle = new() { Text = "Your apps, ready when you are.", FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly StackPanel _dots = new() { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _empty = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _searchFrame = new() { CornerRadius = new CornerRadius(13), BorderThickness = new Thickness(1) };
    private readonly FontIcon _searchIcon = ShellControls.Icon("\uE721", 18);
    private readonly TextBlock _hint = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _previous = new() { Content = "‹", Width = 38 }, _next = new() { Content = "›", Width = 38 };
    private readonly Dictionary<string, Button> _categories = [];
    private sealed record CollectionChoice(string Id, string Name);
    private readonly ComboBox _collections = new() { Width = 178, DisplayMemberPath = "Name" };
    private readonly ComboBox _order = new() { Width = 115 };
    private readonly Button _renameCollection = new() { Content = "Rename" }, _deleteCollection = new() { Content = "Remove" }, _restoreHidden = new() { Content = "Restore hidden" };
    private bool _organizing;
    private readonly Button _refresh;
    private readonly DispatcherTimer _filterTimer = new() { Interval = TimeSpan.FromMilliseconds(45) };
    private readonly LaunchpadCatalogCache _catalog;
    private readonly LaunchpadRefreshGate _refreshGate = new();
    private IReadOnlyList<AppEntry> _filtered = [];
    private (int Pages, int Page, bool HighContrast)? _indicatorKey;
    private int _page, _epoch, _renderVersion;
    private bool _disposed, _open;
    private IReadOnlyList<AppEntry> _shown = [];
    private string _category = "All";
    internal StartMenuView(DesktopEnvironment environment)
    {
        _environment = environment; _catalog = new(Builtins());
        Padding = new Thickness(30); RowSpacing = 18;
        SizeChanged += (_, _) => _subtitle.MaxWidth = Math.Max(140, ActualWidth - 176);
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
        foreach (string category in new[] { "All", "Pinned", "Recent", "System", "Tools", "Development", "Games", "Hidden" })
        {
            var button = new Button { Content = category, CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 6, 12, 6), Style = (Style)Application.Current.Resources["QuietButton"] };
            button.Click += (_, _) => { _category = category; _page = 0; RefreshCollections(); Render(); }; _categories.Add(category, button); tabs.Children.Add(button);
        }
        var organization = new StackPanel { Spacing = 9 };
        organization.Children.Add(new ScrollViewer { Content = tabs, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        tools.Children.Add(_collections); _collections.SelectionChanged += (_, _) => { if (_organizing || _collections.SelectedItem is not CollectionChoice choice) return; _category = choice.Id.Length == 0 ? "All" : "collection:" + choice.Id; _page = 0; RefreshCollections(); Render(); };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_collections, "App collection");
        var create = new Button { Content = "+ Collection" }; create.Flyout = CollectionEditor(false); tools.Children.Add(create);
        _renameCollection.Flyout = CollectionEditor(true); tools.Children.Add(_renameCollection);
        var removeFlyout = new MenuFlyout(); var removeGroup = new MenuFlyoutItem { Text = "Remove collection · apps stay installed" };
        removeGroup.Click += (_, _) => Organize(() => { string id = CurrentCollectionId; environment.Session.State.AppCollections.RemoveAll(g => g.Id == id); _category = "All"; }); removeFlyout.Items.Add(removeGroup); _deleteCollection.Flyout = removeFlyout; tools.Children.Add(_deleteCollection);
        _order.Items.Add("A–Z"); _order.Items.Add("Catalog order"); _order.SelectedIndex = environment.Session.State.LaunchpadOrder == "Catalog" ? 1 : 0;
        _order.SelectionChanged += (_, _) => { if (!_organizing) Organize(() => environment.Session.State.LaunchpadOrder = _order.SelectedIndex == 1 ? "Catalog" : "Name"); }; tools.Children.Add(_order);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_order, "Launchpad app order");
        _restoreHidden.Click += (_, _) => Organize(() => environment.Session.State.HiddenAppTargets.Clear()); tools.Children.Add(_restoreHidden);
        organization.Children.Add(new ScrollViewer { Content = tools, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Grid.SetRow(organization, 2); Children.Add(organization); RefreshCollections();
        _apps.ItemsPanel = (ItemsPanelTemplate)XamlReader.Load("""
            <ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><ItemsWrapGrid Orientation="Horizontal" ItemWidth="104" ItemHeight="124" /></ItemsPanelTemplate>
            """);
        _apps.ItemTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <StackPanel Spacing="11" Padding="4,10" HorizontalAlignment="Stretch" AutomationProperties.Name="{Binding Name}">
                <Grid Width="68" Height="68" HorizontalAlignment="Center">
                  <Border Width="64" Height="64" CornerRadius="16" Background="{Binding TileBrush}"
                          Visibility="{Binding PlaceholderVisibility}" HorizontalAlignment="Center">
                    <TextBlock Text="{Binding Monogram}" FontSize="22" FontWeight="SemiBold"
                               Foreground="{ThemeResource TextFillColorPrimaryBrush}"
                               HorizontalAlignment="Center" VerticalAlignment="Center" />
                  </Border>
                  <Image Source="{Binding Icon}" Width="64" Height="64" Stretch="Uniform"/>
                </Grid>
                <TextBlock Text="{Binding Name}" FontSize="12" TextAlignment="Center" TextTrimming="CharacterEllipsis" TextWrapping="Wrap" MaxLines="2" />
              </StackPanel>
            </DataTemplate>
            """);
        _apps.ItemClick += (_, e) => Open(((LaunchpadTile)e.ClickedItem).App);
        _apps.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter && _apps.SelectedItem is LaunchpadTile tile) { Open(tile.App); e.Handled = true; } };
        var context = new MenuFlyout(); _apps.ContextFlyout = context;
        context.Opening += (_, _) =>
        {
            context.Items.Clear(); if (_apps.SelectedItem is not LaunchpadTile tile) return;
            var app = tile.App; var state = environment.Session.State;
            void Action(string text, Action action) { var item = new MenuFlyoutItem { Text = text }; item.Click += (_, _) => Organize(action); context.Items.Add(item); }
            Action(LaunchpadCatalog.IsPinned(state.PinnedApps, app) ? "Remove from dock" : "Pin to dock", () => LaunchpadCatalog.TogglePin(state.PinnedApps, app));
            if (LaunchpadCatalog.IsPinned(state.PinnedApps, app)) { Action("Move dock pin left", () => DesktopOrganization.MovePin(state.PinnedApps, app.Target, -1)); Action("Move dock pin right", () => DesktopOrganization.MovePin(state.PinnedApps, app.Target, 1)); }
            var groups = new MenuFlyoutSubItem { Text = "Collections", IsEnabled = state.AppCollections.Count > 0 };
            foreach (var group in state.AppCollections)
            { var member = new ToggleMenuFlyoutItem { Text = group.Name, IsChecked = group.Targets.Contains(app.Target, StringComparer.OrdinalIgnoreCase) }; member.Click += (_, _) => Organize(() => DesktopOrganization.ToggleMember(state, group.Id, app.Target)); groups.Items.Add(member); }
            context.Items.Add(groups); context.Items.Add(new MenuFlyoutSeparator());
            bool hidden = state.HiddenAppTargets.Contains(app.Target, StringComparer.OrdinalIgnoreCase);
            Action(hidden ? "Show in Launchpad" : "Hide from Launchpad", () => DesktopOrganization.SetHidden(state, app.Target, !hidden));
        };
        _apps.RightTapped += (_, args) => { DependencyObject? node = args.OriginalSource as DependencyObject; while (node is not null && node is not GridViewItem && !ReferenceEquals(node, _apps)) node = VisualTreeHelper.GetParent(node); _apps.SelectedItem = node is GridViewItem item ? item.Content : null; };
        Grid.SetRow(_apps, 3); Children.Add(_apps);
        var empty = new StackPanel { Spacing = 14, HorizontalAlignment = HorizontalAlignment.Center }; var emptyIcon = ShellControls.Icon("\uE721", 32); emptyIcon.Foreground = environment.Theme.Brush("NexusMuted"); empty.Children.Add(emptyIcon);
        empty.Children.Add(new TextBlock { Text = "Nothing here yet", FontSize = 18, Foreground = environment.Theme.Brush("NexusText"), TextAlignment = TextAlignment.Center });
        empty.Children.Add(new TextBlock { Text = "Try another category or search.\nRight-click apps to add them to a collection.\nHidden apps can be restored from the Hidden tab.", FontSize = 12, Foreground = environment.Theme.Brush("NexusMuted"), TextAlignment = TextAlignment.Center }); _empty.Child = empty; Grid.SetRow(_empty, 3); Children.Add(_empty);
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
        environment.Session.Changed += _catalog.Invalidate;
    }
    private IEnumerable<AppEntry> Builtins() => [new("sections", "Sections", "nexus:sections", "\uE80F", "System"), new("files", "Files", "nexus:files", "\uE8B7", "System"), new("settings", "Settings", "ms-settings:", "\uE713", "System"), new("notes", "Notes", "nexus:notes", "\uE70B", "Utility"), new("calculator", "Calculator", "nexus:calculator", "\uE8EF", "Utility")];
    private string CurrentCollectionId => _category.StartsWith("collection:", StringComparison.Ordinal) ? _category[11..] : "";
    private void Organize(Action action)
    {
        try { action(); _environment.SaveState(); _page = 0; RefreshCollections(); Render(); _subtitle.Text = "Your apps, arranged your way."; }
        catch (Exception error) { _subtitle.Text = error.Message; }
    }
    private void RefreshCollections()
    {
        _organizing = true;
        try
        {
            var choices = new[] { new CollectionChoice("", "All collections") }.Concat(_environment.Session.State.AppCollections.Select(g => new CollectionChoice(g.Id, g.Name))).ToArray();
            _collections.ItemsSource = choices; _collections.SelectedItem = choices.FirstOrDefault(c => c.Id == CurrentCollectionId) ?? choices[0];
            _renameCollection.IsEnabled = _deleteCollection.IsEnabled = CurrentCollectionId.Length > 0;
            _restoreHidden.Visibility = _category == "Hidden" ? Visibility.Visible : Visibility.Collapsed;
            _restoreHidden.IsEnabled = _environment.Session.State.HiddenAppTargets.Count > 0;
        }
        finally { _organizing = false; }
    }
    private Flyout CollectionEditor(bool rename)
    {
        var name = new TextBox { Header = rename ? "Collection name" : "New collection", MaxLength = 40, MinWidth = 230 };
        var feedback = new TextBlock { FontSize = 12, MaxWidth = 260, TextWrapping = TextWrapping.Wrap };
        var save = new Button { Content = rename ? "Save name" : "Create collection" }; var body = new StackPanel { Spacing = 10 }; body.Children.Add(name); body.Children.Add(feedback); body.Children.Add(save); var flyout = new Flyout { Content = body };
        flyout.Opening += (_, _) => { feedback.Text = ""; name.Text = rename ? _environment.Session.State.AppCollections.FirstOrDefault(g => g.Id == CurrentCollectionId)?.Name ?? "" : ""; };
        save.Click += (_, _) =>
        {
            try
            {
                if (rename) DesktopOrganization.RenameCollection(_environment.Session.State, CurrentCollectionId, name.Text);
                else _category = "collection:" + DesktopOrganization.CreateCollection(_environment.Session.State, name.Text).Id;
                _environment.SaveState(); _page = 0; RefreshCollections(); Render(); flyout.Hide();
            }
            catch (Exception error) { feedback.Text = error.Message; }
        };
        return flyout;
    }
    internal Task OpenAsync(bool search)
    {
        ++_epoch; _open = true; _title.Text = "Launchpad";
        _category = "All"; _page = 0; _search.Text = ""; _filterTimer.Stop(); RefreshCollections();
        _catalog.Update(_environment.Session.State.PinnedApps); Render();
        return RefreshCatalogAsync();
    }
    internal void FocusEntry(bool search) { if (search) _search.Focus(FocusState.Programmatic); else _apps.Focus(FocusState.Programmatic); }
    internal void PrepareValidation()
    { _catalog.Update([], []); Render(); }
    internal void VerifyRetainedRendering()
    {
        var items = _apps.ItemsSource; var indicators = _dots.Children.ToArray();
        for (int i = 0; i < 20; i++) Render();
        if (!ReferenceEquals(items, _apps.ItemsSource) || !indicators.SequenceEqual(_dots.Children))
            throw new InvalidOperationException("Unchanged Launchpad renders replaced retained items or page controls.");
    }
    private async Task RefreshCatalogAsync(bool refresh = false)
    {
        int epoch = _epoch;
        if (_disposed || !_open) return;
        var admission = _refreshGate.Request(refresh);
        if (!admission.Started)
        {
            if (refresh) _subtitle.Text = "Refresh queued after the current app scan.";
            return;
        }
        if (admission.Force) { AppIconCache.RefreshCompleted(); _shown = []; }
        // Leave Refresh enabled: a click during a scan queues one follow-up,
        // not a concurrent shortcut enumeration or a lost user request.
        try
        {
            var discovered = await _environment.GetCatalogAsync(admission.Force);
            if (epoch != _epoch || _environment.IsStopping || !_open || _disposed) return;
            _catalog.Update(_environment.Session.State.PinnedApps, discovered);
            Render();
            if (admission.Force)
            {
                _environment.Taskbar?.View.RefreshPinnedAppIcons();
                _subtitle.Text = "Installed apps refreshed.";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { if (_open && !_disposed) _environment.Report("Launchpad could not refresh installed apps", error); }
        finally
        {
            bool pendingForce = _refreshGate.Complete();
            if (!_disposed && _open && (epoch != _epoch || pendingForce)) _ = RefreshCatalogAsync();
        }
    }
    internal void Hide() { ++_epoch; _open = false; _filterTimer.Stop(); }
    private void Render()
    {
        if (_disposed) return;
        using var work = UiTrace.Measure("launchpad-render", "Launchpad", _catalog.Catalog.Count);
        _filtered = _catalog.Filter(_environment.Session.State, _category, _search.Text);
        int pages = Math.Max(1, (_filtered.Count + LaunchpadCatalog.PageSize - 1) / LaunchpadCatalog.PageSize); _page = Math.Clamp(_page, 0, pages - 1);
        var shown = LaunchpadCatalog.Page(_filtered, _page);
        if (!_shown.SequenceEqual(shown))
        {
            _shown = shown.ToArray(); var tiles = shown.Select(app => new LaunchpadTile(app, _environment.Theme.HighContrast)).ToArray(); _apps.ItemsSource = tiles;
            _ = RefreshIconsAsync(tiles, ++_renderVersion);
        }
        _previous.IsEnabled = _page > 0; _next.IsEnabled = _page + 1 < pages;
        _empty.Visibility = _filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var indicatorKey = (pages, _page, _environment.Theme.HighContrast);
        if (_indicatorKey != indicatorKey)
        {
            _dots.Children.Clear();
            for (int index = Math.Max(0, _page - 2); index < Math.Min(pages, _page + 3); index++)
            { int target = index; var dot = new Button { Content = new Border { Width = target == _page ? 15 : 5, Height = 5, CornerRadius = new CornerRadius(3), Background = _environment.Theme.Brush(target == _page ? "NexusAccent" : "NexusMuted") }, Width = 22, Height = 28, Padding = new Thickness(0), Style = (Style)Application.Current.Resources["QuietButton"] }; dot.Click += (_, _) => { _page = target; Render(); }; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(dot, "App page " + (index + 1)); _dots.Children.Add(dot); }
            _indicatorKey = indicatorKey;
        }
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
        // Reuse realized tiles and loaded Windows thumbnails on theme changes.
        // Update just the mutable brush; no catalog scan or item rebuild.
        if (_apps.ItemsSource is IEnumerable<LaunchpadTile> realized)
            foreach (var tile in realized) tile.UpdateContrast(theme.HighContrast);
        _title.Foreground = _apps.Foreground = _search.Foreground = theme.Brush("NexusText"); _hint.Foreground = _subtitle.Foreground = _searchIcon.Foreground = theme.Brush("NexusMuted");
        _searchFrame.Background = theme.Material("Input", _environment.Session.State.NativeGlass); _searchFrame.BorderBrush = _search.FocusState == FocusState.Unfocused ? theme.Edge : theme.Brush("NexusAccent"); _searchFrame.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : 13);
        foreach (var (category, button) in _categories) { button.Background = category == _category ? theme.Brush("NexusSelection") : theme.Material("Card", _environment.Session.State.NativeGlass); button.Foreground = theme.Brush("NexusText"); button.BorderBrush = theme.Edge; button.BorderThickness = new Thickness(1); button.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : 10); }
        if (_empty.Child is StackPanel empty) { foreach (var label in empty.Children.OfType<TextBlock>()) label.Foreground = theme.Brush(label.FontSize > 15 ? "NexusText" : "NexusMuted"); foreach (var icon in empty.Children.OfType<FontIcon>()) icon.Foreground = theme.Brush("NexusMuted"); }
    }
    public void Dispose() { if (_disposed) return; _environment.Session.Changed -= _catalog.Invalidate; Hide(); _disposed = true; ++_renderVersion; _filterTimer.Stop(); }
}
