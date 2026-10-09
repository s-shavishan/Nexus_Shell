using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Nexus.Shell.Desktop;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Windows.System;

namespace Nexus.Shell.UI.Files;

internal sealed class FilesView : Grid
{
    private readonly DesktopEnvironment _environment;
    private readonly FileSelectionRequest _request;
    private readonly Action<IReadOnlyList<string>> _complete;
    private readonly TextBox _address = new() { PlaceholderText = "Folder path", MinWidth = 100 };
    private readonly TextBox _name = new() { PlaceholderText = "File name" };
    private readonly TextBlock _status = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBox _filter = new() { PlaceholderText = "Filter this folder", Width = 170 };
    private readonly Button _choose, _newFolder;
    private readonly Button _empty;
    private readonly Border _placesCard = new() { CornerRadius = new CornerRadius(20), Padding = new Thickness(12), BorderThickness = new Thickness(1) };
    private readonly Border _inspectorCard = new() { CornerRadius = new CornerRadius(20), Padding = new Thickness(20), BorderThickness = new Thickness(1) };
    private readonly TextBlock _fileTitle = new() { Text = "No selection", FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _fileKind = new() { Text = "Choose a file or folder to see its information.", FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _fileLocation = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 5 };
    private readonly ColumnDefinition _inspectorColumn = new() { Width = new GridLength(225) };
    private readonly ColumnDefinition _placesColumn = new() { Width = new GridLength(185) };
    private readonly StackPanel _bin = new() { Spacing = 16, Padding = new Thickness(28), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _binCount = new() { FontSize = 24 };
    private IReadOnlyList<FileEntry> _entries = [];
    private string _folder = "";
    private bool _closed, _recycle, _dialog;
    private CancellationTokenSource? _navigation;
    internal FilesView(DesktopEnvironment environment, FileSelectionRequest request, Action<IReadOnlyList<string>> complete)
    {
        _environment = environment; _request = request; _complete = complete; Padding = new Thickness(18); RowSpacing = 15;
        RequestedTheme = environment.Theme.ElementTheme; Background = environment.Theme.Brush("NexusPanel");
        foreach (var height in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) RowDefinitions.Add(new() { Height = height });
        var toolbar = new Grid { ColumnSpacing = 8 };
        foreach (var width in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) toolbar.ColumnDefinitions.Add(new() { Width = width });
        toolbar.Children.Add(Button("Up", async () => { if (!_recycle && Directory.GetParent(_folder)?.FullName is string parent) await NavigateAsync(parent); }));
        var refresh = Button("Refresh", () => NavigateAsync(_recycle ? "nexus:recycle" : _folder)); Grid.SetColumn(refresh, 1); toolbar.Children.Add(refresh);
        _address.KeyDown += async (_, e) => { if (e.Key == VirtualKey.Enter) { e.Handled = true; await NavigateAsync(_address.Text); } };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_address, "Folder path; press Enter to open");
        Grid.SetColumn(_address, 2); toolbar.Children.Add(_address);
        _filter.TextChanged += (_, _) => Render(); Grid.SetColumn(_filter, 3); toolbar.Children.Add(_filter); Children.Add(toolbar);
        var body = new Grid { ColumnSpacing = 14 };
        body.ColumnDefinitions.Add(_placesColumn);
        body.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(_inspectorColumn);
        body.SizeChanged += (_, _) =>
        {
            bool showInspector = request.Kind == FileSelectionKind.Browse && body.ActualWidth >= 800;
            _inspectorColumn.Width = showInspector ? new GridLength(225) : new GridLength(0);
            _inspectorCard.Visibility = showInspector ? Visibility.Visible : Visibility.Collapsed;
            _placesColumn.Width = body.ActualWidth < 590 ? new GridLength(125) : new GridLength(185);
        };
        var places = new StackPanel { Spacing = 6 };
        places.Children.Add(new TextBlock { Text = "FAVORITES", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(9, 7, 0, 10), Foreground = environment.Theme.Brush("NexusMuted") });
        foreach (var place in new[] { ("My files", Environment.SpecialFolder.UserProfile), ("Desktop", Environment.SpecialFolder.DesktopDirectory),
            ("Documents", Environment.SpecialFolder.MyDocuments), ("Pictures", Environment.SpecialFolder.MyPictures), ("Music", Environment.SpecialFolder.MyMusic), ("Videos", Environment.SpecialFolder.MyVideos) })
        { string path = Environment.GetFolderPath(place.Item2); if (path.Length > 0) places.Children.Add(Button(place.Item1, () => NavigateAsync(path))); }
        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(downloads)) places.Children.Add(Button("Downloads", () => NavigateAsync(downloads)));
        if (request.Kind == FileSelectionKind.Browse) places.Children.Add(Button("Recycle Bin", () => NavigateAsync("nexus:recycle")));
        foreach (var drive in DriveInfo.GetDrives().Take(26))
        { string path = drive.RootDirectory.FullName; places.Children.Add(Button(path, () => NavigateAsync(path))); }
        _placesCard.Child = new ScrollViewer { Content = places, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        body.Children.Add(_placesCard);
        _list.SelectionMode = request.Kind == FileSelectionKind.OpenFiles ? ListViewSelectionMode.Multiple : ListViewSelectionMode.Single;
        _list.ItemTemplate = (DataTemplate)XamlReader.Load("""
          <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
            <Grid Padding="13,11" ColumnSpacing="13" AutomationProperties.Name="{Binding Name}">
              <Grid.ColumnDefinitions><ColumnDefinition Width="36"/><ColumnDefinition Width="*"/><ColumnDefinition Width="135"/></Grid.ColumnDefinitions>
              <Image Width="32" Height="32" Source="{Binding IconUri}"/>
              <TextBlock Grid.Column="1" Text="{Binding Name}" FontSize="13" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
              <TextBlock Grid.Column="2" Text="{Binding Detail}" FontSize="11" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
            </Grid>
          </DataTemplate>
          """);
        _list.DoubleTapped += async (_, _) => { if (_list.SelectedItem is FileEntry entry) await OpenAsync(entry); };
        _list.KeyDown += async (_, e) => { if (e.Key == VirtualKey.Enter && _list.SelectedItem is FileEntry entry) { e.Handled = true; await OpenAsync(entry); } };
        _list.SelectionChanged += (_, _) =>
        {
            if (_request.Kind == FileSelectionKind.SaveFile && _list.SelectedItem is FileEntry { IsFolder: false } entry) _name.Text = entry.Name;
            UpdateInspector();
        };
        Grid.SetColumn(_list, 1); body.Children.Add(_list);
        var details = new StackPanel { Spacing = 12 };
        details.Children.Add(new TextBlock { Text = "INSPECTOR", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = environment.Theme.Brush("NexusMuted") });
        details.Children.Add(new Border { Width = 84, Height = 84, CornerRadius = new CornerRadius(25),
            Background = environment.Theme.Brush("NexusSelection"), HorizontalAlignment = HorizontalAlignment.Left,
            Child = new FontIcon { Glyph = "\uE8B7", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 40, Foreground = environment.Theme.Brush("NexusAccent") } });
        details.Children.Add(_fileTitle); details.Children.Add(_fileKind);
        details.Children.Add(new Border { Height = 1, Background = environment.Theme.Brush("NexusBorder"), Margin = new Thickness(0, 6, 0, 6) });
        details.Children.Add(new TextBlock { Text = "LOCATION", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = environment.Theme.Brush("NexusMuted") });
        details.Children.Add(_fileLocation);
        _inspectorCard.Child = new ScrollViewer { Content = details };
        Grid.SetColumn(_inspectorCard, 2); body.Children.Add(_inspectorCard);
        _inspectorCard.Visibility = request.Kind == FileSelectionKind.Browse ? Visibility.Visible : Visibility.Collapsed;
        if (request.Kind != FileSelectionKind.Browse) _inspectorColumn.Width = new GridLength(0);
        _bin.Children.Add(NexusIcons.Image("Windows", 64)); _bin.Children.Add(_binCount);
        _bin.Children.Add(new TextBlock { Text = "Deleted items are kept by Windows. Nexus can empty the bin here.", TextWrapping = TextWrapping.Wrap, MaxWidth = 340 });
        _empty = Button("Empty Recycle Bin…", EmptyBinAsync); _bin.Children.Add(_empty); _bin.Visibility = Visibility.Collapsed;
        Grid.SetColumn(_bin, 1); body.Children.Add(_bin); Grid.SetRow(body, 1); Children.Add(body);
        var footer = new Grid { ColumnSpacing = 10, RowSpacing = 8 };
        footer.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        footer.RowDefinitions.Add(new() { Height = GridLength.Auto }); footer.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _status.Foreground = environment.Theme.Brush("NexusMuted"); Grid.SetColumnSpan(_status, 3); footer.Children.Add(_status);
        _name.Text = request.SuggestedName; _name.Visibility = request.Kind is FileSelectionKind.SaveFile or FileSelectionKind.OpenFile or FileSelectionKind.OpenFiles ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetRow(_name, 1); footer.Children.Add(_name);
        _newFolder = Button("New folder…", NewFolderAsync); Grid.SetRow(_newFolder, 1); Grid.SetColumn(_newFolder, 1); footer.Children.Add(_newFolder);
        _choose = Button(request.Kind == FileSelectionKind.SaveFile ? "Save here" : request.Kind == FileSelectionKind.Folder ? "Choose this folder" : "Choose", ChooseAsync);
        _choose.Visibility = request.Kind == FileSelectionKind.Browse ? Visibility.Collapsed : Visibility.Visible;
        Grid.SetRow(_choose, 1); Grid.SetColumn(_choose, 2); footer.Children.Add(_choose); Grid.SetRow(footer, 2); Children.Add(footer);
        var escape = new KeyboardAccelerator { Key = VirtualKey.Escape }; escape.Invoked += (_, e) => { if (!_dialog) { complete([]); e.Handled = true; } }; KeyboardAccelerators.Add(escape);
        ApplyAppearance();
    }
    private Button Button(string title, Func<Task> action)
    {
        var button = new Button { Content = title, Style = (Style)Application.Current.Resources["QuietButton"], HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += async (_, _) => { if (_dialog || _closed) return; button.IsEnabled = false; try { await action(); } catch (Exception ex) { ShowError(ex); } finally { if (!_closed) button.IsEnabled = true; } }; return button;
    }
    internal async Task NavigateAsync(string path)
    {
        if (_closed || _dialog) return;
        _navigation?.Cancel(); _navigation?.Dispose(); var navigation = _navigation = new CancellationTokenSource();
        var token = navigation.Token;
        _choose.IsEnabled = _newFolder.IsEnabled = false; _status.Text = "Opening folder…";
        try
        {
            if (path == "nexus:recycle" && _request.Kind == FileSelectionKind.Browse)
            {
                var info = await Task.Run(RecycleBinService.Read, token);
                if (_closed || navigation.IsCancellationRequested) return;
                _recycle = true; _binCount.Text = info.Items + " items · " + FileCatalog.Size(info.Bytes); _empty.IsEnabled = info.Items > 0;
                _address.Text = "Recycle Bin"; _fileTitle.Text = "Recycle Bin"; _fileKind.Text = "Deleted items"; _fileLocation.Text = "Windows Recycle Bin"; _bin.Visibility = Visibility.Visible; _list.Visibility = Visibility.Collapsed; _filter.IsEnabled = false;
                _status.Text = "Recycle Bin · Windows storage, Nexus controls"; return;
            }
            var snapshot = await Task.Run(() => FileCatalog.Read(path, _request.Extensions, token), token);
            if (_closed || navigation.IsCancellationRequested) return;
            _folder = snapshot.Path; _entries = snapshot.Entries; _recycle = false; _filter.IsEnabled = true; _filter.Text = "";
            _address.Text = _folder; _bin.Visibility = Visibility.Collapsed; _list.Visibility = Visibility.Visible; Render(); UpdateInspector();
            _status.Text = _entries.Count + " items" + (snapshot.Limited ? " · First 1,000 entries; use the path field to reach another item" : " · Double-click or press Enter to open");
            _choose.IsEnabled = _newFolder.IsEnabled = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_closed && !navigation.IsCancellationRequested) { ShowError(ex); _address.Text = _recycle ? "Recycle Bin" : _folder; _choose.IsEnabled = _newFolder.IsEnabled = _folder.Length > 0 && !_recycle; } }
    }
    private void Render() { if (!_closed) _list.ItemsSource = _entries.Where(e => e.Name.Contains(_filter.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)).ToArray(); }
    private async Task OpenAsync(FileEntry entry)
    {
        try { if (entry.IsFolder) await NavigateAsync(entry.Path); else if (_request.Kind == FileSelectionKind.Browse) _environment.OpenTarget(entry.Path); else if (_request.Kind is FileSelectionKind.OpenFile or FileSelectionKind.OpenFiles) await ChooseAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }
    private async Task ChooseAsync()
    {
        if (_closed || _dialog || _recycle || _folder.Length == 0) return;
        try
        {
            IReadOnlyList<string> paths;
            if (_request.Kind == FileSelectionKind.Folder) paths = Directory.Exists(_folder) ? [_folder] : [];
            else if (_request.Kind == FileSelectionKind.SaveFile)
            {
                string path = FileCatalog.SavePath(_folder, _name.Text, _request.Extensions);
                if (File.Exists(path) && !await ConfirmAsync("Replace this file?", Path.GetFileName(path) + " already exists. Saving will replace it.", "Replace")) return;
                paths = [path];
            }
            else
            {
                var selected = _list.SelectedItems.Cast<FileEntry>().Where(e => !e.IsFolder && File.Exists(e.Path) && FileCatalog.Accepts(e.Path, _request.Extensions)).Select(e => e.Path).ToArray();
                if (selected.Length == 0 && !string.IsNullOrWhiteSpace(_name.Text))
                {
                    string path = Path.IsPathFullyQualified(_name.Text) ? _name.Text : FileCatalog.ChildPath(_folder, _name.Text);
                    if (File.Exists(path) && FileCatalog.Accepts(path, _request.Extensions)) selected = [path];
                }
                if (selected.Length > 100) throw new InvalidOperationException("Choose up to 100 files at a time.");
                paths = _request.Kind == FileSelectionKind.OpenFile ? selected.Take(1).ToArray() : selected;
            }
            if (paths.Count == 0) { _status.Text = "Choose an available file or folder first."; return; } _complete(paths);
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private async Task NewFolderAsync()
    {
        if (_dialog || _recycle || _folder.Length == 0) return; _dialog = true;
        try
        {
            var name = new TextBox { PlaceholderText = "Folder name" }; var dialog = Dialog("New folder", name, "Create");
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || _closed) return;
            string path = FileCatalog.ChildPath(_folder, name.Text);
            if (File.Exists(path) || Directory.Exists(path)) throw new IOException("That name is already in use.");
            await Task.Run(() => Directory.CreateDirectory(path));
        }
        finally { _dialog = false; }
        await NavigateAsync(_folder);
    }
    private async Task EmptyBinAsync()
    {
        if (!await ConfirmAsync("Empty Recycle Bin?", "All deleted items for this user will be permanently removed from the Recycle Bin.", "Empty bin") || _closed) return;
        await Task.Run(() => RecycleBinService.EmptyConfirmed(IntPtr.Zero)); await NavigateAsync("nexus:recycle");
    }
    private ContentDialog Dialog(string title, object content, string primary) => new() { XamlRoot = XamlRoot, RequestedTheme = RequestedTheme, Title = title, Content = content,
        PrimaryButtonText = primary, CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
    private async Task<bool> ConfirmAsync(string title, string content, string primary)
    { if (_dialog) return false; _dialog = true; try { return await Dialog(title, content, primary).ShowAsync() == ContentDialogResult.Primary; } finally { _dialog = false; } }
    private void ShowError(Exception error) { if (!_closed) _status.Text = error.Message; Log.Write("Nexus Files", error); }
    private void UpdateInspector()
    {
        if (_list.SelectedItem is FileEntry item)
        {
            _fileTitle.Text = item.Name;
            _fileKind.Text = item.IsFolder ? "Folder" : item.Detail;
            _fileLocation.Text = item.Path;
        }
        else
        {
            _fileTitle.Text = "Choose an item";
            _fileKind.Text = "Select a file or folder to inspect it.";
            _fileLocation.Text = _folder;
        }
    }
    internal void ApplyAppearance()
    {
        var theme = _environment.Theme; RequestedTheme = theme.ElementTheme; Background = theme.Brush("NexusPanel");
        _status.Foreground = theme.Brush("NexusMuted"); _list.Foreground = theme.Brush("NexusText");
        _placesCard.Background = theme.Surface("Sidebar"); _placesCard.BorderBrush = theme.Brush("NexusBorder");
        _inspectorCard.Background = theme.Brush("NexusCard"); _inspectorCard.BorderBrush = theme.Brush("NexusBorder");
        _fileTitle.Foreground = theme.Brush("NexusText"); _fileKind.Foreground = theme.Brush("NexusMuted");
        _fileLocation.Foreground = theme.Brush("NexusSecondary");
        foreach (var text in _bin.Children.OfType<TextBlock>()) text.Foreground = theme.Brush(text == _binCount ? "NexusText" : "NexusMuted");
    }
    internal void Release() { _closed = true; _navigation?.Cancel(); _navigation?.Dispose(); _entries = []; }
}
