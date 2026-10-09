using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Desktop;
using Nexus.Shell.Services;
using Windows.System;

namespace Nexus.Shell.UI.Controls;

internal sealed class QuickSettingsView : Grid, IDisposable
{
    private readonly DesktopEnvironment _environment;
    private readonly AudioController _audio = new();
    private readonly BrightnessController _display;
    private readonly Slider _volume = new() { Minimum = 0, Maximum = 100, StepFrequency = 1, IsEnabled = false };
    private readonly Slider _brightness = new() { Minimum = 0, Maximum = 100, StepFrequency = 1, IsEnabled = false };
    private readonly Button _mute = new() { Content = "Mute", IsEnabled = false };
    private readonly TextBlock _audioMessage, _displayMessage, _profileMessage, _power, _network;
    private readonly ComboBox _profile = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ToggleSwitch _compact = new() { Header = "Compact taskbar", OnContent = "On", OffContent = "Off" };
    private readonly List<(TextBlock Label, bool Muted)> _labels = [];
    private readonly List<Border> _cards = [];
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer _writes = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private AudioSnapshot _audioState = AudioSnapshot.Unavailable("Reading audio output…");
    private BrightnessSnapshot _displayState = BrightnessSnapshot.Unavailable("Checking display support…");
    private Task<AudioSnapshot>? _audioTask;
    private Task<BrightnessSnapshot>? _displayTask;
    private Task<PcSnapshot>? _statusTask;
    private CpuTimes? _cpu;
    private float? _queuedVolume;
    private bool? _queuedMute;
    private double? _queuedBrightness;
    private float? _writingVolume;
    private bool? _writingMute;
    private double? _writingBrightness;
    private string _queuedAudioDevice = "", _queuedDisplayDevice = "";
    private bool _open, _disposed, _syncing, _audioBusy, _displayBusy, _statusBusy;

