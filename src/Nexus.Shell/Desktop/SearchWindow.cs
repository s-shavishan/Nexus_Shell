using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Nexus.Runtime;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Nexus.Shell.UI;
using Windows.System;

namespace Nexus.Shell.Desktop;

// A desktop surface, not another app launcher. Launchpad remains the app catalog.
internal sealed class SearchWindow : DesktopPanelWindow
{
    private sealed record Row(string Name, string Detail, string Icon, SearchHit? File = null, string? Command = null, RunningWindow? Window = null);
    private readonly TextBox _query = new() { PlaceholderText = "Find a file, folder, window or desktop control…", MaxLength = SearchPolicy.MaximumQueryLength, FontSize = 17, Padding = new Thickness(14, 11, 14, 11), CornerRadius = new CornerRadius(13) };
    private readonly ComboBox _category = new() { MinWidth = 145 };
    private readonly ComboBox _scope = new() { MinWidth = 150 };
    private readonly ListView _results = new() { SelectionMode = ListViewSelectionMode.Single, IsItemClickEnabled = false };
    private readonly TextBlock _status = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxLines = 3 };
    private readonly TextBlock _engine = new() { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _hint = new() { Text = "↑ ↓ select   ·   Enter open   ·   Ctrl+Enter open folder   ·   Ctrl+Shift+C copy path", FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar _progress = new() { IsIndeterminate = false, Height = 2, Visibility = Visibility.Collapsed };
    private readonly Border _listFrame = new() { CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), Padding = new Thickness(5) };
    private readonly Border _launchFrame = new() { BorderThickness = new Thickness(1), Padding = new Thickness(8, 10, 8, 10) };
    private readonly Button _open, _folder, _copy, _previous, _next;
    private readonly LatestWorkQueue _work = new();
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(140) };
    private readonly List<Row> _rows = [];
    private int _generation, _offset;
    private SearchReply? _reply;
    private bool _initializing = true, _validation;
    internal SearchWindow(DesktopEnvironment environment) : this(environment, new Grid { RowSpacing = 12 }) { }
    private SearchWindow(DesktopEnvironment environment, Grid body) : base(environment, "Search", body, 790, 700, center: true, animateBody: false)
    {
        foreach (bool flexible in new[] { false, false, false, false, true, false, false, false })
            body.RowDefinitions.Add(new() { Height = flexible ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        body.Children.Add(_query);

        // The reference's immediately accessible application strip belongs
        // inside Spotlight, not in the search-results virtualized list.
        // Each button calls the existing shell action; no second launcher or
        // duplicate catalog/index is created.
        var quickApps = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        void QuickApp(string name, string icon, Action open)
        {
            var image = NexusIcons.Image(icon, 34);
            var label = new TextBlock
            {
                Text = name, FontSize = 12,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var contents = new StackPanel { Spacing = 7, HorizontalAlignment = HorizontalAlignment.Center };
            contents.Children.Add(image); contents.Children.Add(label);
            var button = new Button
            {
                Content = contents, Width = 104, Height = 90,
                CornerRadius = new CornerRadius(14),
                Style = (Style)Application.Current.Resources["QuietButton"],
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, "Open " + name);
            ToolTipService.SetToolTip(button, "Open " + name);
            button.Click += (_, _) => { Hide(); open(); };
            quickApps.Children.Add(button);
        }
        QuickApp("Launchpad", "Launchpad", () => Environment.ShowMenu());
        QuickApp("Files", "Files", () => Environment.ShowFiles());
        QuickApp("Explore", "Explore", () => Environment.ShowSections("Explore"));
        QuickApp("Notes", "Note", () => Environment.ShowSections("Study"));
        QuickApp("Settings", "Settings", () => Environment.ShowQuickSettings());
        QuickApp("Windows", "Windows", () => Environment.ShowWindowOverview());
        _launchFrame.Child = new ScrollViewer
        {
            Content = quickApps,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollMode = ScrollMode.Disabled
        };
        Grid.SetRow(_launchFrame, 1);
        body.Children.Add(_launchFrame);
        var filters = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var category in Enum.GetValues<SearchCategory>()) _category.Items.Add(new ComboBoxItem { Content = category == SearchCategory.All ? "All results" : category == SearchCategory.Code ? "Source code" : category.ToString(), Tag = category });
        _category.SelectedIndex = 0;
        _scope.Items.Add(new ComboBoxItem { Content = "My files", Tag = SearchScope.MyFiles });
        _scope.Items.Add(new ComboBoxItem { Content = "Indexed drives", Tag = SearchScope.IndexedDrives }); _scope.SelectedIndex = 0;
        filters.Children.Add(_category); filters.Children.Add(_scope); filters.Children.Add(ActionButton("Refresh", () => Schedule(resetPage: false, immediate: true)));
        var setup = ActionButton("Engine setup", () => { }); setup.Flyout = Setup(); filters.Children.Add(setup);
        var filterScroll = new ScrollViewer { Content = filters, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(filterScroll, 2); body.Children.Add(filterScroll);
        Grid.SetRow(_progress, 3); body.Children.Add(_progress);
        _listFrame.Child = _results; Grid.SetRow(_listFrame, 4); body.Children.Add(_listFrame);
        Grid.SetRow(_status, 5); body.Children.Add(_status);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        _open = ActionButton("Open", OpenSelected); _folder = ActionButton("Open folder", RevealSelected); _copy = ActionButton("Copy path", () => _ = CopySelectedAsync());
        _previous = ActionButton("Previous", () => Page(-1)); _next = ActionButton("Next", () => Page(1));
        foreach (var button in new[] { _open, _folder, _copy, _previous, _next }) actions.Children.Add(button);
        var actionScroll = new ScrollViewer { Content = actions, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(actionScroll, 6); body.Children.Add(actionScroll);
        var footer = new StackPanel { Spacing = 5 }; footer.Children.Add(_hint); footer.Children.Add(_engine); Grid.SetRow(footer, 7); body.Children.Add(footer);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_query, "Search names and desktop controls");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_category, "Result category");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_scope, "File search scope");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_results, "Search results");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(_status, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        _query.TextChanged += (_, _) => Schedule(); _category.SelectionChanged += (_, _) => Schedule(); _scope.SelectionChanged += (_, _) => Schedule();
        _results.SelectionChanged += (_, _) => RefreshActions();
        _results.DoubleTapped += (_, args) =>
        {
            // Double-clicking blank list space must not activate the old selection.
            DependencyObject? node = args.OriginalSource as DependencyObject;
            while (node is not null && node is not ListViewItem) node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node);
            if (node is ListViewItem item && item.Tag is Row) { _results.SelectedItem = item; OpenSelected(); args.Handled = true; }
        };
        _query.PreviewKeyDown += QueryKey;
        Shortcut(VirtualKey.Enter, VirtualKeyModifiers.Control, RevealSelected);
        Shortcut(VirtualKey.C, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, () => _ = CopySelectedAsync());
        Shortcut(VirtualKey.L, VirtualKeyModifiers.Control, () => { _query.Focus(FocusState.Programmatic); _query.SelectAll(); });
        Shortcut(VirtualKey.F5, VirtualKeyModifiers.None, () => Schedule(resetPage: false, immediate: true));
        _results.KeyDown += (_, args) => { if (args.Key == VirtualKey.Enter && !ControlDown()) { OpenSelected(); args.Handled = true; } };
        _debounce.Tick += (_, _) => { _debounce.Stop(); _ = SearchAsync(); };
        _initializing = false; ApplyAppearance(); RefreshActions();
    }
    private void Shortcut(VirtualKey key, VirtualKeyModifiers modifiers, Action action)
    { var shortcut = new KeyboardAccelerator { Key = key, Modifiers = modifiers }; shortcut.Invoked += (_, args) => { action(); args.Handled = true; }; Root.KeyboardAccelerators.Add(shortcut); }
    private Flyout Setup()
    {
        var content = new StackPanel { Spacing = 10, MaxWidth = 330 };
        content.Children.Add(new TextBlock { Text = "Everything powers file search", FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        content.Children.Add(new TextBlock { Text = "Install Everything 1.4.1 or newer from voidtools, then run its normal client in this Windows session. The indexing service alone is not enough. Phase 02 connects to the default (unnamed) instance.\n\nMy files searches your Windows profile. Indexed drives searches local drive paths already indexed by Everything. File contents and network shares are not searched.\n\nDesktop controls and open windows work without Everything. Nexus does not save your search history or create another index.", TextWrapping = TextWrapping.Wrap });
        content.Children.Add(ActionButton("Open official download page", () => Environment.OpenTarget("https://www.voidtools.com/downloads/")));
        content.Children.Add(ActionButton("Open Launchpad", () => Environment.ShowMenu()));
        return new Flyout { Content = content };
    }
    private SearchCategory Category => (_category.SelectedItem as ComboBoxItem)?.Tag is SearchCategory value ? value : SearchCategory.All;
    private SearchScope Scope => (_scope.SelectedItem as ComboBoxItem)?.Tag is SearchScope value ? value : SearchScope.MyFiles;
    private Row? Selected => (_results.SelectedItem as ListViewItem)?.Tag as Row;
    private void Schedule(bool resetPage = true, bool immediate = false)
    {
        if (_initializing || _validation || IsClosed || !IsOpen) return;
        ++_generation; _work.Cancel(); _debounce.Stop(); _reply = null; if (resetPage) _offset = 0;
        _rows.Clear(); Render(); _scope.IsEnabled = Category <= SearchCategory.Code;
        _status.Text = "Searching…"; _engine.Text = ""; SetBusy(false);
        if (immediate) _ = SearchAsync(); else _debounce.Start();
    }
    private async Task SearchAsync()
    {
        int generation = _generation; string query = _query.Text.Trim(); var category = Category;
        var request = new SearchRequest(query, category, Scope, _offset);
        _rows.Clear(); AddLocal(query, category); Render();
        if (category is SearchCategory.Commands or SearchCategory.Windows || query.Length == 0)
        {
            _status.Text = query.Length == 0 ? "Start typing to find files. Desktop controls and open windows are ready below." : _rows.Count == 0 ? "No matching desktop results." : $"{_rows.Count} desktop results";
            _engine.Text = "On-device search · no search history saved"; return;
        }
        SetBusy(true); _status.Text = "Finding indexed files…";
        try
        {
            var reply = await _work.RunAsync(async token =>
            {
                var result = await Environment.SearchFilesAsync(request, token);
                for (int retry = 0; result.Status == SearchStatus.Busy && retry < 2; retry++)
                { await Task.Delay(180, token); result = await Environment.SearchFilesAsync(request, token); }
                return result;
            });
            if (IsClosed || !IsOpen || generation != _generation) return;
            _reply = reply;
            foreach (var hit in reply.Items) _rows.Add(new(hit.Name, hit.Path, FilePresentation.Icon(hit.Path, hit.IsFolder), File: hit));
            Render();
            _status.Text = reply.Status switch
            {
                SearchStatus.Ready => reply.Items.Length == 0 ? "No matching indexed files in this scope. Try another category or Indexed drives."
                    : $"Files {reply.Offset + 1:N0}–{reply.Offset + reply.Items.Length:N0} of {reply.Total:N0}" + (reply.Total > SearchPolicy.MaximumOffset + SearchPolicy.PageSize ? " · refine the name to see more" : ""),
                SearchStatus.Unavailable => "Everything is not running in this session. Open Engine setup; desktop results still work.",
                SearchStatus.Indexing => "Everything is loading its index. Refresh when it is ready; desktop results still work.",
                SearchStatus.Busy => "The previous file search is finishing. Refresh to try again.",
                SearchStatus.TimedOut => "Everything took too long to reply. Refresh to retry, or check its client.",
                SearchStatus.Unsupported => "This Everything instance does not support the required interface. Use a default instance of Everything 1.4.1 or newer.",
                _ => "The file search could not finish. Check Everything and refresh; the desktop remains available."
            };
            _engine.Text = "Files: Everything" + (reply.EngineVersion.Length > 0 ? " " + reply.EngineVersion : "") + " · " + (Scope == SearchScope.MyFiles ? "Windows profile" : "Indexed local drives") + " · names only";
        }
        catch (OperationCanceledException) { }
        catch (Exception) { if (!IsClosed && IsOpen && generation == _generation) { _status.Text = "Nexus Search could not reach Core. Refresh to reconnect; desktop results still work."; _engine.Text = "File search unavailable"; } }
        finally { if (!IsClosed && generation == _generation) SetBusy(false); }
    }
    private void AddLocal(string query, SearchCategory category)
    {
        if (category is SearchCategory.All or SearchCategory.Commands)
            foreach (var command in DesktopSearchCatalog.Find(query).Take(category == SearchCategory.All ? 5 : 20)) _rows.Add(new(command.Name, "Desktop · " + command.Detail, "Settings", Command: command.Id));
        if (category is SearchCategory.All or SearchCategory.Windows)
            foreach (var window in Environment.WindowBoardWindows().Where(window => DesktopSearchCatalog.Matches(query, window.Title + " " + window.ProcessName)).Take(category == SearchCategory.All ? 8 : 50))
                _rows.Add(new(window.Title, "Open window · " + window.ProcessName, "Windows", Window: window));
    }
    private void Render()
    {
        Row? selected = Selected; _results.Items.Clear();
        var theme = Environment.Theme;
        foreach (var row in _rows)
        {
            var face = new Grid { ColumnSpacing = 12 }; face.ColumnDefinitions.Add(new() { Width = new GridLength(32) }); face.ColumnDefinitions.Add(new());
            face.Children.Add(NexusIcons.Image(row.Icon, 28)); var labels = new StackPanel { Spacing = 3 };
            labels.Children.Add(new TextBlock { Text = row.Name, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = theme.Brush("NexusText"), TextTrimming = TextTrimming.CharacterEllipsis });
            labels.Children.Add(new TextBlock { Text = row.Detail, FontSize = 11, Foreground = theme.Brush("NexusMuted"), TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(labels, 1); face.Children.Add(labels);
            var item = new ListViewItem { Content = face, Tag = row, Padding = new Thickness(10, 9, 10, 9), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, row.Name + ", " + row.Detail); ToolTipService.SetToolTip(item, row.Detail); _results.Items.Add(item);
            if (row == selected) _results.SelectedItem = item;
        }
        if (_results.SelectedIndex < 0 && _results.Items.Count > 0) _results.SelectedIndex = 0;
        RefreshActions();
    }
    private void RefreshActions()
    {
        _open.IsEnabled = Selected is not null; _folder.IsEnabled = _copy.IsEnabled = Selected?.File is not null;
        _previous.IsEnabled = _reply?.Status == SearchStatus.Ready && _offset > 0;
        _next.IsEnabled = _reply?.Status == SearchStatus.Ready && (long)_offset + SearchPolicy.PageSize < _reply.Total && _offset < SearchPolicy.MaximumOffset;
    }
    private void SetBusy(bool busy) { _progress.IsIndeterminate = busy; _progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed; }
    private void Page(int direction)
    { if ((direction < 0 && !_previous.IsEnabled) || (direction > 0 && !_next.IsEnabled)) return; _offset += direction * SearchPolicy.PageSize; Schedule(resetPage: false, immediate: true); }
    private void QueryKey(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key is VirtualKey.Down or VirtualKey.Up && _results.Items.Count > 0)
        { _results.SelectedIndex = Math.Clamp(_results.SelectedIndex + (args.Key == VirtualKey.Down ? 1 : -1), 0, _results.Items.Count - 1); _results.ScrollIntoView(_results.SelectedItem); args.Handled = true; }
        else if (args.Key == VirtualKey.Enter && !ControlDown()) { OpenSelected(); args.Handled = true; }
    }
    private static bool ControlDown() => (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
    private void OpenSelected()
    {
        if (Selected is not { } row || _validation) return;
        if (row.File is { } file) { if (file.IsFolder) Environment.ShowFiles(file.Path); else Environment.OpenTarget(file.Path); }
        else if (row.Command is { } command) { Hide(); Environment.RunSearchCommand(command); }
        else if (row.Window is { } window) { Hide(); Environment.RestoreDockWindow(window); }
    }
    private void RevealSelected() { if (!_validation && Selected?.File is { } file) Environment.ShowFiles(file.IsFolder ? file.Path : SearchPolicy.Parent(file.Path)); }
    private async Task CopySelectedAsync()
    {
        if (_validation || Selected?.File is not { } file) return; int generation = _generation;
        bool copied = await Environment.CopyClipboardAsync(file.Path);
        if (!IsClosed && IsOpen && generation == _generation) _status.Text = copied ? "Path copied." : "The clipboard is busy. Try Copy path again.";
    }
    internal void Show() => ShowPanel();
    protected override void OnShown()
    { _query.Focus(FocusState.Programmatic); if (!_validation) Schedule(immediate: true); }
    protected override void OnHidden()
    {
        ++_generation; _debounce.Stop(); _work.Cancel(); SetBusy(false);
        if (_validation) return; _initializing = true; _query.Text = ""; _initializing = false; _rows.Clear(); _reply = null; _results.Items.Clear(); _status.Text = _engine.Text = ""; RefreshActions();
    }
    protected override void OnClosed() { _work.Dispose(); Environment.SearchClosed(this); }
    internal void PopulateValidation()
    {
        _validation = true;
        _rows.Add(new("Project notes.txt", @"C:\Example\Project notes.txt", "Files", File: new(@"C:\Example\Project notes.txt", "Project notes.txt", false)));
        _rows.Add(new("Sound controls", "Desktop · Volume and audio devices", "Settings", Command: "sound"));
        _status.Text = "Files 1–1 of 1"; _engine.Text = "Everything · Windows profile · names only"; Render();
    }
    internal override void ApplyAppearance()
    {
        base.ApplyAppearance(); var theme = Environment.Theme;
        _query.Foreground = _results.Foreground = theme.Brush("NexusText"); _status.Foreground = _engine.Foreground = _hint.Foreground = theme.Brush("NexusMuted");
        _query.Background = _listFrame.Background = theme.Material("Card", Environment.Session.State.NativeGlass);
        _launchFrame.Background = MidnightGlassMetrics.ReferenceEnabled(theme.Palette, theme.HighContrast)
            ? theme.Surface("Dock") : theme.Material("Card", Environment.Session.State.NativeGlass);
        _query.BorderBrush = _listFrame.BorderBrush = _launchFrame.BorderBrush = theme.Edge;
        _query.CornerRadius = new CornerRadius(theme.HighContrast ? 0 : MidnightGlassMetrics.ControlRadius);
        _launchFrame.CornerRadius = _listFrame.CornerRadius =
            new CornerRadius(theme.HighContrast ? 0 : MidnightGlassMetrics.CardRadius);
        if (_rows.Count > 0) Render();
    }
}
