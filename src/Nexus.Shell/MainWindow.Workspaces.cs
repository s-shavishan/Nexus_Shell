using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using System.Diagnostics;
using Windows.Storage.Pickers;
using Windows.System;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    private readonly FocusSession _focusSession = new();
    private readonly DispatcherTimer _focusTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _commandOpen;
    private Control? _previousFocus;
    private TextBlock? _focusClock, _focusSummary;
    private ProgressBar? _focusProgress;
    private Button? _focusAction;
    private TextBox? _studyNote, _savedSearch;
    private StackPanel? _savedCards;
    private StackPanel? _focusPresets, _savedActions;
    private Brush? _chosenWallpaper;
    private int _focusCheckpointTicks;

    private void InitializeWorkspaces()
    {
        QuickNotesBox.Text = _state.QuickNote;
        WallpaperBox.SelectedIndex = _state.Wallpaper switch { "Aurora" => 1, "Slate" => 2, _ => 0 };
        if (_state.FocusRemainingSeconds >= 0)
            _focusSession.Restore(_state.FocusMinutes, TimeSpan.FromSeconds(_state.FocusRemainingSeconds));
        else _focusSession.Reset(_state.FocusMinutes);
        _focusTimer.Tick += Focus_Tick;
        WindowsGrid.Loaded += (_, _) => UpdateWindowTileSize();
        SelectWallpaper();
        RefreshWorkspaceSummary();
    }
    private void ReleaseWorkspaceControls()
    {
        _focusClock = null; _focusSummary = null; _focusProgress = null;
        _focusAction = null; _studyNote = null; _savedSearch = null; _savedCards = null;
        _focusPresets = null; _savedActions = null;
        ReleaseOrbitControls();
        _personalizationSync.Clear(); _moodChoices.Clear(); _moodGrid = null; _personalizeSections = null;
    }
    private void RefreshWorkspaceSummary()
    {
        string day = DateTime.Now.ToString("yyyy-MM-dd");
        if (_state.FocusDay != day)
        {
            _state.FocusDay = day; _state.FocusCompleted = 0;
            if (_ready) SaveState();
        }
        HomeSavedCount.Text = _state.SavedItems.Count + " saved";
        HomeFocusCount.Text = _state.FocusCompleted + " focus sessions today";
        FocusCardLabel.Text = _focusSession.IsRunning ? _state.FocusMinutes == 5 ? "Return to break" : "Return to focus" : "Study time";
        FocusBadge.Text = _focusSession.IsRunning ? FormatRemaining() + (_state.FocusMinutes == 5 ? " · Break" : " · Focus") : "Your orbit";
        if (_focusSummary is not null) _focusSummary.Text = _state.FocusCompleted + " sessions completed today";
    }
    private string FormatRemaining()
    {
        int seconds = (int)Math.Ceiling(_focusSession.Remaining.TotalSeconds);
        return $"{seconds / 60:00}:{seconds % 60:00}";
    }
    private void BuildStudy()
    {
        PageContent.Children.Add(Text("A little focus. A big difference.", 28));
        PageContent.Children.Add(Text("Pick one thing. Give it your attention. Your notes stay on this PC.", 13, true));
        var timer = new StackPanel { Spacing = 16 };
        _focusModeLabel = Text("YOUR FOCUS SESSION", 10, true);
        timer.Children.Add(_focusModeLabel);
        _focusTaskText = Text("", 13);
        timer.Children.Add(_focusTaskText);
        _focusClock = Text(FormatRemaining(), 62);
        timer.Children.Add(_focusClock);
        _focusStateText = Text("", 11, true); timer.Children.Add(_focusStateText);
        _focusProgress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 4 };
        timer.Children.Add(_focusProgress);
        var presets = _focusPresets = new StackPanel { Orientation = PageHost.ActualWidth < 480 ? Orientation.Vertical : Orientation.Horizontal, Spacing = 8 };
        foreach (int minutes in new[] { 25, 50, 5 })
        {
            int chosen = minutes;
            var preset = ActionButton(minutes == 5 ? "5 min break" : minutes + " min", () =>
            {
                _focusTimer.Stop(); _focusSession.Reset(chosen); _state.FocusMinutes = chosen;
                SaveState(); RenderFocus(); RefreshWorkspaceSummary();
            });
            _presetButtons[chosen] = preset; presets.Children.Add(preset);
        }
        timer.Children.Add(presets);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _focusAction = ActionButton("Start focus", ToggleFocusSession);
        controls.Children.Add(_focusAction);
        controls.Children.Add(ActionButton("Reset", () =>
        {
            _focusTimer.Stop(); _focusSession.Reset(_state.FocusMinutes); SaveState(); RenderFocus(); RefreshWorkspaceSummary();
        }));
        timer.Children.Add(controls);
        _focusSummary = Text("", 12, true); timer.Children.Add(_focusSummary);
        PageContent.Children.Add(Card(timer));
        BuildTaskWorkspace();
        PageContent.Children.Add(Text("Make room for your thoughts.", 20));
        _studyNote = new TextBox
        {
            Text = _state.QuickNote, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            MaxLength = 10_000, MinHeight = 180, Header = "Your notes",
            PlaceholderText = "The one thing I want to finish…"
        };
        _studyNote.TextChanged += (_, _) =>
        {
            if (!_ready || _studyNote is null) return;
            _state.QuickNote = _studyNote.Text;
            if (QuickNotesBox.Text != _state.QuickNote) QuickNotesBox.Text = _state.QuickNote;
            SaveState();
        };
        PageContent.Children.Add(_studyNote);
        PageContent.Children.Add(ActionButton(_state.FocusMode ? "Show desktop widgets" : "Hide desktop widgets",
            () => { FocusSwitch.IsOn = !FocusSwitch.IsOn; Navigate("Study", false); }));
        PageStatus.Text = "Local tasks and notes · Closing Nexus saves the timer paused";
        RenderFocus(); RefreshWorkspaceSummary();
    }
    private void ToggleFocusSession()
    {
        if (_focusSession.CompleteIfDue()) { FinishFocusSession(); return; }
        if (_focusSession.IsRunning) { _focusSession.Pause(); _focusTimer.Stop(); }
        else { _focusSession.Start(); _focusTimer.Start(); }
        SaveState(); RenderFocus(); RefreshWorkspaceSummary();
    }
    private void RenderFocus()
    {
        if (_focusClock is not null) _focusClock.Text = FormatRemaining();
        if (_focusProgress is not null)
            _focusProgress.Value = 100 * (1 - _focusSession.Remaining.TotalSeconds / _focusSession.Duration.TotalSeconds);
        if (_focusAction is not null)
            _focusAction.Content = _focusSession.IsRunning ? "Pause" : _focusSession.IsComplete ? "Start again" : "Start / resume";
        if (_focusTaskText is not null)
            _focusTaskText.Text = _state.FocusMinutes == 5 ? "A moment to reset." : _state.Tasks.FirstOrDefault(t => !t.Completed && t.Id == _state.FocusTaskId)?.Title
                ?? "Choose one task below, or focus freely.";
        if (_focusModeLabel is not null) _focusModeLabel.Text = _state.FocusMinutes == 5 ? "YOUR BREAK" : "YOUR FOCUS SESSION";
        if (_focusStateText is not null) _focusStateText.Text = _focusSession.IsRunning ? "In progress · One moment at a time"
            : _focusSession.IsComplete ? "Session finished" : _focusSession.Remaining < _focusSession.Duration ? "Paused · Ready to resume" : "Ready when you are";
        foreach (var preset in _presetButtons)
            preset.Value.Background = preset.Key == _state.FocusMinutes ? _selection : Resource("NexusCard");
        if (_isActive) FocusBadge.Text = _focusSession.IsRunning ? FormatRemaining() + (_state.FocusMinutes == 5 ? " · Break" : " · Focus") : "Your orbit";
    }
    private void Focus_Tick(object? sender, object args)
    {
        if (!_ready) return;
        if (_focusSession.CompleteIfDue())
        {
            FinishFocusSession();
        }
        if (_isActive) RenderFocus();
        if (_focusSession.IsRunning && ++_focusCheckpointTicks >= 30)
        {
            _focusCheckpointTicks = 0; SaveState();
        }
    }
    private void FinishFocusSession(bool updateUi = true)
    {
        _focusTimer.Stop();
        string day = DateTime.Now.ToString("yyyy-MM-dd");
        if (_state.FocusDay != day) { _state.FocusDay = day; _state.FocusCompleted = 0; }
        if (_state.FocusMinutes != 5) _state.FocusCompleted++;
        var task = _state.Tasks.FirstOrDefault(t => t.Id == _state.FocusTaskId && !t.Completed);
        Record(_state.FocusMinutes == 5 ? "Break completed" : "Completed a " + _state.FocusMinutes + "-minute focus session" +
            (task is null ? "" : " · " + task.Title), updateUi);
        if (updateUi)
        {
            SaveState(); RefreshWorkspaceSummary(); RenderFocus();
            ShowStatus(_state.FocusMinutes == 5 ? "Break complete. Ready when you are." : "Session complete. Take a breath—you earned a break.");
        }
    }
    private void QuickNotes_TextChanged(object sender, TextChangedEventArgs args)
    {
        if (!_ready) return;
        _state.QuickNote = QuickNotesBox.Text;
        if (_studyNote is not null && _studyNote.Text != _state.QuickNote) _studyNote.Text = _state.QuickNote;
        SaveState();
    }

    private void BuildExplore()
    {
        PageContent.Children.Add(Text("Keep a little of your world.", 28));
        PageContent.Children.Add(Text("Save links, files and folders here. Your board is separate from your Windows folders.", 13, true));
        var actions = _savedActions = new StackPanel { Orientation = PageHost.ActualWidth < 480 ? Orientation.Vertical : Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(AsyncButton("Save link", AddLinkAsync));
        actions.Children.Add(AsyncButton("File", () => PickSavedAsync(false)));
        actions.Children.Add(AsyncButton("Folder", () => PickSavedAsync(true)));
        PageContent.Children.Add(actions);
        _savedSearch = new TextBox { PlaceholderText = "Find something you saved", MaxLength = 100 };
        _savedSearch.TextChanged += (_, _) => RenderSavedItems();
        PageContent.Children.Add(_savedSearch);
        var filters = new Grid { ColumnSpacing = 10 };
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _savedCollection = new ComboBox { PlaceholderText = "All collections", HorizontalAlignment = HorizontalAlignment.Stretch };
        RefreshCollectionChoices();
        _savedCollection.SelectionChanged += (_, _) =>
        {
            if (_updatingCollections) return;
            _collectionFilter = (_savedCollection?.SelectedItem as ComboBoxItem)?.Tag as string ?? ""; RenderSavedItems();
        };
        var favorites = new CheckBox { Content = "Favorites", IsChecked = _favoritesOnly, VerticalAlignment = VerticalAlignment.Center };
        favorites.Checked += (_, _) => { _favoritesOnly = true; RenderSavedItems(); };
        favorites.Unchecked += (_, _) => { _favoritesOnly = false; RenderSavedItems(); };
        Grid.SetColumn(favorites, 1);
        filters.Children.Add(_savedCollection); filters.Children.Add(favorites);
        PageContent.Children.Add(filters);
        _savedCards = new StackPanel { Spacing = 10 };
        PageContent.Children.Add(_savedCards);
        RenderSavedItems();
    }
    private void RenderSavedItems()
    {
        if (_savedCards is null) return;
        string query = _savedSearch?.Text.Trim() ?? "";
        _savedCards.Children.Clear();
        var entries = _state.SavedItems.Where(a => (!_favoritesOnly || a.Favorite) &&
            (_collectionFilter.Length == 0 || a.Collection.Equals(_collectionFilter, StringComparison.CurrentCultureIgnoreCase)) &&
            (a.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) || a.Target.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
            .OrderByDescending(a => a.Favorite).ToArray();
        if (entries.Length == 0)
            _savedCards.Children.Add(Card(Text(query.Length == 0 ? "Your board is waiting. Save your first link or file." : "Nothing matches this search.", 14, true)));
        foreach (var entry in entries)
        {
            var row = new StackPanel { Spacing = 8 };
            var open = ActionButton(entry.Title, () => OpenSaved(entry));
            open.HorizontalAlignment = HorizontalAlignment.Stretch;
            open.HorizontalContentAlignment = HorizontalAlignment.Left;
            row.Children.Add(open);
            row.Children.Add(Text(entry.Collection + "  ·  " + entry.Kind + "  ·  " + entry.Target, 11, true));
            var options = new Grid { ColumnSpacing = 8 };
            options.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            options.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var favorite = ActionButton(entry.Favorite ? "★ Favorite" : "☆ Favorite", () =>
            {
                int index = _state.SavedItems.FindIndex(a => a.Id == entry.Id);
                if (index < 0) return;
                _state.SavedItems[index] = entry with { Favorite = !entry.Favorite };
                SavedItemsChanged();
            });
            var more = new Button { Content = "•••", CornerRadius = new CornerRadius(10) };
            var menu = new MenuFlyout();
            var edit = new MenuFlyoutItem { Text = "Edit title and collection" };
            edit.Click += async (_, _) => { try { await EditSavedAsync(entry); } catch (Exception ex) { Error("Could not edit saved item", ex); } };
            var remove = new MenuFlyoutItem { Text = "Remove from board" };
            remove.Click += (_, _) =>
            {
                _state.SavedItems.RemoveAll(a => a.Id == entry.Id);
                SavedItemsChanged();
            };
            menu.Items.Add(edit); menu.Items.Add(remove); more.Flyout = menu;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(more, "Options for " + entry.Title);
            Grid.SetColumn(more, 1); options.Children.Add(favorite); options.Children.Add(more);
            row.Children.Add(options);
            _savedCards.Children.Add(Card(row));
        }
        PageStatus.Text = entries.Length + " shown · " + _state.SavedItems.Count + " saved on this PC";
    }
    private async Task AddLinkAsync()
    {
        if (_dialogOpen) return;
        _dialogOpen = true;
        ControlsFlyout.Hide();
        try
        {
            var title = new TextBox { Header = "Title", MaxLength = 100, PlaceholderText = "Give it a name" };
            var url = new TextBox { Header = "Web address", MaxLength = 4096, PlaceholderText = "https://" };
            var collection = new TextBox { Header = "Collection", MaxLength = 24, Text = WorkspaceState.CollectionName(_collectionFilter) };
            var favorite = new CheckBox { Content = "Add to Home favorites", IsChecked = _favoritesOnly };
            var content = new StackPanel { Spacing = 14 };
            content.Children.Add(title); content.Children.Add(url); content.Children.Add(collection); content.Children.Add(favorite);
            var dialog = new ContentDialog
            {
                XamlRoot = DesktopRoot.XamlRoot, RequestedTheme = ElementTheme.Dark, Title = "Save a link",
                Content = content, PrimaryButtonText = "Save", CloseButtonText = "Cancel"
            };
            dialog.PrimaryButtonClick += (_, args) =>
            {
                if (!Uri.TryCreate(url.Text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
                {
                    args.Cancel = true; url.Header = "Enter a complete http:// or https:// address";
                }
            };
            PolishDialog(dialog);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || !_ready) return;
            var link = new Uri(url.Text.Trim());
            SaveItem(string.IsNullOrWhiteSpace(title.Text) ? link.Host : title.Text.Trim(), link.AbsoluteUri, "Link",
                collection.Text, favorite.IsChecked == true);
        }
        finally { _dialogOpen = false; }
    }
    private async Task PickSavedAsync(bool folder)
    {
        if (_picking) return;
        _picking = true;
        try
        {
            if (folder)
            {
                var picker = new FolderPicker();
                WinRT.Interop.InitializeWithWindow.Initialize(picker, _handle);
                picker.FileTypeFilter.Add("*");
                var result = await picker.PickSingleFolderAsync();
                if (_ready && result is not null) SaveItem(result.Name, result.Path, "Folder");
            }
            else
            {
                var picker = new FileOpenPicker();
                WinRT.Interop.InitializeWithWindow.Initialize(picker, _handle);
                picker.FileTypeFilter.Add("*");
                var result = await picker.PickSingleFileAsync();
                if (_ready && result is not null) SaveItem(result.Name, result.Path, "File");
            }
        }
        finally { _picking = false; }
    }
    private void SaveItem(string title, string target, string kind, string? collection = null, bool favorite = false)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(target) || target.Length > 4096)
        { ShowStatus("This item cannot be saved. Choose an item with a shorter path or address."); return; }
        title = title.Trim()[..Math.Min(title.Trim().Length, 100)];
        if (_state.SavedItems.Any(a => a.Target.Equals(target, StringComparison.OrdinalIgnoreCase)))
        { ShowStatus("This is already on your board."); return; }
        if (_state.SavedItems.Count >= 100) { ShowStatus("Your 100 spaces are full. Remove an item to save another."); return; }
        _state.SavedItems.Insert(0, new(Guid.NewGuid().ToString("N"), title, target, kind,
            WorkspaceState.CollectionName(collection ?? _collectionFilter), favorite));
        Record("Saved " + title); SaveState(); RefreshWorkspaceSummary();
        if (_page == "Explore") Navigate("Explore", false);
        RenderHomeWorkspace();
    }
    private void OpenSaved(SavedItem item)
    {
        try
        {
            if (item.Kind == "Link")
            {
                if (!Uri.TryCreate(item.Target, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
                    throw new InvalidDataException("The saved web address is invalid.");
            }
            else if (!File.Exists(item.Target) && !Directory.Exists(item.Target))
                throw new FileNotFoundException("This item moved or is unavailable.", item.Target);
            Process.Start(new ProcessStartInfo(item.Target) { UseShellExecute = true });
            Record("Opened " + item.Title); SaveState();
        }
        catch (Exception ex) { Error("Could not open " + item.Title, ex); }
    }

    private IEnumerable<CommandEntry> CommandEntries()
    {
        foreach (var item in new[] {
            ("Home", "Your personal desktop", "\uE80F"),
            ("Explore", "Saved links, files and folders", "\uE8B7"),
            ("Study", "Tasks, focus timer and local notes", "\uE916"),
            ("Apps", "Start-menu apps and pins", "\uE71D"),
            ("Gaming", "Installed games and shortcuts", "\uE7FC"),
            ("Window overview", "Switch to an open window · Ctrl+4", "\uE7F4"),
            ("Activity", "Your local session", "\uE9D9"),
            ("Personalize", "Moods, desktop widgets and dock", "\uE790") })
            yield return new(item.Item1, item.Item2, item.Item3, "Workspace", item.Item1 == "Window overview" ? "Running apps" : item.Item1);
        yield return new("Control center", "Appearance and Windows settings", "\uE713", "Action", "controls");
        yield return new(_focusSession.IsRunning ? "Pause focus" : "Start focus", "Study session", "\uE916", "Action", "focus");
        yield return new("Toggle full screen", "Nexus desktop view", "\uE740", "Action", "screen");
        foreach (var profile in _state.Profiles)
            yield return new(profile.Name + " workspace", profile.Description, profile.Glyph, "Profile", profile.Id);
        yield return new("Workspaces", "Configure your personal desktop presets", "\uE8F1", "Workspace", "Workspaces");
        yield return new("Hide Nexus", "Return to your other apps", "\uE8BB", "Action", "hide");
        foreach (var app in _orderedCatalog)
            yield return new(app.Name, app.Category == "Game" ? "Installed game" : "Windows app", app.Glyph, "App", app.Target);
        foreach (var saved in _state.SavedItems.OrderByDescending(a => a.Favorite))
            yield return new(saved.Title, saved.Collection + " · Saved " + saved.Kind.ToLowerInvariant(), saved.Kind == "Link" ? "\uE774" : "\uE8B7", "Saved", saved.Id);
        foreach (var window in _desktopWindows)
            yield return new(window.Title, window.ProcessName + " · Open window", "\uE7F4", "Window",
                window.Handle.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var task in _state.Tasks.Where(t => !t.Completed))
            yield return new(task.Title, "Task · Focus on this", "\uE916", "Task", task.Id);
    }
    private void OpenCommands()
    {
        if (!_ready || _dialogOpen || _picking) return;
        if (_commandOpen) { CloseCommands(); return; }
        _previousFocus = FocusManager.GetFocusedElement(DesktopRoot.XamlRoot) as Control;
        ControlsFlyout.Hide();
        _commandOpen = true; CommandOverlay.Visibility = Visibility.Visible;
        _commandCategory = "All";
        CommandSearchBox.Text = "";
        RenderCommands();
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_ready && _commandOpen) CommandSearchBox.Focus(FocusState.Programmatic);
        });
        _motion?.Enter(CommandPanel);
        if (!_catalogReady && !_discovering) _ = DiscoverAsync();
        _ = RefreshDesktopWindowsAsync();
    }
    private void CloseCommands()
    {
        _commandOpen = false; _searchTimer.Stop();
        CommandOverlay.Visibility = Visibility.Collapsed;
        CommandList.ItemsSource = null;
        _previousFocus?.Focus(FocusState.Programmatic); _previousFocus = null;
    }
    private void RenderCommands(bool preserveSelection = false)
    {
        if (!_ready || !_commandOpen) return;
        var previous = preserveSelection ? CommandList.SelectedItem as CommandEntry : null;
        var entries = ShellExperience.Search(CommandEntries(), CommandSearchBox.Text, _commandCategory,
            _state.RecentCommands, _state.RememberRecentItems);
        CommandList.ItemsSource = entries;
        int selected = previous is null ? -1 : Array.FindIndex(entries, e => e.Kind == previous.Kind && e.Target == previous.Target);
        CommandList.SelectedIndex = entries.Length == 0 ? -1 : selected >= 0 ? selected : 0;
        CommandEmpty.Visibility = entries.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        CommandSectionTitle.Text = string.IsNullOrWhiteSpace(CommandSearchBox.Text) && _commandCategory == "All"
            ? entries.Any(e => e.Subtitle.StartsWith("Recent · ", StringComparison.Ordinal)) ? "RECENT & SUGGESTED" : "SUGGESTED"
            : _commandCategory == "All" ? "RESULTS" : _commandCategory.ToUpperInvariant();
        CommandResultCount.Text = entries.Length == 30 ? "30 shown" : entries.Length + " found";
        CommandHint.Text = _discovering ? "Finding your Start-menu apps…" : "↑ ↓ choose     Enter open     Esc close";
        UpdateCommandCategories();
    }
    private void CommandSearch_TextChanged(object sender, TextChangedEventArgs args)
    {
        if (!_ready || !_commandOpen) return;
        _searchTimer.Stop(); _searchTimer.Start();
    }
    private void CommandSearch_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Escape) { CloseCommands(); args.Handled = true; }
        else if (args.Key is VirtualKey.Up or VirtualKey.Down)
        {
            if (CommandList.Items.Count > 0)
            {
                CommandList.SelectedIndex = Math.Clamp(CommandList.SelectedIndex + (args.Key == VirtualKey.Down ? 1 : -1), 0, CommandList.Items.Count - 1);
                CommandList.ScrollIntoView(CommandList.SelectedItem);
            }
            args.Handled = true;
        }
        else if (args.Key == VirtualKey.Enter)
        {
            if (_searchTimer.IsEnabled) { _searchTimer.Stop(); RenderCommands(); }
            if (CommandList.SelectedItem is CommandEntry entry) ExecuteCommand(entry);
            args.Handled = true;
        }
    }
    private void Command_ItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is CommandEntry entry) ExecuteCommand(entry);
    }
    private void ExecuteCommand(CommandEntry entry)
    {
        RememberCommand(entry);
        CloseCommands();
        if (entry.Kind == "Profile") EnterProfile(entry.Target);
        else if (entry.Kind == "Workspace") Navigate(entry.Target);
        else if (entry.Kind == "App")
        {
            var app = _orderedCatalog.FirstOrDefault(a => a.Target == entry.Target);
            if (app is not null) Launch(app);
        }
        else if (entry.Kind == "Saved")
        {
            var saved = _state.SavedItems.FirstOrDefault(a => a.Id == entry.Target);
            if (saved is not null) OpenSaved(saved);
        }
        else if (entry.Kind == "Window")
        {
            var window = _desktopWindows.FirstOrDefault(w => w.Handle.ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture) == entry.Target);
            if (window is null || !NativeMethods.Activate(window.Handle)) ShowStatus("This window is unavailable. Refresh window overview.");
        }
        else if (entry.Kind == "Task") SelectFocusTask(entry.Target);
        else if (entry.Target == "hide") HideNexus();
        else if (entry.Target == "controls") ControlsFlyout.ShowAt(ControlsButton);
        else if (entry.Target == "screen") SetFullScreen(!_state.FullScreen);
        else if (entry.Target == "focus") { Navigate("Study"); ToggleFocusSession(); }
    }
    private void CloseCommand_Click(object sender, RoutedEventArgs args) => CloseCommands();
    private void UpdateWorkspaceLayout()
    {
        var orientation = PageHost.ActualWidth < 480 ? Orientation.Vertical : Orientation.Horizontal;
        if (_focusPresets is not null) _focusPresets.Orientation = orientation;
        if (_savedActions is not null) _savedActions.Orientation = orientation;
        UpdateWindowTileSize();
    }
    private void Wallpaper_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!_ready) return;
        _state.Wallpaper = WallpaperBox.SelectedIndex switch { 1 => "Aurora", 2 => "Slate", _ => "Orbit" };
        SelectWallpaper(); ApplyEffects(); SaveState();
    }
    private void SelectWallpaper()
    {
        ApplyAuraPalette();
        WallpaperAccents.Opacity = _state.Wallpaper == "Slate" ? .35 : .7;
    }
}
