using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Interop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Nexus.Shell.UI;
using System.Diagnostics;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.System;

namespace Nexus.Shell;

public sealed partial class MainWindow : Window
{
    internal const string NativeWindowTitle = "White Dreams Nexus Shell";
    private readonly StateStore _store = new();
    private readonly ShellState _state;
    private readonly UsageTracker _usage;
    private readonly ResourceSampler _resources = new();
    private readonly CancellationTokenSource _discoveryCancellation = new();
    private readonly DispatcherTimer _uiTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer _usageTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(160) };
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly AppWindow _appWindow;
    private readonly IntPtr _handle;
    private readonly Brush _wallpaper;
    private readonly Dictionary<Border, Brush> _surfaceDefaults;
    private readonly Button[] _navigation;
    private readonly Button[] _shortcutCards;
    private MotionController? _motion;
    private List<AppEntry> _catalog = [], _orderedCatalog = [];
    private string _page = "", _lastClockMinute = "";
    private bool _ready, _isActive, _expanded, _discovering, _catalogReady;
    private bool _controlsOpen, _picking, _dialogOpen, _dirty, _saveInFlight;
    private bool _highContrast, _animationsEnabled, _appearanceReadFailed;
    private int _usageTicks, _dockCapacity = 5;
    private readonly SolidColorBrush _selection = MakeBrush(190, 171, 248, 40);
    private readonly SolidColorBrush _solidPanel = MakeBrush(25, 32, 51), _solidCard = MakeBrush(37, 45, 69);
    private readonly SolidColorBrush _transparent = MakeBrush(0, 0, 0, 0);

    public MainWindow()
    {
        Log.Write("Loading MainWindow.xaml");
        InitializeComponent();
        Log.Write("MainWindow.xaml loaded; configuring window");
        _state = _store.Load();
        _usage = new UsageTracker(_state);
        Log.Write("Reading desktop accessibility settings");
        RefreshSystemAppearance();
        Log.Write($"Desktop settings ready; high contrast: {_highContrast}; animations: {_animationsEnabled}");
        if (!_state.CatalogInitialized)
        {
            _state.PinnedApps = AppCatalog.Defaults();
            _state.CatalogInitialized = true;
            _dirty = true;
        }
        _navigation = [TopNavHome, TopNavProfiles, TopNavExplore, TopNavStudy, TopNavApps, NavPersonalize, NavProfiles, NavHome, NavExplore, NavStudy, NavApps, NavGames, NavActivity, NavRunning];
        _shortcutCards = [FilesCard, GamesCard, FocusCard];
        _wallpaper = DesktopRoot.Background;
        _surfaceDefaults = new()
        {
            [HomeBorder] = HomeBorder.Background, [SpaceCard] = SpaceCard.Background,
            [MenuBar] = MenuBar.Background, [DockBorder] = DockBorder.Background,
            [WindowChrome] = WindowChrome.Background, [WindowFooter] = WindowFooter.Background
        };
        _handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_handle));
        _appWindow.Title = NativeWindowTitle;
        var workArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        int initialWidth = Math.Max(320, Math.Min(1440, workArea.Width - 48));
        int initialHeight = Math.Max(420, Math.Min(940, workArea.Height - 48));
        _appWindow.MoveAndResize(new RectInt32(workArea.X + (workArea.Width - initialWidth) / 2,
            workArea.Y + (workArea.Height - initialHeight) / 2, initialWidth, initialHeight));
        if (_appWindow.Presenter is OverlappedPresenter presenter) presenter.SetBorderAndTitleBar(true, false);
        var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "Nexus.ico");
        if (File.Exists(icon)) _appWindow.SetIcon(icon);
        DisplayNameBox.Text = _state.DisplayName;
        ExploreWorkspace.Normalize(_state);
        InitializeWorkspaces();
        InitializeDesktop();
        GlassSwitch.IsOn = _state.NativeGlass;
        TrackingSwitch.IsOn = _state.UsageTracking;
        FocusSwitch.IsOn = _state.FocusMode;
        EffectsSwitch.IsOn = _state.ReducedEffects;
        try { StartupSwitch.IsOn = StartupRegistration.IsEnabled(); RefreshStartupRepair(); }
        catch (Exception ex) { Log.Write("Could not inspect login startup", ex); StartupSwitch.IsEnabled = false; }

        AddAccelerator(VirtualKey.Escape, VirtualKeyModifiers.None, () =>
        {
            if (_commandOpen) CloseCommands();
            else if (_controlsOpen) ControlsFlyout.Hide();
            else if (_state.FullScreen) SetFullScreen(false);
            else Minimize();
        });
        AddAccelerator(VirtualKey.F11, VirtualKeyModifiers.None, () => SetFullScreen(!_state.FullScreen));
        AddAccelerator(VirtualKey.K, VirtualKeyModifiers.Control, SearchApps);
        AddAccelerator(VirtualKey.N, VirtualKeyModifiers.Control, NewExploreNote);
        AddAccelerator(VirtualKey.Number2, VirtualKeyModifiers.Control, () => Navigate("Explore"));
        AddAccelerator(VirtualKey.Number3, VirtualKeyModifiers.Control, () => Navigate("Study"));
        AddAccelerator(VirtualKey.Number1, VirtualKeyModifiers.Control, () => Navigate("Home"));
        AddAccelerator(VirtualKey.Number4, VirtualKeyModifiers.Control, () => Navigate("Running apps"));
        AddAccelerator(VirtualKey.Left, VirtualKeyModifiers.Menu, GoBack);
        AddAccelerator(VirtualKey.Right, VirtualKeyModifiers.Menu, GoForward);
        _uiTimer.Tick += Ui_Tick;
        _usageTimer.Tick += Usage_Tick;
        _searchTimer.Tick += Search_Tick;
        _saveTimer.Tick += Save_Tick;
        Activated += Window_Activated;
        Closed += Window_Closed;
        _ready = true;
        RebuildCatalog();
        UpdateClock(true);
        RefreshDock();
        Navigate(_state.ResumeWorkspace ? _state.LastPage : "Home", animate: false);
        ApplyWidgetLayout();
        ApplyEffects();
        if (_state.FullScreen) SetFullScreen(true);
        if (_state.UsageTracking) _usageTimer.Start();
        if (_dirty) SaveState();
        InitializeDesktopIntegration();
        if (_store.RecoveryMessage.Length > 0) ShowStatus(_store.RecoveryMessage, true);
        // No Start-menu scan until the app library is opened.
    }

    private void AddAccelerator(VirtualKey key, VirtualKeyModifiers modifiers, Action action)
    {
        var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
        accelerator.Invoked += (_, args) => { if (_ready && !_dialogOpen && !_picking) { action(); args.Handled = true; } };
        DesktopRoot.KeyboardAccelerators.Add(accelerator);
    }
    private static SolidColorBrush MakeBrush(byte r, byte g, byte b, byte alpha = 255) => new(Windows.UI.Color.FromArgb(alpha, r, g, b));
    private Brush Resource(string key)
    {
        var theme = _highContrast ? "HighContrast" : "Dark";
        return (Brush)((ResourceDictionary)Application.Current.Resources.ThemeDictionaries[theme])[key];
    }
    private TextBlock Text(string value, double size = 15, bool muted = false) => new()
    {
        Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap,
        Foreground = Resource(muted ? "NexusMuted" : "NexusText")
    };
    private static FontIcon Glyph(string value, double size = 22) => new()
    {
        Glyph = value, FontSize = size, FontFamily = new FontFamily("Segoe MDL2 Assets")
    };
    private Border Card(UIElement content) => new()
    {
        Child = content, Padding = new Thickness(20), CornerRadius = new CornerRadius(22),
        Background = Resource("NexusCard"), BorderBrush = Resource("NexusBorder"), BorderThickness = new Thickness(1)
    };

    private void Navigate(string page, bool animate = true, bool remember = true)
    {
        if (!_ready) return;
        page = page is "Home" or "Explore" or "Study" or "Apps" or "Gaming" or "Activity" or "Running apps" or "Workspaces" or "Personalize" ? page : "Home";
        bool changed = _page != page || HomeBorder.Visibility != Visibility.Visible;
        _page = page;
        if (remember) _pageTrail.Visit(page);
        UpdatePageTrail();
        if (_state.LastPage != page) { _state.LastPage = page; SaveState(); }
        if (_commandOpen) CloseCommands();
        _searchTimer.Stop();
        ControlsFlyout.Hide();
        HomeBorder.Visibility = Visibility.Visible;
        ReopenHomeButton.Visibility = Visibility.Collapsed;
        HomeView.Visibility = page == "Home" ? Visibility.Visible : Visibility.Collapsed;
        AppLibraryView.Visibility = page is "Apps" or "Gaming" ? Visibility.Visible : Visibility.Collapsed;
        WindowOverviewView.Visibility = page == "Running apps" ? Visibility.Visible : Visibility.Collapsed;
        ExploreView.Visibility = page == "Explore" ? Visibility.Visible : Visibility.Collapsed;
        PageScroller.Visibility = page is "Activity" or "Study" or "Workspaces" or "Personalize" ? Visibility.Visible : Visibility.Collapsed;
        if (page != "Running apps") ReleaseWindowOverview();
        if (page != "Explore") ReleaseExploreControls();
        ReleaseWorkspaceControls();
        PageContent.Children.Clear();
        if (page is not ("Apps" or "Gaming"))
        {
            AppsGrid.ItemsSource = null; // release off-page realized library containers
            DiscoveryProgress.IsActive = false;
        }
        PageTitle.Text = page switch { "Home" => "Nexus Home", "Gaming" => "Nexus Games", "Running apps" => "Window overview", _ => page };
        UpdateNavigation();
        foreach (var button in _navigation)
        {
            bool selected = (string)button.Tag == page;
            button.Background = selected ? _selection : _transparent;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(button, selected ? "Current workspace" : "");
        }
        switch (page)
        {
            case "Apps":
            case "Gaming": ShowLibrary(changed); break;
            case "Activity": BuildActivity(); break;
            case "Running apps": ShowWindowOverview(changed); break;
            case "Workspaces": BuildProfiles(); break;
            case "Personalize": BuildPersonalize(); break;
            case "Explore": BuildExplore(); break;
            case "Study": BuildStudy(); break;
            default: RefreshHome(); PageStatus.Text = "Your personal workspace"; break;
        }
        ApplyWidgetLayout();
        if (changed && animate)
            _motion?.Enter(page is "Apps" or "Gaming" ? AppLibraryView : page == "Home" ? HomeView : page == "Running apps" ? WindowOverviewView : page == "Explore" ? ExploreView : PageScroller);
    }

    private void RefreshHome()
    {
        var homeApps = ActiveProfile.Apps.Count > 0 ? ActiveProfile.Apps : _state.PinnedApps;
        HomePins.ItemsSource = homeApps.Take(8).ToArray();
        HomePinsEmpty.Visibility = homeApps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HomePins.Visibility = homeApps.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        HomeRecentText.Text = _state.Activity.FirstOrDefault()?.Message ?? "Ready for your next idea.";
        RefreshWorkspaceSummary();
        RenderHomeWorkspace();
        RenderDesktopIdentity();
    }
    private void RebuildCatalog()
    {
        _orderedCatalog = _state.PinnedApps.Concat(_catalog).DistinctBy(a => a.Target, StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    private void ShowLibrary(bool changed)
    {
        bool gaming = _page == "Gaming";
        LibraryHeading.Text = gaming ? "Your next escape." : "App Library";
        LibraryDescription.Text = gaming
            ? "Add installed games or their shortcuts. Nexus launches them on Windows."
            : "Your Start-menu apps and pins. Right-click an app to make it yours.";
        AddAppButton.Content = gaming ? "Add game…" : "Add…";
        AppsSearchBox.PlaceholderText = gaming ? "Find a game" : "Find an app";
        RefreshAppsButton.Visibility = gaming ? Visibility.Collapsed : Visibility.Visible;
        if (changed) AppsSearchBox.Text = "";
        FilterLibrary();
        DiscoveryProgress.Visibility = !gaming && _discovering ? Visibility.Visible : Visibility.Collapsed;
        DiscoveryProgress.IsActive = !gaming && _discovering;
        if (!gaming && !_catalogReady && !_discovering) _ = DiscoverAsync();
    }
    private void FilterLibrary()
    {
        if (!_ready || _page is not ("Apps" or "Gaming")) return;
        string query = AppsSearchBox.Text.Trim();
        bool gaming = _page == "Gaming";
        var entries = _orderedCatalog.Where(a => (!gaming || a.Category == "Game") &&
            a.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        AppsGrid.ItemsSource = entries;
        LibraryEmpty.Visibility = entries.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        LibraryEmpty.Text = gaming && query.Length == 0 ? "Add your first installed game to begin." :
            _discovering && !gaming ? "Finding your Start-menu apps…" : "No matching apps.";
        LibraryCountText.Text = entries.Length + (gaming ? " games in your space" : " apps in your library");
        PageStatus.Text = _discovering && !gaming ? "Discovering apps…" : entries.Length + (gaming ? " installed games and shortcuts" : " apps · Local library");
    }
    private void AppsSearch_TextChanged(object sender, TextChangedEventArgs args)
    {
        if (!_ready || _page is not ("Apps" or "Gaming")) return;
        _searchTimer.Stop(); _searchTimer.Start();
    }
    private void Search_Tick(object? sender, object args)
    {
        _searchTimer.Stop();
        if (_commandOpen) RenderCommands(); else FilterLibrary();
    }
    private void AppGrid_ItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is AppEntry app) Launch(app);
    }

    private void AppContainer_ContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is not GridViewItem item) return;
        if (args.InRecycleQueue)
        {
            if (item.ContextFlyout is MenuFlyout recycled)
            {
                recycled.Hide();
                recycled.Items.Clear();
            }
            return;
        }
        if (item.ContextFlyout is not null) return;
        // Each realized/recycled container owns one menu, rather than creating
        // an event-bearing Flyout as a shared Style.Setter value during XAML load.
        var menu = new MenuFlyout();
        menu.Opening += AppContext_Opening;
        item.ContextFlyout = menu;
    }

    private void AppContext_Opening(object? sender, object args)
    {
        if (sender is not MenuFlyout menu) return;
        menu.Items.Clear();
        var app = menu.Target?.DataContext as AppEntry;
        if (app is null && menu.Target is GridViewItem item) app = item.Content as AppEntry;
        if (app is null) return;
        var target = app;
        bool pinned = _state.PinnedApps.Any(a => a.Target.Equals(target.Target, StringComparison.OrdinalIgnoreCase));
        var pin = new MenuFlyoutItem { Text = pinned ? "Unpin from Nexus" : "Pin to Nexus" };
        pin.Click += (_, _) =>
        {
            menu.Hide();
            if (pinned) _state.PinnedApps.RemoveAll(a => a.Target.Equals(target.Target, StringComparison.OrdinalIgnoreCase));
            else if (!Pin(target)) return;
            Record((pinned ? "Unpinned " : "Pinned ") + target.Name);
            PinsChanged();
        };
        menu.Items.Add(pin);
        var game = new MenuFlyoutItem { Text = target.Category == "Game" ? "Remove from games" : "Add to games" };
        game.Click += (_, _) =>
        {
            menu.Hide();
            bool wasGame = target.Category == "Game";
            if (!Pin(target with { Category = wasGame ? "App" : "Game" })) return;
            Record((wasGame ? "Removed from games: " : "Added to games: ") + target.Name);
            PinsChanged();
        };
        menu.Items.Add(game);
    }
    private bool Pin(AppEntry app)
    {
        int index = _state.PinnedApps.FindIndex(a => a.Target.Equals(app.Target, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) { _state.PinnedApps[index] = app; return true; }
        if (_state.PinnedApps.Count >= 100) { ShowStatus("Your 100 pins are full. Unpin an app before adding another."); return false; }
        _state.PinnedApps.Add(app);
        return true;
    }
    private void PinsChanged()
    {
        SaveState(); RebuildCatalog(); RefreshDock(); RefreshHome();
        if (_page is "Apps" or "Gaming") FilterLibrary();
    }

    private void BuildActivity()
    {
        PageContent.Children.Add(Text("Your activity", 27));
        PageContent.Children.Add(Text(_state.UsageTracking
            ? "Approximate foreground time while Nexus is open."
            : "Usage tracking is off. Enable it in Control center.", 13, true));
        foreach (var entry in _state.UsageSeconds.OrderByDescending(a => a.Value).Take(12))
            PageContent.Children.Add(Card(Text($"{entry.Key}  ·  {TimeSpan.FromSeconds(entry.Value).TotalMinutes:0.0} min", 15)));
        if (_state.Activity.Count == 0) PageContent.Children.Add(Text("Your launches and setting changes will appear here.", 14, true));
        foreach (var entry in _state.Activity.Take(30))
        {
            var row = new StackPanel { Spacing = 4 };
            row.Children.Add(Text(entry.Message));
            row.Children.Add(Text(entry.Time.ToLocalTime().ToString("dd MMM · HH:mm"), 11, true));
            PageContent.Children.Add(row);
        }
        PageContent.Children.Add(AsyncButton("Clear activity and usage…", ClearActivityAsync));
        PageStatus.Text = "Stored on this PC";
    }
    private Button ActionButton(string title, Action action)
    {
        var button = new Button { Content = Text(title, 14), Style = (Style)Application.Current.Resources["AuraSurfaceButton"] };
        button.Click += (_, _) => { try { action(); } catch (Exception ex) { Error(title, ex); } };
        return button;
    }
    private Button AsyncButton(string title, Func<Task> action)
    {
        var button = new Button { Content = title, Style = (Style)Application.Current.Resources["AuraSurfaceButton"] };
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try { await action(); } catch (Exception ex) { if (_ready) Error(title, ex); else Log.Write(title, ex); }
            finally { if (_ready) button.IsEnabled = true; }
        };
        return button;
    }

    private async Task PickAppAsync()
    {
        if (_picking) return;
        _picking = true; AddAppButton.IsEnabled = false;
        // Capture the page before awaiting a native picker. Do not pull the user
        // back to that page if they navigated elsewhere while it was open.
        string category = _page == "Gaming" ? "Game" : "App";
        try
        {
            var picker = new FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _handle);
            picker.FileTypeFilter.Add(".exe"); picker.FileTypeFilter.Add(".lnk");
            var file = await picker.PickSingleFileAsync();
            if (!_ready || file is null) return;
            var app = AppCatalog.FromFile(file.Path, category);
            if (!Pin(app)) return;
            Record("Added " + app.Name + (category == "Game" ? " to games" : " to Nexus"));
            PinsChanged();
        }
        catch (Exception ex) { if (_ready) Error("Could not add the app", ex); else Log.Write("App picker closed", ex); }
        finally { _picking = false; if (_ready) AddAppButton.IsEnabled = true; }
    }
    private async Task DiscoverAsync()
    {
        if (_discovering || !_ready) return;
        _discovering = true;
        RefreshAppsButton.IsEnabled = false;
        DiscoveryProgress.Visibility = Visibility.Visible; DiscoveryProgress.IsActive = true;
        FilterLibrary();
        try
        {
            var apps = await Task.Run(() => AppCatalog.DiscoverShortcuts(_discoveryCancellation.Token), _discoveryCancellation.Token);
            if (!_ready) return;
            _catalog = apps; _catalogReady = true; RebuildCatalog();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (_ready) Error("Could not discover Start-menu apps", ex); else Log.Write("Discovery stopped", ex); }
        finally
        {
            _discovering = false;
            if (_ready)
            {
                RefreshAppsButton.IsEnabled = true;
                DiscoveryProgress.IsActive = false; DiscoveryProgress.Visibility = Visibility.Collapsed;
                if (_page == "Apps") FilterLibrary();
                if (_commandOpen) RenderCommands(preserveSelection: true);
            }
        }
    }
    private void Launch(AppEntry app)
    {
        try { AppCatalog.Launch(app); Record("Opened " + app.Name); SaveState(); }
        catch (Exception ex) { Error("Could not launch " + app.Name, ex); }
    }
    private void RefreshDock()
    {
        DockApps.Children.Clear();
        var accent = (AppAccentConverter)DesktopRoot.Resources["AppAccent"];
        foreach (var app in (ActiveProfile.Apps.Count > 0 ? ActiveProfile.Apps : _state.PinnedApps).Take(_dockCapacity))
        {
            var button = new Button
            {
                Content = new Border
                {
                    Width = 48, Height = 48, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1),
                    BorderBrush = Resource("NexusBorder"), Child = new FontIcon { Glyph = app.Glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), Foreground = Resource("NexusDesktopText"), FontSize = 22 },
                    Background = (Brush)accent.Convert(app.Category, typeof(Brush), "", "")
                },
                Style = (Style)Application.Current.Resources["DockButton"],
                Background = _transparent, BorderBrush = _transparent
            };
            button.Click += (_, _) => Launch(app);
            ToolTipService.SetToolTip(button, app.Name);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, "Open " + app.Name);
            DockApps.Children.Add(button); _motion?.AttachHover(button);
        }
        ApplyDockDensity();
    }
    private void Record(string message, bool updateUi = true)
    {
        _state.Activity.Insert(0, new(DateTimeOffset.Now, message));
        if (_state.Activity.Count > 200) _state.Activity.RemoveRange(200, _state.Activity.Count - 200);
        _dirty = true;
        if (updateUi) HomeRecentText.Text = message;
        Log.Write(message);
    }
    private void SaveState()
    {
        if (!_ready) return;
        _dirty = true; _saveTimer.Stop(); _saveTimer.Start();
    }
    private async void Save_Tick(object? sender, object args)
    {
        _saveTimer.Stop();
        if (!_ready || !_dirty || _saveInFlight) return;
        _saveInFlight = true; _dirty = false;
        var snapshot = CaptureWorkspaceSnapshot();
        bool succeeded = false;
        try { await Task.Run(() => _store.Save(snapshot)); succeeded = true; }
        catch (Exception ex)
        {
            _dirty = true;
            if (_ready) Error("Could not save settings", ex); else Log.Write("Settings write failed", ex);
        }
        finally
        {
            _saveInFlight = false;
            if (_ready && _dirty && succeeded) _saveTimer.Start();
        }
    }
    private void ShowStatus(string message, bool error = false)
    {
        if (!_ready) return;
        StatusBar.Message = message;
        StatusBar.Severity = error ? InfoBarSeverity.Error : InfoBarSeverity.Informational;
        StatusBar.IsOpen = true;
    }
    private void Error(string message, Exception error)
    {
        Log.Write(message, error); ShowStatus(message + ": " + error.Message, true);
    }

    private void Ui_Tick(object? sender, object args)
    {
        if (!_ready || !_isActive) return;
        if (RefreshSystemAppearance()) ApplyEffects();
        UpdateClock();
        RefreshWorkspaceSummary();
        if (!_commandOpen && !_dialogOpen) _ = RefreshDesktopWindowsAsync();
        if (ResourceText.Visibility != Visibility.Visible) return;
        try
        {
            var sample = _resources.Sample();
            string cpu = sample.CpuPercent is double percent ? $"{percent:0.0}%" : "—";
            ResourceText.Text = $"Nexus · {sample.WorkingSetMB:0} MB\nCPU {cpu}";
            ToolTipService.SetToolTip(ResourceText, $"Process working set: {sample.WorkingSetMB:0} MB\nPrivate bytes: {sample.PrivateMB:0} MB\nCPU normalized across {Environment.ProcessorCount} logical processors.\nGPU/DWM memory is not included.");
        }
        catch (Exception ex) { Log.Write("Resource sample skipped", ex); ResourceText.Text = "Nexus · native desktop"; }
    }
    private void Usage_Tick(object? sender, object args)
    {
        if (!_ready || !_state.UsageTracking) return;
        _dirty |= _usage.Tick();
        if (++_usageTicks >= 12)
        {
            _usageTicks = 0;
            if (_dirty) SaveState();
        }
    }
    private void UpdateClock(bool force = false)
    {
        string minute = DateTime.Now.ToString("yyyyMMddHHmm");
        if (!force && minute == _lastClockMinute) return;
        _lastClockMinute = minute;
        var now = DateTime.Now;
        string timeFormat = _state.Clock24Hour ? "HH:mm" : "h:mm tt";
        DateText.Text = now.ToString("dddd, d MMMM"); ClockText.Text = now.ToString(_state.Clock24Hour ? "HH:mm" : "h:mm");
        ClockPeriod.Text = now.ToString("tt");
        ClockPeriod.Visibility = !_state.Clock24Hour && _state.ShowClockWidget ? Visibility.Visible : Visibility.Collapsed;
        MenuClock.Text = now.ToString("ddd  " + timeFormat);
        string greeting = now.Hour < 12 ? "Good morning" : now.Hour < 18 ? "Good afternoon" : "Good evening";
        GreetingText.Text = greeting + ", " + _state.DisplayName + ".";
        HomeGreeting.Text = "Welcome back, " + _state.DisplayName + ".";
        SpaceName.Text = _state.DisplayName + "’s space"; SidebarName.Text = _state.DisplayName;
    }
    private void ApplyWidgetLayout()
    {
        double width = DesktopRoot.ActualWidth;
        DateText.Visibility = ClockText.Visibility = _state.ShowClockWidget ? Visibility.Visible : Visibility.Collapsed;
        ClockPeriod.Visibility = !_state.Clock24Hour && _state.ShowClockWidget ? Visibility.Visible : Visibility.Collapsed;
        SpaceCard.Visibility = _state.ShowSpaceWidget ? Visibility.Visible : Visibility.Collapsed;
        bool hidden = _page != "Home" || _expanded || _state.FocusMode || !_state.DesktopLayout || width < 1120 || DesktopRoot.ActualHeight < 650;
        DesktopWidgets.Visibility = hidden ? Visibility.Collapsed : Visibility.Visible;
        DesktopColumn.Width = new GridLength(hidden ? 0 : 236);
        Workspace.ColumnSpacing = hidden ? 0 : 28;
        bool sidebar = width >= 760 && DesktopRoot.ActualHeight >= 580;
        Sidebar.Visibility = sidebar ? Visibility.Visible : Visibility.Collapsed;
        SidebarColumn.Width = new GridLength(sidebar ? 170 : 0);
        ResourceText.Visibility = width < 1120 ? Visibility.Collapsed : Visibility.Visible;
        FooterSettingsButton.Visibility = width < 1120 ? Visibility.Collapsed : Visibility.Visible;
        MenuLinks.Visibility = width < 1060 ? Visibility.Collapsed : Visibility.Visible;
        CompactNavigation.Visibility = width < 1060 ? Visibility.Visible : Visibility.Collapsed;
        MenuClock.Visibility = width < 630 ? Visibility.Collapsed : Visibility.Visible;
        BrandingText.Visibility = width < 500 ? Visibility.Collapsed : Visibility.Visible;
        FullScreenButton.Visibility = width < 500 ? Visibility.Collapsed : Visibility.Visible;
        SearchLabel.Visibility = width < 650 ? Visibility.Collapsed : Visibility.Visible;
        SearchShortcut.Visibility = width < 650 ? Visibility.Collapsed : Visibility.Visible;
        MenuBar.Margin = new Thickness(width < 600 ? 8 : 12, 8, width < 600 ? 8 : 12, 2);
        Workspace.Margin = new Thickness(width < 600 ? 12 : _state.DesktopLayout ? 36 : 20, 6, width < 600 ? 12 : _state.DesktopLayout ? 36 : 20, 4);
        int capacity = width < 550 ? 1 : width < 720 ? 2 : width < 1120 ? 4 : 5;
        DockRunningApps.Visibility = width >= 1200 ? Visibility.Visible : Visibility.Collapsed;
        if (_dockCapacity != capacity) { _dockCapacity = capacity; RefreshDock(); }
        ControlPanel.Width = Math.Clamp(width - 64, 256, 400);
        ControlScroller.MaxHeight = Math.Max(120, DesktopRoot.ActualHeight - 160);
        CommandPanel.MaxHeight = Math.Max(180, DesktopRoot.ActualHeight - 128);
        CommandList.MaxHeight = Math.Max(60, Math.Min(360, DesktopRoot.ActualHeight - 360));
        DockSearchButton.Visibility = width < 650 ? Visibility.Collapsed : Visibility.Visible;
        ApplyDesktopLayout();
        ApplyDockDensity();
        UpdateExperienceLayout();
        UpdateHomeColumns();
        UpdateHero();
        UpdateExploreLayout();
    }
    private void UpdateHero()
    {
        HomeHeroArt.Visibility = !_state.ReducedEffects && !_highContrast && PageHost.ActualWidth >= 560
            ? Visibility.Visible : Visibility.Collapsed;
        bool compact = PageHost.ActualWidth < 480;
        HeroCard.Padding = new Thickness(compact ? 20 : 30);
        HeroTitle.FontSize = compact ? 28 : 36;
        ShortcutRow.RowSpacing = compact ? 10 : 0;
        for (int index = 0; index < _shortcutCards.Length; index++)
        {
            Grid.SetRow(_shortcutCards[index], compact ? index : 0);
            Grid.SetColumn(_shortcutCards[index], compact ? 0 : index);
            Grid.SetColumnSpan(_shortcutCards[index], compact ? 3 : 1);
        }
    }
    private void ApplyEffects()
    {
        ApplyAuraPalette();
        bool highContrast = _highContrast;
        bool colorsChanged = _renderedHighContrast != highContrast;
        _renderedHighContrast = highContrast;
        bool simple = _state.ReducedEffects || highContrast;
        bool animation = !simple && _isActive && _animationsEnabled;
        _motion?.SetEnabled(animation);
        WallpaperAccents.Visibility = simple ? Visibility.Collapsed : Visibility.Visible;
        DesktopRoot.Background = highContrast ? Resource("NexusPanel") : _chosenWallpaper ?? _wallpaper;
        ApplyAuraSurfaces(simple);
        ApplyDesktopLayout();
        RefreshPersonalizationControls();
        UpdateCommandCategories();
        UpdateHero();
        if (colorsChanged)
        {
            if (_page == "Activity") { PageContent.Children.Clear(); BuildActivity(); }
            if (_page is "Study" or "Explore" or "Workspaces" or "Personalize") Navigate(_page, false);
            RenderHomeWorkspace(); RenderDesktopIdentity(); RefreshDock(); RenderRunningDock();
        }
    }
    private void Desktop_Loaded(object sender, RoutedEventArgs args)
    {
        if (!_ready || _motion is not null) return;
        try
        {
            _motion = new MotionController(DesktopRoot);
            foreach (var control in new FrameworkElement[] { DockHomeButton, DockExploreButton, DockRunningButton, DockSearchButton, FilesCard, GamesCard, FocusCard })
                _motion.AttachHover(control);
            foreach (var control in DockApps.Children.OfType<FrameworkElement>()) _motion.AttachHover(control);
            ApplyEffects();
            _motion.Enter(HomeView);
        }
        catch (Exception ex) { Log.Write("Custom motion unavailable; using native controls", ex); }
    }
    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (!_ready) return;
        _isActive = args.WindowActivationState != WindowActivationState.Deactivated;
        if (_isActive)
        {
            UpdateClock(true); RenderFocus(); RefreshWorkspaceSummary(); _resources.Reset(); _uiTimer.Start();
            RefreshSystemAppearance(); ApplyEffects();
            _ = RefreshDesktopWindowsAsync();
        }
        else { _uiTimer.Stop(); _motion?.SetEnabled(false); ApplyEffects(); }
    }
    private bool RefreshSystemAppearance()
    {
        // HighContrastChanged is unsupported in desktop apps. Read Win32
        // preferences on startup, activation, and the existing active UI tick.
        bool priorContrast = _highContrast, priorAnimations = _animationsEnabled;
        bool failed = false;
        Exception? error = null;
        try
        {
            if (NativeMethods.TryGetHighContrast(out bool contrast)) _highContrast = contrast;
            else failed = true;
            if (NativeMethods.TryGetAnimationsEnabled(out bool animations)) _animationsEnabled = animations;
            else failed = true;
        }
        catch (Exception ex) { failed = true; error = ex; }
        if (failed && !_appearanceReadFailed)
            Log.Write("Desktop appearance query unavailable; retaining last known preferences", error);
        _appearanceReadFailed = failed;
        return priorContrast != _highContrast || priorAnimations != _animationsEnabled;
    }
    private void SetFullScreen(bool enabled)
    {
        try
        {
            _appWindow.SetPresenter(enabled ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Overlapped);
            if (!enabled && _appWindow.Presenter is OverlappedPresenter p) p.SetBorderAndTitleBar(true, false);
            _state.FullScreen = enabled;
            ToolTipService.SetToolTip(FullScreenButton, enabled ? "Return to windowed · F11" : "Full screen · F11");
            SaveState();
        }
        catch (Exception ex) { Error("Could not change display mode", ex); }
    }
    private void Minimize()
    {
        if (_state.FullScreen) SetFullScreen(false);
        if (_appWindow.Presenter is OverlappedPresenter presenter) presenter.Minimize();
    }
    private async Task ClearActivityAsync()
    {
        if (_dialogOpen) return;
        _dialogOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = DesktopRoot.XamlRoot, RequestedTheme = DesktopRoot.RequestedTheme, Title = "Clear activity?",
                Content = "Delete locally stored launch history and usage totals?",
                PrimaryButtonText = "Clear", CloseButtonText = "Keep", DefaultButton = ContentDialogButton.Close
            };
            PolishDialog(dialog);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || !_ready) return;
            _state.Activity.Clear(); _state.UsageSeconds.Clear(); _usage.ResetSample();
            SaveState(); RefreshHome();
            if (_page == "Activity") Navigate("Activity", false);
        }
        finally { _dialogOpen = false; }
    }

    private void SearchApps()
    {
        OpenCommands();
    }
    private void Navigate_Click(object sender, RoutedEventArgs args) => Navigate((string)((Button)sender).Tag);
    private void NavigateMenu_Click(object sender, RoutedEventArgs args) => Navigate((string)((MenuFlyoutItem)sender).Tag);
    private void SearchApps_Click(object sender, RoutedEventArgs args) => SearchApps();
    private void Running_Click(object sender, RoutedEventArgs args) => Navigate("Running apps");
    private void Reopen_Click(object sender, RoutedEventArgs args) => Navigate("Home");
    private void Controls_Click(object sender, RoutedEventArgs args)
    {
        if (_controlsOpen) ControlsFlyout.Hide();
        else ControlsFlyout.ShowAt(ControlsButton);
    }
    private void Controls_Opening(object sender, object args) => ApplyWidgetLayout();
    private void Controls_Opened(object sender, object args) { _controlsOpen = true; _motion?.Enter(ControlPanel); }
    private void Controls_Closed(object sender, object args) => _controlsOpen = false;
    private void CloseControls_Click(object sender, RoutedEventArgs args) => ControlsFlyout.Hide();
    private void Exit_Click(object sender, RoutedEventArgs args) => ExitNexus();
    private void FullScreen_Click(object sender, RoutedEventArgs args) => SetFullScreen(!_state.FullScreen);
    private void Minimize_Click(object sender, RoutedEventArgs args) => Minimize();
    private void HideHome_Click(object sender, RoutedEventArgs args)
    {
        Navigate("Home");
    }
    private void ExpandHome_Click(object sender, RoutedEventArgs args) { _expanded = !_expanded; ApplyWidgetLayout(); }
    private void Desktop_SizeChanged(object sender, SizeChangedEventArgs args) { if (_ready) ApplyWidgetLayout(); }
    private void PageHost_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        ((Grid)sender).Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, args.NewSize.Width, args.NewSize.Height) };
        if (_ready) { UpdateHero(); UpdateWorkspaceLayout(); UpdateHomeColumns(); UpdateExperienceLayout(); }
    }
    private void DragArea_PointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (_ready && !_state.FullScreen && args.GetCurrentPoint(DesktopRoot).Properties.IsLeftButtonPressed) NativeMethods.BeginDrag(_handle);
    }
    private void QuickLaunch_Click(object sender, RoutedEventArgs args)
    {
        var app = AppCatalog.Defaults().First(a => a.Id == (string)((Button)sender).Tag); Launch(app);
    }
    private void SystemSettings_Click(object sender, RoutedEventArgs args)
    {
        try { AppCatalog.OpenSettings((string)((Button)sender).Tag); ControlsFlyout.Hide(); }
        catch (Exception ex) { Error("Could not open Windows settings", ex); }
    }
    private void DataFolder_Click(object sender, RoutedEventArgs args)
    {
        try
        {
            Directory.CreateDirectory(StateStore.DirectoryPath);
            Process.Start(new ProcessStartInfo(StateStore.DirectoryPath) { UseShellExecute = true }); ControlsFlyout.Hide();
        }
        catch (Exception ex) { Error("Could not open the data folder", ex); }
    }
    private void SaveName_Click(object sender, RoutedEventArgs args)
    {
        string name = DisplayNameBox.Text.Trim();
        if (name.Length == 0) { ShowStatus("Enter a name first."); return; }
        _state.DisplayName = name; SaveState(); UpdateClock(true); ShowStatus("Name updated.");
    }
    private void Tracking_Toggled(object sender, RoutedEventArgs args)
    {
        if (!_ready) return;
        _state.UsageTracking = TrackingSwitch.IsOn; _usage.ResetSample(); _usageTicks = 0;
        if (_state.UsageTracking) _usageTimer.Start(); else _usageTimer.Stop();
        Record("Usage tracking " + (_state.UsageTracking ? "enabled" : "disabled")); SaveState();
        if (_page == "Activity") Navigate("Activity", false);
    }
    private void Startup_Toggled(object sender, RoutedEventArgs args)
    {
        if (!_ready) return;
        try
        {
            StartupRegistration.SetEnabled(StartupSwitch.IsOn);
            RefreshStartupRepair();
            Record(StartupSwitch.IsOn ? "Startup after sign-in enabled" : "Startup after sign-in disabled"); SaveState();
        }
        catch (Exception ex) { _ready = false; StartupSwitch.IsOn = !StartupSwitch.IsOn; _ready = true; Error("Could not change login startup", ex); }
    }
    private void Focus_Toggled(object sender, RoutedEventArgs args)
    {
        if (!_ready || _syncingPersonalization) return; _state.FocusMode = FocusSwitch.IsOn; ApplyWidgetLayout(); RefreshHome(); SaveState();
    }
    private void RefreshStartupRepair()
    {
        bool repair = StartupRegistration.IsEnabled() && !StartupRegistration.UsesCurrentVersion();
        StartupRepairHint.Visibility = repair ? Visibility.Visible : Visibility.Collapsed;
        StartupRepairButton.Visibility = repair ? Visibility.Visible : Visibility.Collapsed;
    }
    private void StartupRepair_Click(object sender, RoutedEventArgs args)
    {
        try
        {
            StartupRegistration.SetEnabled(true); RefreshStartupRepair();
            Record("Sign-in startup updated to this Nexus version"); SaveState();
            ShowStatus("This version will start after Windows sign-in.");
        }
        catch (Exception ex) { Error("Could not update sign-in startup", ex); }
    }
    private void FocusCard_Click(object sender, RoutedEventArgs args) => Navigate("Study");
    private void Effects_Toggled(object sender, RoutedEventArgs args)
    {
        if (!_ready || _syncingPersonalization) return; _state.ReducedEffects = EffectsSwitch.IsOn; ApplyEffects(); SaveState();
    }
    private async void AddApp_Click(object sender, RoutedEventArgs args) => await PickAppAsync();
    private async void RefreshApps_Click(object sender, RoutedEventArgs args) => await DiscoverAsync();

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        if (_focusSession.CompleteIfDue()) FinishFocusSession(updateUi: false);
        _focusSession.Pause();
        var finalSnapshot = CaptureWorkspaceSnapshot();
        _ready = false;
        try { _desktopIntegration?.Dispose(); } catch (Exception ex) { Log.Write("Desktop cleanup skipped", ex); }
        _uiTimer.Stop(); _usageTimer.Stop(); _searchTimer.Stop(); _saveTimer.Stop();
        _focusTimer.Stop();
        _discoveryCancellation.Cancel();
        try { _motion?.Dispose(); } catch (Exception ex) { Log.Write("Motion cleanup skipped", ex); }
        _resources.Dispose();
        // An atomic final save also preserves mutations made after the latest snapshot.
        try { _store.SaveFinal(finalSnapshot); } catch (Exception ex) { Log.Write("Final settings save failed", ex); }
        _discoveryCancellation.Dispose();
    }
}
