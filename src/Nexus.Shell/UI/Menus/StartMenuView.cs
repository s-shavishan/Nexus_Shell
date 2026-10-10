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
    private readonly TextBox _search = new() { PlaceholderText = "Search for apps, notes, settings and more…", FontSize = 16, CornerRadius = new CornerRadius(15), Padding = new Thickness(16, 12, 16, 12) };
    private readonly ListView _results = new() { IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBlock _hint = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private IReadOnlyList<AppEntry> _catalog = [];
    internal StartMenuView(DesktopEnvironment environment)
    {
        _environment = environment; Padding = new Thickness(20); RowSpacing = 13;
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) RowDefinitions.Add(new() { Height = height });
        Children.Add(_search); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_search, "Search Nexus apps and actions");
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var shortcut in new[] { ("Files", "Files", "nexus:files"), ("Apps", "Apps", "nexus:sections"), ("Settings", "Settings", "ms-settings:"), ("Calculator", "Calculator", "nexus:calculator"), ("Notes", "Note", "nexus:notes"), ("Terminal", "Terminal", "cmd.exe") })
        {
            var face = new StackPanel { Spacing = 6 }; face.Children.Add(NexusIcons.Image(shortcut.Item2, 38)); face.Children.Add(new TextBlock { Text = shortcut.Item1, FontSize = 11, TextAlignment = TextAlignment.Center });
            var button = new Button { Content = face, Width = 83, Padding = new Thickness(5, 9, 5, 9), CornerRadius = new CornerRadius(13), Style = (Style)Application.Current.Resources["QuietButton"] };
            button.Click += (_, _) => environment.OpenTarget(shortcut.Item3); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, "Open " + shortcut.Item1); header.Children.Add(button);
        }
        var quick = new ScrollViewer { Content = header, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Enabled }; Grid.SetRow(quick, 1); Children.Add(quick);
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
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 }; footer.Children.Add(_hint);
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
        foreach (var action in new[] {
            new AppEntry("notes", "Notes", "nexus:notes", "\uE70B", "Write and pin your notes"),
            new AppEntry("calculator", "Calculator", "nexus:calculator", "\uE8EF", "Calculate without leaving Nexus"),
            new AppEntry("files", "Files", "nexus:files", "\uE8B7", "Browse folders and image previews") })
            if (query.Length == 0 || action.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)) items.Add(new(action.Name, action.Category, "ms-appx:///Assets/Icons/" + NexusIcons.ForApp(action) + ".svg", action));
        foreach (var app in _environment.Session.State.PinnedApps.Concat(_catalog).DistinctBy(a => a.Target, StringComparer.OrdinalIgnoreCase)
            .Where(a => query.Length == 0 || a.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).Take(60))
            items.Add(new(app.Name, app.Category, "ms-appx:///Assets/Icons/" + NexusIcons.ForApp(app) + ".svg", app));
        _results.ItemsSource = items.DistinctBy(i => i.App?.Target ?? "nexus:sections", StringComparer.OrdinalIgnoreCase).ToArray();
        _hint.Text = items.Count == 0 ? "No matching apps." : query.Length == 0 ? "Your apps, within reach." : items.Count + " results";
    }
    private void Open(LauncherItem item)
    { _environment.HideMenu(); if (item.App is null) _environment.ShowSections(); else _environment.Launch(item.App); }
    internal void ApplyAppearance()
    {
        var theme = _environment.Theme; RequestedTheme = theme.ElementTheme;
        _results.Foreground = theme.Brush("NexusText"); _hint.Foreground = theme.Brush("NexusMuted");
        _search.Foreground = theme.Brush("NexusText"); _search.Background = theme.Brush("NexusSelection");
    }
}
