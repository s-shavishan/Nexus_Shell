using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    private bool _exploreSync;
    private long _explorePreviewEpoch;
    private ExploreSpace ActiveExploreSpace => _state.ExploreSpaces.First(s => s.Id == _state.ActiveExploreSpaceId);
    private bool WideExplore => ExploreView.ActualWidth >= 790;

    private void NewExploreNote_Click(object sender, RoutedEventArgs args) => NewExploreNote();
    private async void NewExploreNote()
    {
        if (!_ready || _dialogOpen || _picking) return;
        try { Navigate("Explore"); await EditExploreItemAsync(null, "Note"); }
        catch (Exception ex) { Error("Could not create a note", ex); }
    }
    private void RefreshDesktopSpaceShortcuts()
    {
        DesktopSpaceShortcuts.Children.Clear();
        foreach (var space in _state.ExploreSpaces.Take(3))
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            var name = Text(space.Name, 13); name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis;
            row.Children.Add(name);
            var count = Text(_state.SavedItems.Count(a => a.SpaceId == space.Id).ToString(), 11, true);
            Grid.SetColumn(count, 1); row.Children.Add(count);
            var button = new Button { Content = row, Style = (Style)Application.Current.Resources["QuietButton"],
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            button.Click += (_, _) => EnterExploreSpace(space.Id);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, "Open " + space.Name + " space");
            DesktopSpaceShortcuts.Children.Add(button);
        }
    }

    private void ReleaseExploreControls()
    {
        _explorePreviewEpoch++;
        _exploreSync = true;
        try { ExploreGrid.ItemsSource = null; ExploreDetailContent.Children.Clear(); }
        finally { _exploreSync = false; }
    }
    private void BuildExplore()
    {
        ExploreWorkspace.Normalize(_state);
        _exploreSync = true;
        try
        {
            ExploreHeading.Text = ActiveExploreSpace.Name;
            ExploreDescription.Text = ActiveExploreSpace.Description;
            ExploreQueryBox.Text = _state.ExploreQuery;
            ExploreFavoriteToggle.IsChecked = _state.ExploreFavoritesOnly;
            ExploreSpacesStrip.Children.Clear();
            foreach (var space in _state.ExploreSpaces)
            {
                var face = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                face.Children.Add(new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 7, Height = 7,
                    Fill = _highContrast ? Resource("NexusText") : ExploreAccent(space.Accent), VerticalAlignment = VerticalAlignment.Center });
                face.Children.Add(Text(space.Name, 12));
                face.Children.Add(Text(_state.SavedItems.Count(a => a.SpaceId == space.Id).ToString(), 10, true));
                var button = new Button { Content = face, Tag = space.Id,
                    Style = (Style)Application.Current.Resources["QuietButton"], Padding = new Thickness(12, 8),
                    Background = space.Id == ActiveExploreSpace.Id ? _selection : _transparent };
                if (_highContrast && space.Id == ActiveExploreSpace.Id)
                    foreach (var text in face.Children.OfType<TextBlock>()) text.Foreground = Resource("NexusAccentText");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, "Explore space: " + space.Name);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(button, space.Id == ActiveExploreSpace.Id ? "Selected space" : "");
                button.Click += (_, _) => EnterExploreSpace(space.Id);
                ExploreSpacesStrip.Children.Add(button);
            }
            string[] collections = _state.SavedItems.Where(a => a.SpaceId == ActiveExploreSpace.Id).Select(a => a.Collection)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(a => a, StringComparer.CurrentCultureIgnoreCase).Prepend("").ToArray();
            ExploreCollectionsBox.Items.Clear();
            foreach (string collection in collections)
                ExploreCollectionsBox.Items.Add(new ComboBoxItem { Content = collection.Length == 0 ? "All collections" : collection, Tag = collection });
            ExploreCollectionsBox.SelectedItem = ExploreCollectionsBox.Items.Cast<ComboBoxItem>().FirstOrDefault(c =>
                ((string)c.Tag).Equals(_state.ExploreCollection, StringComparison.OrdinalIgnoreCase)) ?? ExploreCollectionsBox.Items[0];
        }
        finally { _exploreSync = false; }
        RenderExploreBoard(); UpdateExploreLayout();
    }
    private Brush ExploreAccent(string name) => new SolidColorBrush(AuraColorValue(name switch
    { "Ocean" => "FF8CCBFA", "Mint" => "FF91E0CB", "Rose" => "FFF0ABC7", _ => "FFC8BEFF" }));
    private void EnterExploreSpace(string id)
    {
        if (!_ready || _dialogOpen || _picking || !_state.ExploreSpaces.Any(s => s.Id == id)) return;
        bool changed = _state.ActiveExploreSpaceId != id;
        _state.ActiveExploreSpaceId = id;
        if (changed)
        {
            _state.ExploreSelectedItemId = ""; _state.ExploreQuery = "";
            _state.ExploreCollection = ""; _state.ExploreFavoritesOnly = false;
        }
        SaveState();
        if (_page != "Explore") Navigate("Explore"); else BuildExplore();
    }
    private void SelectExploreItem(SavedItem item)
    {
        if (!_state.SavedItems.Any(a => a.Id == item.Id)) return;
        _state.ActiveExploreSpaceId = item.SpaceId; _state.ExploreSelectedItemId = item.Id;
        _state.ExploreQuery = ""; _state.ExploreCollection = ""; _state.ExploreFavoritesOnly = false;
        SaveState();
        if (_page != "Explore") Navigate("Explore"); else BuildExplore();
        if (!WideExplore) _ = InspectExploreSafelyAsync(item);
    }
    private void RenderExploreBoard()
    {
        if (!_ready || _page != "Explore") return;
        var entries = ExploreWorkspace.Filter(_state);
        _exploreSync = true;
        try
        {
            ExploreGrid.ItemTemplate = (DataTemplate)DesktopRoot.Resources[_state.ExploreView == "List" ? "ExploreListTile" : "ExploreTile"];
            ExploreGrid.ItemsSource = entries;
            ExploreGrid.SelectedItem = entries.FirstOrDefault(a => a.Id == _state.ExploreSelectedItemId);
        }
        finally { _exploreSync = false; }
        ExploreEmpty.Visibility = entries.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ExploreStats.Text = entries.Length + " shown · " + _state.SavedItems.Count(a => a.SpaceId == ActiveExploreSpace.Id) + " in this space";
        PageStatus.Text = ActiveExploreSpace.Name + " · " + _state.SavedItems.Count + " / 100 items · Stored on this PC";
        foreach (var button in new[] { ExploreBoardMode, ExploreListMode })
        {
            bool selected = (string)button.Tag == _state.ExploreView;
            button.Background = selected ? _selection : _transparent;
            button.Foreground = Resource(_highContrast && selected ? "NexusAccentText" : "NexusText");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(button, selected ? "Selected view" : "");
        }
        RenderExploreDetails(ExploreGrid.SelectedItem as SavedItem);
    }
    private void ExploreQuery_TextChanged(object sender, TextChangedEventArgs args)
    {
        if (!_ready || _exploreSync || _page != "Explore") return;
        _state.ExploreQuery = ExploreQueryBox.Text.Trim(); SaveState(); RenderExploreBoard();
    }
    private void ExploreCollection_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!_ready || _exploreSync || _page != "Explore") return;
        _state.ExploreCollection = (ExploreCollectionsBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        SaveState(); RenderExploreBoard();
    }
    private void ExploreFavorite_Changed(object sender, RoutedEventArgs args)
    {
        if (!_ready || _exploreSync || _page != "Explore") return;
        _state.ExploreFavoritesOnly = ExploreFavoriteToggle.IsChecked == true; SaveState(); RenderExploreBoard();
    }
    private void ExploreMode_Click(object sender, RoutedEventArgs args)
    {
        _state.ExploreView = (string)((Button)sender).Tag; SaveState(); RenderExploreBoard(); UpdateExploreLayout();
    }
    private void ExploreSelection_Changed(object sender, SelectionChangedEventArgs args)
    {
        if (!_ready || _exploreSync || _page != "Explore") return;
        if (ExploreGrid.SelectedItem is SavedItem item) _state.ExploreSelectedItemId = item.Id;
        else _state.ExploreSelectedItemId = "";
        SaveState(); RenderExploreDetails(ExploreGrid.SelectedItem as SavedItem);
    }
    private async void ExploreItem_Click(object sender, ItemClickEventArgs args)
    {
        if (!WideExplore && args.ClickedItem is SavedItem item) await InspectExploreSafelyAsync(item);
    }
    private async Task InspectExploreSafelyAsync(SavedItem item)
    {
        try { await InspectExploreAsync(item); }
        catch (Exception ex) { Error("Could not show item preview", ex); }
    }
    private void ExploreGrid_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key is VirtualKey.Enter or VirtualKey.Space && ExploreGrid.SelectedItem is SavedItem item)
        { _ = InspectExploreSafelyAsync(item); args.Handled = true; }
    }
    private void ExploreGrid_SizeChanged(object sender, SizeChangedEventArgs args) => UpdateExploreTileSize();
    private void UpdateExploreTileSize()
    {
        if (!_ready || _page != "Explore") return;
        if (ExploreGrid.ItemsPanelRoot is not ItemsWrapGrid panel) return;
        double width = Math.Max(160, ExploreGrid.ActualWidth - 12);
        bool list = _state.ExploreView == "List";
        int columns = list ? 1 : Math.Clamp((int)(width / 222), 1, 4);
        panel.ItemWidth = Math.Floor(width / columns); panel.ItemHeight = list ? 106 : 212;
    }
    private void UpdateExploreLayout()
    {
        if (!_ready || _page != "Explore") return;
        bool compact = ExploreView.ActualWidth < 680;
        Grid.SetColumnSpan(ExploreQueryBox, compact ? 3 : 1);
        Grid.SetRow(ExploreCollectionsBox, compact ? 1 : 0); Grid.SetColumn(ExploreCollectionsBox, compact ? 0 : 1);
        Grid.SetColumnSpan(ExploreCollectionsBox, compact ? 2 : 1);
        Grid.SetRow(ExploreViewOptions, compact ? 1 : 0);
        ExploreFilterGrid.ColumnDefinitions[1].Width = new GridLength(compact ? 0 : 160);
        bool detail = WideExplore && ExploreGrid.SelectedItem is SavedItem;
        ExploreDetailPanel.Visibility = detail ? Visibility.Visible : Visibility.Collapsed;
        ExploreDetailColumn.Width = new GridLength(detail ? 266 : 0);
        ExploreBody.ColumnSpacing = detail ? 16 : 0;
        ExploreView.Padding = new Thickness(ExploreView.ActualWidth < 420 ? 16 : 24);
        UpdateExploreTileSize();
    }
    private void RenderExploreDetails(SavedItem? item)
    {
        _explorePreviewEpoch++;
        ExploreDetailContent.Children.Clear();
        if (item is not null && WideExplore) FillExploreDetails(ExploreDetailContent, item, true);
        UpdateExploreLayout();
    }
    private void FillExploreDetails(StackPanel content, SavedItem item, bool closeButton, bool actions = true)
    {
        var title = new Grid { ColumnSpacing = 8 };
        title.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        title.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        title.Children.Add(Text(item.Title, 18));
        if (closeButton)
        {
            var close = ActionButton("×", () =>
            { _state.ExploreSelectedItemId = ""; ExploreGrid.SelectedItem = null; SaveState(); RenderExploreDetails(null); });
            close.Padding = new Thickness(8, 2);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(close, "Close item preview");
            Grid.SetColumn(close, 1); title.Children.Add(close);
        }
        content.Children.Add(title);
        content.Children.Add(Text(item.Collection + " · " + item.Kind + (item.Favorite ? " · Favorite" : ""), 11, true));
        if (item.Tags.Length > 0) content.Children.Add(Text("#" + item.Tags.Replace(", ", "   #"), 11, true));
        if (item.Target.Length > 0)
        {
            var address = Text(item.Target, 11, true); address.IsTextSelectionEnabled = true;
            content.Children.Add(address);
        }
        if (item.Note.Length > 0)
        {
            var note = Text(item.Note, 13); note.IsTextSelectionEnabled = true;
            content.Children.Add(Card(note));
        }
        if (item.Kind == "Link") content.Children.Add(Text("Opens in your default browser.", 11, true));
        if (item.Kind is "File" or "Folder")
        {
            var preview = new StackPanel { Spacing = 10 }; content.Children.Add(preview);
            _ = LoadExplorePreviewAsync(item, preview, _explorePreviewEpoch);
        }
        if (!actions) return;
        if (item.Kind != "Note")
            content.Children.Add(ActionButton(item.Kind == "Link" ? "Open in browser ↗" : "Open in Windows ↗", () => OpenSaved(item)));
        content.Children.Add(AsyncButton("Edit details…", () => EditExploreItemAsync(item, item.Kind)));
        content.Children.Add(ActionButton(item.Favorite ? "Remove favorite" : "Add to favorites", () =>
        {
            int index = _state.SavedItems.FindIndex(a => a.Id == item.Id);
            if (index >= 0) { _state.SavedItems[index] = item with { Favorite = !item.Favorite }; SavedItemsChanged(); }
        }));
        var move = new Button { Content = "Move to space…", Style = (Style)Application.Current.Resources["AuraSurfaceButton"], HorizontalAlignment = HorizontalAlignment.Stretch };
        var menu = new MenuFlyout();
        foreach (var space in _state.ExploreSpaces.Where(s => s.Id != item.SpaceId))
        {
            var destination = new MenuFlyoutItem { Text = space.Name };
            destination.Click += (_, _) =>
            {
                int index = _state.SavedItems.FindIndex(a => a.Id == item.Id);
                if (index < 0) return;
                if (_state.SavedItems.Any(a => a.Id != item.Id && ExploreWorkspace.SameReference(a, item with { SpaceId = space.Id })))
                { ShowStatus("This item is already in " + space.Name + "."); return; }
                _state.SavedItems[index] = item with { SpaceId = space.Id };
                SavedItemsChanged(); ShowStatus("Moved to " + space.Name + ".");
            };
            menu.Items.Add(destination);
        }
        move.Flyout = menu; move.IsEnabled = menu.Items.Count > 0; content.Children.Add(move);
        content.Children.Add(AsyncButton("Remove from Explore…", () => RemoveExploreItemAsync(item)));
    }
    private async Task LoadExplorePreviewAsync(SavedItem item, StackPanel host, long epoch)
    {
        try
        {
            bool exists = await Task.Run(() => item.Kind == "Folder" ? Directory.Exists(item.Target) : File.Exists(item.Target));
            if (!_ready || epoch != _explorePreviewEpoch) return;
            if (!exists) { host.Children.Add(Text("This shortcut is unavailable. The original may have moved.", 12, true)); return; }
            string extension = Path.GetExtension(item.Target).ToLowerInvariant();
            if (item.Kind == "File" && extension is ".txt" or ".md" or ".json" or ".log" or ".csv")
            {
                string text = await Task.Run(async () =>
                {
                    using var stream = new FileStream(item.Target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    byte[] buffer = new byte[16 * 1024]; int read = await stream.ReadAsync(buffer);
                    return Encoding.UTF8.GetString(buffer, 0, read);
                });
                if (!_ready || epoch != _explorePreviewEpoch) return;
                host.Children.Add(Text("TEXT PREVIEW · FIRST 16 KB", 9, true));
                var body = Text(text, 12); body.IsTextSelectionEnabled = true;
                host.Children.Add(new ScrollViewer { Content = body, MaxHeight = 210, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            }
            else if (item.Kind == "File" && extension is ".png" or ".jpg" or ".jpeg" or ".bmp")
            {
                var file = await StorageFile.GetFileFromPathAsync(item.Target);
                var properties = await file.GetBasicPropertiesAsync();
                if (!_ready || epoch != _explorePreviewEpoch) return;
                if (properties.Size > 8 * 1024 * 1024) { host.Children.Add(Text("Open this image in Windows to view it at full size.", 12, true)); return; }
                using var stream = await file.OpenReadAsync();
                var bitmap = new BitmapImage { DecodePixelWidth = 512 };
                await bitmap.SetSourceAsync(stream);
                if (!_ready || epoch != _explorePreviewEpoch) return;
                host.Children.Add(new Image { Source = bitmap, MaxHeight = 200, Stretch = Stretch.Uniform });
            }
            else host.Children.Add(Text(item.Kind == "Folder" ? "Folder shortcut · Original files stay in Windows." : "Open in your associated Windows app to view this document.", 12, true));
        }
        catch (Exception ex)
        {
            Log.Write("Explore preview unavailable", ex);
            if (_ready && epoch == _explorePreviewEpoch) host.Children.Add(Text("Preview unavailable. You can still try opening the original.", 12, true));
        }
    }
    private async Task InspectExploreAsync(SavedItem item)
    {
        if (_dialogOpen || _picking || !_ready) return;
        _dialogOpen = true;
        try
        {
            _explorePreviewEpoch++;
            var content = new StackPanel { Spacing = 12 }; FillExploreDetails(content, item, false, false);
            bool removeRequested = false;
            var dialog = new ContentDialog { XamlRoot = DesktopRoot.XamlRoot, RequestedTheme = DesktopRoot.RequestedTheme,
                Title = "Quick Look", Content = new ScrollViewer { Content = content, MaxHeight = Math.Max(120, Math.Min(480, DesktopRoot.ActualHeight - 200)), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
                PrimaryButtonText = "Edit…", CloseButtonText = "Close", SecondaryButtonText = item.Kind == "Note" ? "" : "Open ↗" };
            var remove = ActionButton("Remove from Explore…", () => { removeRequested = true; dialog.Hide(); });
            content.Children.Add(remove);
            PolishDialog(dialog);
            var result = await dialog.ShowAsync();
            _explorePreviewEpoch++;
            _dialogOpen = false;
            if (!_ready) return;
            if (removeRequested) await RemoveExploreItemAsync(item);
            else if (result == ContentDialogResult.Primary) await EditExploreItemAsync(item, item.Kind);
            else if (result == ContentDialogResult.Secondary) OpenSaved(item);
        }
        finally { _dialogOpen = false; }
    }
    private void ExploreCapture_Click(object sender, RoutedEventArgs args) => CaptureExploreText();
    private void ExploreCapture_KeyDown(object sender, KeyRoutedEventArgs args)
    { if (args.Key == VirtualKey.Enter) { CaptureExploreText(); args.Handled = true; } }
    private void CaptureExploreText()
    {
        if (!_ready || _dialogOpen || _picking) return;
        try
        {
            var item = ExploreWorkspace.Capture(ExploreCaptureBox.Text, ActiveExploreSpace.Id);
            if (!ExploreWorkspace.Add(_state, item)) { ShowStatus("This link is already saved in this space."); return; }
            ExploreCaptureBox.Text = ""; _state.ExploreQuery = ""; _state.ExploreCollection = ""; _state.ExploreFavoritesOnly = false;
            Record("Captured " + item.Title); SavedItemsChanged();
        }
        catch (Exception ex) { Error("Could not capture this item", ex); }
    }
    private async void ExploreCaptureMenu_Click(object sender, RoutedEventArgs args)
    {
        try
        {
            switch ((string)((MenuFlyoutItem)sender).Tag)
            {
                case "paste":
                    var data = Clipboard.GetContent();
                    if (data.Contains(StandardDataFormats.WebLink)) ExploreCaptureBox.Text = (await data.GetWebLinkAsync()).AbsoluteUri;
                    else if (data.Contains(StandardDataFormats.Text))
                    {
                        string value = await data.GetTextAsync();
                        if (value.Length > 4096) { ShowStatus("Paste a shorter link or note (up to 2,000 characters).", true); return; }
                        ExploreCaptureBox.Text = value;
                    }
                    else { ShowStatus("Copy a link or some text first."); return; }
                    if (_ready) ExploreCaptureBox.Focus(FocusState.Programmatic);
                    break;
                case "note": await EditExploreItemAsync(null, "Note"); break;
                case "link": await EditExploreItemAsync(null, "Link"); break;
                case "file": await PickExploreFilesAsync(false); break;
                case "folder": await PickExploreFilesAsync(true); break;
            }
        }
        catch (Exception ex) { Error("Could not capture item", ex); }
    }
    private async Task PickExploreFilesAsync(bool folder)
    {
        if (_picking || _dialogOpen) return;
        _picking = true; string spaceId = ActiveExploreSpace.Id;
        try
        {
            var additions = new List<SavedItem>();
            if (folder)
            {
                var picker = new FolderPicker(); WinRT.Interop.InitializeWithWindow.Initialize(picker, _handle);
                picker.FileTypeFilter.Add("*"); var result = await picker.PickSingleFolderAsync();
                if (result is not null) additions.Add(new(Guid.NewGuid().ToString("N"), result.Name, result.Path, "Folder", SpaceId: spaceId));
            }
            else
            {
                var picker = new FileOpenPicker(); WinRT.Interop.InitializeWithWindow.Initialize(picker, _handle);
                picker.FileTypeFilter.Add("*");
                foreach (var file in await picker.PickMultipleFilesAsync())
                    additions.Add(new(Guid.NewGuid().ToString("N"), file.Name, file.Path, "File", SpaceId: spaceId));
            }
            if (_ready) AddExploreBatch(additions);
        }
        finally { _picking = false; }
    }
    private void AddExploreBatch(IEnumerable<SavedItem> additions)
    {
        var requested = additions.ToArray();
        if (requested.Length > WorkspaceState.MaximumItems) throw new InvalidDataException("Choose up to 100 items at a time. Nothing was added.");
        var items = ExploreWorkspace.NormalizeItems(requested).Where(item => !_state.SavedItems.Any(a =>
            ExploreWorkspace.SameReference(a, item))).ToArray();
        if (items.Length + _state.SavedItems.Count > WorkspaceState.MaximumItems) throw new InvalidDataException("These items would exceed the 100-item board limit. Nothing was added.");
        foreach (var item in items) ExploreWorkspace.Add(_state, item);
        if (items.Length > 0) { Record("Captured " + items.Length + " Explore items"); SavedItemsChanged(); }
        ShowStatus(items.Length > 0 ? "Added " + items.Length + " items to Explore." : "These items are already saved.");
    }
    private void Explore_DragOver(object sender, DragEventArgs args)
    {
        if (_dialogOpen || _picking) { args.AcceptedOperation = DataPackageOperation.None; return; }
        if (args.DataView.Contains(StandardDataFormats.StorageItems) || args.DataView.Contains(StandardDataFormats.Text) || args.DataView.Contains(StandardDataFormats.WebLink))
        { args.AcceptedOperation = DataPackageOperation.Copy; args.DragUIOverride.Caption = "Save to " + ActiveExploreSpace.Name; }
        args.Handled = true;
    }
    private async void Explore_Drop(object sender, DragEventArgs args)
    {
        if (!_ready || _dialogOpen || _picking) return;
        var deferral = args.GetDeferral(); string spaceId = ActiveExploreSpace.Id;
        try
        {
            var additions = new List<SavedItem>();
            if (args.DataView.Contains(StandardDataFormats.StorageItems))
                foreach (var item in await args.DataView.GetStorageItemsAsync())
                    additions.Add(new(Guid.NewGuid().ToString("N"), item.Name, item.Path, item is StorageFolder ? "Folder" : "File", SpaceId: spaceId));
            else if (args.DataView.Contains(StandardDataFormats.WebLink)) additions.Add(ExploreWorkspace.Capture((await args.DataView.GetWebLinkAsync()).AbsoluteUri, spaceId));
            else if (args.DataView.Contains(StandardDataFormats.Text)) additions.Add(ExploreWorkspace.Capture(await args.DataView.GetTextAsync(), spaceId));
            if (_ready && _page == "Explore" && ActiveExploreSpace.Id == spaceId) AddExploreBatch(additions);
        }
        catch (Exception ex) { Error("Could not save the dropped items", ex); }
        finally { deferral.Complete(); }
    }
    private async Task EditExploreItemAsync(SavedItem? entry, string kind)
    {
        if (_dialogOpen || _picking || !_ready) return;
        _dialogOpen = true;
        try
        {
            var title = new TextBox { Header = "Title", Text = entry?.Title ?? "", MaxLength = 100 };
            var address = new TextBox { Header = kind == "Link" ? "Web address" : "Windows path", Text = entry?.Target ?? "", PlaceholderText = kind == "Link" ? "https://" : "C:\\", MaxLength = 4096 };
            var note = new TextBox { Header = kind == "Note" ? "Your note" : "A note about this item", Text = entry?.Note ?? "",
                MaxLength = ExploreWorkspace.NoteLimit, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 110, MaxHeight = 200 };
            var collection = new TextBox { Header = "Collection", Text = entry?.Collection ?? WorkspaceState.CollectionName(_state.ExploreCollection), MaxLength = 24 };
            var tags = new TextBox { Header = "Tags · separated by commas", Text = entry?.Tags ?? "", MaxLength = 120 };
            var space = new ComboBox { Header = "Space", ItemsSource = _state.ExploreSpaces, DisplayMemberPath = "Name", HorizontalAlignment = HorizontalAlignment.Stretch,
                SelectedItem = _state.ExploreSpaces.First(s => s.Id == (entry?.SpaceId ?? ActiveExploreSpace.Id)) };
            var favorite = new CheckBox { Content = "Show in Home favorites", IsChecked = entry?.Favorite ?? false };
            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(title); if (kind is "Link" or "File" or "Folder") content.Children.Add(address);
            content.Children.Add(note); content.Children.Add(space); content.Children.Add(collection); content.Children.Add(tags); content.Children.Add(favorite);
            var dialog = new ContentDialog { XamlRoot = DesktopRoot.XamlRoot, RequestedTheme = DesktopRoot.RequestedTheme,
                Title = entry is null ? kind == "Note" ? "New note" : "Save a link" : "Edit item",
                Content = new ScrollViewer { Content = content, MaxHeight = Math.Max(160, Math.Min(480, DesktopRoot.ActualHeight - 220)), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
                PrimaryButtonText = "Save", CloseButtonText = "Cancel" };
            dialog.PrimaryButtonClick += (_, args) =>
            {
                if (string.IsNullOrWhiteSpace(title.Text)) { title.Header = "Give this item a title"; args.Cancel = true; }
                if (kind == "Link" && (!Uri.TryCreate(address.Text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
                { address.Header = "Enter a complete http:// or https:// address"; args.Cancel = true; }
                if ((kind is "File" or "Folder") && !Path.IsPathFullyQualified(address.Text.Trim()))
                { address.Header = "Enter a complete Windows path"; args.Cancel = true; }
                if (entry is null && _state.SavedItems.Count >= WorkspaceState.MaximumItems) { title.Header = "Board full · remove an item first"; args.Cancel = true; }
                string target = address.Text.Trim();
                if (kind == "Link" && Uri.TryCreate(target, UriKind.Absolute, out var normalizedUri)) target = normalizedUri.AbsoluteUri;
                var candidate = new SavedItem(entry?.Id ?? "new", title.Text, target, kind, SpaceId: ((ExploreSpace)space.SelectedItem).Id);
                if (kind != "Note" && _state.SavedItems.Any(a => a.Id != entry?.Id && ExploreWorkspace.SameReference(a, candidate)))
                { address.Header = "This item is already saved in this space"; args.Cancel = true; }
            };
            PolishDialog(dialog);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || !_ready) return;
            var updated = new SavedItem(entry?.Id ?? Guid.NewGuid().ToString("N"), title.Text.Trim(),
                kind == "Link" ? new Uri(address.Text.Trim()).AbsoluteUri : kind is "File" or "Folder" ? address.Text.Trim() : "", kind,
                collection.Text, favorite.IsChecked == true, ((ExploreSpace)space.SelectedItem).Id, note.Text, tags.Text);
            if (entry is null)
            {
                if (!ExploreWorkspace.Add(_state, updated)) { ShowStatus("This item is already saved."); return; }
            }
            else
            {
                int index = _state.SavedItems.FindIndex(a => a.Id == entry.Id); if (index < 0) return;
                _state.SavedItems[index] = updated;
            }
            _state.ActiveExploreSpaceId = updated.SpaceId; _state.ExploreSelectedItemId = updated.Id;
            _state.ExploreQuery = ""; _state.ExploreCollection = ""; _state.ExploreFavoritesOnly = false;
            Record("Saved " + updated.Title); SavedItemsChanged();
        }
        finally { _dialogOpen = false; }
    }
    private async Task RemoveExploreItemAsync(SavedItem item)
    {
        if (_dialogOpen || _picking) return;
        _dialogOpen = true;
        try
        {
            var dialog = new ContentDialog { XamlRoot = DesktopRoot.XamlRoot, RequestedTheme = DesktopRoot.RequestedTheme,
                Title = "Remove " + item.Title + "?", Content = item.Kind == "Note" ? "This removes the note from Explore." : "This removes the card from Explore. The original stays where it is.",
                PrimaryButtonText = "Remove", CloseButtonText = "Keep", DefaultButton = ContentDialogButton.Close };
            PolishDialog(dialog);
            if (await dialog.ShowAsync() == ContentDialogResult.Primary && _ready) { _state.SavedItems.RemoveAll(a => a.Id == item.Id); SavedItemsChanged(); }
        }
        finally { _dialogOpen = false; }
    }
    private async void ExploreSpaceMenu_Click(object sender, RoutedEventArgs args)
    {
        try
        {
            switch ((string)((MenuFlyoutItem)sender).Tag)
            {
                case "new": await EditExploreSpaceAsync(true); break;
                case "edit": await EditExploreSpaceAsync(false); break;
                case "remove": await RemoveExploreSpaceAsync(); break;
                case "export": await ExportExploreSpaceAsync(); break;
                case "import": await ImportExploreSpaceAsync(); break;
            }
        }
        catch (Exception ex) { Error("Could not update Explore space", ex); }
    }
    private async Task EditExploreSpaceAsync(bool create)
    {
        if (_dialogOpen || _picking) return;
        if (create && _state.ExploreSpaces.Count >= ExploreWorkspace.SpaceLimit) { ShowStatus("You can keep up to 12 spaces."); return; }
        _dialogOpen = true;
        try
        {
            var current = ActiveExploreSpace;
            var name = new TextBox { Header = "Space name", Text = create ? "" : current.Name, MaxLength = 32 };
            var description = new TextBox { Header = "A little description", Text = create ? "" : current.Description, MaxLength = 120 };
            var accent = new ComboBox { Header = "Accent", ItemsSource = ExploreWorkspace.Accents, SelectedItem = create ? "Iris" : current.Accent, HorizontalAlignment = HorizontalAlignment.Stretch };
            var content = new StackPanel { Spacing = 14 }; content.Children.Add(name); content.Children.Add(description); content.Children.Add(accent);
            var dialog = new ContentDialog { XamlRoot = DesktopRoot.XamlRoot, RequestedTheme = DesktopRoot.RequestedTheme, Title = create ? "New space" : "Edit space", Content = content, PrimaryButtonText = "Save", CloseButtonText = "Cancel" };
            dialog.PrimaryButtonClick += (_, args) => { if (string.IsNullOrWhiteSpace(name.Text)) { name.Header = "Give this space a name"; args.Cancel = true; } };
            PolishDialog(dialog);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || !_ready) return;
            var updated = new ExploreSpace(create ? Guid.NewGuid().ToString("N") : current.Id, name.Text.Trim(), description.Text.Trim(), (string)accent.SelectedItem);
            if (create) _state.ExploreSpaces.Add(updated); else _state.ExploreSpaces[_state.ExploreSpaces.FindIndex(s => s.Id == current.Id)] = updated;
            _state.ActiveExploreSpaceId = updated.Id; _state.ExploreQuery = ""; _state.ExploreCollection = "";
            SavedItemsChanged();
        }
        finally { _dialogOpen = false; }
    }
    private async Task RemoveExploreSpaceAsync()
    {
        if (_dialogOpen || _picking) return;
        var space = ActiveExploreSpace;
        if (space.Id == "personal") { ShowStatus("Keep Personal as the home for your saved items. You can rename it."); return; }
        _dialogOpen = true;
        try
        {
            var dialog = new ContentDialog { XamlRoot = DesktopRoot.XamlRoot, RequestedTheme = DesktopRoot.RequestedTheme,
                Title = "Remove " + space.Name + "?", Content = "Its items move to Personal so you can keep them.", PrimaryButtonText = "Remove space", CloseButtonText = "Keep", DefaultButton = ContentDialogButton.Close };
            PolishDialog(dialog);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || !_ready) return;
            ExploreWorkspace.RemoveSpace(_state, space.Id); SavedItemsChanged();
        }
        finally { _dialogOpen = false; }
    }
    private async Task ExportExploreSpaceAsync()
    {
        if (_picking || _dialogOpen) return;
        _picking = true;
        try
        {
            string json = ExploreWorkspace.Export(_state);
            var picker = new FileSavePicker { SuggestedFileName = "Nexus-Space-" + string.Concat(ActiveExploreSpace.Name.Split(Path.GetInvalidFileNameChars())) };
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _handle);
            picker.FileTypeChoices.Add("NEXUS space", new List<string> { ".json" });
            var file = await picker.PickSaveFileAsync();
            if (!_ready || file is null) return;
            await FileIO.WriteTextAsync(file, json);
            ShowStatus("Space exported with notes, links and shortcuts. Original files stay on this PC.");
        }
        finally { _picking = false; }
    }
    private async Task ImportExploreSpaceAsync()
    {
        if (_picking || _dialogOpen) return;
        _picking = true;
        try
        {
            var picker = new FileOpenPicker(); WinRT.Interop.InitializeWithWindow.Initialize(picker, _handle);
            picker.FileTypeFilter.Add(".json"); var file = await picker.PickSingleFileAsync();
            if (!_ready || file is null) return;
            var properties = await file.GetBasicPropertiesAsync();
            if (properties.Size > ExploreWorkspace.ImportByteLimit) throw new InvalidDataException("Space files must be smaller than 2 MB.");
            var data = ExploreWorkspace.ReadImport(await FileIO.ReadTextAsync(file));
            if (!_ready) return;
            _dialogOpen = true;
            var dialog = new ContentDialog { XamlRoot = DesktopRoot.XamlRoot, RequestedTheme = DesktopRoot.RequestedTheme,
                Title = "Import " + data.Space.Name + "?", Content = data.Items.Count + " items will be added in a new space. Existing links and file shortcuts are skipped. File paths may need updating on another PC.",
                PrimaryButtonText = "Import", CloseButtonText = "Cancel" };
            PolishDialog(dialog);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || !_ready) return;
            int count = ExploreWorkspace.Import(_state, data); SavedItemsChanged();
            ShowStatus("Imported " + count + " items. Open or preview a card when you need it.");
        }
        finally { _dialogOpen = false; _picking = false; }
    }
}
