using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Nexus.Runtime;
using Nexus.Shell.Desktop;
using Nexus.Shell.Services;
using System.Text.Json;
using Windows.System;

namespace Nexus.Shell.UI.Controls;

// Core-backed shell surface. Poll only while open, coalesce sliders and keep
// their original service epoch. Uncertain commands are never replayed.
internal sealed class QuickSettingsView : Grid, IDisposable
{
    private readonly ControlCenterEnvironment _environment;
    private readonly StackPanel _body = new() { Spacing = 12 };
    private readonly TextBlock _status;
    private readonly InfoBar _error = new() { IsClosable = true, Severity = InfoBarSeverity.Error, Title = "Control Center needs attention" };
    private readonly Grid _nav = new() { ColumnSpacing = 8, RowSpacing = 8 };
    private readonly TextBlock _heading, _subtitle;
    private readonly Button _expand;
    private readonly ProgressBar _progress = new() { IsIndeterminate = false, Height = 2, Opacity = 0 };
    private readonly SurfaceSession _session = new();
    private readonly Button _scan;
    private readonly Dictionary<string, Button> _tabs = [];
    private readonly SettingsIntentBuffer _queued = new();
    private readonly Dictionary<string, (Slider Slider, Button Mute, TextBlock Label)> _mixer = [];
    private readonly List<(TextBlock Label, bool Muted)> _labels = [];
    private readonly List<Border> _cards = [];
    private readonly List<Border> _badges = [];
    private readonly List<Button> _deviceButtons = [];
    private readonly List<(ToggleSwitch Toggle, Func<bool> Read)> _preferences = [];
    private readonly HashSet<StackPanel> _networkEditors = [];
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer _writes = new() { Interval = TimeSpan.FromMilliseconds(140) };
    private readonly int _fixedLabels;
    private SettingsSnapshot? _state;
    private SettingsRequest? _active;
    private Slider? _brightness;
    private TextBlock? _brightnessLabel;
    private TextBlock? _glassStatus;
    private ComboBox? _wallpaper;
    private string _section = "Sound", _signature = "";
    private bool _open, _disposed, _syncing, _busy, _readAgain, _compact;

