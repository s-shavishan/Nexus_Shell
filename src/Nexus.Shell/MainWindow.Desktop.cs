using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Interop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using System.Diagnostics;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    private DesktopIntegration? _desktopIntegration;
    private bool _explicitExit, _desktopWindowsBusy, _launchingWorkspace;
    private IReadOnlyList<RunningWindow> _desktopWindows = [];
    private bool _syncingDesktopSwitches;
    private WorkspaceProfile ActiveProfile => _state.Profiles.First(p => p.Id == _state.ActiveProfileId);

    private void InitializeDesktop()
    {
        DesktopWorkspace.Normalize(_state);
        DesktopLayoutSwitch.IsOn = _state.DesktopLayout;
        GlobalShortcutSwitch.IsOn = _state.GlobalShortcut;
        ResidentSwitch.IsOn = _state.KeepAvailable;
        ResumeWorkspaceSwitch.IsOn = _state.ResumeWorkspace;
        RenderProfileStrip();
    }
    private void InitializeDesktopIntegration()
    {
        try
        {
            _desktopIntegration = new DesktopIntegration(_handle, action => DispatcherQueue.TryEnqueue(() =>
            {
                if (!_ready) return;
                try
                {
                if (action == "exit") ExitNexus();
                else if (action == "tray-lost")
                {
                    _desktopIntegration?.SetResident(false);
                    _state.KeepAvailable = false;
                    _syncingDesktopSwitches = true; ResidentSwitch.IsOn = false; _syncingDesktopSwitches = false;
                    ShowNexus(); SaveState(); ShowStatus("The notification-area icon is unavailable. Nexus is visible again.");
                }
                else
                {
                    ShowNexus();
                    if (action == "search" && !_commandOpen) OpenCommands();
                }
                }
                catch (Exception ex) { Error("Could not use the desktop control", ex); }
            }));
            _appWindow.Closing += AppWindow_Closing;
            if (!_desktopIntegration.SetHotkey(_state.GlobalShortcut))
                Log.Write("Ctrl+Alt+Space is already assigned or unavailable. Local Ctrl+K remains available.");
            if (!_desktopIntegration.SetResident(_state.KeepAvailable))
            {
                _desktopIntegration.SetResident(false);
                _state.KeepAvailable = false;
                _syncingDesktopSwitches = true; ResidentSwitch.IsOn = false; _syncingDesktopSwitches = false;
                SaveState();
                ShowStatus("The notification-area icon could not be created. Nexus will close normally.");
            }
        }
        catch (Exception ex)
        {
            Log.Write("Optional desktop controls unavailable; window remains usable", ex);
            try { _desktopIntegration?.Dispose(); } catch (Exception cleanup) { Log.Write("Desktop control cleanup failed", cleanup); }
            _desktopIntegration = null;
            ResidentSwitch.IsEnabled = false; GlobalShortcutSwitch.IsEnabled = false;
        }
        RefreshDesktopControls();
    }
    private void RefreshDesktopControls()
    {
        DesktopShortcutStatus.Text = _desktopIntegration?.HotkeyAvailable == true
            ? "Ctrl+Alt+Space opens search from your other apps."
            : _state.GlobalShortcut ? "Global shortcut unavailable. Use Ctrl+K inside Nexus or open Nexus again."
            : "Global shortcut is off. Ctrl+K still works inside Nexus.";
    }
    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_explicitExit && _state.KeepAvailable && _desktopIntegration?.TrayAvailable == true && !_dialogOpen && !_picking)
        {
            args.Cancel = true;
            HideNexus();
        }
    }
    private void ShowNexus()
    {
        _appWindow.Show();
        if (_appWindow.Presenter is OverlappedPresenter presenter && presenter.State == OverlappedPresenterState.Minimized) presenter.Restore();
        Activate();
        NativeMethods.Activate(_handle);
        UpdateClock(true);
    }
    private void HideNexus()
    {
        if (_dialogOpen || _picking) return;
        if (_commandOpen) CloseCommands();
        ControlsFlyout.Hide(); SaveState();
        if (_state.KeepAvailable && _desktopIntegration?.TrayAvailable == true) _appWindow.Hide();
        else Minimize();
        _uiTimer.Stop(); _motion?.SetEnabled(false); _isActive = false;
        _desktopWindows = []; DockRunningApps.Children.Clear();
    }
    private void ExitNexus()
    {
        _explicitExit = true;
        Close();
    }
    private void HideNexus_Click(object sender, RoutedEventArgs args) => HideNexus();
    private void DesktopLayout_Toggled(object sender, RoutedEventArgs args)
    {
        if (!_ready || _syncingPersonalization) return;
        _state.DesktopLayout = DesktopLayoutSwitch.IsOn;
        ApplyWidgetLayout(); ApplyEffects(); SaveState();
    }
    private void GlobalShortcut_Toggled(object sender, RoutedEventArgs args)
    {
        if (!_ready || _syncingDesktopSwitches) return;
        _state.GlobalShortcut = GlobalShortcutSwitch.IsOn;
        if (_desktopIntegration?.SetHotkey(_state.GlobalShortcut) == false && _state.GlobalShortcut)
            ShowStatus("Ctrl+Alt+Space is already assigned or unavailable. Ctrl+K still works inside Nexus.");
        RefreshDesktopControls(); SaveState();
    }
    private void Resident_Toggled(object sender, RoutedEventArgs args)
    {
        if (!_ready || _syncingDesktopSwitches) return;
        bool enabled = ResidentSwitch.IsOn;
        if (_desktopIntegration is null || !_desktopIntegration.SetResident(enabled))
        {
            _desktopIntegration?.SetResident(false);
            _syncingDesktopSwitches = true; ResidentSwitch.IsOn = false; _syncingDesktopSwitches = false;
            _state.KeepAvailable = false;
            ShowStatus("The notification-area icon is unavailable. Nexus will close normally."); SaveState(); return;
        }
        _state.KeepAvailable = enabled;
        ShowStatus(enabled ? "Nexus stays available when you close its window. Right-click its notification-area icon to exit."
            : "Closing the Nexus window will exit the app.");
        SaveState();
    }
    private void ResumeWorkspace_Toggled(object sender, RoutedEventArgs args)
    {
        if (!_ready) return;
        _state.ResumeWorkspace = ResumeWorkspaceSwitch.IsOn; SaveState();
    }
    private void ApplyDesktopLayout()
    {
        bool desktop = IsDesktopCanvas;
        WindowChrome.Visibility = WindowFooter.Visibility = desktop ? Visibility.Collapsed : Visibility.Visible;
        ChromeRow.Height = new GridLength(desktop ? 0 : 44);
        FooterRow.Height = new GridLength(desktop ? 0 : 30);
        HomeBorder.MaxWidth = desktop || _expanded ? double.PositiveInfinity : 1180;
        HomeBorder.BorderThickness = new Thickness(desktop ? 0 : 1);
        HomeBorder.Background = desktop ? _transparent : _highContrast ? Resource("NexusPanel")
            : _state.ReducedEffects ? _solidPanel : (Brush?)_auraGlass ?? Resource("NexusShell");
        if (desktop)
        {
            Sidebar.Visibility = Visibility.Collapsed; SidebarColumn.Width = new GridLength(0);
            HomeBorder.Shadow = null; HomeBorder.Translation = new(0, 0, 0);
        }
        HomeView.Visibility = _page == "Home" && !desktop ? Visibility.Visible : Visibility.Collapsed;
        DesktopCanvas.Visibility = desktop ? Visibility.Visible : Visibility.Collapsed;
        UpdateDesktopCanvasLayout();
        HomeGreeting.Visibility = HomeWorkspacesButton.Visibility = desktop ? Visibility.Collapsed : Visibility.Visible;
        HomeWindowsSummary.Foreground = HomeRecentText.Foreground = Resource(desktop ? "NexusDesktopMuted" : "NexusMuted");
        HomeOverviewButton.Foreground = Resource(desktop ? "NexusDesktopText" : "NexusText");
    }
    private void UpdateHomeColumns()
    {
        bool wide = PageHost.ActualWidth >= 780;
        HomeCards.ColumnSpacing = wide ? 16 : 0;
        HomePrimaryColumn.Width = new GridLength(1, GridUnitType.Star);
        HomeSecondaryColumn.Width = new GridLength(wide ? 1 : 0, GridUnitType.Star);
        HomeEssentialsCard.Visibility = _state.ShowHomeEssentials ? Visibility.Visible : Visibility.Collapsed;
        HomeNotesCard.Visibility = _state.ShowHomeNotes ? Visibility.Visible : Visibility.Collapsed;
        var cards = new[] { HomeEssentialsCard, HomeTaskCard, HomeFavoritesCard, HomeNotesCard }
            .Where(card => card.Visibility == Visibility.Visible).ToArray();
        int columns = wide ? 2 : 1;
        HomeCards.RowDefinitions.Clear();
        for (int row = 0; row < (cards.Length + columns - 1) / columns; row++)
            HomeCards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int index = 0; index < cards.Length; index++)
        {
            Grid.SetRow(cards[index], index / columns); Grid.SetColumn(cards[index], index % columns);
        }
        ProfileHeroActions.Orientation = PageHost.ActualWidth < 600 ? Orientation.Vertical : Orientation.Horizontal;
        LayoutProfileStrip();
    }
    private void RenderDesktopIdentity()
    {
        RefreshDesktopSpaceShortcuts();
        var profile = ActiveProfile;
        HeroTitle.Text = profile.Id == "personal" ? "Your day, your space." : profile.Name + ". A space to begin.";
        HeroDescription.Text = profile.Description;
        UpdatePageTrail();
        ProfileOpenButton.Content = profile.Apps.Count + profile.SavedItemIds.Count == 0 ? "Set up workspace" : "Open " + profile.Name;
        ProfileOpenButton.IsEnabled = !_launchingWorkspace;
        ProfileDestinationButton.Content = profile.Page == "Home" ? "Your apps" : "Go to " + profile.Page;
        ProfileSummary.Text = profile.Apps.Count + " apps · " + profile.SavedItemIds.Count + " saved items";
        RenderProfileStrip();
        RenderDesktopCanvas();
    }
    private void RenderProfileStrip()
    {
        ProfileStrip.Children.Clear();
        foreach (var profile in _state.Profiles)
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            bool selected = profile.Id == _state.ActiveProfileId;
            var glyph = Glyph(profile.Glyph, 18); var label = Text(profile.Name, 13);
            glyph.Foreground = label.Foreground = Resource(_highContrast && selected ? "NexusAccentText" : "NexusText");
            content.Children.Add(glyph); content.Children.Add(label);
            var button = new Button { Content = content, HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(18), Padding = new Thickness(16, 14, 16, 14),
                Background = selected && !(_page == "Home" && _state.DesktopLayout && !_highContrast) ? _selection : Resource("NexusCard"),
                BorderBrush = profile.Id == _state.ActiveProfileId ? Resource("NexusAccent") : Resource("NexusBorder"), BorderThickness = new Thickness(1) };
            button.Click += (_, _) => EnterProfile(profile.Id);
            ToolTipService.SetToolTip(button, profile.Description);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, "Enter " + profile.Name + " workspace");
            ProfileStrip.Children.Add(button);
        }
        LayoutProfileStrip();
    }
    private void LayoutProfileStrip()
    {
        int columns = PageHost.ActualWidth < 520 ? 1 : PageHost.ActualWidth < 1000 ? 2 : 4;
        int rows = Math.Max(1, (ProfileStrip.Children.Count + columns - 1) / columns);
        ProfileStrip.ColumnDefinitions.Clear(); ProfileStrip.RowDefinitions.Clear();
        for (int i = 0; i < columns; i++) ProfileStrip.ColumnDefinitions.Add(new ColumnDefinition());
        for (int i = 0; i < rows; i++) ProfileStrip.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int i = 0; i < ProfileStrip.Children.Count; i++)
        { Grid.SetRow((FrameworkElement)ProfileStrip.Children[i], i / columns); Grid.SetColumn((FrameworkElement)ProfileStrip.Children[i], i % columns); }
    }
    private void EnterProfile(string id)
    {
        if (!_state.Profiles.Any(p => p.Id == id)) return;
        if (_launchingWorkspace && ActiveProfile.Id != id) return;
        _state.ActiveProfileId = id;
        SaveState(); RefreshDock(); Navigate("Home");
        ShowStatus(ActiveProfile.Name + " workspace selected. Open it when you’re ready.");
    }
    private void ProfileDestination_Click(object sender, RoutedEventArgs args) => Navigate(ActiveProfile.Page == "Home" ? "Apps" : ActiveProfile.Page);
    private async void ProfileEdit_Click(object sender, RoutedEventArgs args)
    {
        try { await EditProfileAsync(ActiveProfile); }
        catch (Exception ex) { Error("Could not configure this workspace", ex); }
    }
    private async void ProfileOpen_Click(object sender, RoutedEventArgs args)
    {
        try
        {
            if (ActiveProfile.Apps.Count + ActiveProfile.SavedItemIds.Count == 0) await EditProfileAsync(ActiveProfile);
            else await OpenProfileAsync(ActiveProfile);
        }
        catch (Exception ex) { if (_ready) Error("Could not open the workspace", ex); else Log.Write("Workspace opening stopped", ex); }
    }
    private void BuildProfiles()
    {
        PageContent.Children.Add(Text("A place for every part of your day.", 28));
        PageContent.Children.Add(Text("Choose the apps and saved items you want together. Entering a workspace changes your desktop; Open asks before launching anything.", 13, true));
        foreach (var profile in _state.Profiles)
        {
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(Text(profile.Name, 21)); panel.Children.Add(Text(profile.Description, 13, true));
            panel.Children.Add(Text(profile.Apps.Count + " apps · " + profile.SavedItemIds.Count + " saved items", 12, true));
            var actions = new StackPanel { Orientation = Orientation.Vertical, Spacing = 7 };
            actions.Children.Add(ActionButton(profile.Id == _state.ActiveProfileId ? "Current workspace" : "Enter workspace", () => EnterProfile(profile.Id)));
            actions.Children.Add(AsyncButton("Open workspace…", () => OpenProfileAsync(profile)));
            actions.Children.Add(AsyncButton("Configure…", () => EditProfileAsync(profile)));
            actions.Children.Add(AsyncButton("Remove workspace…", () => RemoveProfileAsync(profile)));
            panel.Children.Add(actions); PageContent.Children.Add(Card(panel));
        }
        if (_state.Profiles.Count < DesktopWorkspace.MaximumProfiles)
            PageContent.Children.Add(AsyncButton("Create workspace…", () => EditProfileAsync(new(Guid.NewGuid().ToString("N"), "New workspace", "", "Home", "\uE8F1", [], []), true)));
        PageStatus.Text = "Up to 8 workspaces · Your existing notes and study timer stay shared";
    }
    private async Task EditProfileAsync(WorkspaceProfile profile, bool create = false)
    {
        if (_dialogOpen || _launchingWorkspace) return;
        _dialogOpen = true;
        try
        {
            var name = new TextBox { Header = "Workspace name", Text = profile.Name, MaxLength = 32 };
            var description = new TextBox { Header = "Description", Text = profile.Description, MaxLength = 160 };
            var destination = new ComboBox { Header = "Workspace destination", HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = new[] { "Home", "Study", "Explore", "Apps", "Gaming", "PC controls" }, SelectedItem = profile.Page };
            var apps = new ListView { ItemsSource = profile.Apps.Concat(_orderedCatalog).DistinctBy(a => a.Target, StringComparer.OrdinalIgnoreCase)
                .OrderBy(a => a.Name).ToArray(), DisplayMemberPath = "Name", SelectionMode = ListViewSelectionMode.Multiple,
                MaxHeight = 170, MinHeight = 80 };
            foreach (var app in ((IEnumerable<AppEntry>)apps.ItemsSource).Where(a => profile.Apps.Any(p => p.Target.Equals(a.Target, StringComparison.OrdinalIgnoreCase))))
                apps.SelectedItems.Add(app);
            var saved = new ListView { ItemsSource = _state.SavedItems.ToArray(), DisplayMemberPath = "Title",
                SelectionMode = ListViewSelectionMode.Multiple, MaxHeight = 140, MinHeight = 60 };
            foreach (var item in _state.SavedItems.Where(s => profile.SavedItemIds.Contains(s.Id))) saved.SelectedItems.Add(item);
            var validation = Text("", 12); validation.Foreground = Resource("NexusAccent");
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(name); panel.Children.Add(description); panel.Children.Add(destination);
            panel.Children.Add(Text("Apps · Choose up to 8", 13)); panel.Children.Add(apps);
            panel.Children.Add(Text("Saved items · Choose up to 8", 13)); panel.Children.Add(saved);
            panel.Children.Add(Text("Add apps in App Library and links or files in Explore. Nothing opens when you save.", 12, true));
            panel.Children.Add(validation);
            var dialog = new ContentDialog { XamlRoot = DesktopRoot.XamlRoot, RequestedTheme = DesktopRoot.RequestedTheme,
                Title = create ? "Create a workspace" : "Configure " + profile.Name, PrimaryButtonText = "Save", CloseButtonText = "Cancel",
                Content = new ScrollViewer { Content = panel, MaxHeight = Math.Max(180, Math.Min(520, DesktopRoot.ActualHeight - 210)),
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } };
            dialog.PrimaryButtonClick += (_, args) =>
            {
                if (string.IsNullOrWhiteSpace(name.Text) || apps.SelectedItems.Count > 8 || saved.SelectedItems.Count > 8)
                { validation.Text = "Enter a name and choose at most 8 apps and 8 saved items."; args.Cancel = true; }
            };
            PolishDialog(dialog);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || !_ready) return;
            var updated = profile with { Name = name.Text.Trim(), Description = description.Text.Trim(), Page = (string)destination.SelectedItem,
                Apps = apps.SelectedItems.Cast<AppEntry>().ToList(), SavedItemIds = saved.SelectedItems.Cast<SavedItem>().Select(s => s.Id).ToList() };
            if (create) _state.Profiles.Add(updated);
            else { int index = _state.Profiles.FindIndex(p => p.Id == profile.Id); if (index >= 0) _state.Profiles[index] = updated; }
            DesktopWorkspace.Normalize(_state); SaveState(); RefreshDock(); RefreshHome();
            if (_page == "Workspaces") Navigate("Workspaces", false);
            ShowStatus(updated.Name + " workspace saved.");
        }
        catch (Exception ex) { Error("Could not configure the workspace", ex); }
        finally { _dialogOpen = false; }
    }
    private async Task RemoveProfileAsync(WorkspaceProfile profile)
    {
        if (_dialogOpen || _state.Profiles.Count <= 1 || _launchingWorkspace)
        { ShowStatus("Keep at least one workspace."); return; }
        _dialogOpen = true;
        try
        {
            var dialog = new ContentDialog { XamlRoot = DesktopRoot.XamlRoot, RequestedTheme = DesktopRoot.RequestedTheme,
                Title = "Remove " + profile.Name + "?", Content = "This removes the workspace preset. Your apps, saved items, tasks and notes stay available.",
                PrimaryButtonText = "Remove", CloseButtonText = "Keep", DefaultButton = ContentDialogButton.Close };
            PolishDialog(dialog);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || !_ready) return;
            _state.Profiles.RemoveAll(p => p.Id == profile.Id); DesktopWorkspace.Normalize(_state);
            SaveState(); RefreshDock(); RefreshHome(); Navigate("Workspaces", false);
        }
        finally { _dialogOpen = false; }
    }
    private async Task OpenProfileAsync(WorkspaceProfile profile)
    {
        if (_dialogOpen || _launchingWorkspace) return;
        var plan = DesktopWorkspace.Plan(profile, _state.SavedItems);
        if (plan.Length == 0) { await EditProfileAsync(profile); return; }
        _dialogOpen = true;
        try
        {
            var preview = new StackPanel { Spacing = 10 };
            preview.Children.Add(Text("Open these items with Windows? Apps may create another window if they are already open.", 13, true));
            foreach (var item in plan) preview.Children.Add(Text("• " + item.Title, 14));
            var dialog = new ContentDialog { XamlRoot = DesktopRoot.XamlRoot, RequestedTheme = DesktopRoot.RequestedTheme,
                Title = "Open " + profile.Name, PrimaryButtonText = "Open " + plan.Length + " items", CloseButtonText = "Cancel",
                Content = new ScrollViewer { Content = preview, MaxHeight = Math.Max(120, Math.Min(400, DesktopRoot.ActualHeight - 240)),
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } };
            PolishDialog(dialog);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || !_ready) return;
        }
        finally { _dialogOpen = false; }
        EnterProfile(profile.Id);
        _launchingWorkspace = true; ProfileOpenButton.IsEnabled = false;
        int requested = 0; var failures = new List<string>();
        try
        {
            foreach (var item in plan)
            {
                if (!_ready) break;
                try
                {
                    if (item.Kind == "App") AppCatalog.Launch(new("workspace", item.Title, item.Target, "\uE8A5"));
                    else
                    {
                        if (item.Kind == "Link")
                        {
                            if (!Uri.TryCreate(item.Target, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
                                throw new InvalidDataException("Invalid web address.");
                        }
                        else if (!File.Exists(item.Target) && !Directory.Exists(item.Target)) throw new FileNotFoundException("Item moved or unavailable.");
                        Process.Start(new ProcessStartInfo(item.Target) { UseShellExecute = true });
                    }
                    requested++;
                }
                catch (Exception ex) { failures.Add(item.Title); Log.Write("Workspace item unavailable: " + item.Title, ex); }
                await Task.Delay(120); // Let native launches and the UI message loop progress.
            }
            if (_ready)
            {
                Record("Opened workspace · " + profile.Name + " · " + requested + " requests"); SaveState();
                // Launching can take focus away from Nexus; leave the desktop ready for a later return.
                Navigate(profile.Page);
                ShowStatus(failures.Count == 0 ? profile.Name + " is ready. Requested " + requested + " opens."
                    : "Requested " + requested + " opens. Unavailable: " + string.Join(", ", failures), failures.Count > 0);
            }
        }
        finally { _launchingWorkspace = false; if (_ready) ProfileOpenButton.IsEnabled = true; }
    }
    private async Task RefreshDesktopWindowsAsync()
    {
        if (_desktopWindowsBusy || !_ready || !_isActive) return;
        _desktopWindowsBusy = true;
        try
        {
            var windows = await Task.Run(() => NativeMethods.RunningWindows(_handle));
            if (!_ready || !_isActive) return;
            if (!windows.SequenceEqual(_desktopWindows))
            {
                _desktopWindows = windows; RenderRunningDock();
                if (_commandOpen) RenderCommands(preserveSelection: true);
            }
            if (_page == "Running apps") { _openWindows = windows; FilterWindows(); }
        }
        catch (Exception ex) { Log.Write("Running dock refresh skipped", ex); }
        finally { _desktopWindowsBusy = false; }
    }
    private void RenderRunningDock()
    {
        DockRunningApps.Children.Clear();
        foreach (var window in _desktopWindows.Take(3))
        {
            var face = new StackPanel { Spacing = 4 };
            var tile = new Border { Width = 40, Height = 38, CornerRadius = new CornerRadius(11), Background = Resource("NexusCard") };
            tile.Child = new TextBlock { Text = window.ProcessName.Length > 0 ? window.ProcessName[..1].ToUpperInvariant() : "•",
                FontSize = 17, Foreground = Resource("NexusText"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            face.Children.Add(tile);
            face.Children.Add(new Border { Width = 5, Height = 3, CornerRadius = new CornerRadius(2), Background = Resource("NexusAccent"), HorizontalAlignment = HorizontalAlignment.Center });
            var button = new Button { Content = face, Style = (Style)Application.Current.Resources["DockButton"],
                Background = _transparent, BorderBrush = _transparent };
            button.Click += (_, _) => { if (!NativeMethods.Activate(window.Handle)) ShowStatus("This window is unavailable. Refresh window overview."); };
            ToolTipService.SetToolTip(button, window.ProcessName + " · " + window.Title);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, "Return to " + window.Title);
            DockRunningApps.Children.Add(button);
        }
        ApplyDockDensity();
        HomeWindowsSummary.Text = _desktopWindows.Count + " open windows · Return through the dock or window overview";
    }
}
