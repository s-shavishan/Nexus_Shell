using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Nexus.Shell.Desktop;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Nexus.Runtime.Transfers;
using Nexus.Shell.UI.Menus;
using Windows.System;

namespace Nexus.Shell.UI.Files;

internal sealed class FilesView : Grid
{
    private readonly FilesEnvironment _environment;
    private readonly FileSelectionRequest _request;
    private readonly Action<IReadOnlyList<string>> _complete;
    private readonly TextBox _address = new() { PlaceholderText = "Folder path", MinWidth = 100 };
    private readonly TextBox _name = new() { PlaceholderText = "File name" };
    private readonly TextBlock _status = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.Single };
    private readonly GridView _tiles = new() { SelectionMode = ListViewSelectionMode.Single };
    private ListViewBase ActiveList => _showGrid ? _tiles : _list;
    private readonly NavigationTrail _history = new();
    private readonly FileMutationHistory _edits = new();
    private readonly Button _rename, _undo;
    private readonly Button _back, _forward, _gridButton, _listButton;
    private readonly ComboBox _sort = new() { Width = 110, FontSize = 12 };
    private readonly Image _preview = new() { Height = 172, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Image _fileIcon = new() { Width = 86, Height = 86, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Border _previewStage = new() { Height = 200, Padding = new Thickness(12), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1) };
    private readonly Border _contentsCard = new() { CornerRadius = new CornerRadius(MidnightGlassMetrics.CardRadius), Padding = new Thickness(13, 14, 13, 12), BorderThickness = new Thickness(1) };
    private readonly TextBlock _folderHeading = new() { Text = "Contents", FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _modified = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly ProgressRing _loading = new() { Width = 28, Height = 28, IsActive = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
    private readonly DispatcherTimer _slowRead = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly Dictionary<Button, string> _places = [];
    internal event Action<string>? FolderChanged;
    private string? _previewPath;
    private int _previewVersion;
    private bool _showGrid, _rendering;
    private readonly TextBox _filter = new() { PlaceholderText = "Filter this folder", Width = 170 };
    private readonly Button _choose, _newFolder;
    private readonly Button _empty;
    private readonly Border _placesCard = new() { CornerRadius = new CornerRadius(12), Padding = new Thickness(10), BorderThickness = new Thickness(1) };
    private readonly Border _inspectorCard = new() { CornerRadius = new CornerRadius(14), Padding = new Thickness(16), BorderThickness = new Thickness(1) };
    private readonly TextBlock _fileTitle = new() { Text = "No selection", FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _fileKind = new() { Text = "Choose a file or folder to see its information.", FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _fileLocation = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 5 };
    private readonly ColumnDefinition _inspectorColumn = new() { Width = new GridLength(228) };
    private readonly ColumnDefinition _placesColumn = new() { Width = new GridLength(190) };
    private readonly StackPanel _bin = new() { Spacing = 16, Padding = new Thickness(28), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _binCount = new() { FontSize = 24 };
    private IReadOnlyList<FileEntry> _entries = [];
    private string _folder = "";
    private bool _closed, _recycle, _dialog, _navigating, _binHasItems;
    private int _navigationVersion;
    private readonly LatestWorkQueue _navigation = new(), _previews = new();
    internal FilesView(FilesEnvironment environment, FileSelectionRequest request, Action<IReadOnlyList<string>> complete)
    {
        _environment = environment; _request = request; _complete = complete; Padding = new Thickness(18); RowSpacing = 14; IsHitTestVisible = !environment.Validation;
        _slowRead.Tick += (_, _) =>
        {
            _slowRead.Stop();
            if (!_closed && _navigating) _status.Text = "This location is still responding. If it stays unavailable, close Files and reopen it to recover.";
        };
        _showGrid = request.Kind == FileSelectionKind.Browse;
        RequestedTheme = environment.Theme.ElementTheme; KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden; Background = environment.Theme.Brush("NexusPanel");
        foreach (var height in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) RowDefinitions.Add(new() { Height = height });
        var toolbar = new Grid { ColumnSpacing = 10 };
        foreach (var width in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) toolbar.ColumnDefinitions.Add(new() { Width = width });
        var history = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 1 };
        _back = Button("‹", () => _history.BackTarget is string path ? NavigateAsync(path, -1) : Task.CompletedTask); history.Children.Add(_back);
        _forward = Button("›", () => _history.ForwardTarget is string path ? NavigateAsync(path, 1) : Task.CompletedTask); history.Children.Add(_forward);
        ToolTipService.SetToolTip(_back, "Back · Alt+Left"); ToolTipService.SetToolTip(_forward, "Forward · Alt+Right");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_back, "Previous folder"); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_forward, "Next folder");
        history.Children.Add(Button("↑", async () => { if (!_recycle && Directory.GetParent(_folder)?.FullName is string parent) await NavigateAsync(parent); })); toolbar.Children.Add(history);
        var refresh = Button("↻", () => NavigateAsync(_recycle ? "nexus:recycle" : _folder)); Grid.SetColumn(refresh, 1); toolbar.Children.Add(refresh); ToolTipService.SetToolTip(refresh, "Refresh · F5");
        _address.KeyDown += async (_, e) => { if (e.Key == VirtualKey.Enter) { e.Handled = true; await NavigateAsync(_address.Text); } };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_address, "Folder path; press Enter to open");
        Grid.SetColumn(_address, 2); toolbar.Children.Add(_address);
        _filter.TextChanged += (_, _) => Render(); Grid.SetColumn(_filter, 3); toolbar.Children.Add(_filter); Children.Add(toolbar);
        var body = new Grid { ColumnSpacing = 12 };
        body.ColumnDefinitions.Add(_placesColumn);
        body.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        body.ColumnDefinitions.Add(_inspectorColumn);
        body.SizeChanged += (_, _) =>
        {
            var pane = FilesPaneLayoutPolicy.Resolve(body.ActualWidth, request.Kind == FileSelectionKind.Browse);
            _inspectorColumn.Width = new GridLength(pane.InspectorWidth);
            _inspectorCard.Visibility = pane.InspectorVisible ? Visibility.Visible : Visibility.Collapsed;
            _placesColumn.Width = new GridLength(pane.SidebarWidth);
        };
        var places = new StackPanel { Spacing = 7 };
        places.Children.Add(new TextBlock { Text = "FAVORITES", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(9, 7, 0, 10), Foreground = environment.Theme.Brush("NexusMuted") });
        foreach (var place in new[] { ("My files", Environment.SpecialFolder.UserProfile), ("Desktop", Environment.SpecialFolder.DesktopDirectory),
            ("Documents", Environment.SpecialFolder.MyDocuments), ("Pictures", Environment.SpecialFolder.MyPictures), ("Music", Environment.SpecialFolder.MyMusic), ("Videos", Environment.SpecialFolder.MyVideos) })
        { string path = Environment.GetFolderPath(place.Item2); if (path.Length > 0) places.Children.Add(PlaceButton(place.Item1, path)); }
        string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (environment.Validation || Directory.Exists(downloads)) places.Children.Add(PlaceButton("Downloads", downloads));
        if (request.Kind == FileSelectionKind.Browse) places.Children.Add(PlaceButton("Recycle Bin", "nexus:recycle"));
        places.Children.Add(new TextBlock { Text = "LOCATIONS", FontSize = 11, Foreground = environment.Theme.Brush("NexusMuted"), Margin = new Thickness(9, 15, 0, 6) });
        if (environment.Validation) places.Children.Add(PlaceButton("C:\\", "C:\\"));
        else foreach (var drive in DriveInfo.GetDrives().Take(26))
        { string path = drive.RootDirectory.FullName; places.Children.Add(PlaceButton(path, path)); }
        _placesCard.Child = new ScrollViewer { Content = places, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        body.Children.Add(_placesCard);
        _list.SelectionMode = request.Kind is FileSelectionKind.OpenFiles or FileSelectionKind.Browse
            ? ListViewSelectionMode.Multiple : ListViewSelectionMode.Single;
        _tiles.SelectionMode = _list.SelectionMode;
        _tiles.ItemsPanel = (ItemsPanelTemplate)XamlReader.Load("<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><ItemsWrapGrid ItemWidth='116' ItemHeight='136' Orientation='Horizontal'/></ItemsPanelTemplate>");
        _tiles.ItemTemplate = (DataTemplate)XamlReader.Load("""
          <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
            <StackPanel Width="104" Padding="6,10" Spacing="8" AutomationProperties.Name="{Binding Name}">
              <Image Width="68" Height="68" Source="{Binding IconUri}" HorizontalAlignment="Center"/>
              <TextBlock Text="{Binding Name}" FontSize="12" TextAlignment="Center" TextTrimming="CharacterEllipsis" MaxLines="1"/>
              <TextBlock Text="{Binding Detail}" FontSize="10" Foreground="{ThemeResource NexusMuted}" TextAlignment="Center" TextTrimming="CharacterEllipsis"/>
            </StackPanel>
          </DataTemplate>
          """);
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
        void Selected()
        {
            if (_rendering) return;
            if (_request.Kind == FileSelectionKind.SaveFile && ActiveList.SelectedItem is FileEntry { IsFolder: false } entry) _name.Text = entry.Name;
            UpdateInspector();
        }
        foreach (var view in new ListViewBase[] { _list, _tiles })
        {
            view.DoubleTapped += async (_, args) =>
            {
                var entry = ItemAt(view, args.OriginalSource);
                if (entry is not null) { view.SelectedItem = entry; args.Handled = true; await OpenAsync(entry); }
            };
            view.KeyDown += async (_, e) => { if (e.Key == VirtualKey.Enter && ActiveList.SelectedItem is FileEntry entry) { e.Handled = true; await OpenAsync(entry); } };
            view.SelectionChanged += (_, _) => Selected();
            if (request.Kind == FileSelectionKind.Browse)
            {
                var renameKey = new KeyboardAccelerator { Key = VirtualKey.F2 }; renameKey.Invoked += async (_, e) => { e.Handled = true; try { await RenameAsync(); } catch (Exception error) { ShowError(error); } }; view.KeyboardAccelerators.Add(renameKey);
                var undoKey = new KeyboardAccelerator { Key = VirtualKey.Z, Modifiers = VirtualKeyModifiers.Control }; undoKey.Invoked += async (_, e) => { e.Handled = true; try { await UndoAsync(); } catch (Exception error) { ShowError(error); } }; view.KeyboardAccelerators.Add(undoKey);
                var selectAll = new KeyboardAccelerator { Key = VirtualKey.A, Modifiers = VirtualKeyModifiers.Control };
                selectAll.Invoked += (_, args) => { if (!_navigating && !_recycle) { view.SelectAll(); args.Handled = true; } };
                view.KeyboardAccelerators.Add(selectAll);
                var context = ShellMenu.Create();
                context.Opening += (_, _) => BuildContext(context,
                    view.SelectedItems.Count == 1 ? view.SelectedItem as FileEntry : null);
                view.ContextFlyout = context;
                view.RightTapped += (_, args) =>
                {
                    var clicked = ItemAt(view, args.OriginalSource);
                    if (clicked is not null && !view.SelectedItems.Contains(clicked)) view.SelectedItem = clicked;
                };
            }
        }
        var contents = new Grid { RowSpacing = 12 };
        contents.RowDefinitions.Add(new() { Height = GridLength.Auto });
        contents.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var contentHeader = new Grid { ColumnSpacing = 10 };
        contentHeader.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        contentHeader.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        contentHeader.Children.Add(_folderHeading);
        var views = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        _gridButton = Button("Grid", () => { _showGrid = true; Render(); return Task.CompletedTask; }); _listButton = Button("List", () => { _showGrid = false; Render(); return Task.CompletedTask; }); views.Children.Add(_gridButton); views.Children.Add(_listButton);
        _rename = Button("Rename…", () => RenameAsync()); _undo = Button("Undo edit", UndoAsync);
        _rename.Visibility = _undo.Visibility = request.Kind == FileSelectionKind.Browse ? Visibility.Visible : Visibility.Collapsed;
        views.Children.Add(_rename); views.Children.Add(_undo);
        foreach (string sort in new[] { "Name", "Newest", "Size", "Kind" }) _sort.Items.Add(sort);
        _sort.SelectedIndex = 0; _sort.SelectionChanged += (_, _) => Render(); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_sort, "Sort files"); views.Children.Add(_sort);
        var viewScroll = new ScrollViewer { Content = views, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetColumn(viewScroll, 1); contentHeader.Children.Add(viewScroll);
        contents.Children.Add(contentHeader);
        Grid.SetRow(_list, 1); Grid.SetRow(_tiles, 1); contents.Children.Add(_list); contents.Children.Add(_tiles);
        _contentsCard.Child = contents; Grid.SetColumn(_contentsCard, 1); body.Children.Add(_contentsCard);
        Grid.SetColumn(_loading, 1); body.Children.Add(_loading);
        var details = new StackPanel { Spacing = 14 };
        details.Children.Add(new TextBlock { Text = "PREVIEW", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = environment.Theme.Brush("NexusMuted") });
        var artwork = new Grid(); artwork.Children.Add(_preview); artwork.Children.Add(_fileIcon);
        _previewStage.Child = artwork; details.Children.Add(_previewStage);
        details.Children.Add(_fileTitle); details.Children.Add(_fileKind);
        details.Children.Add(Button("Open", async () => { if (ActiveList.SelectedItem is FileEntry entry) await OpenAsync(entry); }));
        details.Children.Add(_modified);
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
        void Shortcut(VirtualKey key, VirtualKeyModifiers modifiers, Action action)
        { var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers }; accelerator.Invoked += (_, e) => { if (!_dialog) { action(); e.Handled = true; } }; KeyboardAccelerators.Add(accelerator); }
        Shortcut(VirtualKey.Left, VirtualKeyModifiers.Menu, () => { if (_history.BackTarget is string path) _ = NavigateAsync(path, -1); });
        Shortcut(VirtualKey.Right, VirtualKeyModifiers.Menu, () => { if (_history.ForwardTarget is string path) _ = NavigateAsync(path, 1); });
        Shortcut(VirtualKey.L, VirtualKeyModifiers.Control, () => { _address.Focus(FocusState.Programmatic); _address.SelectAll(); });
        Shortcut(VirtualKey.F, VirtualKeyModifiers.Control, () => _filter.Focus(FocusState.Programmatic));
        Shortcut(VirtualKey.F5, VirtualKeyModifiers.None, () => _ = NavigateAsync(_recycle ? "nexus:recycle" : _folder));
        SizeChanged += (_, _) => _filter.Width = ActualWidth < 700 ? 120 : 170;
        ApplyAppearance(); UpdateActions();
    }
    private static FileEntry? ItemAt(ListViewBase view, object source)
    {
        DependencyObject? node = source as DependencyObject;
        while (node is not null && node is not ListViewItem && node is not GridViewItem && !ReferenceEquals(node, view)) node = VisualTreeHelper.GetParent(node);
        return (node as ContentControl)?.Content as FileEntry;
    }
    private Button Button(string title, Func<Task> action)
    {
        var button = new Button { Content = title, Style = (Style)Application.Current.Resources["QuietButton"], HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += async (_, _) => { if (_dialog || _closed || _environment.Validation) return; button.IsEnabled = false; try { await action(); } catch (Exception ex) { ShowError(ex); } finally { if (!_closed) { button.IsEnabled = true; UpdateActions(); } } }; return button;
    }
    private Button PlaceButton(string title, string path)
    {
        var button = Button(title, () => NavigateAsync(path)); button.HorizontalContentAlignment = HorizontalAlignment.Left; button.Padding = new Thickness(9, 7, 9, 7); button.CornerRadius = new CornerRadius(8);
        var face = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9 };
        string glyph = title switch { "Downloads" => "\uE896", "Desktop" => "\uE7F4", "Documents" => "\uE8A5", "Pictures" => "\uEB9F", "Music" => "\uE8D6", "Videos" => "\uE714", "Recycle Bin" => "\uE74D", _ => "\uE8B7" };
        face.Children.Add(new FontIcon { Glyph = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 }); face.Children.Add(new TextBlock { Text = title, FontSize = 12 }); button.Content = face;
        _places.Add(button, path); return button;
    }
    internal async Task NavigateAsync(string path, int historyMove = 0)
    {
        if (_closed || _dialog || _environment.Validation || (historyMove != 0 && _navigating)) return;
        int version = ++_navigationVersion; _navigating = true;
        ResetPreview(); UpdateActions(); _status.Text = "Opening folder…"; _loading.IsActive = true;
        _slowRead.Stop(); _slowRead.Start();
        try
        {
            if (path == "nexus:recycle" && _request.Kind == FileSelectionKind.Browse)
            {
                var info = await _navigation.RunAsync(token => Task.Run(RecycleBinService.Read, token));
                if (_closed || version != _navigationVersion) return;
                _recycle = true; _folder = ""; _entries = []; _binHasItems = info.Items > 0;
                _list.ItemsSource = _tiles.ItemsSource = null;
                _binCount.Text = info.Items + " items · " + FileCatalog.Size(info.Bytes);
                _address.Text = "Recycle Bin"; _bin.Visibility = Visibility.Visible; _list.Visibility = _tiles.Visibility = Visibility.Collapsed; _filter.IsEnabled = false;
                UpdateInspector(); CommitHistory("nexus:recycle", historyMove); _status.Text = "Recycle Bin · Windows storage, Nexus controls"; return;
            }
            var snapshot = await _navigation.RunAsync(token => Task.Run(() => FileCatalog.Read(path, _request.Extensions, token), token));
            if (_closed || version != _navigationVersion) return;
            _folder = snapshot.Path; _entries = snapshot.Entries; _recycle = false; _filter.IsEnabled = true; _filter.Text = "";
            CommitHistory(_folder, historyMove);
            _address.Text = _folder; _bin.Visibility = Visibility.Collapsed; _list.Visibility = Visibility.Visible; Render(); UpdateInspector();
            _status.Text = _entries.Count + " items" + (snapshot.Limited ? " · First 1,000 entries; use the path field to reach another item" : " · Double-click or press Enter to open");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_closed && version == _navigationVersion) { ShowError(ex); _address.Text = _recycle ? "Recycle Bin" : _folder; } }
        finally { if (!_closed && version == _navigationVersion) { _slowRead.Stop(); _navigating = false; _loading.IsActive = false; UpdateInspector(); } }
    }
    private void CommitHistory(string path, int move)
    {
        if (move < 0) _history.Back(); else if (move > 0) _history.Forward(); else _history.Visit(path);
        RefreshPlaces(); FolderChanged?.Invoke(path == "nexus:recycle" ? "Recycle Bin" : Path.GetFileName(Path.TrimEndingDirectorySeparator(path)) is { Length: > 0 } name ? name : path);
    }
    private void RefreshPlaces()
    {
        foreach (var (button, path) in _places)
        {
            bool selected = string.Equals(path, _recycle ? "nexus:recycle" : _folder, StringComparison.OrdinalIgnoreCase);
            button.Background = selected ? _environment.Theme.Brush("NexusSelection") : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            button.Foreground = _environment.Theme.Brush(selected ? "NexusText" : "NexusSecondary");
        }
    }
    private void Render()
    {
        if (_closed || _rendering) return; _rendering = true;
        try
        {
            var selected = _list.SelectedItems.Concat(_tiles.SelectedItems).OfType<FileEntry>().Select(e => e.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var entries = FilePresentation.Filter(_entries, _filter.Text, (FileSort)Math.Max(0, _sort.SelectedIndex), _sort.SelectedIndex is 1 or 2);
            _list.ItemsSource = _showGrid ? null : entries; _tiles.ItemsSource = _showGrid ? entries : null;
            _list.Visibility = !_showGrid && !_recycle ? Visibility.Visible : Visibility.Collapsed; _tiles.Visibility = _showGrid && !_recycle ? Visibility.Visible : Visibility.Collapsed;
            foreach (var item in entries.Where(e => selected.Contains(e.Path))) ActiveList.SelectedItems.Add(item);
            _gridButton.Background = _environment.Theme.Brush(_showGrid ? "NexusSelection" : "NexusSidebar"); _listButton.Background = _environment.Theme.Brush(!_showGrid ? "NexusSelection" : "NexusSidebar");
            string folderName = _folder.Length > 0 ? Path.GetFileName(Path.TrimEndingDirectorySeparator(_folder)) : "";
            _folderHeading.Text = _recycle ? "Recycle Bin" : folderName.Length > 0 ? folderName : "Contents";
            if (!_recycle) _status.Text = entries.Length + " of " + _entries.Count + " items";
        }
        finally { _rendering = false; }
        UpdateInspector();
    }
    private async Task OpenAsync(FileEntry entry)
    {
        if (_closed || _dialog || _navigating || _recycle) return;
        try { if (entry.IsFolder) await NavigateAsync(entry.Path); else if (_request.Kind == FileSelectionKind.Browse) _environment.OpenTarget(entry.Path); else if (_request.Kind is FileSelectionKind.OpenFile or FileSelectionKind.OpenFiles) await ChooseAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }
    private async Task ChooseAsync()
    {
        if (_closed || _dialog || _navigating || _recycle || _folder.Length == 0) return;
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
                var selected = ActiveList.SelectedItems.Cast<FileEntry>().Where(e => !e.IsFolder && File.Exists(e.Path) && FileCatalog.Accepts(e.Path, _request.Extensions)).Select(e => e.Path).ToArray();
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
        if (_closed || _dialog || _navigating || _recycle || _folder.Length == 0) return; _dialog = true; UpdateActions();
        try
        {
            var name = new TextBox { PlaceholderText = "Folder name" }; var dialog = Dialog("New folder", name, "Create");
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || _closed) return;
            string folder = _folder, value = name.Text;
            await Task.Run(() => _edits.CreateFolder(folder, value));
        }
        finally { _dialog = false; if (!_closed) UpdateActions(); }
        await NavigateAsync(_folder);
    }
    private async Task RenameAsync(FileEntry? target = null)
    {
        var item = target ?? ActiveList.SelectedItem as FileEntry;
        if (_closed || _dialog || _navigating || _recycle || _request.Kind != FileSelectionKind.Browse || item is null) return;
        _dialog = true; UpdateActions(); string? destination = null;
        try
        {
            var name = new TextBox { Header = "Name, including the file extension", Text = item.Name, MaxLength = 240 };
            if (await Dialog("Rename item", name, "Rename").ShowAsync() != ContentDialogResult.Primary || _closed) return;
            string value = name.Text; _status.Text = "Renaming…";
            destination = await Task.Run(() => _edits.Rename(item.Path, value));
        }
        finally { _dialog = false; if (!_closed) UpdateActions(); }
        if (_closed) return; await NavigateAsync(_folder);
        ActiveList.SelectedItem = _entries.FirstOrDefault(e => e.Path == destination);
        _status.Text = _edits.CanUndo ? "Renamed. Use Undo edit or Ctrl+Z while the file list has focus." : "Renamed. Metadata was unavailable, so this edit cannot be undone here.";
    }
    private async Task UndoAsync()
    {
        if (_closed || _dialog || _navigating || _request.Kind != FileSelectionKind.Browse || !_edits.CanUndo) return; _dialog = true; UpdateActions();
        try { _status.Text = "Undoing file edit…"; await Task.Run(_edits.Undo); }
        finally { _dialog = false; if (!_closed) UpdateActions(); }
        if (!_closed) { await NavigateAsync(_recycle ? "nexus:recycle" : _folder); _status.Text = "File edit undone."; }
    }
    private async Task EmptyBinAsync()
    {
        if (_navigating || !_recycle) return;
        if (!await ConfirmAsync("Empty Recycle Bin?", "All deleted items for this user will be permanently removed from the Recycle Bin.", "Empty bin") || _closed) return;
        _dialog = true; UpdateActions();
        try { await Task.Run(() => RecycleBinService.EmptyConfirmed(IntPtr.Zero)); }
        finally { _dialog = false; if (!_closed) UpdateActions(); }
        await NavigateAsync("nexus:recycle");
    }
    internal void ShowMessage(string message) { if (!_closed) _status.Text = message; }
    private async Task TransferAsync(FileJobKind kind, FileEntry? target = null)
    {
        var item = target ?? ActiveList.SelectedItem as FileEntry;
        if (_closed || _dialog || _navigating || _recycle || item is null) return;
        _dialog = true;
        try { await _environment.QueueTransferAsync([item.Path], kind); }
        catch (OperationCanceledException) when (_closed) { }
        catch (Exception error) { if (!_closed) ShowError(error); }
        finally { _dialog = false; UpdateActions(); }
    }
    private void BuildContext(MenuFlyout menu, FileEntry? target)
    {
        menu.Items.Clear();
        if (_closed || _navigating || _dialog || _recycle) { menu.Items.Add(new MenuFlyoutItem { Text = "File actions unavailable", IsEnabled = false }); return; }
        if (target is null)
        {
            menu.Items.Add(ShellMenu.Action("New folder…", () => RunContextAsync(NewFolderAsync), "\uE8F4", enabled: _folder.Length > 0));
            menu.Items.Add(ShellMenu.Action("Refresh", () => RunContextAsync(() => NavigateAsync(_folder)), "\uE72C", "F5"));
            var view = ShellMenu.Group("View", "\uE8A9"); view.Items.Add(ShellMenu.Toggle("List", !_showGrid, () => { _showGrid = false; Render(); })); view.Items.Add(ShellMenu.Toggle("Grid", _showGrid, () => { _showGrid = true; Render(); })); menu.Items.Add(view);
            if (_request.Kind == FileSelectionKind.Browse) menu.Items.Add(ShellMenu.Action("Open folder in Windows Explorer", () => _environment.OpenTarget(_folder), "\uED25"));
            return;
        }
        // A context invocation keeps its original target even if selection or
        // a folder refresh changes before the user chooses an action.
        var policy = FileContextPolicy.For(target.Path, target.IsFolder);
        menu.Items.Add(ShellMenu.Action(policy.OpenLabel, () => RunContextAsync(() => OpenAsync(target)), policy.Icon));
        if (policy.OpenWith) menu.Items.Add(ShellMenu.Action("Open with…", () => RunContextAsync(() => _environment.WindowsFileAsync(target.Path, "open-with")), "\uE8A5"));
        menu.Items.Add(new MenuFlyoutSeparator());
        var transfer = ShellMenu.Group("Transfer", "\uE8B7"); transfer.Items.Add(ShellMenu.Action("Copy to…", () => RunContextAsync(() => TransferAsync(FileJobKind.Copy, target)), "\uE8C8")); transfer.Items.Add(ShellMenu.Action("Move to…", () => RunContextAsync(() => TransferAsync(FileJobKind.Move, target)), "\uE8DE")); menu.Items.Add(transfer);
        menu.Items.Add(ShellMenu.Action("Rename…", () => RunContextAsync(() => RenameAsync(target)), "\uE8AC", "F2"));
        var more = ShellMenu.Group("More", "\uE712");
        more.Items.Add(ShellMenu.Action("Copy path", () =>
        {
            var data = new Windows.ApplicationModel.DataTransfer.DataPackage(); data.SetText(target.Path);
            try { Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data); _status.Text = "Path copied."; } catch (Exception error) { ShowError(error); }
        }, "\uE8C8"));
        more.Items.Add(ShellMenu.Action("Show in Windows Explorer", () => RunContextAsync(() => _environment.WindowsFileAsync(target.Path, "reveal")), "\uED25"));
        more.Items.Add(ShellMenu.Action("Properties…", () => RunContextAsync(() => _environment.WindowsFileAsync(target.Path, "properties")), "\uE946"));
        if (_edits.CanUndo) more.Items.Add(ShellMenu.Action(_edits.UndoLabel, () => RunContextAsync(UndoAsync), "\uE7A7", "Ctrl+Z")); menu.Items.Add(more);
        menu.Items.Add(new MenuFlyoutSeparator()); menu.Items.Add(ShellMenu.Action("Windows options…", () => RunContextAsync(() => _environment.WindowsFileAsync(target.Path, "menu")), "\uE713"));
    }
    private async void RunContextAsync(Func<Task> action)
    { try { if (!_closed && !_environment.Validation) await action(); } catch (OperationCanceledException) when (_closed) { } catch (Exception error) { if (!_closed) ShowError(error); } }
    internal void VerifyValidationContexts()
    {
        if (!_environment.Validation) throw new InvalidOperationException("Context-menu validation requires an isolated Files fixture.");
        if (_list.SelectionMode != ListViewSelectionMode.Multiple
            || _tiles.SelectionMode != ListViewSelectionMode.Multiple)
            throw new InvalidOperationException("Files browse must enable native multiple selection in list and grid.");
        foreach (var (path, folder) in new[] { (@"C:\Example\Folder", true), (@"C:\Example\photo.png", false), (@"C:\Example\music.flac", false), (@"C:\Example\archive.zip", false), (@"C:\Example\setup.exe", false), (@"C:\Example\unknown.custom", false) })
        {
            var menu = ShellMenu.Create(); BuildContext(menu, new FileEntry(Path.GetFileName(path), path, folder, "Fixture"));
            var policy = FileContextPolicy.For(path, folder);
            if (menu.Items.Count > 9 || menu.Items.OfType<MenuFlyoutItem>().First().Text != policy.OpenLabel || menu.Items.OfType<MenuFlyoutItem>().Any(item => item.Text == "Open with…") != policy.OpenWith)
                throw new InvalidOperationException("The production file context did not preserve compact type-specific actions.");
        }
    }
    private ContentDialog Dialog(string title, object content, string primary) => new() { XamlRoot = XamlRoot, RequestedTheme = RequestedTheme, Title = title, Content = content,
        PrimaryButtonText = primary, CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
    private async Task<bool> ConfirmAsync(string title, string content, string primary)
    { if (_dialog) return false; _dialog = true; UpdateActions(); try { return await Dialog(title, content, primary).ShowAsync() == ContentDialogResult.Primary; } finally { _dialog = false; if (!_closed) UpdateActions(); } }
    private void ShowError(Exception error) { if (!_closed) _status.Text = error.Message; Log.Write("Nexus Files", error); }
    private void UpdateInspector()
    {
        UpdateActions();
        if (_recycle)
        {
            ResetPreview(); _fileTitle.Text = "Recycle Bin"; _fileKind.Text = "Deleted items";
            _fileLocation.Text = "Windows Recycle Bin"; _modified.Text = ""; _fileIcon.Source = NexusIcons.Source("Windows"); return;
        }
        if (ActiveList.SelectedItems.Count > 1)
        {
            ResetPreview(); _fileIcon.Source = NexusIcons.Source("Files");
            _fileTitle.Text = ActiveList.SelectedItems.Count + " items selected";
            _fileKind.Text = "Use Ctrl or Shift to change selection.";
            _fileLocation.Text = _folder; _modified.Text = "";
        }
        else if (ActiveList.SelectedItem is FileEntry item)
        {
            _fileTitle.Text = item.Name;
            _fileKind.Text = item.IsFolder ? "Folder" : item.Detail;
            _fileLocation.Text = item.Path;
            _modified.Text = item.ModifiedUtc == default ? "" : "Modified\n" + item.ModifiedUtc.ToLocalTime().ToString("d MMM yyyy, h:mm tt");
            _fileIcon.Source = NexusIcons.Source(FilePresentation.Icon(item.Path, item.IsFolder));
            if (!_navigating && _previewPath != item.Path) { _previewPath = item.Path; _ = PreviewAsync(item, ++_previewVersion); }
        }
        else
        {
            _fileTitle.Text = "Choose an item";
            _fileKind.Text = "Select a file or folder to inspect it.";
            _fileLocation.Text = _folder;
            _modified.Text = ""; ResetPreview(); _fileIcon.Source = NexusIcons.Source("Files");
        }
    }
    private void UpdateActions()
    {
        bool ready = !_closed && !_dialog && !_navigating;
        _back.IsEnabled = ready && _history.CanGoBack; _forward.IsEnabled = ready && _history.CanGoForward;
        _list.IsEnabled = _tiles.IsEnabled = ready;
        _choose.IsEnabled = _newFolder.IsEnabled = ready && !_recycle && _folder.Length > 0;
        _rename.IsEnabled = ready && !_recycle
            && ActiveList.SelectedItems.Count == 1 && ActiveList.SelectedItem is FileEntry;
        _undo.IsEnabled = ready && _edits.CanUndo; _undo.Content = _edits.UndoLabel;
        _empty.IsEnabled = ready && _recycle && _binHasItems;
    }
    private void ResetPreview()
    {
        _previews.Cancel(); _previewPath = null; _previewVersion++;
        _preview.Source = null; _preview.Visibility = Visibility.Collapsed; _fileIcon.Visibility = Visibility.Visible;
    }
    private async Task PreviewAsync(FileEntry entry, int version)
    {
        _previews.Cancel(); _preview.Source = null; _preview.Visibility = Visibility.Collapsed; _fileIcon.Visibility = Visibility.Visible;
        if (entry.IsFolder || !FilePresentation.CanPreview(entry.Path) || entry.Bytes > 20 * 1024 * 1024) return;
        try
        {
            var bitmap = await _previews.RunAsync(async token =>
            {
                var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(entry.Path);
                token.ThrowIfCancellationRequested();
                using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
                token.ThrowIfCancellationRequested();
                var image = new BitmapImage { DecodePixelWidth = 440, DecodePixelHeight = 440 };
                await image.SetSourceAsync(stream); return image;
            });
            if (_closed || version != _previewVersion) return;
            _preview.Source = bitmap; _preview.Visibility = Visibility.Visible; _fileIcon.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Log.Write("File preview unavailable; keeping the file icon", ex); }
    }
    internal void ApplyAppearance()
    {
        var theme = _environment.Theme; RequestedTheme = theme.ElementTheme; Background = null;
        _status.Foreground = _modified.Foreground = theme.Brush("NexusMuted"); _list.Foreground = _tiles.Foreground = theme.Brush("NexusText");
        _placesCard.Background = MidnightGlassMetrics.ReferenceEnabled(theme.Palette, theme.HighContrast)
            ? theme.Surface("Sidebar") : theme.Material("Panel", _environment.State.NativeGlass);
        _placesCard.BorderBrush = theme.Edge;
        _placesCard.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : MidnightGlassMetrics.CardRadius);
        _inspectorCard.Background = theme.Material("Card", _environment.State.NativeGlass);
        _inspectorCard.BorderBrush = theme.Edge;
        _inspectorCard.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : MidnightGlassMetrics.CardRadius);
        _contentsCard.Background = MidnightGlassMetrics.ReferenceEnabled(theme.Palette, theme.HighContrast)
            ? theme.Surface("Dock") : theme.Material("Card", _environment.State.NativeGlass);
        _contentsCard.BorderBrush = theme.Edge;
        _contentsCard.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : MidnightGlassMetrics.CardRadius);
        _previewStage.Background = MidnightGlassMetrics.ReferenceEnabled(theme.Palette, theme.HighContrast)
            ? theme.Surface("Hero") : theme.Material("Card", _environment.State.NativeGlass);
        _previewStage.BorderBrush = theme.Edge;
        _previewStage.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : 14);
        _folderHeading.Foreground = theme.Brush("NexusText");
        _fileTitle.Foreground = theme.Brush("NexusText"); _fileKind.Foreground = theme.Brush("NexusMuted");
        _fileLocation.Foreground = theme.Brush("NexusSecondary");
        RefreshPlaces();
        foreach (var text in _bin.Children.OfType<TextBlock>()) text.Foreground = theme.Brush(text == _binCount ? "NexusText" : "NexusMuted");
    }
    internal void Release() { _closed = true; _slowRead.Stop(); _navigationVersion++; _previewVersion++; _preview.Source = null; _navigation.Dispose(); _previews.Dispose(); _entries = []; }
}
