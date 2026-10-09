using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Models;
using Nexus.Shell.Services;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    private readonly NavigationTrail _pageTrail = new();
    private readonly List<Action> _personalizationSync = [];
    private readonly List<Button> _moodChoices = [];
    private Grid? _moodGrid, _personalizeSections;
    private bool _syncingPersonalization;
    private string _commandCategory = "All";

    private void Back_Click(object sender, RoutedEventArgs args) => GoBack();
    private void Forward_Click(object sender, RoutedEventArgs args) => GoForward();
    private void GoBack()
    {
        if (!_ready || _dialogOpen || _picking || _launchingWorkspace) return;
        if (_pageTrail.Back() is string page) Navigate(page, remember: false);
    }
    private void GoForward()
    {
        if (!_ready || _dialogOpen || _picking || _launchingWorkspace) return;
        if (_pageTrail.Forward() is string page) Navigate(page, remember: false);
    }
    private void UpdatePageTrail()
    {
        BackButton.IsEnabled = _pageTrail.CanGoBack;
        ForwardButton.IsEnabled = _pageTrail.CanGoForward;
        string pageName = _page == "Home" ? "Overview" : _page == "Running apps" ? "Windows" : _page == "Gaming" ? "Entertainment" : _page;
        ActiveProfileText.Text = pageName + "  /  " + ActiveProfile.Name;
    }
    private void Personalize_Click(object sender, RoutedEventArgs args) => Navigate("Personalize");
    private void CommandCategory_Click(object sender, RoutedEventArgs args)
    {
        if (!_commandOpen) return;
        _commandCategory = (string)((Button)sender).Tag;
        _searchTimer.Stop(); RenderCommands();
        CommandSearchBox.Focus(FocusState.Programmatic);
    }
    private void UpdateCommandCategories()
    {
        foreach (var button in CommandCategories.Children.OfType<Button>())
        {
            bool selected = (string)button.Tag == _commandCategory;
            SetSegment(button, selected);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(button, selected ? "Selected search category" : "");
        }
    }
    private void RememberCommand(CommandEntry entry)
    {
        ShellExperience.Remember(_state, entry);
        SaveState();
    }
    private void ApplyExperiencePreferences()
    {
        RefreshDock(); RenderRunningDock();
        ApplyWidgetLayout(); ApplyEffects(); UpdateClock(true); SaveState();
    }
    private void RefreshPersonalizationControls()
    {
        if (_syncingPersonalization) return;
        _syncingPersonalization = true;
        try
        {
            GlassSwitch.IsOn = _state.NativeGlass; EffectsSwitch.IsOn = _state.ReducedEffects;
            
            foreach (var update in _personalizationSync) update();
        }
        finally { _syncingPersonalization = false; }
    }
    private Grid PersonalizeToggle(string title, Func<bool> get, Action<bool> set, string on = "On", string off = "Off")
    {
        var row = new Grid { ColumnSpacing = 16, MinHeight = 48 };
        row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var label = new StackPanel { Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(Text(title, 14));
        var caption = Text(get() ? on : off, 12, true); label.Children.Add(caption);
        var toggle = new ToggleSwitch { IsOn = get(), OnContent = "", OffContent = "", Width = 50, MinWidth = 50,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(toggle, title);
        Grid.SetColumn(toggle, 1); row.Children.Add(label); row.Children.Add(toggle);
        toggle.Toggled += (_, _) =>
        {
            if (!_ready || _syncingPersonalization) return;
            set(toggle.IsOn); ApplyExperiencePreferences();
        };
        _personalizationSync.Add(() => { toggle.IsOn = get(); caption.Text = get() ? on : off; });
        return row;
    }
    private void BuildPersonalize()
    {
        PageContent.Children.Add(Text("Make Nexus yours.", 30));
        PageContent.Children.Add(Text("Choose your mood, shape the desktop, and keep what matters within reach.", 14, true));
        PageContent.Children.Add(Card(DesktopModeCard()));
        PageContent.Children.Add(Text("DESKTOP MOODS", 11, true));
        _moodGrid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        foreach (string mood in AuraPalette.Moods)
        {
            var palette = AuraPalette.For(mood);
            var content = new StackPanel { Spacing = 12 };
            var swatches = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
            foreach (string color in new[] { palette.Accent, palette.Secondary, palette.Canvas })
                swatches.Children.Add(new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12),
                    Background = _highContrast ? Resource("NexusCard") : new SolidColorBrush(AuraColorValue(color)),
                    BorderBrush = Resource("NexusBorder"), BorderThickness = new Thickness(1) });
            var moodTitle = Text(palette.Name, 17);
            content.Children.Add(swatches); content.Children.Add(moodTitle);
            content.Children.Add(Text(mood == "Solstice" ? "Warm pearl & amber" : mood == "Ember" ? "Warm midnight" : mood == "Opal" ? "Luminous lavender" : mood == "Orbit" ? "Iris & charcoal" : mood == "Aurora" ? "Deep teal" : "Cool blue", 12, true));
            var button = new Button { Tag = mood, Content = content, Style = (Style)Application.Current.Resources["AuraSurfaceButton"],
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(20), CornerRadius = new CornerRadius(20) };
            button.Click += (_, _) =>
            {
                _state.Wallpaper = mood;
                WallpaperBox.SelectedIndex = Array.IndexOf(AuraPalette.Moods, mood);
                SelectWallpaper(); ApplyEffects(); SaveState();
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, "Use " + palette.Name + " mood");
            _moodChoices.Add(button); _moodGrid.Children.Add(button);
            _personalizationSync.Add(() =>
            {
                bool selected = _state.Wallpaper == mood;
                button.BorderBrush = Resource(selected ? "NexusAccent" : "NexusBorder");
                button.Background = Resource("NexusCard");
                moodTitle.Text = palette.Name + (selected ? " · Current" : "");
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(button, selected ? "Current desktop mood" : "");
            });
        }
        PageContent.Children.Add(_moodGrid);
        var desktop = new StackPanel { Spacing = 18 };
        desktop.Children.Add(Text("Your desktop", 21));
        desktop.Children.Add(Text("Choose a mood for your desktop and taskbar. Open Sections from its desktop icon; your desktop stays available when this window closes.", 13, true));
        desktop.Children.Add(PersonalizeToggle("Overview app card", () => _state.ShowHomeEssentials, v => _state.ShowHomeEssentials = v));
        desktop.Children.Add(PersonalizeToggle("Overview note card", () => _state.ShowHomeNotes, v => _state.ShowHomeNotes = v));
        var interfacePanel = new StackPanel { Spacing = 18 };
        interfacePanel.Children.Add(Text("Feel & interaction", 21));
        interfacePanel.Children.Add(PersonalizeToggle("Native glass", () => _state.NativeGlass, v => _state.NativeGlass = v,
            "Windows acrylic", "Layered surfaces"));
        interfacePanel.Children.Add(PersonalizeToggle("Reduced effects", () => _state.ReducedEffects, v => _state.ReducedEffects = v));
        interfacePanel.Children.Add(PersonalizeToggle("Floating taskbar", () => _state.FloatingTaskbar, v => _state.FloatingTaskbar = v));
        interfacePanel.Children.Add(PersonalizeToggle("Compact taskbar", () => _state.CompactDock, v => _state.CompactDock = v,
            "Compact taskbar height", "Comfortable taskbar height"));
        var format = new ComboBox { Header = "Clock format", HorizontalAlignment = HorizontalAlignment.Stretch };
        format.Items.Add("24-hour · 14:56"); format.Items.Add("12-hour · 2:56 PM");
        format.SelectedIndex = _state.Clock24Hour ? 0 : 1;
        format.SelectionChanged += (_, _) =>
        {
            if (_syncingPersonalization || !_ready) return;
            _state.Clock24Hour = format.SelectedIndex == 0; UpdateClock(true); SaveState();
        };
        _personalizationSync.Add(() => format.SelectedIndex = _state.Clock24Hour ? 0 : 1);
        interfacePanel.Children.Add(format);
        interfacePanel.Children.Add(Text("Glass follows Windows availability. High contrast and reduced effects use simpler surfaces.", 12, true));
        interfacePanel.Children.Add(ActionButton("Open control center", () => ControlsFlyout.ShowAt(ControlsButton)));
        var search = new StackPanel { Spacing = 14 };
        search.Children.Add(Text("Quick access", 21));
        search.Children.Add(PersonalizeToggle("Remember recent search items", () => _state.RememberRecentItems, v =>
        {
            _state.RememberRecentItems = v;
            if (!v) _state.RecentCommands.Clear();
        }));
        search.Children.Add(Text("Shows your last eight app, saved-item and workspace choices. Saved only on this PC; search text isn’t kept.", 12, true));
        var recentStatus = Text("", 12, true);
        _personalizationSync.Add(() => recentStatus.Text = _state.RecentCommands.Count + " recent items");
        search.Children.Add(recentStatus);
        var clear = ActionButton("Clear recent items", () => { _state.RecentCommands.Clear(); SaveState(); RefreshPersonalizationControls(); });
        _personalizationSync.Add(() => clear.IsEnabled = _state.RecentCommands.Count > 0);
        search.Children.Add(clear);
        search.Children.Add(Text("Alt+← / → changes pages. Ctrl+K opens search. Ctrl+Alt+Space summons it when enabled.", 12, true));
        _personalizeSections = new Grid { ColumnSpacing = 16, RowSpacing = 16 };
        _personalizeSections.Children.Add(Card(desktop)); _personalizeSections.Children.Add(Card(interfacePanel));
        _personalizeSections.Children.Add(Card(search));
        PageContent.Children.Add(_personalizeSections);
        PageContent.Children.Add(ActionButton("Restore appearance defaults", RestoreAppearanceDefaults));
        PageStatus.Text = "Personal settings · saved on this PC";
        UpdateExperienceLayout(); RefreshPersonalizationControls();
    }
    private void RestoreAppearanceDefaults()
    {
        _state.Wallpaper = "Solstice"; _state.NativeGlass = true; _state.ReducedEffects = false;
        _state.DesktopLayout = true; _state.FocusMode = false; _state.Clock24Hour = true;
        _state.ShowClockWidget = true; _state.ShowSpaceWidget = true;
        _state.ShowHomeNotes = true; _state.ShowHomeEssentials = true; _state.CompactDock = false;
        WallpaperBox.SelectedIndex = 0; SelectWallpaper(); ApplyExperiencePreferences();
        ShowStatus("Appearance defaults restored.");
    }
    private void UpdateExperienceLayout()
    {
        if (_moodGrid is not null)
        {
            int columns = PageHost.ActualWidth >= 850 ? 3 : PageHost.ActualWidth >= 520 ? 2 : 1;
            _moodGrid.ColumnDefinitions.Clear(); _moodGrid.RowDefinitions.Clear();
            for (int i = 0; i < columns; i++) _moodGrid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < (_moodChoices.Count + columns - 1) / columns; i++) _moodGrid.RowDefinitions.Add(new() { Height = GridLength.Auto });
            for (int i = 0; i < _moodChoices.Count; i++) { Grid.SetRow(_moodChoices[i], i / columns); Grid.SetColumn(_moodChoices[i], i % columns); }
        }
        if (_personalizeSections is not null)
        {
            bool wide = PageHost.ActualWidth >= 760;
            _personalizeSections.ColumnDefinitions.Clear(); _personalizeSections.RowDefinitions.Clear();
            for (int i = 0; i < (wide ? 2 : 1); i++) _personalizeSections.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < (wide ? 2 : 3); i++) _personalizeSections.RowDefinitions.Add(new() { Height = GridLength.Auto });
            for (int i = 0; i < _personalizeSections.Children.Count; i++)
            {
                var card = (FrameworkElement)_personalizeSections.Children[i];
                Grid.SetRow(card, wide ? i / 2 : i); Grid.SetColumn(card, wide ? i % 2 : 0);
                Grid.SetColumnSpan(card, wide && i == 2 ? 2 : 1);
            }
        }
    }
    
}
