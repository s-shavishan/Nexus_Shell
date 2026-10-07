using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Nexus.Shell.Interop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Windows.System;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    private TextBlock? _focusTaskText, _taskSummary, _focusStateText, _focusModeLabel;
    private readonly Dictionary<int, Button> _presetButtons = new();
    private TextBox? _taskInput;
    private StackPanel? _taskCards;
    private ComboBox? _savedCollection;
    private string _collectionFilter = "";
    private bool _favoritesOnly, _showCompletedTasks, _updatingCollections, _refreshingWindows;
    private long _windowEpoch;
    private IReadOnlyList<RunningWindow> _openWindows = [];
    private bool? _renderedHighContrast;

    private void ReleaseOrbitControls()
    {
        _focusTaskText = null; _focusStateText = null; _focusModeLabel = null;
        _taskSummary = null; _taskInput = null; _taskCards = null; _savedCollection = null; _presetButtons.Clear();
    }
    private ShellState CaptureWorkspaceSnapshot()
    {
        _state.FocusRemainingSeconds = _focusSession.Remaining.TotalSeconds;
        return _state.Snapshot();
    }
    private void RenderHomeWorkspace()
    {
        HomeTaskList.Children.Clear(); HomeFavorites.Children.Clear();
        int remaining = _state.Tasks.Count(t => !t.Completed);
        var selected = _state.Tasks.FirstOrDefault(t => !t.Completed && t.Id == _state.FocusTaskId);
        HomeTaskSummary.Text = selected is not null ? "Focusing on: " + selected.Title : remaining == 0
            ? "A clear space. Add something you want to finish in Study." : remaining + " tasks waiting · One step at a time";
        foreach (var task in _state.Tasks.Where(t => !t.Completed).OrderByDescending(t => t.Id == _state.FocusTaskId).Take(3))
        {
            var check = new CheckBox { Content = Text(task.Title, 13), HorizontalAlignment = HorizontalAlignment.Stretch };
            check.Checked += (_, _) => SetTaskCompleted(task.Id, true);
            HomeTaskList.Children.Add(check);
        }
        var selectedSaved = ActiveProfile.SavedItemIds.ToHashSet(StringComparer.Ordinal);
        var homeSaved = selectedSaved.Count > 0 ? _state.SavedItems.Where(s => selectedSaved.Contains(s.Id)) : _state.SavedItems.Where(s => s.Favorite);
        foreach (var saved in homeSaved.Take(4))
        {
            var button = ActionButton("★  " + saved.Title, () => OpenSaved(saved));
            button.HorizontalAlignment = HorizontalAlignment.Stretch; button.HorizontalContentAlignment = HorizontalAlignment.Left;
            ToolTipService.SetToolTip(button, saved.Collection + " · " + saved.Target);
            HomeFavorites.Children.Add(button);
        }
        if (HomeFavorites.Children.Count == 0)
            HomeFavorites.Children.Add(Text("Favorite a saved item in Explore to keep it close.", 12, true));
    }
    private void BuildTaskWorkspace()
    {
        PageContent.Children.Add(Text("One thing at a time.", 22));
        PageContent.Children.Add(Text("Keep up to 100 tasks here. Completing a focus session leaves the task for you to check off.", 12, true));
        var form = new Grid { ColumnSpacing = 8 };
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var input = _taskInput = new TextBox { PlaceholderText = "A small thing I want to finish…", MaxLength = 160 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(input, "New study task");
        input.KeyDown += TaskInput_KeyDown;
        var add = ActionButton("Add", AddTask); Grid.SetColumn(add, 1);
        form.Children.Add(input); form.Children.Add(add); PageContent.Children.Add(form);
        _taskSummary = Text("", 11, true); PageContent.Children.Add(_taskSummary);
        var completed = new CheckBox { Content = "Show completed tasks", IsChecked = _showCompletedTasks };
        completed.Checked += (_, _) => { _showCompletedTasks = true; RenderTaskCards(); };
        completed.Unchecked += (_, _) => { _showCompletedTasks = false; RenderTaskCards(); };
        PageContent.Children.Add(completed);
        _taskCards = new StackPanel { Spacing = 8 }; PageContent.Children.Add(_taskCards);
        PageContent.Children.Add(ActionButton("Clear completed tasks", () =>
        {
            int removed = _state.Tasks.RemoveAll(t => t.Completed);
            if (removed > 0) TasksChanged();
        }));
        RenderTaskCards();
    }
    private void TaskInput_KeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Enter) { AddTask(); args.Handled = true; }
    }
    private void AddTask()
    {
        string title = _taskInput?.Text.Trim() ?? "";
        if (title.Length == 0) return;
        if (_state.Tasks.Count >= WorkspaceState.MaximumItems) { ShowStatus("Your task list is full. Clear a completed task first."); return; }
        if (_state.Tasks.Any(t => !t.Completed && t.Title.Equals(title, StringComparison.CurrentCultureIgnoreCase)))
        { ShowStatus("This task is already on your list."); return; }
        _state.Tasks.Insert(0, new(Guid.NewGuid().ToString("N"), title));
        if (_taskInput is not null) _taskInput.Text = "";
        Record("Added task · " + title); TasksChanged();
    }
    private void SetTaskCompleted(string id, bool completed)
    {
        int index = _state.Tasks.FindIndex(t => t.Id == id);
        if (index < 0 || _state.Tasks[index].Completed == completed) return;
        var task = _state.Tasks[index];
        _state.Tasks[index] = task with { Completed = completed };
        if (completed && _state.FocusTaskId == id) _state.FocusTaskId = "";
        Record((completed ? "Completed task · " : "Reopened task · ") + task.Title);
        TasksChanged();
    }
    private void TasksChanged()
    {
        SaveState(); RenderTaskCards(); RenderFocus(); RenderHomeWorkspace(); RefreshWorkspaceSummary();
        if (_commandOpen) RenderCommands();
    }
    private void SelectFocusTask(string id)
    {
        var task = _state.Tasks.FirstOrDefault(t => t.Id == id && !t.Completed);
        if (task is null) return;
        if (_state.FocusTaskId == task.Id) { if (_page != "Study") Navigate("Study"); return; }
        if (_focusSession.IsRunning) { ShowStatus("Pause the timer before choosing a different task."); return; }
        _state.FocusTaskId = task.Id;
        TasksChanged();
        if (_page != "Study") Navigate("Study");
        ShowStatus("Ready to focus on: " + task.Title);
    }
    private void RenderTaskCards()
    {
        if (_taskCards is null) return;
        _taskCards.Children.Clear();
        int done = _state.Tasks.Count(t => t.Completed);
        if (_taskSummary is not null) _taskSummary.Text = (_state.Tasks.Count - done) + " remaining · " + done + " completed";
        foreach (var task in _state.Tasks.Where(t => _showCompletedTasks || !t.Completed).OrderBy(t => t.Completed))
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var check = new CheckBox { Content = Text(task.Title, 13, task.Completed), IsChecked = task.Completed,
                HorizontalAlignment = HorizontalAlignment.Stretch };
            check.Checked += (_, _) => SetTaskCompleted(task.Id, true);
            check.Unchecked += (_, _) => SetTaskCompleted(task.Id, false);
            var more = new Button { Content = "•••", CornerRadius = new CornerRadius(10), VerticalAlignment = VerticalAlignment.Center };
            var menu = new MenuFlyout();
            if (!task.Completed)
            {
                var focus = new MenuFlyoutItem { Text = task.Id == _state.FocusTaskId ? "Current focus task" : "Focus on this" };
                focus.Click += (_, _) => SelectFocusTask(task.Id); menu.Items.Add(focus);
            }
            var remove = new MenuFlyoutItem { Text = "Remove task" };
            remove.Click += (_, _) =>
            {
                _state.Tasks.RemoveAll(t => t.Id == task.Id);
                if (_state.FocusTaskId == task.Id) _state.FocusTaskId = "";
                TasksChanged();
            };
            menu.Items.Add(remove); more.Flyout = menu;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(more, "Options for task " + task.Title);
            Grid.SetColumn(more, 1); row.Children.Add(check); row.Children.Add(more);
            var card = Card(row); card.Padding = new Thickness(12);
            if (task.Id == _state.FocusTaskId) card.BorderBrush = Resource("NexusAccent");
            _taskCards.Children.Add(card);
        }
        if (_taskCards.Children.Count == 0) _taskCards.Children.Add(Text("Room for your next idea.", 13, true));
    }
    private void RefreshCollectionChoices()
    {
        if (_savedCollection is null) return;
        _updatingCollections = true;
        try
        {
            var choices = _state.SavedItems.Select(a => a.Collection).Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(a => a, StringComparer.CurrentCultureIgnoreCase).Prepend("").ToArray();
            _savedCollection.Items.Clear();
            foreach (string choice in choices)
                _savedCollection.Items.Add(new ComboBoxItem { Content = choice.Length == 0 ? "All collections" : choice, Tag = choice });
            if (!choices.Contains(_collectionFilter, StringComparer.CurrentCultureIgnoreCase)) _collectionFilter = "";
            _savedCollection.SelectedItem = _savedCollection.Items.Cast<ComboBoxItem>().First(c =>
                ((string)c.Tag).Equals(_collectionFilter, StringComparison.CurrentCultureIgnoreCase));
        }
        finally { _updatingCollections = false; }
    }
    private void SavedItemsChanged()
    {
        DesktopWorkspace.Normalize(_state);
        SaveState(); RefreshCollectionChoices(); RenderSavedItems(); RenderHomeWorkspace(); RefreshWorkspaceSummary(); RenderDesktopIdentity();
        if (_commandOpen) RenderCommands();
    }
    private async Task EditSavedAsync(SavedItem entry)
    {
        if (_dialogOpen) return;
        _dialogOpen = true;
        try
        {
            var title = new TextBox { Header = "Title", Text = entry.Title, MaxLength = 100 };
            var collection = new TextBox { Header = "Collection", Text = entry.Collection, MaxLength = 24 };
            var favorite = new CheckBox { Content = "Show on Home", IsChecked = entry.Favorite };
            var address = new TextBox { Header = "Web address", Text = entry.Target, MaxLength = 4096 };
            var content = new StackPanel { Spacing = 14 };
            content.Children.Add(title); content.Children.Add(collection); content.Children.Add(favorite);
            if (entry.Kind == "Link") content.Children.Add(address);
            var dialog = new ContentDialog { XamlRoot = DesktopRoot.XamlRoot, RequestedTheme = ElementTheme.Dark,
                Title = "Make it yours", Content = content, PrimaryButtonText = "Save", CloseButtonText = "Cancel" };
            dialog.PrimaryButtonClick += (_, args) =>
            {
                if (string.IsNullOrWhiteSpace(title.Text)) { title.Header = "Enter a title"; args.Cancel = true; }
                if (entry.Kind == "Link" && (!Uri.TryCreate(address.Text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")))
                { address.Header = "Enter a complete http:// or https:// address"; args.Cancel = true; }
            };
            PolishDialog(dialog);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || !_ready) return;
            string target = entry.Kind == "Link" ? new Uri(address.Text.Trim()).AbsoluteUri : entry.Target;
            if (_state.SavedItems.Any(s => s.Id != entry.Id && s.Target.Equals(target, StringComparison.OrdinalIgnoreCase)))
            { ShowStatus("That address is already on your board."); return; }
            int index = _state.SavedItems.FindIndex(s => s.Id == entry.Id);
            if (index < 0) return;
            _state.SavedItems[index] = entry with { Title = title.Text.Trim(), Collection = WorkspaceState.CollectionName(collection.Text),
                Favorite = favorite.IsChecked == true, Target = target };
            SavedItemsChanged();
        }
        finally { _dialogOpen = false; }
    }

    private void ShowWindowOverview(bool changed)
    {
        if (changed) WindowsSearchBox.Text = "";
        FilterWindows(); UpdateWindowTileSize();
        _ = RefreshWindowsAsync();
    }
    private void ReleaseWindowOverview()
    {
        _windowEpoch++; _openWindows = []; WindowsGrid.ItemsSource = null;
    }
    private async Task RefreshWindowsAsync()
    {
        if (_refreshingWindows || !_ready || _page != "Running apps" || HomeBorder.Visibility != Visibility.Visible) return;
        _refreshingWindows = true;
        long epoch = _windowEpoch;
        RefreshWindowsButton.IsEnabled = false;
        try
        {
            var windows = await Task.Run(() => NativeMethods.RunningWindows(_handle));
            if (!_ready || _page != "Running apps" || epoch != _windowEpoch) return;
            if (!windows.SequenceEqual(_openWindows)) { _openWindows = windows; FilterWindows(); }
        }
        catch (Exception ex) { if (_ready) Error("Could not list open windows", ex); else Log.Write("Window overview stopped", ex); }
        finally
        {
            _refreshingWindows = false;
            if (_ready)
            {
                RefreshWindowsButton.IsEnabled = true;
                if (_page == "Running apps" && epoch != _windowEpoch) _ = RefreshWindowsAsync();
            }
        }
    }
    private void FilterWindows()
    {
        if (!_ready || _page != "Running apps") return;
        string[] terms = WindowsSearchBox.Text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var entries = _openWindows.Where(w => terms.All(q => w.Title.Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
            w.ProcessName.Contains(q, StringComparison.CurrentCultureIgnoreCase))).ToArray();
        IntPtr selected = (WindowsGrid.SelectedItem as RunningWindow)?.Handle ?? IntPtr.Zero;
        WindowsGrid.ItemsSource = entries;
        WindowsGrid.SelectedItem = entries.FirstOrDefault(w => w.Handle == selected);
        WindowsEmpty.Visibility = entries.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        WindowsEmpty.Text = terms.Length == 0 ? "No other visible windows yet." : "No windows match this search.";
        WindowCountText.Text = entries.Length + " of " + _openWindows.Count + " open windows";
        PageStatus.Text = _openWindows.Count + " visible windows · Refreshes while this view is active";
    }
    private void WindowsSearch_TextChanged(object sender, TextChangedEventArgs args) => FilterWindows();
    private void WindowsGrid_SizeChanged(object sender, SizeChangedEventArgs args) => UpdateWindowTileSize();
    private async void RefreshWindows_Click(object sender, RoutedEventArgs args) => await RefreshWindowsAsync();
    private void Window_ItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is not RunningWindow window) return;
        if (!NativeMethods.Activate(window.Handle))
            ShowStatus("The window closed or Windows declined the switch. Try Alt+Tab.");
    }
    private void UpdateWindowTileSize()
    {
        if (WindowsGrid.ItemsPanelRoot is not ItemsWrapGrid panel) return;
        double available = Math.Max(160, PageHost.ActualWidth - 48);
        int columns = Math.Max(1, (int)(available / 260));
        panel.ItemWidth = Math.Clamp((available - columns * 6) / columns, 160, 300);
        panel.ItemHeight = 186;
    }
}
