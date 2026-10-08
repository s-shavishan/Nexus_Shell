using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Nexus.Shell.Interop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    private AudioController? _audio;
    private AudioSnapshot _audioSnapshot = AudioSnapshot.Unavailable("Choose Refresh to read Windows audio.");
    private readonly WindowLayouts _windowLayouts = new();
    private readonly DispatcherTimer _audioWriteTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly Dictionary<string, AudioChange> _audioChanges = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AudioChange> _inFlightAudioChanges = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Slider Slider, TextBlock Title, Button Mute)> _mixerRows = new(StringComparer.Ordinal);
    private readonly List<ComboBox> _windowChoices = [];
    private TextBlock? _pcCpu, _pcMemory, _pcNetwork, _pcPower, _pcStorage, _pcUptime, _pcAudioDevice;
    private Slider? _pcVolume;
    private TextBlock? _pcVolumeTitle;
    private Button? _pcMute, _undoLayout;
    private StackPanel? _mixerPanel;
    private ComboBox? _layoutChoice;
    private CheckBox? _showArrangedApps;
    private CpuTimes? _pcCpuTimes;
    private IReadOnlyList<RunningWindow> _pcWindows = [];
    private bool _syncingAudio, _refreshingPc, _writingAudio, _initializedWindowChoices, _arranging;
    private Task<AudioSnapshot>? _pendingAudioRead;
    private long _pcEpoch;
    private sealed record WindowChoice(string Label, RunningWindow? Window);
    private bool PcVisible => _page == "PC controls" || _controlsOpen;

    private void BuildPcControls()
    {
        ++_pcEpoch; PcContent.Children.Clear(); _mixerRows.Clear(); _windowChoices.Clear(); _pcWindows = []; _initializedWindowChoices = false;
        var vitals = new StackPanel { Spacing = 10 };
        vitals.Children.Add(Text("Your PC, at a glance", 20));
        vitals.Children.Add(Text(Environment.MachineName + " · " + Environment.ProcessorCount + " logical processors", 12, true));
        _pcCpu = Text("CPU · collecting the first sample", 17);
        _pcMemory = Text("Memory · reading…", 14); _pcUptime = Text("", 12, true);
        vitals.Children.Add(_pcCpu); vitals.Children.Add(_pcMemory); vitals.Children.Add(_pcUptime);
        _pcPower = Text("", 12, true); _pcNetwork = Text("", 12, true); _pcStorage = Text("", 12, true);
        vitals.Children.Add(_pcPower); vitals.Children.Add(_pcNetwork); vitals.Children.Add(_pcStorage);
        vitals.Children.Add(Text("Network shows the link and local address, not an internet connectivity test.", 11, true));
        vitals.Children.Add(ActionButton("Lock PC", () => { if (!WindowLayouts.Lock()) ShowStatus("Windows could not lock this session.", true); }));
        AddPcCard(vitals, 0, 0);

        var output = new StackPanel { Spacing = 12 };
        output.Children.Add(Text("Sound", 20)); _pcAudioDevice = Text("Reading the default audio output…", 12, true); output.Children.Add(_pcAudioDevice);
        var master = AudioRow("", "Output volume"); _pcVolume = master.Slider; _pcMute = master.Mute; _pcVolumeTitle = master.Title;
        output.Children.Add(master.Panel);
        output.Children.Add(Text("App audio sessions", 15));
        output.Children.Add(Text("Apps appear here after they create audio on this output. Each session keeps its own level.", 11, true));
        _mixerPanel = new StackPanel { Spacing = 12 };
        output.Children.Add(new ScrollViewer { Content = _mixerPanel, MaxHeight = 300,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        AddPcCard(output, 0, 1);

        var arrangement = new StackPanel { Spacing = 12 };
        arrangement.Children.Add(Text("Arrange your windows", 20));
        arrangement.Children.Add(Text("Select one to four windows. The layout uses the first window’s display and leaves the Windows taskbar clear.", 12, true));
        for (int i = 0; i < 4; i++)
        {
            var choice = new ComboBox { Header = "Window " + (i + 1), DisplayMemberPath = "Label", HorizontalAlignment = HorizontalAlignment.Stretch };
            choice.Items.Add(new WindowChoice("Leave this slot empty", null)); choice.SelectedIndex = 0;
            _windowChoices.Add(choice); arrangement.Children.Add(choice);
        }
        _layoutChoice = new ComboBox { Header = "Layout", HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var name in new[] { "Side by side", "Stack vertically", "Grid" }) _layoutChoice.Items.Add(name);
        _layoutChoice.SelectedIndex = 0; arrangement.Children.Add(_layoutChoice);
        _showArrangedApps = new CheckBox { Content = "Minimize Nexus to show the arranged apps", IsChecked = true }; arrangement.Children.Add(_showArrangedApps);
        arrangement.Children.Add(AsyncButton("Arrange selected windows", ArrangeWindowsAsync));
        _undoLayout = new Button { Content = "Undo last arrangement", Style = (Style)Application.Current.Resources["AuraSurfaceButton"] };
        _undoLayout.Click += async (_, _) =>
        {
            _undoLayout.IsEnabled = false;
            try { await UndoWindowsAsync(); } catch (Exception ex) { if (_ready) Error("Could not restore the windows", ex); }
            finally { if (_ready) _undoLayout.IsEnabled = _windowLayouts.CanUndo; }
        };
        _undoLayout.IsEnabled = _windowLayouts.CanUndo;
        arrangement.Children.Add(_undoLayout); arrangement.Children.Add(ActionButton("Window overview", () => Navigate("Running apps")));
        AddPcCard(arrangement, 1, 0);

        var shell = new StackPanel { Spacing = 14 };
        shell.Children.Add(Text("Make space for your work", 20));
        shell.Children.Add(Text("Keep your apps in a layout, tune their audio and return to your study session from the same surface.", 12, true));
        shell.Children.Add(ActionButton("Study & focus timer", () => Navigate("Study")));
        shell.Children.Add(ActionButton("Workspaces", () => Navigate("Workspaces")));
        shell.Children.Add(ActionButton("Personalize Nexus", () => Navigate("Personalize")));
        shell.Children.Add(ActionButton("Advanced Windows settings", () => AppCatalog.OpenSettings("ms-settings:")));
        shell.Children.Add(Text("Live data refreshes every five seconds while this view is active. Audio changes are applied only when you use a slider or mute button.", 11, true));
        AddPcCard(shell, 1, 1);
        ApplyAudioSnapshot(_audioSnapshot); UpdatePcLayout(); _ = RefreshPcAsync();
        PageStatus.Text = "Live PC controls · Ctrl+5 · audio, windows and system status";
    }
    private void AddPcCard(StackPanel content, int row, int column)
    {
        var card = Card(content); card.Padding = new Thickness(18); card.CornerRadius = new CornerRadius(18);
        card.VerticalAlignment = VerticalAlignment.Top; card.Tag = (row, column);
        PcContent.Children.Add(card);
    }
    private void UpdatePcLayout()
    {
        if (!_ready) return;
        bool wide = PageHost.ActualWidth >= 780;
        PcContent.ColumnDefinitions.Clear(); PcContent.RowDefinitions.Clear();
        PcContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (wide) PcContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < (wide ? 2 : 4); i++) PcContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        foreach (var card in PcContent.Children.OfType<FrameworkElement>())
        { var (row, column) = ((int, int))card.Tag; Grid.SetRow(card, wide ? row : row * 2 + column); Grid.SetColumn(card, wide ? column : 0); }
    }
    private (StackPanel Panel, Slider Slider, TextBlock Title, Button Mute) AudioRow(string id, string name)
    {
        var panel = new StackPanel { Spacing = 5 };
        var title = Text(name, 12); panel.Children.Add(title);
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var slider = new Slider { Minimum = 0, Maximum = 100, StepFrequency = 1, IsEnabled = false, Tag = id };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(slider, name + " volume");
        slider.ValueChanged += (_, _) =>
        {
            if (_syncingAudio) return;
            title.Text = name + " · " + slider.Value.ToString("0") + "%";
            QueueAudioChange(id, (float)(slider.Value / 100), null);
        };
        var mute = ActionButton("Mute", () => ToggleAudioMute(id)); mute.IsEnabled = false; Grid.SetColumn(mute, 1);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(mute, name + " mute");
        row.Children.Add(slider); row.Children.Add(mute); panel.Children.Add(row);
        return (panel, slider, title, mute);
    }
    private void QueueAudioChange(string id, float? volume, bool? muted)
    {
        if (!_ready || _syncingAudio || !_audioSnapshot.Available) return;
        _audioChanges.TryGetValue(id, out var before);
        _audioChanges[id] = new(id, volume ?? before?.Volume, muted ?? before?.Muted);
        if (muted is bool state)
        {
            if (id.Length == 0) { QuickMuteButton.Content = state ? "Unmute" : "Mute"; if (_pcMute is not null) _pcMute.Content = state ? "Unmute" : "Mute"; }
            else if (_mixerRows.TryGetValue(id, out var row)) row.Mute.Content = state ? "Unmute" : "Mute";
        }
        _audioWriteTimer.Start();
    }
    private void ToggleAudioMute(string id)
    {
        bool current = id.Length == 0 ? _audioSnapshot.Muted : _audioSnapshot.Apps.FirstOrDefault(a => a.Id == id)?.Muted ?? false;
        if (_inFlightAudioChanges.TryGetValue(id, out var writing) && writing.Muted is bool writtenState) current = writtenState;
        if (_audioChanges.TryGetValue(id, out var queued) && queued.Muted is bool state) current = state;
        QueueAudioChange(id, null, !current);
    }
    private async void AudioWrite_Tick(object? sender, object args)
    {
        if (_writingAudio) return;
        _audioWriteTimer.Stop(); if (!_ready || _audioChanges.Count == 0) return;
        var changes = _audioChanges.Values.ToArray(); _audioChanges.Clear(); _writingAudio = true;
        _inFlightAudioChanges.Clear(); foreach (var change in changes) _inFlightAudioChanges[change.Id] = change;
        try
        {
            _audio ??= new AudioController();
            var snapshot = await _audio.ChangeAsync(_audioSnapshot.DeviceId, changes).WaitAsync(TimeSpan.FromSeconds(5));
            if (_ready && _audioChanges.Count == 0) ApplyAudioSnapshot(snapshot);
        }
        catch (Exception ex)
        {
            if (_ready)
            {
                if (ex is TimeoutException) { _audioChanges.Clear(); ApplyAudioSnapshot(AudioSnapshot.Unavailable("Windows audio is not responding. Try Refresh.")); }
                Error("Could not change audio", ex);
            }
        }
        finally
        {
            _writingAudio = false;
            _inFlightAudioChanges.Clear();
            if (_ready && _audioChanges.Count > 0) _audioWriteTimer.Start();
            else if (_ready && PcVisible) _ = RefreshPcAsync();
            if (_ready && (!PcVisible || !_isActive)) _ = SuspendAudioAsync();
        }
    }
    private void ApplyAudioSnapshot(AudioSnapshot snapshot)
    {
        _audioSnapshot = snapshot; _syncingAudio = true;
        try
        {
            QuickAudioDevice.Text = snapshot.Available ? snapshot.Device : snapshot.Message;
            QuickMasterSlider.IsEnabled = QuickMuteButton.IsEnabled = snapshot.Available;
            QuickMasterSlider.Value = snapshot.Volume * 100; QuickVolumeText.Text = snapshot.Available ? $"Output · {snapshot.Volume * 100:0}%" : "Audio unavailable";
            QuickMuteButton.Content = snapshot.Muted ? "Unmute" : "Mute";
            if (_pcAudioDevice is not null) _pcAudioDevice.Text = snapshot.Available ? snapshot.Device : snapshot.Message;
            if (_pcVolume is not null && _pcMute is not null && _pcVolumeTitle is not null)
            {
                _pcVolume.IsEnabled = _pcMute.IsEnabled = snapshot.Available; _pcVolume.Value = snapshot.Volume * 100;
                _pcMute.Content = snapshot.Muted ? "Unmute" : "Mute";
                _pcVolumeTitle.Text = snapshot.Available ? $"Output volume · {snapshot.Volume * 100:0}%" : "Output unavailable";
            }
            if (_mixerPanel is not null && _page == "PC controls")
            {
                if (!_mixerRows.Keys.Order().SequenceEqual(snapshot.Apps.Select(a => a.Id).Order()))
                {
                    _mixerPanel.Children.Clear(); _mixerRows.Clear();
                    foreach (var app in snapshot.Apps)
                    {
                        var row = AudioRow(app.Id, app.Name); _mixerPanel.Children.Add(row.Panel);
                        _mixerRows.Add(app.Id, (row.Slider, row.Title, row.Mute));
                    }
                }
                if (_mixerRows.Count == 0 && _mixerPanel.Children.Count == 0)
                    _mixerPanel.Children.Add(Text("Play audio in an app, then refresh to see its session.", 12, true));
                foreach (var app in snapshot.Apps)
                {
                    var row = _mixerRows[app.Id]; row.Slider.Value = app.Volume * 100; row.Slider.IsEnabled = row.Mute.IsEnabled = true;
                    row.Title.Text = app.Name + $" · {app.Volume * 100:0}%" + (app.Muted ? " · muted" : ""); row.Mute.Content = app.Muted ? "Unmute" : "Mute";
                }
            }
        }
        finally { _syncingAudio = false; }
    }
    private async Task RefreshPcAsync()
    {
        if (!_ready || !_isActive || !PcVisible || _refreshingPc) return;
        _refreshingPc = true; long epoch = _pcEpoch;
        try
        {
            if (!_writingAudio && _audioChanges.Count == 0)
            {
                try
                {
                    _audio ??= new AudioController();
                    // Reuse an unfinished read after a timeout instead of filling the
                    // worker queue if a Windows audio driver stops responding.
                    if (_pendingAudioRead is null || _pendingAudioRead.IsCompleted) _pendingAudioRead = _audio.ReadAsync();
                    var snapshot = await _pendingAudioRead.WaitAsync(TimeSpan.FromSeconds(5));
                    if (_ready && _isActive && PcVisible && !_writingAudio && _audioChanges.Count == 0) ApplyAudioSnapshot(snapshot);
                }
                catch (Exception ex)
                {
                    Log.Write("Audio refresh unavailable", ex);
                    if (_ready && _isActive && PcVisible) ApplyAudioSnapshot(AudioSnapshot.Unavailable("Windows audio is not responding. Try Refresh."));
                }
            }
            if (!_ready || !_isActive || _page != "PC controls") return;
            var data = await Task.Run(() => (PcMetrics.Read(ref _pcCpuTimes), NativeMethods.RunningWindows(_handle)));
            if (!_ready || !_isActive || _page != "PC controls" || epoch != _pcEpoch) return;
            var (pc, windows) = data;
            _pcCpu!.Text = pc.Cpu is double cpu ? $"CPU · {cpu:0}%" : "CPU · collecting the first sample";
            _pcMemory!.Text = pc.TotalMemory > 0 ? "Memory · " + PcMetrics.Size(pc.TotalMemory - Math.Min(pc.AvailableMemory, pc.TotalMemory)) + " used of " + PcMetrics.Size(pc.TotalMemory) : "Memory information unavailable";
            _pcPower!.Text = pc.Power; _pcNetwork!.Text = pc.Network + (pc.Address.Length > 0 ? "\n" + pc.Address : "");
            _pcStorage!.Text = pc.Storage; _pcUptime!.Text = $"Windows uptime · {(int)pc.Uptime.TotalDays}d {pc.Uptime.Hours}h {pc.Uptime.Minutes}m";
            if (!_windowChoices.Any(c => c.IsDropDownOpen) && !windows.SequenceEqual(_pcWindows)) UpdateWindowChoices(windows);
        }
        catch (Exception ex) { if (_ready && PcVisible) Error("Could not refresh PC controls", ex); }
        finally { _refreshingPc = false; }
    }
    private void UpdateWindowChoices(IReadOnlyList<RunningWindow> windows)
    {
        _pcWindows = windows;
        for (int i = 0; i < _windowChoices.Count; i++)
        {
            var choice = _windowChoices[i]; var selected = (choice.SelectedItem as WindowChoice)?.Window;
            var entries = new[] { new WindowChoice("Leave this slot empty", null) }.Concat(windows.Select(w => new WindowChoice(w.Title, w))).ToArray();
            if (choice.ItemsSource is null) choice.Items.Clear();
            choice.ItemsSource = entries;
            choice.SelectedItem = entries.FirstOrDefault(e => e.Window is not null && selected is not null && e.Window.Handle == selected.Handle && e.Window.ProcessId == selected.ProcessId)
                ?? entries[!_initializedWindowChoices && i < 2 && i < windows.Count ? i + 1 : 0];
        }
        if (windows.Count > 0) _initializedWindowChoices = true;
    }
    private async Task ArrangeWindowsAsync()
    {
        if (_arranging) return;
        var windows = _windowChoices.Select(c => (c.SelectedItem as WindowChoice)?.Window).Where(w => w is not null).Cast<RunningWindow>().ToArray();
        string layout = _layoutChoice?.SelectedIndex == 1 ? "Stack" : _layoutChoice?.SelectedIndex == 2 ? "Grid" : "Columns";
        bool showApps = _showArrangedApps?.IsChecked == true; _arranging = true;
        try
        {
            int count = await Task.Run(() => _windowLayouts.Arrange(windows, layout));
            if (!_ready) return; _undoLayout!.IsEnabled = _windowLayouts.CanUndo;
            ShowStatus("Arranged " + count + " windows. Undo is available in PC controls."); if (showApps && _page == "PC controls") Minimize();
        }
        finally { _arranging = false; }
    }
    private async Task UndoWindowsAsync()
    {
        if (_arranging) return; _arranging = true;
        try { int count = await Task.Run(_windowLayouts.Undo); if (_ready) { _undoLayout!.IsEnabled = _windowLayouts.CanUndo; ShowStatus("Restored " + count + " windows; unavailable windows were skipped."); } }
        finally { _arranging = false; }
    }
    private async Task SuspendAudioAsync()
    {
        if (_audio is null || _writingAudio || _audioChanges.Count > 0 || PcVisible && _isActive) return;
        try { await _audio.SuspendAsync(); } catch (Exception ex) { Log.Write("Audio controls released", ex); }
    }
    private void PcControls_Click(object sender, RoutedEventArgs args) => Navigate("PC controls");
    private async void PcRefresh_Click(object sender, RoutedEventArgs args) => await RefreshPcAsync();
    private void QuickVolume_Changed(object sender, RangeBaseValueChangedEventArgs args)
    { if (_ready && !_syncingAudio) { QuickVolumeText.Text = $"Output · {args.NewValue:0}%"; QueueAudioChange("", (float)(args.NewValue / 100), null); } }
    private void QuickMute_Click(object sender, RoutedEventArgs args) => ToggleAudioMute("");
}
