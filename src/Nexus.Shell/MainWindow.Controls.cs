using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Nexus.Shell.Services;
using Nexus.Shell.UI;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    private TextBlock Text(string value, double size = 14, bool muted = false)
    {
        var text = new TextBlock { Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap };
        _paints.Add(() => text.Foreground = _environment.Theme.Brush(muted ? "NexusMuted" : "NexusText")); return text;
    }
    private TextBlock BoundText(Func<string> value, double size = 13, bool muted = true)
    {
        var text = Text(value(), size, muted);
        _bindings.Add(() => { string next = value(); if (text.Text != next) text.Text = next; }); return text;
    }
    private Border Card(UIElement child, string? anchor = null)
    {
        var card = new Border { Child = child, Padding = new Thickness(20), BorderThickness = new Thickness(1) };
        _paints.Add(() => { card.Background = _environment.Theme.Material("Card", _state.NativeGlass); card.BorderBrush = _environment.Theme.Edge; card.CornerRadius = new CornerRadius(_highContrast ? 0 : 16); });
        if (anchor is not null) _anchors[anchor] = card; return card;
    }
    private StackPanel Group(string title, string? detail = null)
    {
        var group = new StackPanel { Spacing = 14 }; var heading = Text(title, 19); heading.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold; group.Children.Add(heading);
        if (detail is not null) group.Children.Add(Text(detail, 12, true)); return group;
    }
    private Button ActionButton(string label, Action action)
    {
        var button = new Button { Content = Text(label), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Style = (Style)Application.Current.Resources["AuraSurfaceButton"], Padding = new Thickness(14, 10, 14, 10), IsEnabled = !_validation };
        button.Click += (_, _) => { if (!_ready || _dialogOpen || _validation) return; try { action(); } catch (Exception error) { Log.Write("Nexus Controls action failed", error); ShowStatus(error.Message); } };
        return button;
    }
    private Button AsyncButton(string label, Func<Task> action)
    {
        var button = new Button { Content = Text(label), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Style = (Style)Application.Current.Resources["AuraSurfaceButton"], Padding = new Thickness(14, 10, 14, 10), IsEnabled = !_validation };
        button.Click += async (_, _) =>
        {
            if (!_ready || _validation || !button.IsEnabled) return; button.IsEnabled = false;
            try { await action(); } catch (Exception error) { Log.Write("Nexus Controls action failed", error); ShowStatus(error.Message); }
            finally { if (_ready) button.IsEnabled = true; }
        };
        return button;
    }
    private FrameworkElement Toggle(string title, string detail, Func<bool> read, Action<bool> write, bool savesItself = false)
    {
        var row = new Grid { ColumnSpacing = 16, MinHeight = 48 };
        row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var label = new StackPanel { Spacing = 4 }; label.Children.Add(Text(title)); label.Children.Add(Text(detail, 12, true)); row.Children.Add(label);
        var toggle = new ToggleSwitch { IsOn = read(), OnContent = "", OffContent = "", MinWidth = 50, Width = 50, VerticalAlignment = VerticalAlignment.Center, IsEnabled = !_validation };
        AutomationProperties.SetName(toggle, title); Grid.SetColumn(toggle, 1); row.Children.Add(toggle);
        toggle.Toggled += (_, _) => { if (!_ready || _syncing || _validation) return; write(toggle.IsOn); if (!savesItself) SaveState(); RefreshBindings(); };
        _bindings.Add(() => { if (toggle.IsOn != read()) toggle.IsOn = read(); }); return row;
    }
    private ComboBox Choice(string title, IEnumerable<string> choices, Func<string> read, Action<string> write, bool savesItself = false)
    {
        var box = new ComboBox { Header = title, ItemsSource = choices.ToArray(), SelectedItem = read(), HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = !_validation };
        AutomationProperties.SetName(box, title);
        box.SelectionChanged += (_, _) => { if (!_ready || _syncing || _validation || box.SelectedItem is not string value || value == read()) return; write(value); if (!savesItself) SaveState(); };
        _bindings.Add(() => { if (!Equals(box.SelectedItem, read())) box.SelectedItem = read(); }); return box;
    }
    private static Grid Tiles(params FrameworkElement[] children)
    {
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 }; int columns = 0;
        void Layout(double width)
        {
            int next = width >= 580 ? 2 : 1; if (next == columns) return; columns = next;
            grid.ColumnDefinitions.Clear(); grid.RowDefinitions.Clear();
            for (int i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new());
            for (int i = 0; i < (children.Length + columns - 1) / columns; i++) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
            for (int i = 0; i < children.Length; i++) { Grid.SetColumn(children[i], i % columns); Grid.SetRow(children[i], i / columns); }
        }
        foreach (var child in children) grid.Children.Add(child); grid.SizeChanged += (_, args) => Layout(args.NewSize.Width); Layout(0); return grid;
    }
    private string Service(string title) => _environment.DesktopServiceStatus().FirstOrDefault(row => row.Title == title)?.Detail ?? "Status is not available yet.";
    private void BuildHome()
    {
        var hero = Group("One home for Nexus.", "Shape your desktop, manage its services and choose what it remembers.");
        hero.Children.Add(DesktopPreview());
        hero.Children.Add(BoundText(() => _state.DisplayName + "’s desktop · " + _environment.Mode + " · " + _state.Wallpaper, 14, false));
        hero.Children.Add(ActionButton("Make it yours →", () => Navigate("personalize"))); PageContent.Children.Add(Card(hero));
        var core = Group("Nexus Core"); core.Children.Add(BoundText(() => Service("Nexus Core")));
        core.Children.Add(ActionButton("Core & recovery →", () => Navigate("core")));
        var privacy = Group("Your choices");
        privacy.Children.Add(BoundText(() => "Recent history " + (_state.RememberRecentItems ? "on" : "off") + " · usage tracking " + (_state.UsageTracking ? "on" : "off") + " · clipboard " + (_state.ClipboardShelf ? "on" : "off")));
        privacy.Children.Add(ActionButton("Review privacy →", () => Navigate("privacy"))); PageContent.Children.Add(Tiles(Card(core), Card(privacy)));
        var shortcuts = Group("Everyday controls", "Open the system controls you need without leaving the Nexus desktop.");
        shortcuts.Children.Add(Tiles(ActionButton("Sound & audio", () => _environment.ShowControlCenter("Sound")), ActionButton("Network & Wi-Fi", () => _environment.ShowControlCenter("Network")),
            ActionButton("Display & brightness", () => _environment.ShowControlCenter("Display")), ActionButton("Bluetooth devices", () => _environment.ShowControlCenter("Bluetooth")),
            ActionButton("Power & battery", () => _environment.ShowControlCenter("Power")), ActionButton("Windows tools & Keep awake", _environment.ShowWindowsTools)));
        PageContent.Children.Add(Card(shortcuts));
    }
    private FrameworkElement DesktopPreview()
    {
        var scene = new Grid { Height = 178 };
        var wallpaper = new Image { Stretch = Stretch.UniformToFill }; scene.Children.Add(wallpaper);
        var menu = new Border { Height = 22, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(10, 10, 10, 0), CornerRadius = new CornerRadius(7), Padding = new Thickness(10, 3, 10, 3) };
        menu.Child = Text("Nexus     File     View     Window", 10); scene.Children.Add(menu);
        var dock = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (string icon in new[] { "Launchpad", "Files", "Settings", "Note", "Browser" }) dock.Children.Add(NexusIcons.Image(icon, 23));
        var dockFrame = new Border { Child = dock, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(10, 7, 10, 7), CornerRadius = new CornerRadius(12) }; scene.Children.Add(dockFrame);
        var frame = new Border { Child = scene, CornerRadius = new CornerRadius(12) };
        string? current = null;
        _bindings.Add(() => { string name = AuraPalette.For(_state.Wallpaper).Name; if (current != name) { current = name; wallpaper.Source = new BitmapImage(new Uri("ms-appx:///Assets/Wallpapers/" + name + ".png")); } dock.Spacing = _state.CompactDock ? 3 : 8; dockFrame.Margin = new Thickness(0, 0, 0, _state.FloatingTaskbar ? 12 : 0); });
        _paints.Add(() => { menu.Background = dockFrame.Background = _environment.Theme.Brush("NexusPanel"); wallpaper.Visibility = _highContrast ? Visibility.Collapsed : Visibility.Visible; scene.Background = _environment.Theme.Brush("NexusPanel"); });
        AutomationProperties.SetName(frame, "Illustration of the selected wallpaper and dock density"); return frame;
    }
    private void BuildCore()
    {
        var actions = Group("Session recovery", "Check services reconnects stopped Nexus services using their existing recovery limits.");
        actions.Children.Add(Tiles(ActionButton("Check & reconnect services", () => { _environment.CheckDesktopServices(); ShowStatus("Service checks requested. Observed status updates below."); }),
            ActionButton("Repair desktop layout", () => { _environment.RepairDesktopLayout(); ShowStatus("Desktop layout repair requested."); })));
        actions.Children.Add(ActionButton("Refresh desktop", () => { _environment.RefreshDesktop(); ShowStatus("Desktop refresh requested."); }));
        PageContent.Children.Add(Card(actions, "repair"));
        var persistence = Group("Your settings", "Nexus saves changes automatically. You can also request a snapshot now.");
        persistence.Children.Add(BoundText(() => Service("Workspace persistence")));
        persistence.Children.Add(AsyncButton("Save settings now", async () => { ShowStatus("Saving settings through Nexus Core…"); await _environment.Session.SaveAsync(); ShowStatus("Settings snapshot saved through Nexus Core."); }));
        persistence.Children.Add(ActionButton("Open data & logs folder", OpenDataFolder)); PageContent.Children.Add(Card(persistence, "save"));
        var health = Group("Observed service status");
        foreach (string title in new[] { "Nexus Core", "Control Center", "Search", "Windows integration", "Desktop folder updates" })
        { health.Children.Add(Text(title, 14)); health.Children.Add(BoundText(() => Service(title))); }
        health.Children.Add(BoundText(() => _material.Status)); PageContent.Children.Add(Card(health, "services"));
    }
    private void OpenDataFolder() { Directory.CreateDirectory(StateStore.DirectoryPath); _environment.OpenTargetChecked(StateStore.DirectoryPath); }
    private void BuildPersonalize()
    {
        PageContent.Children.Add(DesktopPreview());
        var presets = Group("Start with a feeling", "Presets change appearance and dock behavior. Your content and privacy choices stay yours.");
        presets.Children.Add(Tiles(PresetButton("midnight", "Midnight Glass", "Floating dock · balanced glass · motion"), PresetButton("quiet", "Quiet desktop", "Solid surfaces · still motion · fewer previews"), PresetButton("compact", "Compact desktop", "Smaller dock · balanced glass · previews")));
        var undo = ActionButton("Undo last preset", () => { _appearanceUndo?.Apply(_state); _appearanceUndo = null; SaveState(); ShowStatus("Previous appearance restored."); });
        _bindings.Add(() => undo.IsEnabled = !_validation && _appearanceUndo is not null); presets.Children.Add(undo); PageContent.Children.Add(Card(presets, "presets"));
        var palettes = Group("Wallpaper & palette");
        var choices = new List<FrameworkElement>();
        foreach (string mood in AuraPalette.Moods)
        {
            var palette = AuraPalette.For(mood); var content = new StackPanel { Spacing = 8 };
            var swatches = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (string color in new[] { palette.Accent, palette.Secondary, palette.Canvas })
            { var swatch = new Border { Width = 26, Height = 18, CornerRadius = new CornerRadius(5) }; _paints.Add(() => swatch.Background = _highContrast ? _environment.Theme.Brush("NexusCard") : new SolidColorBrush(ShellTheme.Color(color))); swatches.Children.Add(swatch); }
            content.Children.Add(swatches); content.Children.Add(BoundText(() => palette.Name + (_state.Wallpaper == mood ? " · Selected" : ""), 14, false));
            var button = ActionButton("", () => { _state.Wallpaper = mood; SaveState(); }); button.Content = content; button.HorizontalContentAlignment = HorizontalAlignment.Left; button.HorizontalAlignment = HorizontalAlignment.Stretch;
            AutomationProperties.SetName(button, "Use " + palette.Name + " palette"); choices.Add(button);
        }
        palettes.Children.Add(Tiles(choices.ToArray())); PageContent.Children.Add(Card(palettes, "palette"));
        var effects = Group("Glass & motion");
        effects.Children.Add(Toggle("Glass surfaces", "Use native acrylic when Windows makes it available.", () => _state.NativeGlass, v => _state.NativeGlass = v));
        effects.Children.Add(Choice("Glass style", GlassStyle.Choices, () => _state.GlassStyle, v => _state.GlassStyle = v));
        effects.Children.Add(Choice("Requested quality", GlassQuality.Choices, () => _state.GlassQuality, v => _state.GlassQuality = v));
        effects.Children.Add(Toggle("Interface animations", "Animate Nexus surfaces when Windows allows motion.", () => _state.SurfaceAnimations, v => _state.SurfaceAnimations = v));
        effects.Children.Add(Toggle("Reduced effects", "Use solid surfaces and limit visual effects.", () => _state.ReducedEffects, v => _state.ReducedEffects = v));
        effects.Children.Add(BoundText(() => _material.Status)); PageContent.Children.Add(Card(effects, "glass"));
        var identity = Group("Display name"); var name = new TextBox { Text = _state.DisplayName, MaxLength = 40, PlaceholderText = "Your name", IsEnabled = !_validation };
        AutomationProperties.SetName(name, "Display name"); identity.Children.Add(name);
        identity.Children.Add(ActionButton("Save name", () => { string value = name.Text.Trim(); if (value.Length == 0) { ShowStatus("Enter a display name first."); return; } _state.DisplayName = value; SaveState(); ShowStatus("Display name updated."); }));
        PageContent.Children.Add(Card(identity, "name"));
    }
    private Button PresetButton(string id, string title, string detail)
    {
        var content = new StackPanel { Spacing = 8 }; content.Children.Add(Text(title, 16)); content.Children.Add(Text(detail, 12, true));
        var button = ActionButton("", () => { _appearanceUndo = NexusAppearance.Capture(_state); NexusAppearance.Preset(id).Apply(_state); SaveState(); ShowStatus(title + " applied. Undo is available in this window."); });
        button.Content = content; button.HorizontalContentAlignment = HorizontalAlignment.Left; button.HorizontalAlignment = HorizontalAlignment.Stretch; AutomationProperties.SetName(button, "Apply " + title); return button;
    }
    private void BuildDesktop()
    {
        var dock = Group("Dock behavior");
        dock.Children.Add(Toggle("Floating dock", "Let the dock float above the desktop.", () => _state.FloatingTaskbar, v => _state.FloatingTaskbar = v));
        dock.Children.Add(Toggle("Compact dock", "Use smaller icons and a tighter layout.", () => _state.CompactDock, v => _state.CompactDock = v));
        dock.Children.Add(Toggle("Window previews", "Show a preview when hovering over an open app.", () => _state.DockPreviews, v => _state.DockPreviews = v)); PageContent.Children.Add(Card(dock, "dock"));
        var icons = Group("Desktop icons"); icons.Children.Add(Toggle("Show desktop icons", "Show files and Nexus shortcuts on the desktop.", () => _state.ShowDesktopIcons, v => _state.ShowDesktopIcons = v));
        icons.Children.Add(Toggle("Focus desktop", "Temporarily hide icons and the desktop date card.", () => _state.FocusMode, v => _state.FocusMode = v));
        icons.Children.Add(Choice("Sort desktop items", ["Name", "Newest", "Kind"], () => _state.DesktopIconOrder, _environment.SetDesktopOrder, savesItself: true)); PageContent.Children.Add(Card(icons, "icons"));
        var clock = Group("Clock"); clock.Children.Add(Toggle("24-hour time", "Use 24-hour time in the Nexus menu bar and dock.", () => _state.Clock24Hour, v => _state.Clock24Hour = v));
        clock.Children.Add(Toggle("Desktop date card", "Show the date card when focus desktop is off.", () => _state.ShowClockWidget, v => _state.ShowClockWidget = v)); PageContent.Children.Add(Card(clock, "clock"));
        var alerts = Group("Notifications"); alerts.Children.Add(Toggle("Quiet notifications", "Keep new alerts in the inbox without desktop banners.", () => _state.QuietNotifications, v => _state.QuietNotifications = v));
        alerts.Children.Add(ActionButton("Open notification inbox", _environment.ShowNotifications)); PageContent.Children.Add(Card(alerts, "notifications"));
    }
    private void BuildPrivacy()
    {
        var recent = Group("Recent apps & searches", "History stays in your local Nexus settings.");
        recent.Children.Add(Toggle("Remember recent items", "Turning this off also clears stored recent references.", () => _state.RememberRecentItems, v => { _state.RememberRecentItems = v; if (!v) { _state.RecentApps.Clear(); _state.RecentCommands.Clear(); } }));
        recent.Children.Add(BoundText(() => _state.RecentApps.Count + " recent apps · " + _state.RecentCommands.Count + " recent references"));
        recent.Children.Add(AsyncButton("Clear recent history…", async () => { if (await ConfirmAsync("Clear recent history?", "Remove recent app and search references? Pinned apps and saved content stay in place.", "Clear history")) { _state.RecentApps.Clear(); _state.RecentCommands.Clear(); SaveState(); ShowStatus("Recent history cleared."); } }));
        PageContent.Children.Add(Card(recent, "recent"));
        var usage = Group("Usage tracking"); usage.Children.Add(Toggle("Track app usage locally", "Keep usage totals for apps while the desktop session is active.", () => _state.UsageTracking, v => { _state.UsageTracking = v; _environment.ResetUsageSample(); }));
        usage.Children.Add(BoundText(() => _state.UsageSeconds.Count + " apps with usage totals · " + _state.Activity.Count + " activity entries"));
        usage.Children.Add(AsyncButton("Clear usage & activity…", async () => { if (await ConfirmAsync("Clear usage and activity?", "Delete stored usage totals and activity entries? Your files, notes and saved content stay in place.", "Clear activity")) { _state.UsageSeconds.Clear(); _state.Activity.Clear(); _environment.ResetUsageSample(); SaveState(); ShowStatus("Usage and activity cleared."); } })); PageContent.Children.Add(Card(usage, "usage"));
        var clipboard = Group("Clipboard shelf", "Optional text history is kept in session memory, never written to Nexus settings.");
        clipboard.Children.Add(Toggle("Capture copied text", "Capture starts only when enabled and pauses when the session is locked.", () => _state.ClipboardShelf, _environment.SetClipboardShelf, savesItself: true));
        clipboard.Children.Add(BoundText(() => _environment.ClipboardStatus + " · " + _environment.Clipboard.Items.Count + " retained entries"));
        clipboard.Children.Add(ActionButton("Clear captured text", () => { _environment.ClearClipboardShelf(); RefreshBindings(); ShowStatus("Nexus clipboard history cleared."); })); PageContent.Children.Add(Card(clipboard, "clipboard"));
        var saved = Group("Your saved content", "Existing notes and saved workspace data are retained when upgrading to Nexus Controls.");
        saved.Children.Add(ActionButton("Open Notes", () => _environment.OpenTarget("nexus:notes"))); saved.Children.Add(ActionButton("Open local data folder", OpenDataFolder)); PageContent.Children.Add(Card(saved));
    }
    private async Task<bool> ConfirmAsync(string title, string detail, string action)
    {
        if (!_ready || _dialogOpen || _validation) return false; _dialogOpen = true;
        try
        {
            var dialog = new ContentDialog { XamlRoot = DesktopRoot.XamlRoot, RequestedTheme = DesktopRoot.RequestedTheme, Title = title, Content = detail,
                PrimaryButtonText = action, CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
            PolishDialog(dialog); return await dialog.ShowAsync() == ContentDialogResult.Primary && _ready;
        }
        finally { _dialogOpen = false; }
    }
    private void PolishDialog(ContentDialog dialog)
    {
        dialog.Background = _environment.Theme.Brush("NexusPanel"); dialog.Foreground = _environment.Theme.Brush("NexusText");
        dialog.BorderBrush = _environment.Theme.Edge; dialog.CornerRadius = new CornerRadius(_highContrast ? 0 : 16);
        dialog.PrimaryButtonStyle = (Style)Application.Current.Resources["AuraPrimaryButton"];
        dialog.CloseButtonStyle = (Style)Application.Current.Resources["AuraSurfaceButton"];
    }
}