    internal QuickSettingsView(ControlCenterEnvironment environment, Action close)
    {
        _environment = environment; Padding = new Thickness(22); RowSpacing = 16;
        KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;
        RowDefinitions.Add(new() { Height = GridLength.Auto }); RowDefinitions.Add(new() { Height = GridLength.Auto });
        RowDefinitions.Add(new()); RowDefinitions.Add(new() { Height = GridLength.Auto });
        var header = new Grid { ColumnSpacing = 12 }; header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var heading = new StackPanel { Spacing = 4 }; _heading = Label("Control Center", 23); _heading.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        _subtitle = Label("Everything you need, close at hand.", 12, true);
        heading.Children.Add(_heading); heading.Children.Add(_subtitle); header.Children.Add(heading);
        var dismiss = ShellControls.IconButton("\uE8BB", "Close Control Center", close); Grid.SetColumn(dismiss, 1); header.Children.Add(dismiss); Children.Add(header);
        var nav = _nav;
        for (int i = 0; i < 3; i++) nav.ColumnDefinitions.Add(new()); for (int i = 0; i < 2; i++) nav.RowDefinitions.Add(new());
        var sections = new[] { ("Sound", "\uE767"), ("Network", "\uE701"), ("Bluetooth", "\uE702"), ("Display", "\uE7F4"), ("Power", "\uE7E8"), ("Desktop", "\uE8FC") };
        for (int i = 0; i < sections.Length; i++)
        {
            var (name, glyph) = sections[i]; var content = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
            var badge = ShellControls.Badge(glyph, environment.Theme, 30); _badges.Add(badge); content.Children.Add(badge); var label = Label(name, 12); label.HorizontalAlignment = HorizontalAlignment.Center; content.Children.Add(label);
            var button = ActionButton(name, () => Navigate(name)); button.Content = content; button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment = HorizontalAlignment.Center; button.Padding = new Thickness(8, 12, 8, 12); button.CornerRadius = new CornerRadius(15); button.BorderThickness = new Thickness(1);
            _tabs[name] = button; Grid.SetRow(button, i / 3); Grid.SetColumn(button, i % 3); nav.Children.Add(button);
        }
        Grid.SetRow(nav, 1); Children.Add(nav);
        var scroll = new ScrollViewer { Content = _body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 2); Children.Add(scroll);
        var footer = new StackPanel { Spacing = 8 }; footer.Children.Add(_error); footer.Children.Add(_progress); _status = Label("", 11, true); AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite); footer.Children.Add(_status);
        _expand = ActionButton("All controls", environment.Expand); _expand.Visibility = Visibility.Collapsed; footer.Children.Add(_expand);
        var tools = new Grid { ColumnSpacing = 6 }; tools.ColumnDefinitions.Add(new()); tools.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); tools.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        tools.Children.Add(ActionButton("More options…", Advanced));
        _scan = ActionButton("Scan", () => _ = ReadAsync(true)); Grid.SetColumn(_scan, 1); tools.Children.Add(_scan);
        var refresh = ActionButton("Refresh", () => _ = ReadAsync()); Grid.SetColumn(refresh, 2); tools.Children.Add(refresh); footer.Children.Add(tools); Grid.SetRow(footer, 3); Children.Add(footer);
        var escape = new KeyboardAccelerator { Key = VirtualKey.Escape }; escape.Invoked += (_, args) => { close(); args.Handled = true; }; KeyboardAccelerators.Add(escape);
        _writes.Tick += (_, _) => { _writes.Stop(); _ = PumpAsync(); };
        _poll.Tick += (_, _) => { if (!_busy && _queued.Count == 0 && _networkEditors.Count == 0 && _section != "Desktop") _ = ReadAsync(); };
        _fixedLabels = _labels.Count; ApplyAppearance();
    }
    private TextBlock Label(string text, double size = 13, bool muted = false)
    {
        var label = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Foreground = _environment.Theme.Brush(muted ? "NexusMuted" : "NexusText") };
        _labels.Add((label, muted)); return label;
    }
    private static Button ActionButton(string name, Action action)
    {
        var button = new Button { Content = name, Style = (Style)Application.Current.Resources["QuietButton"], Padding = new Thickness(10, 8, 10, 8) };
        AutomationProperties.SetName(button, name); button.Click += (_, _) => action(); return button;
    }
    private Button DeviceButton(string name, Action action, bool enabled = true)
    {
        string section = _section;
        var button = ActionButton(name, () => { if (_open && !_disposed && _section == section) action(); });
        button.IsEnabled = enabled && !_busy; if (enabled) _deviceButtons.Add(button); return button;
    }
    private StackPanel Card(string title, string? detail = null)
    {
        var body = new StackPanel { Spacing = 12 }; var heading = Label(title, 15); heading.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold; body.Children.Add(heading);
        if (!string.IsNullOrWhiteSpace(detail)) body.Children.Add(Label(detail, 12, true));
        var card = new Border { Child = body, Padding = new Thickness(16), CornerRadius = new CornerRadius(_environment.Theme.HighContrast ? 0 : 16), BorderThickness = new Thickness(1),
            Background = _environment.Theme.Material("Card", _environment.State.NativeGlass), BorderBrush = _environment.Theme.Edge };
        _cards.Add(card); _body.Children.Add(card); return body;
    }
    private void ClearBody()
    {
        if (_labels.Count > _fixedLabels) _labels.RemoveRange(_fixedLabels, _labels.Count - _fixedLabels);
        _body.Children.Clear(); _cards.Clear(); _deviceButtons.Clear(); _preferences.Clear(); _networkEditors.Clear(); _mixer.Clear(); _brightness = null; _brightnessLabel = null; _wallpaper = null; _glassStatus = null;
    }
    internal void ApplyAppearance()
    {
        RequestedTheme = _environment.Theme.ElementTheme;
        foreach (var (label, muted) in _labels) label.Foreground = _environment.Theme.Brush(muted ? "NexusMuted" : "NexusText");
        foreach (var badge in _badges) { badge.Background = _environment.Theme.Brush("NexusSelection"); badge.CornerRadius = new CornerRadius(_environment.Theme.HighContrast ? 0 : 9); if (badge.Child is FontIcon icon) icon.Foreground = _environment.Theme.Brush("NexusAccent"); }
        foreach (var card in _cards) { card.Background = _environment.Theme.Material("Card", _environment.State.NativeGlass); card.BorderBrush = _environment.Theme.Edge; card.CornerRadius = new CornerRadius(_environment.Theme.HighContrast ? 0 : 16); }
        foreach (var (section, tab) in _tabs) { tab.Background = section == _section ? _environment.Theme.Brush("NexusSelection") : _environment.Theme.Material("Card", _environment.State.NativeGlass); tab.BorderBrush = section == _section ? _environment.Theme.Brush("NexusAccent") : _environment.Theme.Edge; tab.CornerRadius = new CornerRadius(_environment.Theme.HighContrast ? 0 : 15); }
        RefreshPreferences();
        if (_glassStatus is not null) _glassStatus.Text = _environment.Theme.HighContrast ? "High contrast uses your Windows system colors."
            : !_environment.Theme.Transparency ? "Windows transparency is off. Nexus uses solid surfaces until it is enabled."
            : _environment.State.ReducedEffects ? "Reduced effects uses solid surfaces. Turn it off to allow glass."
            : !_environment.State.NativeGlass ? "Glass is disabled in Nexus."
            : "Glass is enabled in Nexus. Windows supplies live blur when graphics and power settings allow it.";
    }
    internal void RefreshPreferences()
    { if (_disposed) return; _syncing = true; try { foreach (var (toggle, read) in _preferences) if (toggle.IsEnabled) toggle.IsOn = read(); if (_wallpaper is { IsEnabled: true }) _wallpaper.SelectedItem = _environment.State.Wallpaper; } finally { _syncing = false; } }
    internal void Open(string? section = null, bool compact = false)
    {
        if (_disposed) return; _compact = compact; _open = true; _poll.Start();
        _nav.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        _expand.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        Navigate(section ?? _section);
    }
    private void Navigate(string section)
    {
        if (section != "Desktop" && !SettingsRules.Sections.Contains(section)) section = "Sound";
        bool retain = _section == section && _state is not null && _body.Children.Count > 0;
        _ = PumpAsync(); _section = section; _session.Open(section); _error.IsOpen = false;
        _heading.Text = _compact ? section : "Control Center";
        _subtitle.Text = _compact ? section switch { "Sound" => "Output volume and application levels", "Network" => "Your connections, managed by Nexus", "Bluetooth" => "Nearby radios and paired devices", "Display" => "Brightness and display controls", _ => "Your device controls" } : "Everything you need, close at hand.";
        if (!retain) { _state = null; _signature = ""; ClearBody(); }
        ApplyAppearance();
        _scan.Visibility = section is "Network" or "Bluetooth" ? Visibility.Visible : Visibility.Collapsed;
        if (section == "Desktop") { RenderDesktop(); _status.Text = "Preferences save automatically through Nexus Core."; }
        else { if (!retain) Card(section, "Reading device state…"); _ = ReadAsync(); }
    }
    private async Task ReadAsync(bool scan = false)
    {
        if (!_open || _disposed || _section == "Desktop") return;
        if (_networkEditors.Count != 0) { _status.Text = "Finish or cancel the connection details before refreshing."; return; }
        if (_busy || _queued.Count != 0) { if (!scan) _readAgain = true; _status.Text = "Finishing the current device request…"; return; }
        _readAgain = false; await ExecuteAsync(new(_section, Scan: scan));
    }
    private void Queue(SettingsChange change, bool immediate = false)
    {
        if (_syncing || !_open || _disposed || _state is null) return;
        var request = new SettingsRequest(_section, Change: change with { Epoch = _state.Epoch });
        try { _queued.Set(request); } catch (RuntimeFailure error) { _status.Text = error.Message; return; }
        _status.Text = "Applying your change…";
        if (immediate) _ = PumpAsync(); else { _writes.Stop(); _writes.Start(); }
    }
    private async Task PumpAsync()
    {
        if (_disposed || _busy || _queued.Count == 0 || _environment.IsStopping) return;
        if (_queued.Take() is { } request) await ExecuteAsync(request);
    }
    private async Task ExecuteAsync(SettingsRequest request)
    {
        if (_busy || _disposed) return; long generation = _session.Capture(); _busy = true; _active = request; SetBusy();
        AutomationProperties.SetLiveSetting(_status, request.Change is not null || request.Scan ? AutomationLiveSetting.Polite : AutomationLiveSetting.Off);
        if (_open && request.Section == _section) _status.Text = request.Scan ? "Scanning devices…" : request.Change is null ? "Reading device state…" : "Applying your change…";
        try
        {
            var state = await _environment.ExecuteSettingsAsync(request);
            if (!_disposed && _session.Accepts(generation, state.Section))
            { _state = state; Render(state); _error.IsOpen = false; _status.Text = state.Message.Length == 0 ? "Up to date · " + DateTime.Now.ToString("HH:mm") : state.Message; }
        }
        catch (Exception error)
        {
            _queued.Clear(); Log.Write("Control Center " + request.Section + " request failed", error);
            if (!_disposed && _session.Accepts(generation, request.Section))
            { _state = null; _signature = ""; ClearBody(); Card(request.Section, "The current device state could not be read. Refresh to try again."); ShowError(error.Message); _status.Text = "Device state needs a refresh."; }
        }
        finally
        {
            _busy = false; _active = null;
            if (!_disposed) { SetBusy(); if (_queued.Count != 0) _ = PumpAsync(); else if (_open && _readAgain) _ = ReadAsync(); }
        }
    }
    private void SetBusy() { foreach (var button in _deviceButtons) button.IsEnabled = !_busy; _scan.IsEnabled = !_busy; _progress.IsIndeterminate = _busy && _open; _progress.Opacity = _busy && _open ? 1 : 0; }
    private SettingsChange? Pending(string kind, string device, string item = "")
    {
        return _queued.Find(_section, kind, device, item) ?? (_active?.Section == _section && _active.Change is { } active
            && active.Kind == kind && active.DeviceId == device && active.ItemId == item ? active : null);
    }
    private void Render(SettingsSnapshot state)
    {
        if (state.Section == "Sound" && state.Sound is { } sound) { RenderSound(sound); return; }
        if (state.Section == "Display") { RenderDisplay(state); return; }
        // Stable snapshots retain focus, expansion and confirmation buttons.
        string signature = state.Section + "|" + JsonSerializer.Serialize<object?>(state.Network ?? (object?)state.Bluetooth ?? state.Power) + "|" + state.Message;
        if (_signature == signature) return;
        _signature = signature; ClearBody();
        if (state.Network is { } network) RenderNetwork(network);
        else if (state.Bluetooth is { } bluetooth) RenderBluetooth(bluetooth);
        else if (state.Power is { } power) RenderPower(power);
        else Card(state.Section, "No state was returned. Choose Refresh.");
    }
    private void RenderSound(SoundState sound)
    {
        string signature = sound.DeviceId + "|" + sound.Available + "|" + string.Join("|", sound.Sessions.Select(s => s.Id));
        if (signature != _signature || _mixer.Count == 0)
        {
            _signature = signature; ClearBody(); var output = Card("Audio output", sound.Available ? sound.Device : sound.Message);
            if (sound.Available)
            {
                AddMixer(output, "", "Output volume", sound.DeviceId);
                var mixer = Card("Volume mixer", sound.Sessions.Length == 0 ? "Play audio in an application to see its controls here." : "Levels follow the applications’ current audio sessions.");
                foreach (var session in sound.Sessions) AddMixer(mixer, session.Id, session.Name, sound.DeviceId);
            }
        }
        _syncing = true;
        try
        {
            void Update(string id, string name, float volume, bool muted)
            {
                if (!_mixer.TryGetValue(id, out var row)) return; var pending = Pending("volume", sound.DeviceId, id);
                double percent = pending?.Value ?? Math.Round(volume * 100); bool silent = pending?.Enabled ?? muted;
                row.Slider.Value = percent; row.Mute.Content = silent ? "Unmute" : "Mute"; AutomationProperties.SetName(row.Mute, (silent ? "Unmute " : "Mute ") + name); row.Label.Text = name + " · " + Math.Round(percent) + "%" + (silent ? " · muted" : "");
            }
            Update("", "Output volume", sound.Volume, sound.Muted); foreach (var session in sound.Sessions) Update(session.Id, session.Name, session.Volume, session.Muted);
        }
        finally { _syncing = false; }
    }
    private void AddMixer(StackPanel parent, string id, string name, string device)
    {
        var label = Label(name, 12); parent.Children.Add(label); var row = new Grid { ColumnSpacing = 10 }; row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var slider = new Slider { Minimum = 0, Maximum = 100, StepFrequency = 1, MinWidth = 100 }; AutomationProperties.SetName(slider, name);
        slider.ValueChanged += (_, args) =>
        { if (_syncing || _section != "Sound" || !_mixer.TryGetValue(id, out var current) || !ReferenceEquals(current.Slider, slider)) return; var pending = Pending("volume", device, id); Queue(new("volume", device, id, args.NewValue, pending?.Enabled)); label.Text = name + " · " + Math.Round(args.NewValue) + "%"; };
        var mute = DeviceButton("Mute", () =>
        {
            var current = id.Length == 0 ? _state?.Sound?.Muted : _state?.Sound?.Sessions.FirstOrDefault(s => s.Id == id)?.Muted;
            var pending = Pending("volume", device, id); Queue(new("volume", device, id, pending?.Value, !(pending?.Enabled ?? current ?? false)), true);
        });
        AutomationProperties.SetName(mute, "Mute or unmute " + name); row.Children.Add(slider); Grid.SetColumn(mute, 1); row.Children.Add(mute); parent.Children.Add(row); _mixer[id] = (slider, mute, label);
    }
    private void RenderDisplay(SettingsSnapshot state)
    {
        var brightness = state.Brightness;
        string signature = (brightness?.DeviceId ?? "") + "|" + brightness?.Available + "|" + string.Join("|", (state.Displays ?? []).Select(d => d.ToString()));
        if (_signature != signature || _brightnessLabel is null)
        {
            _signature = signature; ClearBody();
            foreach (var display in state.Displays ?? []) Card(display.Primary ? "Primary display" : "Display", display.Name + $"\n{display.Width} × {display.Height} · {display.RefreshHz} Hz");
            var card = Card("Brightness"); _brightnessLabel = Label(brightness?.Message ?? "Brightness is unavailable.", 12, true); card.Children.Add(_brightnessLabel);
            var control = _brightness = new Slider { Minimum = 0, Maximum = 100, StepFrequency = 1, IsEnabled = brightness?.Available == true }; AutomationProperties.SetName(control, "Hardware brightness");
            control.ValueChanged += (_, args) => { if (!_syncing && _section == "Display" && ReferenceEquals(control, _brightness) && brightness?.Available == true) { Queue(new("brightness", brightness.DeviceId, Value: args.NewValue)); _brightnessLabel!.Text = brightness.Device + " · " + Math.Round(args.NewValue) + "%"; } }; card.Children.Add(control);
            Card("Display options", "Resolution, scaling, projection and night light remain in advanced Windows controls. Brightness needs a monitor and driver with hardware control support.");
        }
        _syncing = true;
        try { if (brightness?.Available == true && _brightness is not null) { double value = Pending("brightness", brightness.DeviceId)?.Value ?? brightness.Percent; _brightness.Value = value; _brightnessLabel!.Text = brightness.Device + " · " + Math.Round(value) + "%"; } else if (_brightnessLabel is not null) _brightnessLabel.Text = brightness?.Message ?? "Brightness is unavailable."; }
        finally { _syncing = false; }
    }
    private void RenderNetwork(NetworkState state)
    {
        foreach (var link in state.Links)
        {
            var card = Card(link.Name, link.Kind + " · " + link.State + (link.SpeedMbps > 0 ? $" · {link.SpeedMbps} Mbps" : ""));
            var info = Label($"Address: {link.Address}\nGateway: {link.Gateway}\nDNS: {link.Dns}", 11, true); info.IsTextSelectionEnabled = true; card.Children.Add(info);
        }
        foreach (var adapter in state.Wifi)
        {
            string radio = !adapter.RadioAvailable ? "Radio state unavailable" : !adapter.HardwareOn ? "Hardware radio is off" : adapter.SoftwareOn ? "Wi-Fi on" : "Wi-Fi off";
            var card = Card(adapter.Name, radio); card.Children.Add(DeviceButton(adapter.SoftwareOn ? "Turn Wi-Fi off" : "Turn Wi-Fi on", () => Queue(new("wifi-radio", adapter.Id, Enabled: !adapter.SoftwareOn), true), adapter.RadioAvailable && adapter.HardwareOn));
            if (adapter.Message.Length != 0) card.Children.Add(Label(adapter.Message, 11, true));
            foreach (var network in adapter.Networks)
            {
                card.Children.Add(Label(network.Name + $" · {network.Signal}%" + (network.Secured ? " · secured" : " · open") + (network.Connected ? " · connected" : ""), 12));
                if (network.Connected) card.Children.Add(DeviceButton("Disconnect", () => Queue(new("wifi-disconnect", adapter.Id), true)));
                else if (network.Profile.Length != 0) card.Children.Add(DeviceButton("Connect", () => Queue(new("wifi-connect", adapter.Id, network.Profile), true), adapter.SoftwareOn && adapter.HardwareOn));
                else if (network.CanJoin) card.Children.Add(DeviceButton(network.Secured ? "Connect with password…" : "Connect to open network",
                    () => { if (network.Secured) JoinEditor(card, adapter.Id, network); else Queue(new("wifi-join", adapter.Id, network.Id), true); }, adapter.SoftwareOn && adapter.HardwareOn));
                if (network.CanForget)
                {
                    bool confirm = false; Button? forget = null;
                    forget = DeviceButton("Forget Nexus connection", () => { if (confirm) Queue(new("wifi-forget", adapter.Id, network.Profile), true); else { confirm = true; forget!.Content = "Confirm forget"; } }); card.Children.Add(forget);
                }
            }
        }
        if (state.Message.Length != 0) Card("Network support", state.Message);
    }
    private void JoinEditor(StackPanel card, string adapter, WifiNetwork network)
    {
        if (_networkEditors.Count != 0) { _status.Text = "Finish or cancel the current connection first."; return; }
        var editor = new StackPanel { Spacing = 8 }; _networkEditors.Add(editor); editor.Children.Add(Label("Password for " + network.Name, 12));
        var password = new PasswordBox { MaxLength = 64 }; AutomationProperties.SetName(password, "Wi-Fi password for " + network.Name); editor.Children.Add(password);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(DeviceButton("Connect", () =>
        {
            string secret = password.Password; password.Password = ""; _networkEditors.Remove(editor); card.Children.Remove(editor);
            Queue(new("wifi-join", adapter, network.Id, Secret: secret), true);
        }));
        actions.Children.Add(ActionButton("Cancel", () => { password.Password = ""; _networkEditors.Remove(editor); card.Children.Remove(editor); })); editor.Children.Add(actions); card.Children.Add(editor); password.Focus(FocusState.Programmatic);
    }
    private void RenderBluetooth(BluetoothState state)
    {
        foreach (var radio in state.Radios)
        {
            var card = Card(radio.Name, radio.Discoverable ? "Visible to nearby devices" : "Hidden from nearby devices");
            card.Children.Add(DeviceButton(radio.Discoverable ? "Make hidden" : "Make discoverable", () => Queue(new("bluetooth-discovery", radio.Id, Enabled: !radio.Discoverable), true)));
            if (radio.Devices.Length == 0) card.Children.Add(Label("No cached classic Bluetooth devices. Choose Scan to search nearby.", 12, true));
            foreach (var device in radio.Devices) card.Children.Add(Label(device.Name + (device.Connected ? " · connected" : " · disconnected") + (device.Paired ? " · paired" : " · unpaired"), 12));
        }
        Card("Bluetooth support", state.Message);
    }
    private void RenderPower(Nexus.Runtime.PowerState state)
    {
        Card("Power status", state.Status); var plans = Card("Power plans", "Choose an existing Windows power plan. Nexus applies it directly.");
        foreach (var plan in state.Plans) plans.Children.Add(DeviceButton((plan.Active ? "✓ " : "") + plan.Name, () => Queue(new("power-plan", plan.Id), true), !plan.Active));
        if (state.Message.Length != 0) plans.Children.Add(Label(state.Message, 12, true)); plans.Children.Add(ActionButton("Lock desktop", _environment.LockScreen));
    }
    internal void ShowError(string message) { if (!_disposed && _open) { _error.Message = message; _error.IsOpen = true; _status.Text = "Review the message or refresh the current state."; } }
    private void Preference(StackPanel card, string name, DesktopPreference preference, Func<bool> read)
    {
        var row = new Grid { ColumnSpacing = 10 }; row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var label = Label(name, 13); label.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(label);
        var toggle = new ToggleSwitch { IsOn = read(), OnContent = "", OffContent = "", MinWidth = 46 }; AutomationProperties.SetName(toggle, name); Grid.SetColumn(toggle, 1); row.Children.Add(toggle);
        toggle.Toggled += async (_, _) =>
        {
            if (_syncing || !_open || _disposed || !toggle.IsEnabled || !_preferences.Any(item => ReferenceEquals(item.Toggle, toggle))) return; long generation = _session.Capture(); toggle.IsEnabled = false;
            try { await _environment.SetPreferenceAsync(preference, toggle.IsOn); }
            catch (Exception error) { if (!_disposed && _session.Accepts(generation, "Desktop")) ShowError(error.Message); }
            finally { if (!_disposed) { toggle.IsEnabled = true; RefreshPreferences(); } }
        }; _preferences.Add((toggle, read)); card.Children.Add(row);
    }
    private void RenderDesktop()
    {
        var state = _environment.State; var appearance = Card("Appearance", "Choose the surfaces and motion that suit your display.");
        var wallpaper = _wallpaper = new ComboBox { Header = "Wallpaper palette", ItemsSource = AuraPalette.Moods, SelectedItem = state.Wallpaper, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(wallpaper, "Wallpaper palette");
        wallpaper.SelectionChanged += async (_, _) =>
        {
            if (_syncing || !_open || _disposed || !wallpaper.IsEnabled || !ReferenceEquals(wallpaper, _wallpaper) || wallpaper.SelectedItem is not string mood) return;
            long generation = _session.Capture(); wallpaper.IsEnabled = false;
            try { await _environment.SetPreferenceAsync(DesktopPreference.Wallpaper, value: mood); }
            catch (Exception error) { if (!_disposed && _session.Accepts(generation, "Desktop")) ShowError(error.Message); }
            finally { if (!_disposed) { wallpaper.IsEnabled = true; RefreshPreferences(); } }
        }; appearance.Children.Add(wallpaper);
        Preference(appearance, "Glass surfaces", DesktopPreference.NativeGlass, () => state.NativeGlass);
        _glassStatus = Label("", 12, true); appearance.Children.Add(_glassStatus);
        Preference(appearance, "Interface animations", DesktopPreference.Animations, () => state.SurfaceAnimations);
        appearance.Children.Add(Label("Turn animations off for immediate interactions while keeping glass enabled.", 12, true));
        Preference(appearance, "Reduce effects", DesktopPreference.ReducedEffects, () => state.ReducedEffects);
        var dock = Card("Dock");
        Preference(dock, "Floating dock", DesktopPreference.FloatingDock, () => state.FloatingTaskbar);
        Preference(dock, "Compact dock", DesktopPreference.CompactDock, () => state.CompactDock);
        Preference(dock, "Window previews", DesktopPreference.WindowPreviews, () => state.DockPreviews);
        var desktop = Card("Desktop & notifications");
        Preference(desktop, "24-hour clock", DesktopPreference.Clock24Hour, () => state.Clock24Hour);
        Preference(desktop, "Desktop clock", DesktopPreference.ClockWidget, () => state.ShowClockWidget);
        Preference(desktop, "Space widget", DesktopPreference.SpaceWidget, () => state.ShowSpaceWidget);
        Preference(desktop, "Quiet Nexus alerts", DesktopPreference.QuietAlerts, () => state.QuietNotifications);
        var signIn = Card("Desktop at sign-in", _environment.IsManagedDesktop
            ? "Nexus is managing this desktop session. Sign-in replacement and startup alongside Windows are separate choices."
            : "This is a preview alongside Windows. Use session mode to give the standalone Windows key to Nexus Launchpad.");
        signIn.Children.Add(ActionButton("Review startup and sign-in setup…", () => _environment.ShowSections("Personalize")));
        ApplyAppearance();
    }
    private void Advanced() => _environment.Advanced(_section);
    internal void Hide()
    {
        if (!_open) return; _open = false; _session.Hide(); _poll.Stop(); _writes.Stop(); _readAgain = false; _progress.IsIndeterminate = false; _progress.Opacity = 0;
        foreach (var editor in _networkEditors) foreach (var password in editor.Children.OfType<PasswordBox>()) password.Password = "";
        _ = PumpAsync();
    }
    public void Dispose()
    { if (_disposed) return; Hide(); _disposed = true; _queued.Clear(); _poll.Stop(); _writes.Stop(); }
}
