using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Input;
using Nexus.Shell.Desktop;
using Nexus.Shell.Models;
using Nexus.Shell.UI;
using Nexus.Shell.Services;
using Windows.System;

namespace Nexus.Shell.UI.Menus;

internal sealed record LauncherItem(string Name, string Detail, string IconUri, AppEntry? App);

internal sealed class StartMenuView : Grid
{
    private readonly DesktopEnvironment _environment;
    private readonly TextBox _search = new() { PlaceholderText = "Search your apps", FontSize = 15, CornerRadius = new CornerRadius(20), Padding = new Thickness(16, 12, 16, 12) };
    private readonly ListView _results = new() { IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBlock _name = new() { FontSize = 21, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Border _mark = new() { Width = 46, Height = 46, CornerRadius = new CornerRadius(16), VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _hint = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private IReadOnlyList<AppEntry> _catalog = [];
    internal StartMenuView(DesktopEnvironment environment)
    {
        _environment = environment; Padding = new Thickness(20); RowSpacing = 16;
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) RowDefinitions.Add(new() { Height = height });
        var header = new Grid { ColumnSpacing = 14 };
        header.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); header.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        _mark.Child = new FontIcon { FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"), Glyph = "\uE80F", FontSize = 24 };
        header.Children.Add(_mark);
        var words = new StackPanel { Spacing = 5, VerticalAlignment = VerticalAlignment.Center }; words.Children.Add(_name); words.Children.Add(_hint); Grid.SetColumn(words, 1); header.Children.Add(words); Children.Add(header);
        Grid.SetRow(_search, 1); Children.Add(_search); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_search, "Search Start menu apps");
        _search.TextChanged += (_, _) => Render();
        _search.KeyDown += (_, args) => { if (args.Key == VirtualKey.Down && _results.Items.Count > 0) { _results.SelectedIndex = 0; _results.Focus(FocusState.Programmatic); args.Handled = true; } else if (args.Key == VirtualKey.Enter && _results.Items.Count > 0) { Open((LauncherItem)_results.Items[_results.SelectedIndex >= 0 ? _results.SelectedIndex : 0]); args.Handled = true; } };
        _results.ItemTemplate = (DataTemplate)XamlReader.Load("""
          <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
            <Grid Padding="8,7" ColumnSpacing="12" AutomationProperties.Name="{Binding Name}">
              <Grid.ColumnDefinitions><ColumnDefinition Width="36"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
              <Image Width="30" Height="30" Source="{Binding IconUri}"/>
              <StackPanel Grid.Column="1" Spacing="2"><TextBlock Text="{Binding Name}" FontSize="13" TextTrimming="CharacterEllipsis"/><TextBlock Text="{Binding Detail}" FontSize="11" TextTrimming="CharacterEllipsis"/></StackPanel>
            </Grid>
          </DataTemplate>
          """);
        _results.ItemClick += (_, args) => Open((LauncherItem)args.ClickedItem);
        _results.KeyDown += (_, args) => { if (args.Key == VirtualKey.Enter && _results.SelectedItem is LauncherItem item) { Open(item); args.Handled = true; } };
        Grid.SetRow(_results, 2); Children.Add(_results);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var action in new[] { ("My files", (Action)(() => environment.OpenTarget(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))),
            ("Personalize", () => environment.ShowSections("Personalize")), (environment.Mode == DesktopSessionMode.DesktopShell ? "Session…" : "Exit", environment.RequestExit) })
        { var b = new Button { Content = action.Item1, Style = (Style)Application.Current.Resources["QuietButton"] }; b.Click += (_, _) => { environment.HideMenu(); action.Item2(); }; footer.Children.Add(b); }
        Grid.SetRow(footer, 3); Children.Add(footer);
        var escape = new KeyboardAccelerator { Key = VirtualKey.Escape };
        escape.Invoked += (_, args) => { environment.HideMenu(); args.Handled = true; }; KeyboardAccelerators.Add(escape);
        ApplyAppearance(); Render();
    }
    internal async Task OpenAsync(bool search)
    {
        _search.Text = ""; ApplyAppearance(); Render();
        _search.Focus(FocusState.Programmatic);
        try { _catalog = await _environment.GetCatalogAsync(); if (!_environment.IsStopping) Render(); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _hint.Text = "Your pinned apps are available. App discovery could not finish."; _environment.Report("Start menu app discovery failed", ex, false); }
    }
    private void Render()
    {
        string query = _search.Text.Trim();
        var items = new List<LauncherItem>();
        if (query.Length == 0 || "Sections workspaces study explore notes".Contains(query, StringComparison.CurrentCultureIgnoreCase))
            items.Add(new("Sections", "Your workspaces, study and saved resources", "ms-appx:///Assets/Icons/Apps.svg", null));
        foreach (var app in _environment.Session.State.PinnedApps.Concat(_catalog).DistinctBy(a => a.Target, StringComparer.OrdinalIgnoreCase)
            .Where(a => query.Length == 0 || a.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).Take(60))
            items.Add(new(app.Name, app.Category, "ms-appx:///Assets/Icons/" + NexusIcons.ForApp(app) + ".svg", app));
        _results.ItemsSource = items;
        _hint.Text = items.Count == 0 ? "No matching apps." : query.Length == 0 ? "Your apps, within reach." : items.Count + " results";
    }
    private void Open(LauncherItem item)
    { _environment.HideMenu(); if (item.App is null) _environment.ShowSections(); else _environment.Launch(item.App); }
    internal void ApplyAppearance()
    {
        var theme = _environment.Theme; RequestedTheme = theme.ElementTheme;
        _name.Text = _environment.Session.State.DisplayName + "’s desktop"; _name.Foreground = theme.Brush("NexusText");
        _results.Foreground = theme.Brush("NexusText"); _hint.Foreground = theme.Brush("NexusMuted");
        _mark.Background = theme.Surface("Accent"); ((FontIcon)_mark.Child).Foreground = theme.Brush("NexusAccentText");
    }
}
