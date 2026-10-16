// නමෝ බුද්ධාය 🤍
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Nexus.Runtime.Startup;
using Nexus.Shell.Desktop;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Windows.ApplicationModel.DataTransfer;

namespace Nexus.Shell.UI.Controls;

internal sealed class StartupAppsView : Grid
{
    private readonly DesktopEnvironment _environment;
    private readonly CancellationTokenSource _life = new();
    private readonly CancellationToken _cancellation;
    private readonly TextBlock _summary = new() { Text = "Startup apps", FontSize = 23, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { Text = "Reading startup registrations…", FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxLines = 6 };
    private readonly TextBox _query = new() { PlaceholderText = "Find a startup app or command", MaxLength = 200 };
    private readonly ComboBox _scope = new() { ItemsSource = new[] { "All registrations", "Can pause", "Saved originals", "View only", "Conflicts" }, SelectedIndex = 0, MinWidth = 155 };
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBlock _title = new() { Text = "Select a startup item", FontSize = 16, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _command = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 66, MaxHeight = 150 };
    private readonly TextBlock _detail = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxLines = 5 };
    private readonly TextBlock _pages = new() { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _refresh, _pause, _restore, _copy, _export, _previous, _next;
    private readonly ProgressRing _loading = new() { IsActive = false, Width = 22, Height = 22 };
    private readonly List<Border> _cards = [];
    private StartupCoordinator? _coordinator;
    private StartupSnapshot? _snapshot;
    private int _offset, _count;
    private const int PageSize = 80;
    private bool _busy, _valid, _released;
    private StartupItem? Selected => (_list.SelectedItem as ListViewItem)?.Tag as StartupItem;

    internal StartupAppsView(DesktopEnvironment environment)
    {
        _environment = environment;
        _cancellation = _life.Token;
        Padding = new Thickness(20, 8, 20, 18); RowSpacing = 12;
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto })
            RowDefinitions.Add(new() { Height = height });
        var header = new StackPanel { Spacing = 8 };
        header.Children.Add(_summary);
        header.Children.Add(new TextBlock { Text = "Review what is registered for sign-in. Pause supported entries for your account and restore their saved originals.", TextWrapping = TextWrapping.Wrap });
        header.Children.Add(new TextBlock { Text = "Registered does not guarantee an app will run: Windows approval and app settings also apply. Services, scheduled tasks and packaged startup tasks stay in Windows controls.", FontSize = 11, TextWrapping = TextWrapping.Wrap });
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        _refresh = Button("Refresh", RefreshAsync); tools.Children.Add(_refresh);
        tools.Children.Add(Button("Windows startup settings", () => { environment.RunWindowsTool("startup-apps"); return Task.CompletedTask; }));
        tools.Children.Add(Button("Task Manager", () => { environment.RunWindowsTool("task-manager"); return Task.CompletedTask; }));
        tools.Children.Add(Button("Nexus sign-in settings", () => { environment.ShowControls("startup"); return Task.CompletedTask; }));
        tools.Children.Add(_loading); header.Children.Add(Scrollable(tools)); Children.Add(header);
        var filters = new Grid { ColumnSpacing = 10 };
        filters.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); filters.ColumnDefinitions.Add(new());
        filters.Children.Add(_scope); Grid.SetColumn(_query, 1); filters.Children.Add(_query); Grid.SetRow(filters, 1); Children.Add(filters);
        Grid.SetRow(_list, 2); Children.Add(_list);
        _list.SelectionChanged += (_, _) => Details();
        var selection = new StackPanel { Spacing = 8 };
        selection.Children.Add(_title); selection.Children.Add(_command); selection.Children.Add(_detail);
        AutomationProperties.SetName(_command, "Original or registered startup command; read only");
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        _pause = Button("Pause at sign-in…", () => ChangeAsync(restore: false));
        _restore = Button("Restore original…", () => ChangeAsync(restore: true));
        _copy = Button("Copy details", () =>
        {
            if (Selected is not { } item) return Task.CompletedTask;
            var data = new DataPackage(); data.SetText(item.Entry.Name + "\n" + StartupRules.Scope(item.Entry.Source) + "\n" + item.Status + "\nOriginal/registered: " + item.Entry.Command +
                (item.CurrentCommand.Length > 0 ? "\nCurrent: " + item.CurrentCommand : "") + "\n" + item.Entry.Location);
            Clipboard.SetContent(data); _status.Text = "Startup details copied."; return Task.CompletedTask;
        });
        _export = Button("Export inventory…", ExportAsync);
        actions.Children.Add(_pause); actions.Children.Add(_restore); actions.Children.Add(_copy); actions.Children.Add(_export);
        selection.Children.Add(Scrollable(actions));
        var folders = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        folders.Children.Add(Button("Your Startup folder", () => OpenFolder(Environment.SpecialFolder.Startup)));
        folders.Children.Add(Button("All-user Startup folder", () => OpenFolder(Environment.SpecialFolder.CommonStartup)));
        selection.Children.Add(Scrollable(folders));
        var card = Card(selection); Grid.SetRow(card, 3); Children.Add(card);
        var footer = new StackPanel { Spacing = 7 };
        var pages = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _previous = Button("Previous", () => { _offset = Math.Max(0, _offset - PageSize); Render(); return Task.CompletedTask; });
        _next = Button("Next", () => { _offset += PageSize; Render(); return Task.CompletedTask; });
        pages.Children.Add(_previous); pages.Children.Add(_pages); pages.Children.Add(_next); footer.Children.Add(pages); footer.Children.Add(_status);
        Grid.SetRow(footer, 4); Children.Add(footer);
        _query.TextChanged += (_, _) => { _offset = 0; Render(); };
        _scope.SelectionChanged += (_, _) => { _offset = 0; Render(); };
        // Keep the full review/confirmation controls reachable on short and
        // narrow desktops, while the inventory itself remains virtualized.
        var body = new Grid { RowSpacing = 12 };
        for (int i = 0; i < 5; i++) body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var children = Children.ToArray(); Children.Clear();
        foreach (var child in children) body.Children.Add(child);
        RowDefinitions.Clear(); RowDefinitions.Add(new());
        _list.Height = 230;
        Children.Add(new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        SizeChanged += (_, _) => _list.Height = Math.Clamp(ActualHeight * 0.32, 140, 340);
        Loaded += async (_, _) => { if (_snapshot is null && !_released) await RefreshAsync(); };
        ApplyAppearance(); UpdateActions();
    }

    private static ScrollViewer Scrollable(UIElement content) => new() { Content = content,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollMode = ScrollMode.Enabled,
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollMode = ScrollMode.Disabled };
    private Border Card(UIElement content)
    {
        var card = new Border { Child = content, Padding = new Thickness(12), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12) };
        _cards.Add(card); return card;
    }
    private Button Button(string text, Func<Task> action)
    {
        var button = new Button { Content = text, Style = (Style)Application.Current.Resources["QuietButton"] };
        AutomationProperties.SetName(button, text);
        button.Click += async (_, _) =>
        {
            if (_released) return;
            try { await action(); }
            catch (Exception error) { if (!_released) _status.Text = error.Message; Log.Write("Startup Apps", error); }
            finally { if (!_released) UpdateActions(); }
        };
        return button;
    }

    private StartupSnapshot Read()
    {
        _coordinator ??= new(new WindowsStartupBackend(), new StartupJournal(StateStore.DirectoryPath));
        return _coordinator.Read(_cancellation);
    }
    private async Task<bool> ReadFreshAsync()
    {
        _valid = false;
        var task = StartupWork.TryRun(Read);
        if (task is null) { _status.Text = "An earlier startup read/change is still finishing. Retry Refresh after it returns."; return false; }
        var snapshot = await task;
        if (_released) return false;
        _snapshot = snapshot; _valid = true; _offset = 0;
        _summary.Text = $"{snapshot.Items.Length:N0} startup registrations · {snapshot.SavedOriginals} saved originals";
        _status.Text = "Observed " + snapshot.ObservedAt.ToString("g") + ". Refresh before reviewing changes.\n" + string.Join("\n", snapshot.Notices);
        Render(); return true;
    }
    private async Task RefreshAsync()
    {
        if (_busy || _released) return;
        _busy = true; _loading.IsActive = true; UpdateActions();
        try { await ReadFreshAsync(); }
        catch (OperationCanceledException) when (_released) { }
        catch (Exception error) { if (!_released) { _valid = false; _status.Text = error.Message; } Log.Write("Startup inventory", error); }
        finally { _busy = false; if (!_released) { _loading.IsActive = false; UpdateActions(); } }
    }

    private async Task ChangeAsync(bool restore)
    {
        if (_busy || !_valid || Selected is not { } selected || (restore ? !selected.CanRestore : !selected.CanPause)) return;
        _busy = true; UpdateActions();
        try
        {
            var text = new TextBlock { TextWrapping = TextWrapping.Wrap, Text = (restore
                ? "Restore the exact saved startup registration, or finish clearing its saved record if it is already present. A newer command will never be overwritten."
                : "Save the exact original, then remove this account's startup registration. This affects the next sign-in; the running app is not stopped.") + "\n\n" + selected.Entry.Command };
            var dialog = new ContentDialog { XamlRoot = XamlRoot, RequestedTheme = RequestedTheme,
                Title = (restore ? "Restore " : "Pause ") + selected.Entry.Name + "?", Content = new ScrollViewer { Content = text, MaxHeight = 260 },
                PrimaryButtonText = restore ? "Restore original" : "Pause at sign-in", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || _released) return;
            _loading.IsActive = true; _valid = false;
            var task = StartupWork.TryRun(() => { if (restore) _coordinator!.Restore(selected.BackupId!.Value); else _coordinator!.Pause(selected.Entry); return true; });
            if (task is null) { _status.Text = "An earlier startup operation is still finishing. Refresh before retrying."; return; }
            string outcome;
            try { await task; outcome = restore ? "Original restored. Windows startup approval still applies." : "Startup registration paused. Its exact original is saved for Restore."; }
            catch (Exception error) { outcome = error.Message; Log.Write("Startup change", error); }
            if (_released) return;
            try { if (await ReadFreshAsync()) _status.Text = outcome + "\n" + _status.Text; }
            catch (Exception error) { if (!_released) _status.Text = outcome + "\nRefresh failed: " + error.Message; }
        }
        finally { _busy = false; if (!_released) { _loading.IsActive = false; UpdateActions(); } }
    }

    private void Render()
    {
        if (_released || _snapshot is null) return;
        string? selected = Selected?.Entry.Id;
        string query = _query.Text.Trim();
        var rows = _snapshot.Items.Where(i => _scope.SelectedIndex switch
        { 1 => i.CanPause, 2 => i.BackupId is not null, 3 => !i.CanPause && i.BackupId is null, 4 => i.BackupId is not null && !i.CanRestore, _ => true })
            .Where(i => query.Length == 0 || (i.Entry.Name + " " + i.Entry.Command + " " + i.CurrentCommand + " " + StartupRules.Scope(i.Entry.Source)).Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        _count = rows.Length; _offset = Math.Clamp(_offset, 0, Math.Max(0, (_count - 1) / PageSize * PageSize));
        _list.Items.Clear();
        foreach (var row in rows.Skip(_offset).Take(PageSize))
        {
            var face = new StackPanel { Spacing = 4 };
            face.Children.Add(new TextBlock { Text = row.Entry.Name.Length > 0 ? row.Entry.Name : "(Default registration)", FontSize = 15, TextTrimming = TextTrimming.CharacterEllipsis });
            face.Children.Add(new TextBlock { Text = row.Status + " · " + StartupRules.Scope(row.Entry.Source), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis });
            var item = new ListViewItem { Content = face, Tag = row, Padding = new Thickness(10, 9, 10, 9), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(item, row.Entry.Name + ", " + row.Status + ", " + StartupRules.Scope(row.Entry.Source));
            _list.Items.Add(item); if (row.Entry.Id == selected) _list.SelectedItem = item;
        }
        if (rows.Length == 0) _list.Items.Add(new ListViewItem { Content = "No startup registrations match this view.", IsEnabled = false });
        _pages.Text = $"{(_count == 0 ? 0 : _offset + 1)}–{Math.Min(_offset + PageSize, _count)} of {_count}";
        Details();
    }
    private void Details()
    {
        var item = Selected;
        _title.Text = item is null ? "Select a startup item" : item.Entry.Name + " · " + item.Status;
        _command.Text = item?.Entry.Command ?? "";
        _detail.Text = item is null ? "Registration commands are shown for review and are never launched by this manager."
            : item.Entry.Location + "\n" + item.Detail + (item.CurrentCommand.Length > 0 && item.CurrentCommand != item.Entry.Command ? "\nCurrently registered: " + item.CurrentCommand : "");
        UpdateActions();
    }
    private Task OpenFolder(Environment.SpecialFolder folder)
    {
        string path = Environment.GetFolderPath(folder);
        if (path.Length == 0) throw new DirectoryNotFoundException("Windows did not report this Startup folder.");
        _environment.ShowFiles(path); return Task.CompletedTask;
    }
    private async Task ExportAsync()
    {
        if (_busy || _snapshot is not { } snapshot) return;
        _busy = true; UpdateActions();
        try
        {
            var paths = await _environment.PickAsync(new(FileSelectionKind.SaveFile, "Export startup inventory to a new CSV", [".csv"], "Nexus-startup-apps.csv"));
            if (_released || paths.Count == 0) return;
            await Task.Run(() => StartupReport.ExportNew(snapshot, paths[0]));
            if (!_released) _status.Text = "Inventory saved. It contains commands/paths from the observed list; keep it private as needed.";
        }
        finally { _busy = false; }
    }
    private void UpdateActions()
    {
        bool ready = !_released && !_busy;
        _refresh.IsEnabled = ready;
        _scope.IsEnabled = _query.IsEnabled = _list.IsEnabled = ready;
        _pause.IsEnabled = ready && _valid && Selected?.CanPause == true;
        _restore.IsEnabled = ready && _valid && Selected?.CanRestore == true;
        _copy.IsEnabled = ready && Selected is not null;
        _export.IsEnabled = ready && _snapshot is not null;
        _previous.IsEnabled = ready && _offset > 0; _next.IsEnabled = ready && _offset + PageSize < _count;
    }
    internal void ApplyAppearance()
    {
        RequestedTheme = _environment.Theme.ElementTheme;
        _status.Foreground = _detail.Foreground = _pages.Foreground = _environment.Theme.Brush("NexusMuted");
        foreach (var card in _cards) { card.Background = _environment.Theme.Brush("NexusPanel"); card.BorderBrush = _environment.Theme.Edge; card.CornerRadius = new CornerRadius(_environment.Theme.HighContrast ? 0 : 12); }
    }
    internal void Release()
    {
        if (_released) return;
        _released = true; _life.Cancel(); _life.Dispose(); _snapshot = null; _list.Items.Clear(); _command.Text = "";
    }
}
