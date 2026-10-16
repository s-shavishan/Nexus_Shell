// නමෝ බුද්ධාය 🤍
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Nexus.Runtime.Storage;
using Nexus.Shell.Desktop;
using Nexus.Shell.Services;
using Windows.ApplicationModel.DataTransfer;

namespace Nexus.Shell.UI.Controls;

internal sealed class StorageAnalyzerView : Grid
{
    private sealed record Row(string Title, string Path, long Bytes, string Detail, string OpenPath, bool Folder = false);
    private readonly DesktopEnvironment _environment;
    private readonly TextBox _root = new() { Header = "Folder to analyze", MaxLength = 32767, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _limit = new() { Header = "Entry limit", ItemsSource = new[] { "100,000", "500,000", "1,000,000" }, SelectedIndex = 1 };
    private readonly CheckBox _hidden = new() { Content = "Include hidden items", IsChecked = true };
    private readonly TextBlock _summary = new() { Text = "Find what uses space in a folder or drive.", FontSize = 19, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { Text = "Choose a location and Analyze. No scan runs until you ask.", FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _location = new() { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly ComboBox _section = new() { ItemsSource = new[] { "Folders", "Largest files", "File types", "Skipped paths" }, SelectedIndex = 0, MinWidth = 150 };
    private readonly TextBox _filter = new() { PlaceholderText = "Filter these results", MaxLength = 200 };
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBlock _pageLabel = new() { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
    private readonly ProgressBar _progressBar = new() { Visibility = Visibility.Collapsed, IsIndeterminate = true };
    private readonly Button _choose, _analyze, _cancel, _export, _up, _home, _open, _copy, _previous, _next;
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly List<Border> _cards = [];
    private StorageScanResult? _result;
    private StorageProgress? _progress;
    private CancellationTokenSource? _scan;
    private string _folder = "";
    private bool _busy, _running, _released;
    private int _offset, _count;
    private const int PageSize = 100;
    private Row? Selected => (_list.SelectedItem as ListViewItem)?.Tag as Row;

    internal StorageAnalyzerView(DesktopEnvironment environment)
    {
        _environment = environment;
        Padding = new Thickness(20, 8, 20, 18); RowSpacing = 12;
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
            RowDefinitions.Add(new() { Height = height });
        _root.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        AutomationProperties.SetName(_root, "Folder or drive to analyze");
        var form = new StackPanel { Spacing = 8 };
        form.Children.Add(_summary); form.Children.Add(_root);
        var options = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        _choose = Button("Choose folder…", PickAsync);
        _analyze = Button("Analyze", ScanAsync);
        _cancel = Button("Cancel scan", () => { _scan?.Cancel(); _status.Text = "Stopping after the current filesystem call returns…"; return Task.CompletedTask; });
        options.Children.Add(_choose); options.Children.Add(_analyze); options.Children.Add(_cancel);
        form.Children.Add(Scrollable(options));
        var settings = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 15 };
        settings.Children.Add(_limit); settings.Children.Add(_hidden); form.Children.Add(Scrollable(settings));
        Children.Add(Card(form));
        Grid.SetRow(_progressBar, 1); Children.Add(_progressBar);
        var resultsHeader = new StackPanel { Spacing = 8 };
        var filters = new Grid { ColumnSpacing = 10 };
        filters.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); filters.ColumnDefinitions.Add(new());
        filters.Children.Add(_section); Grid.SetColumn(_filter, 1); filters.Children.Add(_filter); resultsHeader.Children.Add(filters);
        var trail = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        _home = Button("Scan root", () => { if (_result is not null) Navigate(_result.Root); return Task.CompletedTask; });
        _up = Button("Up", () => { if (_result?.Tree.FirstOrDefault(f => Same(f.Path, _folder))?.Parent is { } parent) Navigate(parent); return Task.CompletedTask; });
        trail.Children.Add(_home); trail.Children.Add(_up); resultsHeader.Children.Add(trail); resultsHeader.Children.Add(_location);
        Grid.SetRow(resultsHeader, 2); Children.Add(resultsHeader);
        Grid.SetRow(_list, 3); Children.Add(_list);
        _list.SelectionChanged += (_, _) => UpdateActions();
        _list.DoubleTapped += (_, _) =>
        {
            if (_busy || _released || Selected is not { } row) return;
            if (row.Folder) Navigate(row.Path); else if (row.OpenPath.Length > 0) environment.ShowFiles(row.OpenPath);
        };
        var footer = new StackPanel { Spacing = 8 };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        _open = Button("Open in Files", () => { if (Selected is { OpenPath.Length: > 0 } row) environment.ShowFiles(row.OpenPath); return Task.CompletedTask; });
        _copy = Button("Copy path", () => { if (Selected is { Path.Length: > 0 } row) { var data = new DataPackage(); data.SetText(row.Path); Clipboard.SetContent(data); _status.Text = "Path copied."; } return Task.CompletedTask; });
        _export = Button("Export CSV…", ExportAsync);
        actions.Children.Add(_open); actions.Children.Add(_copy); actions.Children.Add(_export); footer.Children.Add(Scrollable(actions));
        var pages = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _previous = Button("Previous", () => { _offset = Math.Max(0, _offset - PageSize); Render(); return Task.CompletedTask; });
        _next = Button("Next", () => { _offset += PageSize; Render(); return Task.CompletedTask; });
        pages.Children.Add(_previous); pages.Children.Add(_pageLabel); pages.Children.Add(_next); footer.Children.Add(pages); footer.Children.Add(_status);
        Grid.SetRow(footer, 4); Children.Add(footer);
        _section.SelectionChanged += (_, _) => { _offset = 0; Render(); };
        _filter.TextChanged += (_, _) => { _offset = 0; Render(); };
        _poll.Tick += (_, _) =>
        {
            if (_released || !_running || Volatile.Read(ref _progress) is not { } p) return;
            _summary.Text = StorageReport.Size(p.Bytes) + $" observed · {p.Files:N0} files · {p.Folders:N0} folders";
            _status.Text = (_scan?.IsCancellationRequested == true ? "Stopping after current filesystem call… " : "Scanning… ") +
                $"{p.Skipped:N0} skipped · {p.Elapsed:mm\\:ss}\n" + p.Path;
        };
        var body = new Grid { RowSpacing = 12 };
        for (int i = 0; i < 5; i++) body.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var children = Children.ToArray(); Children.Clear();
        foreach (var child in children) body.Children.Add(child);
        RowDefinitions.Clear(); RowDefinitions.Add(new());
        _list.Height = 260;
        Children.Add(new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        SizeChanged += (_, _) => _list.Height = Math.Clamp(ActualHeight * 0.34, 140, 380);
        ApplyAppearance(); UpdateActions();
    }

    private static ScrollViewer Scrollable(UIElement content) => new() { Content = content,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollMode = ScrollMode.Enabled,
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollMode = ScrollMode.Disabled };
    private Border Card(UIElement content)
    {
        var card = new Border { Child = content, Padding = new Thickness(14), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12) };
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
            catch (Exception error) { if (!_released) _status.Text = error.Message; Log.Write("Storage Analyzer", error); }
            finally { if (!_released) UpdateActions(); }
        };
        return button;
    }

    private async Task PickAsync()
    {
        if (_busy) return;
        _busy = true; UpdateActions();
        try { var paths = await _environment.PickAsync(new(FileSelectionKind.Folder, "Analyze a folder")); if (!_released && paths.Count > 0) _root.Text = paths[0]; }
        finally { _busy = false; }
    }

    private async Task ScanAsync()
    {
        if (_busy) return;
        var options = new StorageScanOptions(_root.Text.Trim(), MaximumEntries: new[] { 100_000, 500_000, 1_000_000 }[Math.Clamp(_limit.SelectedIndex, 0, 2)], IncludeHidden: _hidden.IsChecked == true);
        var cancellation = new CancellationTokenSource();
        Task<StorageScanResult>? task;
        try { task = StorageScanRunner.TryStart(options, p => Volatile.Write(ref _progress, p), cancellation.Token); }
        catch { cancellation.Dispose(); throw; }
        if (task is null) { cancellation.Dispose(); _status.Text = "An earlier storage scan is still finishing its filesystem call. Retry when it returns."; return; }
        _scan = cancellation; _busy = _running = true; _progress = null;
        _status.Text = "Scanning metadata. Links and cloud-recall items are skipped; Cancel stops between filesystem calls.";
        _progressBar.Visibility = Visibility.Visible; _poll.Start(); UpdateActions();
        try
        {
            var result = await task;
            if (_released) return;
            _result = result; _folder = result.Root; _offset = 0;
            _summary.Text = StorageReport.Size(result.Bytes) + $" logical size · {result.Files:N0} files · {result.Folders:N0} folders";
            _status.Text = result.State + (result.Skipped > 0 ? " with skipped paths" : "") + $" · {result.Skipped:N0} skipped · {result.Elapsed:mm\\:ss} · observed {result.ObservedAt:t}.\n" +
                "Logical sizes can count hard links more than once; they are not allocated disk usage. " +
                (result.State != StorageScanState.Complete || result.Skipped > 0 ? "Totals are partial. Choose a smaller folder or review skipped paths." : "");
            Render();
        }
        catch
        {
            if (!_released) _summary.Text = _result is null ? "Scan failed. Choose an available folder and retry." : "Scan failed · previous results from " + _result.ObservedAt.ToString("g") + " are retained.";
            throw;
        }
        finally
        {
            cancellation.Dispose(); _scan = null; _busy = _running = false;
            if (!_released) { _poll.Stop(); _progressBar.Visibility = Visibility.Collapsed; UpdateActions(); }
        }
    }

    private void Navigate(string folder)
    {
        if (_result is null || !_result.Tree.Any(f => Same(f.Path, folder))) return;
        _folder = folder; _section.SelectedIndex = 0; _offset = 0; Render();
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private void Render()
    {
        if (_released) return;
        _list.Items.Clear();
        if (_result is null) return;
        IEnumerable<Row> source = _section.SelectedIndex switch
        {
            1 => _result.LargestFiles.Select(f => new Row(Path.GetFileName(f.Path), f.Path, f.Bytes, "Modified " + f.ModifiedUtc.ToLocalTime().ToString("g"), Path.GetDirectoryName(f.Path) ?? _result.Root)),
            2 => _result.FileTypes.Select(t => new Row(t.Extension, "", t.Bytes, $"{t.Files:N0} files", "")),
            3 => _result.SkippedPaths.Select(s => new Row(Path.GetFileName(s.Path), s.Path, 0, s.Reason, Path.GetDirectoryName(s.Path) ?? _result.Root)),
            _ => _result.Tree.Where(f => f.Parent is not null && Same(f.Parent, _folder)).OrderByDescending(f => f.Bytes)
                .Select(f => new Row(Path.GetFileName(f.Path), f.Path, f.Bytes, $"{f.Files:N0} files · {f.Folders:N0} subfolders" + (f.Partial ? " · partial" : ""), f.Path, true))
        };
        string query = _filter.Text.Trim();
        var rows = source.Where(r => query.Length == 0 || (r.Title + " " + r.Path + " " + r.Detail).Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        _count = rows.Length; _offset = Math.Clamp(_offset, 0, Math.Max(0, (_count - 1) / PageSize * PageSize));
        long total = _section.SelectedIndex == 0 ? _result.Tree.First(f => Same(f.Path, _folder)).Bytes : _result.Bytes;
        foreach (var row in rows.Skip(_offset).Take(PageSize))
        {
            var content = new StackPanel { Spacing = 5, HorizontalAlignment = HorizontalAlignment.Stretch };
            content.Children.Add(new TextBlock { Text = row.Title + (_section.SelectedIndex == 3 ? "" : " · " + StorageReport.Size(row.Bytes)), FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis });
            if (_section.SelectedIndex != 3) content.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = total > 0 ? Math.Clamp(row.Bytes * 100d / total, 0, 100) : 0, Height = 5 });
            content.Children.Add(new TextBlock { Text = row.Detail + (row.Path.Length > 0 ? "\n" + row.Path : ""), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 3 });
            var item = new ListViewItem { Content = content, Tag = row, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(10, 9, 10, 9) };
            AutomationProperties.SetName(item, row.Title + ", " + StorageReport.Size(row.Bytes) + ", " + row.Detail); _list.Items.Add(item);
        }
        _pageLabel.Text = $"{(_count == 0 ? 0 : _offset + 1)}–{Math.Min(_offset + PageSize, _count)} of {_count}";
        _location.Text = _section.SelectedIndex switch { 1 => "Largest " + _result.LargestFiles.Length + " observed files in the scan", 2 => "Observed file types · at most 256 groups", 3 => $"First {_result.SkippedPaths.Length} sampled paths / {_result.Skipped:N0} skipped", _ => _folder + " · double-click a folder to drill down" };
        if (_count == 0) _list.Items.Add(new ListViewItem { Content = "No results in this view. Try another section or filter.", IsEnabled = false });
        UpdateActions();
    }

    private async Task ExportAsync()
    {
        if (_busy || _result is not { } result) return;
        _busy = true; UpdateActions();
        try
        {
            var paths = await _environment.PickAsync(new(FileSelectionKind.SaveFile, "Export storage report to a new CSV", [".csv"], "Nexus-storage-report.csv"));
            if (_released || paths.Count == 0) return;
            await Task.Run(() => StorageReport.ExportNew(result, paths[0]));
            if (!_released) _status.Text = "Report saved to " + paths[0] + ". Existing files are never overwritten.";
        }
        finally { _busy = false; }
    }

    private void UpdateActions()
    {
        bool ready = !_released && !_busy;
        _choose.IsEnabled = _analyze.IsEnabled = _root.IsEnabled = _limit.IsEnabled = _hidden.IsEnabled = ready;
        _cancel.IsEnabled = !_released && _running && _scan?.IsCancellationRequested != true;
        _section.IsEnabled = _filter.IsEnabled = _list.IsEnabled = ready && _result is not null;
        _export.IsEnabled = ready && _result is not null;
        _home.IsEnabled = ready && _result is not null;
        _up.IsEnabled = ready && _result is not null && !Same(_folder, _result.Root);
        _open.IsEnabled = ready && Selected?.OpenPath.Length > 0;
        _copy.IsEnabled = ready && Selected?.Path.Length > 0;
        _previous.IsEnabled = ready && _offset > 0; _next.IsEnabled = ready && _offset + PageSize < _count;
    }

    internal void ApplyAppearance()
    {
        RequestedTheme = _environment.Theme.ElementTheme;
        _status.Foreground = _location.Foreground = _pageLabel.Foreground = _environment.Theme.Brush("NexusMuted");
        foreach (var card in _cards)
        { card.Background = _environment.Theme.Brush("NexusPanel"); card.BorderBrush = _environment.Theme.Edge; card.CornerRadius = new CornerRadius(_environment.Theme.HighContrast ? 0 : 12); }
    }

    internal void Release()
    {
        if (_released) return;
        _released = true; _poll.Stop(); _scan?.Cancel(); _result = null; _progress = null; _list.Items.Clear();
    }
}
