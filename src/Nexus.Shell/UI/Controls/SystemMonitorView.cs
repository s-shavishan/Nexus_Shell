using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Nexus.Runtime.Monitoring;
using Nexus.Shell.Desktop;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;
using Windows.System;

namespace Nexus.Shell.UI.Controls;

internal sealed class SystemMonitorView : Grid
{
    private readonly DesktopEnvironment _environment;
    private readonly bool _validation;
    private readonly WindowsSystemMonitor? _source;
    private readonly SystemMonitorReader? _reader;
    private readonly SystemMonitorSampler _sampler = new();
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly ObservableCollection<SystemMonitorProcessRow> _rows = [];
    private readonly Dictionary<ProcessIdentity, SystemMonitorProcessRow> _retained = [];
    private readonly TextBox _search = new() { PlaceholderText = "Search name, PID or executable path", MaxLength = 160 };
    private readonly ComboBox _sort = new() { ItemsSource = new[] { "CPU · highest first", "Memory · highest first", "Name · A–Z", "PID · lowest first" }, SelectedIndex = 0, MinWidth = 180, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly CheckBox _mine = new() { Content = "My Windows session", IsChecked = true };
    private readonly ComboBox _page = new() { ItemsSource = new[] { "Processes", "Storage" }, SelectedIndex = 0, Width = 150 };
    private readonly TextBlock _cpu = new(), _memory = new(), _uptime = new(), _count = new(), _detail = new() { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
    private readonly TextBlock _status = new() { Text = "Activate this window to observe your PC.", FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly ListView _list = new() { SelectionMode = ListViewSelectionMode.Single, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly StackPanel _drives = new() { Spacing = 12 };
    private readonly Grid _processes = new() { RowSpacing = 10 }, _filters = new() { ColumnSpacing = 10, RowSpacing = 8 };
    private readonly ScrollViewer _storage;
    private readonly Button _pause, _refresh, _previous, _next, _close, _end, _location, _copy;
    private readonly List<Button> _buttons = [];
    private readonly List<Border> _cards = [];
    private CancellationTokenSource _readLife = new();
    private SystemMonitorSnapshot? _snapshot;
    private StorageSnapshot? _storageSnapshot;
    private long _generation, _lastSample, _lastStorage;
    private string _processNotice = "Waiting for process information.", _driveNotice = "Waiting for drive information.", _actionNotice = "";
    private long _actionAt;
    private const int PageSize = 200;
    private int _offset, _filteredCount;
    private bool _active, _paused, _released, _reading, _readingStorage, _busy, _rendering, _narrow;
    internal SystemMonitorView(DesktopEnvironment environment, bool validation = false)
    {
        _environment = environment; _validation = validation;
        if (!validation) { _source = new(); _reader = new(_source); }
        Padding = new Thickness(20, 8, 20, 18); RowSpacing = 12;
        RowDefinitions.Add(new() { Height = GridLength.Auto }); RowDefinitions.Add(new() { Height = GridLength.Auto });
        RowDefinitions.Add(new() { Height = GridLength.Auto }); RowDefinitions.Add(new()); RowDefinitions.Add(new() { Height = GridLength.Auto });
        Children.Add(new TextBlock { Text = "System Monitor", FontSize = 28, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var summary = new Grid { ColumnSpacing = 10 }; for (int i = 0; i < 3; i++) summary.ColumnDefinitions.Add(new());
        int column = 0;
        foreach (var item in new[] { ("CPU", _cpu), ("MEMORY", _memory), ("UPTIME", _uptime) })
        {
            var content = new StackPanel { Spacing = 5 }; content.Children.Add(new TextBlock { Text = item.Item1, FontSize = 11 });
            item.Item2.Text = "Waiting…"; item.Item2.FontSize = 15; item.Item2.TextWrapping = TextWrapping.Wrap; content.Children.Add(item.Item2);
            var card = Card(content); Grid.SetColumn(card, column++); summary.Children.Add(card);
        }
        Grid.SetRow(summary, 1); Children.Add(summary);
        var commands = new Grid { ColumnSpacing = 8 }; commands.ColumnDefinitions.Add(new()); commands.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); commands.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        commands.Children.Add(_page); _pause = Button("Pause", () => { _paused = !_paused; RestartObservation(); return Task.CompletedTask; });
        _refresh = Button("Refresh", async () => { if (_page.SelectedIndex == 1) await RefreshStorageAsync(true); else await RefreshAsync(); });
        Grid.SetColumn(_pause, 1); Grid.SetColumn(_refresh, 2); commands.Children.Add(_pause); commands.Children.Add(_refresh); Grid.SetRow(commands, 2); Children.Add(commands);
        _processes.RowDefinitions.Add(new() { Height = GridLength.Auto }); _processes.RowDefinitions.Add(new() { Height = GridLength.Auto }); _processes.RowDefinitions.Add(new()); _processes.RowDefinitions.Add(new() { Height = GridLength.Auto });
        _filters.Children.Add(_search); _filters.Children.Add(_sort); _filters.Children.Add(_mine); _processes.Children.Add(_filters);
        var paging = new Grid { ColumnSpacing = 8 }; paging.ColumnDefinitions.Add(new()); paging.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); paging.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        _previous = Button("Previous", () => { _offset = Math.Max(0, _offset - PageSize); RenderProcesses(); return Task.CompletedTask; });
        _next = Button("Next", () => { _offset += PageSize; RenderProcesses(); return Task.CompletedTask; });
        _count.FontSize = 12; _count.TextWrapping = TextWrapping.Wrap; paging.Children.Add(_count); Grid.SetColumn(_previous, 1); paging.Children.Add(_previous); Grid.SetColumn(_next, 2); paging.Children.Add(_next);
        Grid.SetRow(paging, 1); _processes.Children.Add(paging);
        _list.ItemsSource = _rows;
        _list.ItemTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <Grid ColumnSpacing="8" Padding="4,7" MinHeight="45">
                <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="64"/><ColumnDefinition Width="88"/><ColumnDefinition Width="56"/></Grid.ColumnDefinitions>
                <TextBlock Text="{Binding Name}" TextTrimming="CharacterEllipsis" VerticalAlignment="Center"/>
                <TextBlock Grid.Column="1" Text="{Binding Cpu}" FontSize="12" VerticalAlignment="Center" TextAlignment="Right"/>
                <TextBlock Grid.Column="2" Text="{Binding Memory}" FontSize="12" VerticalAlignment="Center" TextAlignment="Right"/>
                <TextBlock Grid.Column="3" Text="{Binding Pid}" FontSize="12" VerticalAlignment="Center" TextAlignment="Right"/>
              </Grid>
            </DataTemplate>
            """);
        var itemStyle = new Style(typeof(ListViewItem)); itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch)); _list.ItemContainerStyle = itemStyle;
        Grid.SetRow(_list, 2); _processes.Children.Add(_list);
        var selected = new StackPanel { Spacing = 8 }; _detail.FontSize = 12; _detail.MaxHeight = 96; selected.Children.Add(new ScrollViewer { Content = _detail, MaxHeight = 96, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var actions = new Grid { ColumnSpacing = 8, RowSpacing = 8 }; actions.ColumnDefinitions.Add(new()); actions.ColumnDefinitions.Add(new()); actions.RowDefinitions.Add(new()); actions.RowDefinitions.Add(new());
        _close = Button("Close windows", () => ActAsync(ProcessAction.CloseWindows)); _end = Button("End process…", () => ActAsync(ProcessAction.EndProcess));
        _location = Button("Open file location", () => { if (Selected is { } row) _environment.ShowFiles(Path.GetDirectoryName(row.Observation.ImagePath)); return Task.CompletedTask; });
        _copy = Button("Copy details", async () => { if (Selected is { } row) _status.Text = await _environment.CopyClipboardAsync(Details(row)) ? "Selected process details copied." : "The clipboard is unavailable. Try again."; });
        actions.Children.Add(_close); Grid.SetColumn(_end, 1); actions.Children.Add(_end); Grid.SetRow(_location, 1); actions.Children.Add(_location); Grid.SetRow(_copy, 1); Grid.SetColumn(_copy, 1); actions.Children.Add(_copy);
        selected.Children.Add(actions); var details = Card(selected); Grid.SetRow(details, 3); _processes.Children.Add(details);
        _storage = new ScrollViewer { Content = _drives, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Visibility = Visibility.Collapsed };
        Grid.SetRow(_processes, 3); Grid.SetRow(_storage, 3); Children.Add(_processes); Children.Add(_storage); Grid.SetRow(_status, 4); Children.Add(_status);
        AutomationProperties.SetName(_search, "Filter processes"); AutomationProperties.SetName(_sort, "Sort processes"); AutomationProperties.SetName(_mine, "Show only processes owned by my Windows session");
        AutomationProperties.SetName(_page, "System Monitor page"); AutomationProperties.SetName(_list, "Processes: name, CPU percent, working set and PID");
        AutomationProperties.SetLiveSetting(_status, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        void Refilter() { _offset = 0; RenderProcesses(); }
        _search.TextChanged += (_, _) => Refilter(); _sort.SelectionChanged += (_, _) => Refilter();
        _mine.Checked += (_, _) => Refilter(); _mine.Unchecked += (_, _) => Refilter(); _list.SelectionChanged += (_, _) => { if (!_rendering) UpdateActions(); };
        _page.SelectionChanged += (_, _) => { _processes.Visibility = _page.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed; _storage.Visibility = _page.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed; RestartObservation(); };
        _poll.Tick += async (_, _) => await Task.WhenAll(RefreshAsync(), _page.SelectedIndex == 1 ? RefreshStorageAsync() : Task.CompletedTask);
        var focus = new KeyboardAccelerator { Key = VirtualKey.F, Modifiers = VirtualKeyModifiers.Control }; focus.Invoked += (_, e) => { if (_page.SelectedIndex == 0) _search.Focus(FocusState.Keyboard); e.Handled = true; }; KeyboardAccelerators.Add(focus);
        SizeChanged += (_, _) => Layout(); Layout();
        if (validation) LoadFixture(); ApplyAppearance(); UpdateActions();
    }
    private MonitoredProcess? Selected => (_list.SelectedItem as SystemMonitorProcessRow)?.Value;
    private Border Card(UIElement child)
    { var border = new Border { Child = child, Padding = new Thickness(12), BorderThickness = new Thickness(1) }; _cards.Add(border); return border; }
    private Button Button(string name, Func<Task> action)
    {
        var button = new Button { Content = name, FontSize = 12, Padding = new Thickness(9, 7, 9, 7), HorizontalAlignment = HorizontalAlignment.Stretch, Style = (Style)Application.Current.Resources["AuraSurfaceButton"] };
        AutomationProperties.SetName(button, name); _buttons.Add(button);
        button.Click += async (_, _) => { if (_released || _busy || _validation) return; _busy = true; UpdateActions(); try { await action(); } catch (Exception error) { if (!_released) _status.Text = error.Message; } finally { _busy = false; if (!_released) UpdateActions(); } }; return button;
    }
    private void Layout()
    {
        bool narrow = ActualWidth < 680; if (_filters.ColumnDefinitions.Count > 0 && narrow == _narrow) return; _narrow = narrow;
        _filters.ColumnDefinitions.Clear(); _filters.RowDefinitions.Clear(); _filters.ColumnDefinitions.Add(new()); if (!narrow) _filters.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        for (int i = 0; i < (narrow ? 3 : 2); i++) _filters.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Grid.SetColumn(_sort, narrow ? 0 : 1); Grid.SetRow(_sort, narrow ? 1 : 0); Grid.SetColumnSpan(_mine, narrow ? 1 : 2); Grid.SetRow(_mine, narrow ? 2 : 1);
    }
    internal void SetActive(bool active)
    { if (_released || _validation || _active == active) return; _active = active; RestartObservation(); }
    private void RestartObservation()
    {
        if (_released || _validation) return; _generation++; _poll.Stop(); _readLife.Cancel(); _readLife.Dispose(); _readLife = new(); _sampler.Reset(); _lastSample = 0;
        _pause.Content = _paused ? "Resume" : "Pause";
        if (_active && !_paused) { _poll.Start(); _ = RefreshAsync(); if (_page.SelectedIndex == 1) _ = RefreshStorageAsync(true); }
        _status.Text = _paused ? "Updates paused. Resume to refresh process controls." : !_active ? "Updates paused while this window is inactive." : "Reading Windows…";
        UpdateActions();
    }
    private async Task RefreshAsync()
    {
        if (_released || _reading || !_active || _paused || _validation || _reader is null) return;
        _reading = true; long generation = _generation; CancellationToken token = _readLife.Token;
        try
        {
            var read = await _reader.ReadAsync(token); if (_released || generation != _generation) return;
            if (read is null) { _processNotice = "An earlier process read is still finishing."; RefreshStatus(); return; }
            _snapshot = _sampler.Sample(read); _lastSample = Stopwatch.GetTimestamp();
            _cpu.Text = _snapshot.CpuPercent is { } cpu ? $"{cpu:0.0}%" : "Sampling…";
            _memory.Text = _snapshot.TotalMemory > 0 ? $"{Size(_snapshot.TotalMemory - _snapshot.AvailableMemory)} / {Size(_snapshot.TotalMemory)}" : "Unavailable";
            _uptime.Text = $"{(int)_snapshot.Uptime.TotalDays}d {_snapshot.Uptime.Hours}h {_snapshot.Uptime.Minutes}m";
            RenderProcesses(); _processNotice = read.Notice.Length > 0 ? read.Notice : "Live · every 2 seconds · CPU uses total logical-processor capacity; memory is working set."; RefreshStatus();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { if (!_released && generation == _generation) { _lastSample = 0; _sampler.Reset(); _processNotice = "Process read failed: " + error.Message; RefreshStatus(); } }
        finally { _reading = false; if (!_released) UpdateActions(); }
    }
    private async Task RefreshStorageAsync(bool force = false)
    {
        if (_released || _readingStorage || !_active || _paused || _validation || _reader is null) return;
        if (!force && _lastStorage != 0 && Stopwatch.GetElapsedTime(_lastStorage).TotalSeconds < 15) return;
        _readingStorage = true; long generation = _generation; CancellationToken token = _readLife.Token;
        try
        {
            _driveNotice = "Reading local drive space…"; RefreshStatus();
            var read = await _reader.ReadStorageAsync(token); if (_released || generation != _generation) return;
            if (read is null) { _driveNotice = "An earlier drive read is still finishing. Previous drive values may be out of date."; RefreshStatus(); return; }
            _storageSnapshot = read; _lastStorage = Stopwatch.GetTimestamp(); RenderStorage();
            _driveNotice = "Drive space observed " + DateTime.Now.ToString("T") + ". Updates every 15 seconds; network capacity is not probed."; RefreshStatus();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { if (!_released && generation == _generation) { _driveNotice = "Drive read failed; previous values may be out of date: " + error.Message; RefreshStatus(); } }
        finally { _readingStorage = false; if (!_released) UpdateActions(); }
    }
    private void RenderProcesses()
    {
        if (_snapshot is null) return; var selected = Selected?.Identity;
        var filtered = SystemMonitorSampler.Select(_snapshot.Processes, _search.Text, _mine.IsChecked == true, (ProcessSort)_sort.SelectedIndex);
        _filteredCount = filtered.Length; _offset = Math.Clamp(_offset, 0, Math.Max(0, (_filteredCount - 1) / PageSize * PageSize));
        var current = filtered.Skip(_offset).Take(PageSize).ToArray();
        _rendering = true;
        try
        {
            var live = _snapshot.Processes.Select(p => p.Identity).ToHashSet(); foreach (var stale in _retained.Keys.Where(id => !live.Contains(id)).ToArray()) _retained.Remove(stale);
            var desired = new HashSet<SystemMonitorProcessRow>();
            foreach (var process in current) { if (!_retained.TryGetValue(process.Identity, out var row)) _retained[process.Identity] = row = new(process); else row.Update(process); desired.Add(row); }
            for (int i = _rows.Count - 1; i >= 0; i--) if (!desired.Contains(_rows[i])) _rows.RemoveAt(i);
            for (int i = 0; i < current.Length; i++)
            {
                var row = _retained[current[i].Identity]; if (i < _rows.Count && ReferenceEquals(_rows[i], row)) continue;
                int existing = _rows.IndexOf(row); if (existing >= 0) _rows.Move(existing, i); else _rows.Insert(i, row);
            }
            _list.SelectedItem = selected is { } id && _retained.TryGetValue(id, out var item) && desired.Contains(item) ? item : null;
            _count.Text = $"{(_filteredCount == 0 ? 0 : _offset + 1)}–{_offset + current.Length} of {_filteredCount} matches / {_snapshot.Processes.Length} processes\nName / CPU / Memory / PID";
        }
        finally { _rendering = false; }
        UpdateActions();
    }
    private void RenderStorage()
    {
        // Drive cards are few; release their registered actions/paint references
        // before rebuilding instead of retaining a history every 15 seconds.
        foreach (var card in _drives.Children.OfType<Border>()) _cards.Remove(card);
        foreach (var button in _drives.Children.OfType<Border>().Select(b => b.Child).OfType<StackPanel>().SelectMany(p => p.Children).OfType<Button>()) _buttons.Remove(button);
        _drives.Children.Clear(); if (_storageSnapshot is null) return;
        foreach (var volume in _storageSnapshot.Volumes)
        {
            var content = new StackPanel { Spacing = 8 }; content.Children.Add(new TextBlock { Text = volume.Root + " · " + volume.Kind, FontSize = 18 });
            content.Children.Add(new TextBlock { Text = volume.Capacity is { } capacity && volume.Available is { } free ? Size(free) + " available to you / " + Size(capacity) + " account capacity" : volume.Detail, TextWrapping = TextWrapping.Wrap });
            if (volume.AvailablePercent is { } percent) content.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = percent });
            content.Children.Add(new TextBlock { Text = volume.Capacity is not null ? "Available space · quotas may limit your account’s capacity." : "No capacity read succeeded.", FontSize = 11, TextWrapping = TextWrapping.Wrap });
            // Explicit user navigation is permitted even for a network drive;
            // background monitoring does not query its capacity.
            content.Children.Add(Button("Open " + volume.Root + " in Files", () => { _environment.ShowFiles(volume.Root); return Task.CompletedTask; }));
            _drives.Children.Add(Card(content));
        }
        if (_storageSnapshot.Volumes.Length == 0) _drives.Children.Add(new TextBlock { Text = "No drive letters were reported by Windows." });
        PaintCards();
    }
    private async Task ActAsync(ProcessAction action)
    {
        if (Selected is not { } selected || _source is null || !_active || _paused) return;
        if (_lastSample == 0 || Stopwatch.GetElapsedTime(_lastSample).TotalSeconds > 6)
        { _status.Text = "Refresh the process list before requesting this action."; return; }
        if (ProcessActionRules.Refusal(selected.Observation, Environment.ProcessId) is { Length: > 0 } refusal) { _status.Text = refusal; return; }
        if (action == ProcessAction.EndProcess)
        {
            var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "End " + selected.Observation.Name + "?", Content = $"PID {selected.Identity.Id}. Unsaved work in this process will be lost. Only this process will be ended; child processes are not included.", PrimaryButtonText = "End process", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close, RequestedTheme = RequestedTheme };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || _released) return;
        }
        var result = await Task.Run(() => ProcessActions.Execute(_source, selected.Identity, action, Environment.ProcessId));
        if (!_released) { _actionNotice = result.Detail; _actionAt = Stopwatch.GetTimestamp(); RefreshStatus(); _lastSample = 0; UpdateActions(); }
    }
    private void RefreshStatus()
    {
        if (_released || !_active || _paused) return;
        _status.Text = (_actionAt != 0 && Stopwatch.GetElapsedTime(_actionAt).TotalSeconds < 10 ? _actionNotice + "\n" : "")
            + _processNotice + (_page.SelectedIndex == 1 ? "\n" + _driveNotice : "");
    }
    private void UpdateActions()
    {
        if (_end is null) return;
        bool can = !_validation && !_released && !_busy; foreach (var button in _buttons) button.IsEnabled = can;
        bool fresh = _validation || _lastSample != 0 && Stopwatch.GetElapsedTime(_lastSample).TotalSeconds <= 6;
        var selected = Selected; string reason = selected is null ? "Select a process to inspect its details." : ProcessActionRules.Refusal(selected.Observation, Environment.ProcessId);
        _detail.Text = selected is null ? reason : Details(selected) + (reason.Length > 0 ? "\n" + reason : "");
        _close.IsEnabled = _end.IsEnabled = can && fresh && _active && !_paused && selected is not null && reason.Length == 0;
        _location.IsEnabled = can && selected is not null && Path.IsPathFullyQualified(selected.Observation.ImagePath);
        _copy.IsEnabled = can && selected is not null;
        _previous.IsEnabled = can && _offset > 0; _next.IsEnabled = can && _offset + PageSize < _filteredCount;
        _search.IsEnabled = _sort.IsEnabled = _mine.IsEnabled = _page.IsEnabled = !_validation && !_busy;
        _refresh.IsEnabled = can && _active && !_paused && !(_page.SelectedIndex == 1 ? _readingStorage : _reading);
    }
    private static string Details(MonitoredProcess row)
    {
        var p = row.Observation;
        return $"{p.Name} · PID {p.Identity.Id} · {(p.SameUser && p.SameSession ? "your session" : "other or unavailable owner")}\n"
            + $"Working set: {(p.WorkingSet is { } memory ? Size(memory) : "unavailable")} · Private committed: {(p.PrivateBytes is { } committed ? Size(committed) : "unavailable")}\n"
            + (p.ImagePath.Length > 0 ? p.ImagePath : p.Unavailable.Length > 0 ? p.Unavailable : "Executable path unavailable.");
    }
    private static string Size(ulong value) => value >= 1073741824 ? $"{value / 1073741824d:0.0} GB" : value >= 1048576 ? $"{value / 1048576d:0.0} MB" : $"{value / 1024d:0} KB";
    private void PaintCards()
    { foreach (var card in _cards) { card.Background = _environment.Theme.Brush("NexusPanel"); card.BorderBrush = _environment.Theme.Edge; card.CornerRadius = new CornerRadius(_environment.Theme.HighContrast ? 0 : 12); } }
    internal void ApplyAppearance()
    { RequestedTheme = _environment.Theme.ElementTheme; _status.Foreground = _count.Foreground = _environment.Theme.Brush("NexusMuted"); PaintCards(); }
    internal void Release()
    { if (_released) return; _released = true; _generation++; _poll.Stop(); _readLife.Cancel(); _readLife.Dispose(); _sampler.Reset(); _rows.Clear(); _retained.Clear(); _snapshot = null; _storageSnapshot = null; }
    private void LoadFixture()
    {
        _snapshot = new(32.5, 16UL * 1073741824, 6UL * 1073741824, TimeSpan.FromHours(26),
            [new(new(new(9001, 12345), "Example app.exe", @"C:\Example\Example app.exe", 100, 1, 48UL * 1048576, 64UL * 1048576, true, true, false), 12.5)], "Isolated UI fixture");
        _cpu.Text = "32.5%"; _memory.Text = "10.0 GB / 16.0 GB"; _uptime.Text = "1d 2h 0m"; RenderProcesses(); _list.SelectedIndex = 0;
        _storageSnapshot = new([new(@"C:\", "Local drive", 500UL * 1073741824, 120UL * 1073741824, 120UL * 1073741824, "Fixture"), new(@"E:\", "Removable", null, null, null, "No ready media.")]); RenderStorage();
    }
    internal void VerifyValidationLayout(bool narrow, bool storage = false)
    {
        if (!_validation || _source is not null || _reader is not null || _poll.IsEnabled || _rows.Count != 1 || (!storage && (_list.ActualWidth <= 0 || _list.ActualHeight <= 0)) || _narrow != narrow)
            throw new InvalidOperationException("System Monitor must lay out its real list with an isolated, non-polling fixture.");
        if (!storage)
        {
            bool Bound(DependencyObject parent, string expected)
            {
                if (parent is TextBlock text && text.Text == expected) return true;
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) if (Bound(VisualTreeHelper.GetChild(parent, i), expected)) return true;
                return false;
            }
            if (!Bound(_list, "Example app.exe") || !Bound(_list, "9001") || !Bound(_list, _rows[0].Cpu) || !Bound(_list, _rows[0].Memory))
                throw new InvalidOperationException("The real virtualized row did not bind process name, CPU, memory and PID text.");
        }
        if (_buttons.Any(button => button.IsEnabled)) throw new InvalidOperationException("System Monitor validation must disable every command.");
        if (storage && (_drives.Children.Count != 2 || _storage.ActualWidth <= 0 || _storage.ActualHeight <= 0)) throw new InvalidOperationException("Storage fixture lost the ready and empty-media cards or did not lay out.");
    }
    internal void ValidationPage(bool storage) { if (!_validation) throw new InvalidOperationException(); _page.SelectedIndex = storage ? 1 : 0; }
}

// Public runtime binding surface for the DataTemplate loaded from source.
[Microsoft.UI.Xaml.Data.Bindable]
public sealed class SystemMonitorProcessRow : INotifyPropertyChanged
{
    internal SystemMonitorProcessRow(MonitoredProcess value) => Value = value;
    internal MonitoredProcess Value { get; private set; }
    public string Name => Value.Observation.Name;
    public string Cpu => Value.CpuPercent is { } cpu ? $"{cpu:0.0}%" : "—";
    public string Memory => Value.Observation.WorkingSet is { } bytes ? (bytes >= 1073741824 ? $"{bytes / 1073741824d:0.0} GB" : $"{bytes / 1048576d:0.0} MB") : "—";
    public string Pid => Value.Identity.Id.ToString();
    public event PropertyChangedEventHandler? PropertyChanged;
    internal void Update(MonitoredProcess next)
    {
        string name = Name, cpu = Cpu, memory = Memory; Value = next;
        if (name != Name) PropertyChanged?.Invoke(this, new(nameof(Name)));
        if (cpu != Cpu) PropertyChanged?.Invoke(this, new(nameof(Cpu)));
        if (memory != Memory) PropertyChanged?.Invoke(this, new(nameof(Memory)));
    }
}
