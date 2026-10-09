using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Models;
using Nexus.Shell.UI;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    private readonly List<(Button Button, Border IconWell, FontIcon Icon, TextBlock Value, TextBlock Label)> _homeSummaries = [];
    private string _homeSavedFilter = "All", _homeSavedTheme = "";
    private SavedItem[] _homeSavedShown = [];
    private int _summaryColumns;
    private string? _spotlightMood;

    private void RefreshOverviewSummary()
    {
        if (_homeSummaries.Count == 0)
        {
            foreach (var entry in new[] { ("Workspaces", "Workspaces", "\uE8B7"), ("Saved resources", "Explore", "\uE8A5"),
                ("Open tasks", "Study", "\uE8F1"), ("Focus today", "Study", "\uE916") })
            {
                var face = new Grid { ColumnSpacing = 12 };
                face.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
                face.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
                var icon = Glyph(entry.Item3, 21);
                var well = new Border { Child = icon, Width = 44, Height = 44, CornerRadius = new CornerRadius(22), VerticalAlignment = VerticalAlignment.Center };
                face.Children.Add(well);
                var words = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
                var value = Text("0", 26); value.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                var label = Text(entry.Item1, 12); label.TextWrapping = TextWrapping.Wrap;
                words.Children.Add(value); words.Children.Add(label); Grid.SetColumn(words, 1); face.Children.Add(words);
                string page = entry.Item2;
                var button = new Button { Content = face, Tag = page, HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(18, 18, 18, 18),
                    CornerRadius = new CornerRadius(20), MinHeight = 98, Style = (Style)Application.Current.Resources["QuietButton"] };
                button.Click += (_, _) => Navigate(page);
                button.Loaded += (_, _) => _motion?.AttachHover(button);
                _homeSummaries.Add((button, well, icon, value, label)); HomeSummaryCards.Children.Add(button);
            }
            _summaryColumns = 0;
            ApplyOverviewAppearance();
        }
        int[] counts = [_state.Profiles.Count, _state.SavedItems.Count, _state.Tasks.Count(t => !t.Completed), _state.FocusCompleted];
        HomeWorkspacesButton.IsEnabled = _state.Profiles.Count < 8 && !_launchingWorkspace;
        for (int index = 0; index < counts.Length; index++)
        {
            var entry = _homeSummaries[index]; string count = counts[index].ToString();
            if (entry.Value.Text != count) entry.Value.Text = count;
            AutomationProperties.SetName(entry.Button, count + " " + entry.Label.Text);
        }
        LayoutOverviewSummary();
    }
    private void LayoutOverviewSummary()
    {
        int columns = PageHost.ActualWidth >= 820 ? 4 : PageHost.ActualWidth >= 440 ? 2 : 1;
        if (_summaryColumns == columns) return;
        _summaryColumns = columns;
        HomeSummaryCards.ColumnDefinitions.Clear(); HomeSummaryCards.RowDefinitions.Clear();
        for (int i = 0; i < columns; i++) HomeSummaryCards.ColumnDefinitions.Add(new());
        for (int i = 0; i < (4 + columns - 1) / columns; i++) HomeSummaryCards.RowDefinitions.Add(new() { Height = GridLength.Auto });
        for (int i = 0; i < _homeSummaries.Count; i++)
        { Grid.SetRow(_homeSummaries[i].Button, i / columns); Grid.SetColumn(_homeSummaries[i].Button, i % columns); }
    }
    private void ApplyOverviewAppearance()
    {
        var theme = _environment.Theme;
        foreach (var entry in _homeSummaries)
        {
            entry.Button.Background = theme.Surface("Accent");
            entry.Button.BorderBrush = Resource("NexusBorder"); entry.Button.BorderThickness = new Thickness(_highContrast ? 1 : 0);
            entry.IconWell.Background = _highContrast ? Resource("NexusAccent") : Resource("NexusHighlight");
            entry.Icon.Foreground = entry.Value.Foreground = entry.Label.Foreground = Resource("NexusAccentText");
        }
        HomeSpotlightArt.Visibility = _highContrast || _state.ReducedEffects ? Visibility.Collapsed : Visibility.Visible;
        if (_spotlightMood != theme.Palette.Name)
        {
            HomeSpotlightImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/Wallpapers/" + theme.Palette.Name + ".png"));
            _spotlightMood = theme.Palette.Name;
        }
        RenderOverviewSaved();
    }
    private void HomeSavedFilter_Click(object sender, RoutedEventArgs args)
    { _homeSavedFilter = (string)((Button)sender).Tag; RenderOverviewSaved(); }
    private void RenderOverviewSaved()
    {
        foreach (var button in HomeSavedFilters.Children.OfType<Button>()) SetSegment(button, (string)button.Tag == _homeSavedFilter);
        bool Matches(SavedItem item) => _homeSavedFilter switch
        { "Files" => item.Kind is "File" or "Folder", "Links" => item.Kind == "Link", "Notes" => item.Kind == "Note", _ => true };
        var selected = ActiveProfile.SavedItemIds.ToHashSet(StringComparer.Ordinal);
        var shown = _state.SavedItems.Where(Matches).OrderByDescending(item => selected.Contains(item.Id)).ThenByDescending(item => item.Favorite).Take(5).ToArray();
        string themeKey = _environment.Theme.Palette.Name + ":" + _highContrast;
        HomeSavedEmpty.Visibility = shown.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        HomeSavedEmpty.Text = _state.SavedItems.Count == 0 ? "Save a file, link or note in Explore to keep it here." : "No " + _homeSavedFilter.ToLowerInvariant() + " in your saved resources.";
        if (_homeSavedTheme == themeKey && _homeSavedShown.SequenceEqual(shown)) return;
        _homeSavedShown = shown; _homeSavedTheme = themeKey; HomeSavedRows.Children.Clear();
        foreach (var item in shown)
        {
            var face = new Grid { ColumnSpacing = 12 };
            face.ColumnDefinitions.Add(new() { Width = new GridLength(32) });
            face.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            face.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            face.Children.Add(NexusIcons.Image(item.Kind == "Link" ? "Browser" : item.Kind == "Note" ? "Note" : item.Kind == "Folder" ? "Files" : "Document", 28));
            var words = new StackPanel { Spacing = 4 };
            var title = Text(item.Title, 13); title.TextWrapping = TextWrapping.NoWrap; title.TextTrimming = TextTrimming.CharacterEllipsis;
            var detail = Text(item.Collection, 11, true); detail.TextWrapping = TextWrapping.NoWrap; detail.TextTrimming = TextTrimming.CharacterEllipsis;
            words.Children.Add(title); words.Children.Add(detail); Grid.SetColumn(words, 1); face.Children.Add(words);
            var kind = Text(item.Kind, 11, true); kind.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(kind, 2); face.Children.Add(kind);
            var button = new Button { Content = face, Style = (Style)Application.Current.Resources["QuietButton"],
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 11, 10, 11), CornerRadius = new CornerRadius(12), Background = Resource("NexusInput") };
            button.Click += (_, _) => OpenSaved(item);
            AutomationProperties.SetName(button, "Open " + item.Title + ", " + item.Kind);
            ToolTipService.SetToolTip(button, item.Kind == "Note" ? item.Title : item.Target);
            HomeSavedRows.Children.Add(button);
        }
    }
}
