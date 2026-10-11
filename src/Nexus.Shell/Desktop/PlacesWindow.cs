using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Services;
using Nexus.Shell.UI;

namespace Nexus.Shell.Desktop;

internal sealed class PlacesWindow : DesktopPanelWindow
{
    private sealed record Place(string Id, string Name, string Path);
    private readonly ListView _places = new() { SelectionMode = ListViewSelectionMode.Single };
    private readonly StackPanel _preview = new() { Spacing = 6 };
    private readonly TextBlock _status = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _path = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxLines = 3, IsTextSelectionEnabled = true };
    private readonly TextBlock _name = new() { Text = "Choose a folder", FontSize = 19, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly Button _open, _pin, _rename, _remove, _up, _down, _refresh;
    private readonly Border _sidebar = new() { CornerRadius = new CornerRadius(15), BorderThickness = new Thickness(1), Padding = new Thickness(7) };
    private readonly DispatcherTimer _slow = new() { Interval = TimeSpan.FromSeconds(3) };
    private Place? _selected, _pending;
    private CancellationTokenSource? _read;
    private bool _busy, _rendering, _picking, _validation;
    private int _generation;
    internal PlacesWindow(DesktopEnvironment environment) : this(environment, new Grid { RowSpacing = 12, ColumnSpacing = 16 }) { }
    private PlacesWindow(DesktopEnvironment environment, Grid body) : base(environment, "Places", body, 790, 650, center: true)
    {
        body.RowDefinitions.Add(new() { Height = GridLength.Auto }); body.RowDefinitions.Add(new()); body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var left = new ColumnDefinition { Width = new GridLength(210) }; body.ColumnDefinitions.Add(left); body.ColumnDefinitions.Add(new());
        body.SizeChanged += (_, _) => left.Width = new GridLength(body.ActualWidth < 630 ? 150 : 210);
        var intro = new TextBlock { Text = "Favorite folders and a quick look inside. Open a folder in Nexus Files to keep working.", FontSize = 12, TextWrapping = TextWrapping.Wrap }; Grid.SetColumnSpan(intro, 2); body.Children.Add(intro);
        _sidebar.Child = _places; Grid.SetRow(_sidebar, 1); body.Children.Add(_sidebar);
        _places.SelectionChanged += (_, _) => { if (!_rendering && _places.SelectedItem is ListViewItem { Tag: Place place }) Select(place); };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_places, "Favorite and common folders");
        var details = new Grid { RowSpacing = 10 }; details.RowDefinitions.Add(new() { Height = GridLength.Auto }); details.RowDefinitions.Add(new() { Height = GridLength.Auto }); details.RowDefinitions.Add(new());
        var heading = new StackPanel { Spacing = 5 }; heading.Children.Add(_name); heading.Children.Add(_path); details.Children.Add(heading);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        _open = ActionButton("Open in Files", () => { if (_selected is { } place) environment.ShowFiles(place.Path); }); actions.Children.Add(_open);
        _pin = ActionButton("Pin folder", () => Change(() => { if (_selected is { } place) { var pin = DesktopOrganization.AddPlace(environment.Session.State, ShortName(place.Name), place.Path); _selected = new(pin.Id, pin.Name, pin.Path); } })); actions.Children.Add(_pin);
        _refresh = ActionButton("Refresh", () => { if (_selected is { } place) Select(place); }); actions.Children.Add(_refresh);
        var actionScroll = new ScrollViewer { Content = actions, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(actionScroll, 1); details.Children.Add(actionScroll);
        var content = new ScrollViewer { Content = _preview, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(content, 2); details.Children.Add(content); Grid.SetRow(details, 1); Grid.SetColumn(details, 1); body.Children.Add(details);
        var footer = new StackPanel { Spacing = 9 };
        var edit = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        edit.Children.Add(ActionButton("+ Favorite folder", () => _ = PickFolderAsync()));
        _rename = ActionButton("Rename", () => { }); _rename.Flyout = RenameFlyout(); edit.Children.Add(_rename);
        _remove = ActionButton("Unpin", () => Change(() => { if (_selected is { } place) environment.Session.State.Places.RemoveAll(p => p.Id == place.Id); })); edit.Children.Add(_remove);
        _up = ActionButton("Move up", () => Change(() => { if (_selected is { } place) DesktopOrganization.MovePlace(environment.Session.State, place.Id, -1); })); edit.Children.Add(_up);
        _down = ActionButton("Move down", () => Change(() => { if (_selected is { } place) DesktopOrganization.MovePlace(environment.Session.State, place.Id, 1); })); edit.Children.Add(_down);
        footer.Children.Add(new ScrollViewer { Content = edit, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }); footer.Children.Add(_status);
        Grid.SetRow(footer, 2); Grid.SetColumnSpan(footer, 2); body.Children.Add(footer);
        _slow.Tick += (_, _) => { _slow.Stop(); if (IsOpen && _busy) _status.Text = "This folder is taking longer to respond. You can still use the desktop; one folder read is pending."; };
        ApplyAppearance();
    }
    private static string ShortName(string value) => value[..Math.Min(40, value.Length)];
    private IEnumerable<Place> CommonPlaces()
    {
        foreach (var (label, folder) in new[] { ("My files", System.Environment.SpecialFolder.UserProfile), ("Desktop", System.Environment.SpecialFolder.DesktopDirectory), ("Documents", System.Environment.SpecialFolder.MyDocuments), ("Pictures", System.Environment.SpecialFolder.MyPictures), ("Music", System.Environment.SpecialFolder.MyMusic), ("Videos", System.Environment.SpecialFolder.MyVideos) })
        { string path = System.Environment.GetFolderPath(folder); if (path.Length > 0) yield return new("", label, path); }
        string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        if (home.Length > 0) yield return new("", "Downloads", Path.Combine(home, "Downloads"));
    }
    private void Populate()
    {
        _rendering = true;
        try
        {
            string? path = _selected?.Path; _places.Items.Clear();
            var favorites = Environment.Session.State.Places.Select(p => new Place(p.Id, p.Name, p.Path));
            foreach (var place in favorites.Concat(CommonPlaces()).DistinctBy(p => p.Path, StringComparer.OrdinalIgnoreCase))
            {
                var face = new StackPanel { Spacing = 3 }; face.Children.Add(new TextBlock { Text = place.Name, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis });
                face.Children.Add(new TextBlock { Text = place.Id.Length > 0 ? "Favorite folder" : "Common folder", FontSize = 11, Foreground = Environment.Theme.Brush("NexusMuted") });
                var row = new ListViewItem { Content = face, Tag = place, Padding = new Thickness(9, 8, 9, 8) }; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(row, place.Name + ", " + place.Path); _places.Items.Add(row);
                if (string.Equals(place.Path, path, StringComparison.OrdinalIgnoreCase)) { _places.SelectedItem = row; _selected = place; }
            }
            if (_places.SelectedItem is null && _places.Items.Count > 0) { _places.SelectedIndex = 0; _selected = (_places.SelectedItem as ListViewItem)?.Tag as Place; }
            RefreshActions();
        }
        finally { _rendering = false; }
    }
    private void RefreshActions()
    {
        _open.IsEnabled = _refresh.IsEnabled = _selected is not null; bool favorite = _selected is { Id.Length: > 0 };
        _pin.IsEnabled = _selected is not null && !favorite; _rename.IsEnabled = _remove.IsEnabled = favorite;
        int index = Environment.Session.State.Places.FindIndex(p => p.Id == _selected?.Id);
        _up.IsEnabled = index > 0; _down.IsEnabled = index >= 0 && index + 1 < Environment.Session.State.Places.Count;
        _name.Text = _selected?.Name ?? "Choose a folder"; _path.Text = _selected?.Path ?? "";
    }
    private void Change(Action action)
    {
        try { string? previous = _selected?.Path; action(); Environment.SaveState(); Populate(); if (_selected is { } place && !string.Equals(previous, place.Path, StringComparison.OrdinalIgnoreCase)) Select(place); else _status.Text = "Favorites updated. Your folders stay where they are."; }
        catch (Exception error) { _status.Text = error.Message; }
    }
    private Flyout RenameFlyout()
    {
        var name = new TextBox { Header = "Favorite name", MaxLength = 40, MinWidth = 230 }; var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxWidth = 250 }; var save = new Button { Content = "Save name" };
        var body = new StackPanel { Spacing = 10 }; body.Children.Add(name); body.Children.Add(feedback); body.Children.Add(save); var flyout = new Flyout { Content = body };
        flyout.Opening += (_, _) => { name.Text = _selected?.Name ?? ""; feedback.Text = "This changes the favorite label only."; };
        save.Click += (_, _) =>
        {
            try { if (_selected is not { } place) return; DesktopOrganization.RenamePlace(Environment.Session.State, place.Id, name.Text); Environment.SaveState(); Populate(); flyout.Hide(); }
            catch (Exception error) { feedback.Text = error.Message; }
        };
        return flyout;
    }
    private async Task PickFolderAsync()
    {
        if (_picking || IsClosed) return; _picking = true; Hide();
        try
        {
            var paths = await Environment.PickAsync(new(FileSelectionKind.Folder));
            if (IsClosed || Environment.IsStopping) return;
            Show();
            if (paths.FirstOrDefault() is not { } path) return;
            string label = Path.GetFileName(Path.TrimEndingDirectorySeparator(path)); if (label.Length == 0) label = path;
            var pin = DesktopOrganization.AddPlace(Environment.Session.State, ShortName(label), path); Environment.SaveState(); _selected = new(pin.Id, pin.Name, pin.Path); Populate(); Select(_selected);
        }
        catch (Exception error) { if (!IsClosed) _status.Text = error.Message; }
        finally { _picking = false; }
    }
    private void Select(Place place)
    {
        _selected = place; RefreshActions(); _preview.Children.Clear(); _status.Text = "Reading folder…"; _pending = place; ++_generation;
        _read?.Cancel(); _slow.Stop(); _slow.Start(); if (!_busy) _ = ReadPendingAsync();
    }
    private async Task ReadPendingAsync()
    {
        if (_busy || IsClosed || _validation) return; _busy = true;
        try
        {
            while (IsOpen && _pending is { } place)
            {
                _pending = null; int generation = _generation; _read?.Dispose(); _read = new(); var token = _read.Token;
                try
                {
                    var snapshot = await Task.Run(() => FileCatalog.Read(place.Path, cancellation: token), token);
                    if (IsClosed || !IsOpen || generation != _generation) continue;
                    RenderPreview(snapshot);
                }
                catch (OperationCanceledException) { }
                catch (Exception error) { if (!IsClosed && IsOpen && generation == _generation) _status.Text = "Folder unavailable: " + error.Message; }
            }
        }
        finally { _busy = false; _read?.Dispose(); _read = null; _slow.Stop(); }
    }
    private void RenderPreview(FolderSnapshot snapshot)
    {
        _preview.Children.Clear(); var theme = Environment.Theme;
        foreach (var entry in snapshot.Entries.Take(24))
        {
            var face = new Grid { ColumnSpacing = 10 }; face.ColumnDefinitions.Add(new() { Width = new GridLength(32) }); face.ColumnDefinitions.Add(new());
            face.Children.Add(NexusIcons.Image(FilePresentation.Icon(entry.Path, entry.IsFolder), 28));
            var labels = new StackPanel { Spacing = 3 }; labels.Children.Add(new TextBlock { Text = entry.Name, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = theme.Brush("NexusText") });
            labels.Children.Add(new TextBlock { Text = entry.Detail, FontSize = 11, Foreground = theme.Brush("NexusMuted"), TextTrimming = TextTrimming.CharacterEllipsis }); Grid.SetColumn(labels, 1); face.Children.Add(labels);
            var row = new Button { Content = face, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(10), CornerRadius = new CornerRadius(theme.HighContrast ? 0 : 12), Background = theme.Material("Card", Environment.Session.State.NativeGlass), BorderBrush = theme.Edge, BorderThickness = new Thickness(1) };
            row.Click += (_, _) => { try { if (entry.IsFolder) Environment.ShowFiles(entry.Path); else Environment.OpenTarget(entry.Path); } catch (Exception error) { if (!IsClosed) _status.Text = error.Message; } }; Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(row, "Open " + entry.Name); _preview.Children.Add(row);
        }
        _status.Text = snapshot.Entries.Count == 0 ? "This folder is empty." : $"Showing {Math.Min(24, snapshot.Entries.Count)} of {snapshot.Entries.Count}" + (snapshot.Limited ? "+" : "") + " items. Open in Files for the full browser.";
    }
    internal void Show() { ShowPanel(); }
    internal void PopulateValidation()
    {
        _validation = true; Populate(); RenderPreview(new("C:\\Example", [new("Documents", "C:\\Example\\Documents", true, "Folder"), new("Readme.txt", "C:\\Example\\Readme.txt", false, "1 KB · TXT")], false));
    }
    protected override void OnShown() { Populate(); if (!_validation && _selected is { } place) Select(place); }
    protected override void OnHidden() { ++_generation; _pending = null; _read?.Cancel(); _slow.Stop(); if (!_validation) _preview.Children.Clear(); }
    protected override void OnClosed() => Environment.PlacesClosed(this);
    internal override void ApplyAppearance()
    {
        base.ApplyAppearance(); var theme = Environment.Theme; _places.Foreground = _name.Foreground = theme.Brush("NexusText"); _status.Foreground = _path.Foreground = theme.Brush("NexusMuted"); _sidebar.Background = theme.Material("Card", Environment.Session.State.NativeGlass); _sidebar.BorderBrush = theme.Edge; _sidebar.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : 15);
        if (IsOpen) { Populate(); if (!_validation && _selected is { } place) Select(place); }
    }
}