    internal QuickSettingsView(DesktopEnvironment environment, Action close)
    {
        _environment = environment; _display = new(environment.Taskbar.Handle);
        var body = new StackPanel { Spacing = 12, Margin = new Thickness(18) };
        var header = new Grid { ColumnSpacing = 10 };
        header.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var title = Label("Quick settings", 23); title.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        header.Children.Add(title);
        var dismiss = Button("Close", close, true); Grid.SetColumn(dismiss, 1); header.Children.Add(dismiss); body.Children.Add(header);

        var audio = new StackPanel { Spacing = 6 };
        audio.Children.Add(Label("Sound", 15));
        _audioMessage = Label(_audioState.Message, 12, true); audio.Children.Add(_audioMessage);
        AutomationProperties.SetName(_volume, "Output volume"); audio.Children.Add(_volume);
        _mute.Style = (Style)Application.Current.Resources["AuraSurfaceButton"]; audio.Children.Add(_mute);
        body.Children.Add(Card(audio));

        var display = new StackPanel { Spacing = 6 }; display.Children.Add(Label("Brightness", 15));
        _displayMessage = Label(_displayState.Message, 12, true); display.Children.Add(_displayMessage);
        AutomationProperties.SetName(_brightness, "Display brightness"); display.Children.Add(_brightness); body.Children.Add(Card(display));

        var visuals = new StackPanel { Spacing = 8 }; visuals.Children.Add(Label("Desktop performance", 15));
        foreach (string name in new[] { "Fast", "Balanced", "Full" }) _profile.Items.Add(name);
        AutomationProperties.SetName(_profile, "Desktop visual quality"); visuals.Children.Add(_profile);
        _profileMessage = Label("", 12, true); visuals.Children.Add(_profileMessage); visuals.Children.Add(_compact); body.Children.Add(Card(visuals));

        var status = new StackPanel { Spacing = 4 };
        _power = Label("Reading power status…", 12, true); _network = Label("Reading network link…", 12, true);
        status.Children.Add(_power); status.Children.Add(_network); body.Children.Add(status);
        var actions = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
        actions.ColumnDefinitions.Add(new()); actions.ColumnDefinitions.Add(new()); actions.RowDefinitions.Add(new()); actions.RowDefinitions.Add(new());
        void AddAction(string name, Action action, int row, int column)
        { var button = Button(name, action); button.HorizontalAlignment = HorizontalAlignment.Stretch; Grid.SetRow(button, row); Grid.SetColumn(button, column); actions.Children.Add(button); }
        AddAction("Settings", () => environment.OpenTarget("ms-settings:"), 0, 0);
        AddAction("Control Panel", () => environment.OpenTarget("control.exe"), 0, 1);
        AddAction("Task Manager", () => environment.OpenTarget("taskmgr.exe"), 1, 0);
        AddAction("More controls", () => environment.ShowSections("PC controls"), 1, 1);
        body.Children.Add(actions);
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        footer.Children.Add(Button("Refresh", Refresh));
        footer.Children.Add(Button(environment.IsManagedDesktop ? "Return to Windows" : "Exit Nexus", environment.RequestExit)); body.Children.Add(footer);
        Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });

        _volume.ValueChanged += (_, args) =>
        {
            if (_syncing || !_open || !_audioState.Available) return;
            _queuedAudioDevice = _audioState.DeviceId; _queuedVolume = (float)(args.NewValue / 100); ShowAudio(); ScheduleWrites();
        };
        _mute.Click += (_, _) =>
        {
            if (!_open || !_audioState.Available) return;
            _queuedAudioDevice = _audioState.DeviceId; _queuedMute = !(_queuedMute ?? _writingMute ?? _audioState.Muted); ShowAudio(); ScheduleWrites();
        };
        _brightness.ValueChanged += (_, args) =>
        {
            if (_syncing || !_open || !_displayState.Available) return;
            _queuedDisplayDevice = _displayState.DeviceId; _queuedBrightness = args.NewValue; ShowDisplay(); ScheduleWrites();
        };
        _profile.SelectionChanged += (_, _) =>
        {
            if (_syncing || _profile.SelectedIndex < 0) return;
            DesktopVisuals.Apply(environment.Session.State, (DesktopVisualProfile)_profile.SelectedIndex); environment.SaveState(); RefreshPreferences();
        };
        _compact.Toggled += (_, _) => { if (!_syncing) { environment.Session.State.CompactDock = _compact.IsOn; environment.SaveState(); } };
        KeyDown += (_, args) => { if (args.Key == VirtualKey.Escape) { close(); args.Handled = true; } };
        _writes.Tick += (_, _) => { _writes.Stop(); _ = UpdateAudioAsync(false); _ = UpdateDisplayAsync(false); };
        _poll.Tick += (_, _) => { _ = UpdateAudioAsync(true); if (_displayTask is not null) _ = UpdateDisplayAsync(false); _ = UpdateStatusAsync(); };
        ApplyAppearance(); RefreshPreferences();
    }
    private TextBlock Label(string text, double size, bool muted = false)
    { var label = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap }; _labels.Add((label, muted)); return label; }
    private Border Card(UIElement child)
    { var card = new Border { Child = child, Padding = new Thickness(14, 12, 14, 12), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1) }; _cards.Add(card); return card; }
    private static Button Button(string name, Action action, bool quiet = false)
    {
        var button = new Button { Content = name, Style = (Style)Application.Current.Resources[quiet ? "QuietButton" : "AuraSurfaceButton"], Padding = new Thickness(10, 7, 10, 7) };
        AutomationProperties.SetName(button, name); button.Click += (_, _) => action(); return button;
    }
    internal void ApplyAppearance()
    {
        RequestedTheme = _environment.Theme.ElementTheme;
        foreach (var (label, muted) in _labels) label.Foreground = _environment.Theme.Brush(muted ? "NexusMuted" : "NexusText");
        foreach (var card in _cards) { card.Background = _environment.Theme.Brush("NexusCard"); card.BorderBrush = _environment.Theme.Brush("NexusBorder"); }
        RefreshPreferences();
    }
    internal void RefreshPreferences()
    {
        _syncing = true;
        try
        {
            var profile = DesktopVisuals.Read(_environment.Session.State);
            _profile.SelectedIndex = (int)profile; _compact.IsOn = _environment.Session.State.CompactDock;
            _profileMessage.Text = profile switch
            {
                DesktopVisualProfile.Fast => "Best for VMs · simple background, no glass or motion.",
                DesktopVisualProfile.Balanced => "Cached wallpaper and motion, with glass turned off.",
                _ => "Cached wallpaper, motion and glass where available."
            };
        }
        finally { _syncing = false; }
    }
    internal void Open()
    {
        if (_disposed) return; _open = true; RefreshPreferences(); _poll.Start();
        _ = UpdateAudioAsync(true); _ = UpdateDisplayAsync(true); _ = UpdateStatusAsync();
    }
    private void Refresh()
    { if (_open) { _ = UpdateAudioAsync(true); _ = UpdateDisplayAsync(true); _ = UpdateStatusAsync(); } }
    private void ScheduleWrites() { _writes.Stop(); _writes.Start(); }
    private static void Observe(Task task) => _ = task.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

    private async Task UpdateAudioAsync(bool read)
    {
        if (!_open || _disposed || _audioBusy) return; _audioBusy = true;
        try
        {
            if (_audioTask is null)
            {
                if (_queuedVolume is not null || _queuedMute is not null)
                {
                    _audioTask = _audio.ChangeAsync(_queuedAudioDevice, [new("", _queuedVolume, _queuedMute)]);
                    _writingVolume = _queuedVolume; _writingMute = _queuedMute;
                    _queuedVolume = null; _queuedMute = null;
                }
                else if (read) _audioTask = _audio.ReadAsync();
                else return;
                Observe(_audioTask);
            }
            var snapshot = await _audioTask.WaitAsync(TimeSpan.FromSeconds(5)); _audioTask = null;
            _writingVolume = null; _writingMute = null;
            if (_open && !_disposed) { _audioState = snapshot; ShowAudio(); }
        }
        catch (TimeoutException) { if (_open) _audioMessage.Text = "Audio is taking longer to respond. Choose Refresh to check again."; }
        catch (Exception ex)
        { _audioTask = null; _writingVolume = null; _writingMute = null; Log.Write("Quick settings audio failed", ex); if (_open && !_disposed) { _audioState = AudioSnapshot.Unavailable(ex.Message); ShowAudio(); } }
        finally
        { _audioBusy = false; if (_open && (_queuedVolume is not null || _queuedMute is not null) && _audioTask is null) ScheduleWrites(); }
    }
    private void ShowAudio()
    {
        _syncing = true;
        try
        {
            _volume.IsEnabled = _mute.IsEnabled = _audioState.Available;
            float level = _queuedVolume ?? _writingVolume ?? _audioState.Volume; bool muted = _queuedMute ?? _writingMute ?? _audioState.Muted;
            _volume.Value = Math.Round(level * 100); _mute.Content = muted ? "Unmute" : "Mute";
            _audioMessage.Text = _audioState.Available ? $"{_audioState.Device} · {Math.Round(level * 100)}%{(muted ? " · muted" : "")}" : _audioState.Message;
        }
        finally { _syncing = false; }
    }
    private async Task UpdateDisplayAsync(bool read)
    {
        if (!_open || _disposed || _displayBusy) return; _displayBusy = true;
        try
        {
            if (_displayTask is null)
            {
                if (_queuedBrightness is double level) { _displayTask = _display.ChangeAsync(_queuedDisplayDevice, level); _writingBrightness = level; _queuedBrightness = null; }
                else if (read) _displayTask = _display.ReadAsync();
                else return;
                Observe(_displayTask);
            }
            var snapshot = await _displayTask.WaitAsync(TimeSpan.FromSeconds(5)); _displayTask = null;
            _writingBrightness = null;
            if (_open && !_disposed) { _displayState = snapshot; ShowDisplay(); }
        }
        catch (TimeoutException) { if (_open) _displayMessage.Text = "The display is taking longer to respond. Choose Refresh to check again."; }
        catch (Exception ex)
        { _displayTask = null; _writingBrightness = null; Log.Write("Quick settings brightness failed", ex); if (_open && !_disposed) { _displayState = BrightnessSnapshot.Unavailable(ex.Message); ShowDisplay(); } }
        finally { _displayBusy = false; if (_open && _queuedBrightness is not null && _displayTask is null) ScheduleWrites(); }
    }
    private void ShowDisplay()
    {
        _syncing = true;
        try
        {
            _brightness.IsEnabled = _displayState.Available;
            double level = _queuedBrightness ?? _writingBrightness ?? _displayState.Percent; _brightness.Value = Math.Round(level);
            _displayMessage.Text = _displayState.Available ? $"{_displayState.Device} · {Math.Round(level)}%" : _displayState.Message;
        }
        finally { _syncing = false; }
    }
    private async Task UpdateStatusAsync()
    {
        if (!_open || _disposed || _statusBusy) return; _statusBusy = true;
        try
        {
            _statusTask ??= Task.Run(() => PcMetrics.Read(ref _cpu, includeStorage: false)); Observe(_statusTask);
            var status = await _statusTask.WaitAsync(TimeSpan.FromSeconds(5)); _statusTask = null;
            if (_open && !_disposed) { _power.Text = status.Power; _network.Text = status.Network; }
        }
        catch (TimeoutException) { if (_open) _network.Text = "Network status is taking longer to respond."; }
        catch (Exception ex) { _statusTask = null; Log.Write("Quick settings status failed", ex); }
        finally { _statusBusy = false; }
    }
    internal void Hide()
    {
        if (!_open) return; _open = false; _poll.Stop(); _writes.Stop();
        // Preserve the user's final slider position, then release audio sessions.
        // This queues at most one final write; no closed-panel polling continues.
        if (_queuedVolume is not null || _queuedMute is not null)
            Observe(_audio.ChangeAsync(_queuedAudioDevice, [new("", _queuedVolume, _queuedMute)]));
        if (_queuedBrightness is double level) Observe(_display.ChangeAsync(_queuedDisplayDevice, level));
        _queuedVolume = null; _queuedMute = null; _queuedBrightness = null;
        Observe(_audio.SuspendAsync());
    }
    public void Dispose() { if (_disposed) return; Hide(); _disposed = true; _audio.Dispose(); _poll.Stop(); _writes.Stop(); }
}
