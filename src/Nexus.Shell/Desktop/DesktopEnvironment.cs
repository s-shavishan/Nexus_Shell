using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Interop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Nexus.Shell.UI;
using Nexus.Shell.UI.Menus;
using System.Diagnostics;
using Nexus.Runtime;

namespace Nexus.Shell.Desktop;

// The coordinator owns lifetime and shared data. Each surface owns its UI and
// its own HWND; Sections is created only after an explicit open action.
internal sealed partial class DesktopEnvironment
{
    internal ShellSession Session { get; }
    internal ShellTheme Theme { get; } = new();
    internal NotificationInbox Notifications { get; }
    internal DesktopMenus Menus { get; }
    internal DesktopWindow Desktop { get; private set; } = null!;
    internal TaskbarWindow Taskbar { get; private set; } = null!;
    internal bool IsStopping { get; private set; }
    internal bool SectionsOpen => _sections is not null;
    internal bool DockInteraction => _menu?.IsOpen == true || _controlVisible || _notifications?.IsOpen == true || _switcher?.IsOpen == true || _dockPreview?.IsOpen == true || _sessionDialog;
    internal DesktopSessionMode Mode { get; }
    internal bool IsManagedDesktop => Mode != DesktopSessionMode.Preview;
    internal event Action? Stopped;
    private MainWindow? _sections;
    private SwitcherWindow? _switcher;
    private readonly CoreProcessSession _core;
    private bool _checkingCore, _coreFailureReported;
    private Guid _coreIssueId;
    private int _saveFailures;
    private readonly NexusDesktopToggle _desktopToggle = new();
    private WindowEventObserver? _windowEvents;
    private readonly DispatcherTimer _windowRefreshTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private bool _windowEventQueued, _foregroundChanged, _windowRefreshPending;
    private EventWaitHandle? _pulse;
    private Process? _host;
    private bool _sessionDialog;
    private readonly UsageTracker _usage;
    private int _usageTicks;
    private bool _usageTracking;
    private bool? _compact;
    private bool? _floating;
    private (string, bool, bool, bool, bool, bool, bool, string)? _appearance;
    private MenuWindow? _menu;
    private NotificationWindow? _notifications;
    private MenuBarWindow? _topBar;
    private DesktopIntegration? _integration;
    private StartKeyRouter? _startKey;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly CancellationTokenSource _cancel = new();
    private Task<List<AppEntry>>? _catalog;
    private IReadOnlyList<RunningWindow> _windows = [];
    private bool _refreshing, _saving, _dirty;

    internal DesktopEnvironment(ShellSession session, CoreProcessSession core, DesktopSessionMode mode = DesktopSessionMode.Preview, string? hostToken = null, int? hostPid = null)
    {
        Session = session; _core = core; Notifications = new(Session.State.NotificationHistory); Notifications.Quiet = Session.State.QuietNotifications;
        Mode = mode; Menus = new(this); _usage = new(Session.State); _usageTracking = Session.State.UsageTracking;
        if (IsManagedDesktop)
        {
            if (!Guid.TryParseExact(hostToken, "N", out _)) throw new ArgumentException("Start desktop mode through Nexus.DesktopHost.exe.");
            if (hostPid is null || hostPid <= 0) throw new ArgumentException("Start desktop mode with the matching Nexus desktop host.");
            _host = Process.GetProcessById(hostPid.Value);
            if (_host.SessionId != Process.GetCurrentProcess().SessionId
                || !string.Equals(_host.MainModule?.FileName, Path.Combine(AppContext.BaseDirectory, "Nexus.DesktopHost.exe"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The desktop host does not belong to this Nexus folder and session.");
            // Retain the native process handle, so PID reuse cannot mask a dead host.
            _ = _host.Handle;
            _pulse = EventWaitHandle.OpenExisting(@"Local\WhiteDreams.Nexus.Pulse." + hostToken);
        }
    }
    internal void Start(Action<StartupStep>? progress = null)
    {
        try
        {
            if (Mode == DesktopSessionMode.DesktopShell && ShellLayerInterop.ExplorerDesktopPresent())
                throw new InvalidOperationException("Windows Explorer is already providing this desktop. Use preview mode, or sign in after configuring Nexus in Personalize.");
            if (!Session.State.CatalogInitialized)
            { Session.State.PinnedApps = AppCatalog.Defaults(); Session.State.CatalogInitialized = true; _dirty = true; }
            for (int i = 0; i < Session.State.PinnedApps.Count; i++)
                if (IsExplorer(Session.State.PinnedApps[i].Target)) { Session.State.PinnedApps[i] = Session.State.PinnedApps[i] with { Target = "nexus:files" }; _dirty = true; }
            Theme.Apply(Session.State.Wallpaper, Session.State.ReducedEffects);
            Desktop = new(this); progress?.Invoke(StartupStep.Desktop);
            Taskbar = new(this);
            _topBar = new(this);
            Session.Changed += SessionChanged;
            Notifications.Changed += NotificationHistoryChanged;
            _saveTimer.Tick += SaveTick; _timer.Tick += Tick;
            _windowRefreshTimer.Tick += (_, _) => { _windowRefreshTimer.Stop(); UpdateTaskbar(); };
            _integration = new(Desktop.Handle, command => Desktop.DispatcherQueue.TryEnqueue(() =>
            {
                if (IsStopping) return;
                if (command == "exit") RequestExit(); else if (command == "search") ShowMenu(true);
                else if (command == "start") ShowMenu();
                else if (command == "start-unavailable") Report("Windows-key routing is unavailable in this context. Use Launchpad in the Nexus dock.");
                else if (command == "show") ShowDesktop();
                else if (command == "tray-lost") Report("The Windows notification icon is unavailable. The Nexus taskbar is still running.");
            }));
            RefreshIntegration(); Desktop.ShowSurface(); Taskbar.ShowBar();
            if (IsManagedDesktop) _ = ConnectStartKeyAsync();
            _topBar.SetFullscreen(false);
            progress?.Invoke(StartupStep.Dock);
            // Native Windows key combinations pass through. Events observe
            // app lifecycle only; the five-second tick is a recovery fallback.
            try { _windowEvents = new(QueueWindowEvent, includeOwnProcess: true); }
            catch (Exception ex) { Report("Window notifications unavailable; the dock will use periodic refresh", ex); }
            RefreshDesktop(); UpdateTaskbar(); _timer.Start();
            StartControlCenterSupervision();
            _ = GetCatalogAsync();
            Desktop.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            { if (IsStopping || _menu is not null) return; try { _menu = new(this); } catch (Exception error) { Log.Write("Launchpad preparation deferred to its next opening", error); } });
            _pulse?.Set();
            if (_dirty) _saveTimer.Start();
            if (Session.RecoveryMessage.Length > 0) Report(Session.RecoveryMessage);
        }
        catch { Shutdown(DesktopExitCode.Stop); throw; }
    }
    internal void SaveState() => Session.NotifyChanged();
    private int _noticeQueued;
    private void NotificationHistoryChanged()
    {
        if (IsStopping || Interlocked.Exchange(ref _noticeQueued, 1) != 0) return;
        if (!Desktop.DispatcherQueue.TryEnqueue(() =>
        { Interlocked.Exchange(ref _noticeQueued, 0); if (IsStopping) return; Session.State.NotificationHistory = Notifications.Items.ToList(); SaveState(); })) Interlocked.Exchange(ref _noticeQueued, 0);
    }
    private async Task ConnectStartKeyAsync()
    {
        try { _startKey = new StartKeyRouter(Desktop.Handle); await _startKey.Ready; if (!IsStopping) Log.Write("Standalone Windows key connected to Nexus Launchpad"); }
        catch (Exception error) { if (!IsStopping) Report("The Windows key could not connect to Launchpad. Use its dock button", error); _startKey?.Dispose(); }
    }
    private void SessionChanged()
    {
        if (IsStopping) return;
        Notifications.Quiet = Session.State.QuietNotifications;
        if (!Session.State.RememberRecentItems) Session.State.RecentApps.Clear();
        _dirty = true; _saveTimer.Stop(); _saveTimer.Start();
        RefreshAppearance(); RefreshIntegration();
        if (!Session.State.DockPreviews) HideDockPreview();
        Taskbar.View.Refresh(DockWindows());
        if (_compact != Session.State.CompactDock || _floating != Session.State.FloatingTaskbar)
        { _compact = Session.State.CompactDock; _floating = Session.State.FloatingTaskbar; Taskbar.Position(); }
        Desktop.Surface.RefreshContent();
        _sections?.RefreshSharedNotes();
        if (_usageTracking != Session.State.UsageTracking) { _usageTracking = Session.State.UsageTracking; ResetUsageSample(); }
    }
    internal void RefreshAppearance()
    {
        if (IsStopping || Desktop is null) return;
        Theme.Apply(Session.State.Wallpaper, Session.State.ReducedEffects);
        var appearance = (Theme.Palette.Name, Theme.HighContrast, Theme.Animations, Theme.Transparency, Session.State.ReducedEffects, Session.State.NativeGlass, Session.State.SurfaceAnimations, Session.State.DisplayName);
        if (_appearance == appearance) return;
        _appearance = appearance; HideDockPreview(); Desktop.Surface.ApplyAppearance();
        Taskbar?.ApplyAppearance(); _menu?.ApplyAppearance(this);
        foreach (var utility in _utilities.Values) utility.ApplyAppearance();
        _switcher?.ApplyAppearance();
        _sections?.RefreshSharedAppearance();
        _notifications?.ApplyAppearance(); _topBar?.ApplyAppearance();
    }
    internal void RefreshIntegration()
    {
        if (_integration is null || IsStopping) return;
        bool resident = Mode == DesktopSessionMode.Preview && Session.State.KeepAvailable;
        if (_lastResident != resident)
        { _lastResident = resident; if (!_integration.SetResident(resident)) Report("The notification icon is unavailable. Exit remains available in the desktop menu."); }
    }
    private bool? _lastResident;
    private async void SaveTick(object? sender, object args)
    {
        _saveTimer.Stop(); if (IsStopping || !_dirty || _saving) return;
        _saving = true; _dirty = false;
        try { await Session.SaveAsync(); _saveFailures = 0; _saveTimer.Interval = TimeSpan.FromSeconds(2); }
        catch (Exception ex)
        {
            _dirty = true;
            if (_saveFailures == 0) Report("Your latest settings are still in the desktop. Nexus will retry saving them", ex);
            _saveFailures = Math.Min(6, _saveFailures + 1);
            _saveTimer.Interval = TimeSpan.FromSeconds(Math.Min(30, 2 << _saveFailures));
        }
        finally { _saving = false; if (!IsStopping && _dirty) _saveTimer.Start(); }
    }
    internal void ResetUsageSample() { _usage.ResetSample(); _usageTicks = 0; }
    private void Tick(object? sender, object args)
    {
        // Keep host supervision alive while an asynchronous final save or
        // recovery dialog is in progress. Stop this timer only at final cleanup.
        _pulse?.Set();
        if (IsStopping) return;
        if (_host?.HasExited == true)
        {
            Log.Write("Desktop host exited; requesting independent Windows recovery.");
            try
            {
                var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "Nexus.DesktopHost.exe")) { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory };
                start.ArgumentList.Add(Mode == DesktopSessionMode.NexusSession ? "--restore-session" : "--restore-windows");
                start.ArgumentList.Add("--wait-for-preview"); start.ArgumentList.Add(Environment.ProcessId.ToString());
                Process.Start(start);
            }
            catch (Exception ex) { Log.Write("Could not start independent recovery; use Restore-Windows-Desktop.bat.", ex); }
            Shutdown(DesktopExitCode.RestoreWindows); return;
        }
        CheckCore();
        RefreshAppearance(); Taskbar.View.RefreshClock(); UpdateTaskbar();
        if (!Session.State.UsageTracking) return;
        try
        {
            _dirty |= _usage.Tick();
            if (++_usageTicks >= 12) { _usageTicks = 0; if (_dirty && !_saveTimer.IsEnabled) _saveTimer.Start(); }
        }
        catch (Exception ex) { Report("Usage sample skipped", ex, false); }
    }
    private async void CheckCore()
    {
        if (IsStopping || _checkingCore) return; _checkingCore = true;
        try
        {
            var health = await _core.HealthAsync();
            if (IsStopping) return;
            _coreFailureReported = false;
            if (health.IssueId != Guid.Empty && health.IssueId != _coreIssueId)
            { _coreIssueId = health.IssueId; Report(health.Issue); }
        }
        catch (Exception ex)
        { if (!IsStopping && !_coreFailureReported) { _coreFailureReported = true; Report("Nexus Core is unavailable. The desktop remains open while recovery is attempted", ex); } }
        finally { _checkingCore = false; }
    }
    internal async void UpdateTaskbar()
    {
        if (IsStopping || Taskbar is null) return;
        _windowRefreshPending = true;
        if (_refreshing) return; _refreshing = true; _windowRefreshPending = false;
        try
        {
            var windows = await Task.Run(() => NativeMethods.RunningWindows(Desktop.Handle));
            // A new event during enumeration schedules another pass instead of
            // being lost behind the old _refreshing early return.
            if (!IsStopping) { _windows = windows; Taskbar.View.Refresh(DockWindows()); Taskbar.RefreshVisibility(); }
        }
        catch (Exception ex) { Report("Could not refresh running windows", ex, false); }
        finally
        {
            _refreshing = false;
            if (!IsStopping && _windowRefreshPending && !_windowRefreshTimer.IsEnabled) _windowRefreshTimer.Start();
        }
    }
    private void QueueWindowEvent(WindowEvent change)
    {
        if (IsStopping) return;
        // Observe native minimize/restore of Nexus content windows too, while
        // ignoring our tool windows and popups to avoid self-refresh loops.
        if (NativeMethods.WindowProcessId(change.Window) == Environment.ProcessId && !IsOwnAppWindow(change.Window)) return;
        _foregroundChanged |= change.IsForeground;
        if (_windowEventQueued) return; _windowEventQueued = true;
        if (!Desktop.DispatcherQueue.TryEnqueue(() =>
        {
            _windowEventQueued = false; if (IsStopping) return;
            bool foregroundChanged = _foregroundChanged; _foregroundChanged = false;
            if (foregroundChanged && ShellLayerInterop.IsExplorerDesktopWindow(NativeMethods.GetForegroundWindow())) Desktop.ShowSurface();
            Taskbar.View.RefreshWindowStates(); Taskbar.RefreshVisibility();
            // Fixed coalescing window, not a sliding debounce that can starve.
            if (!_windowRefreshTimer.IsEnabled) _windowRefreshTimer.Start();
        })) _windowEventQueued = false;
    }
    private IReadOnlyList<RunningWindow> DockWindows()
    {
        var windows = _windows.ToList();
        if (_sections is not null) windows.Add(new(WinRT.Interop.WindowNative.GetWindowHandle(_sections), "Sections", "nexus", Environment.ProcessId));
        windows.AddRange(UtilityWindows());
        return windows;
    }
    internal void ToggleDockWindow(RunningWindow window)
    {
        HideDockPreview();
        if (IsStopping || !NativeMethods.OwnsWindow(window)) { UpdateTaskbar(); return; }
        if (_sections is not null && window.Handle == WinRT.Interop.WindowNative.GetWindowHandle(_sections)) _sections.ToggleFromDock();
        else if (UtilityFor(window) is { } utility) utility.Toggle();
        else if (!NativeMethods.ToggleDockWindow(window)) Report("Windows could not switch this app. Try the window overview.");
        Taskbar.View.RefreshWindowStates(); UpdateTaskbar();
    }
    internal void RestoreDockWindow(RunningWindow window)
    {
        HideDockPreview();
        if (IsStopping || !NativeMethods.OwnsWindow(window)) { UpdateTaskbar(); return; }
        if (_sections is not null && window.Handle == WinRT.Interop.WindowNative.GetWindowHandle(_sections)) _sections.OpenSection(null);
        else if (UtilityFor(window) is { } utility) utility.Restore();
        else if (!NativeMethods.Activate(window)) Report("Windows could not switch this app. Try the window overview.");
        UpdateTaskbar();
    }
    internal void MinimizeDockWindow(RunningWindow window)
    {
        HideDockPreview();
        if (IsStopping || !NativeMethods.OwnsWindow(window)) { UpdateTaskbar(); return; }
        if (_sections is not null && window.Handle == WinRT.Interop.WindowNative.GetWindowHandle(_sections)) _sections.MinimizeFromDock();
        else if (UtilityFor(window) is { } utility) utility.Minimize();
        else if (!NativeMethods.Minimize(window)) Report("Windows could not minimize this app.");
        UpdateTaskbar();
    }
    internal void RefreshDesktop() { if (!IsStopping && Desktop is not null) _ = Desktop.Surface.RefreshAsync(); }
    internal void ShowSections(string? page = null)
    {
        if (IsStopping) return; HideMenu();
        try
        {
            if (_sections is null)
            {
                var window = new MainWindow(this); _sections = window;
                window.Closed += (_, _) => { if (ReferenceEquals(_sections, window)) _sections = null; if (!IsStopping) UpdateTaskbar(); };
            }
            _sections.OpenSection(page); UpdateTaskbar();
        }
        catch (Exception ex) { Report("Could not open Sections", ex); }
    }
    internal void ShowMenu(bool search = false)
    {
        if (IsStopping) return;
        HideDockPreview();
        HideControlCenter();
        _notifications?.Hide();
        try { _menu ??= new(this); if (_menu.IsOpen && !search) _menu.HideMenu(); else _menu.ShowMenu(this, Taskbar.BarBounds, search); }
        catch (Exception ex) { Report("Could not open Start", ex); }
    }
    internal void ShowQuickSettings()
    {
        if (IsStopping) return; HideDockPreview(); _menu?.HideMenu(); _notifications?.Hide();
        if (_controlVisible && !_controlCompact) HideControlCenter(); else ShowControlCenter(_controlSection);
    }
    internal void ShowQuickControl(string section)
    {
        if (_controlVisible && _controlCompact && _controlSection == section) HideControlCenter();
        else ShowControlCenter(section, compact: true);
    }
    internal void ShowControlCenter(string section, bool compact = false)
    {
        if (IsStopping) return; HideDockPreview(); _menu?.HideMenu(); _notifications?.Hide();
        _controlVisible = true; _controlCompact = compact; _controlSection = ControlCenterPreferences.Sections.Contains(section) ? section : "Sound"; ++_controlSequence;
        _ = SyncControlCenterAsync();
    }
    internal Task<SettingsSnapshot> ExecuteSettingsAsync(SettingsRequest request)
        => _core.ExecuteSettingsAsync(request with { Window = Taskbar.Handle.ToInt64() }, _cancel.Token);
    internal void PositionQuickSettings() { if (_controlVisible) _ = SyncControlCenterAsync(); }
    internal void PositionDesktopPanels() { _topBar?.Position(); PositionQuickSettings(); if (_notifications?.IsOpen == true) _notifications.Position(); }
    internal void SetDesktopFullscreen(bool fullscreen) => _topBar?.SetFullscreen(fullscreen);
    internal void SetMenuBarAttached(bool attached) { if (!DockInteraction) _topBar?.SetAttached(attached); }
    internal void ShowNotifications()
    {
        if (IsStopping) return; HideDockPreview(); _menu?.HideMenu(); HideControlCenter();
        try { _notifications ??= new(this); if (_notifications.IsOpen) _notifications.Hide(); else _notifications.Show(); }
        catch (Exception error) { Report("Could not open notifications", error); }
    }
    internal void NotificationClosed(NotificationWindow window) { if (ReferenceEquals(_notifications, window)) _notifications = null; }
    internal void HideMenu() { HideDockPreview(); _menu?.HideMenu(); HideControlCenter(); _notifications?.Hide(); }
    internal void MenuClosed(MenuWindow window) { if (ReferenceEquals(_menu, window)) _menu = null; }
    internal Task<List<AppEntry>> GetCatalogAsync(bool refresh = false)
    {
        if (_catalog is null || _catalog.IsCanceled || _catalog.IsFaulted || (refresh && _catalog.IsCompleted))
            _catalog = Task.Run(() => AppCatalog.DiscoverShortcuts(_cancel.Token), _cancel.Token);
        return _catalog;
    }
    internal void OpenDesktopItem(DesktopShortcut item)
    { if (item.Id == "sections") ShowSections(); else OpenTarget(item.Target); }
    internal void Launch(AppEntry app) => TryLaunch(app);
    internal bool TryLaunch(AppEntry app)
    {
        if (IsStopping) return false;
        HideDockPreview();
        try { OpenTargetChecked(app.Target); RecentApplications.Record(Session.State, app); Session.State.Activity.Insert(0, new(DateTimeOffset.Now, "Opened " + app.Name)); if (Session.State.Activity.Count > 200) Session.State.Activity.RemoveRange(200, Session.State.Activity.Count - 200); SaveState(); return true; }
        catch (Exception ex) { Report("Could not open " + app.Name, ex); return false; }
    }
    internal void OpenTarget(string target)
    {
        HideMenu(); if (target.Length == 0) return;
        try { OpenTargetChecked(target); }
        catch (Exception ex) { Report("Could not open this shortcut", ex); }
    }
    private static bool IsExplorer(string target) => Path.GetFileName(target.Trim().Trim('"')).Equals("explorer.exe", StringComparison.OrdinalIgnoreCase);
    internal void OpenTargetChecked(string target)
    {
        if (SettingsRules.SectionForUri(target) is { } settingsSection) { ShowControlCenter(settingsSection); return; }
        if (target == "nexus:settings") { ShowControlCenter("Desktop"); return; }
        if (target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase)) { OpenAdvancedWindowsSettings(target); return; }
        if (target == "nexus:sections") { ShowSections(); return; }
        if (target == "nexus:notes") { ShowUtility("Notes"); return; }
        if (target == "nexus:calculator") { ShowUtility("Calculator"); return; }
        if (target is "nexus:recycle" or "shell:RecycleBinFolder") { ShowFiles("nexus:recycle"); return; }
        if (target == "nexus:files" || IsExplorer(target)) { ShowFiles(); return; }
        if (Directory.Exists(target)) { ShowFiles(target); return; }
        string arguments = "", working = "";
        if (Path.GetExtension(target).Equals(".lnk", StringComparison.OrdinalIgnoreCase) && File.Exists(target))
        {
            var link = ShortcutResolver.Read(target);
            if (Directory.Exists(link.Target)) { ShowFiles(link.Target); return; }
            if (IsExplorer(link.Target))
            {
                string folder = link.Arguments.Trim().Trim('"');
                if (Directory.Exists(folder)) ShowFiles(folder); else ShowFiles(); return;
            }
            if (link.Target.Length > 0) { target = link.Target; arguments = link.Arguments; working = link.WorkingDirectory; }
            else if (IsManagedDesktop) throw new NotSupportedException("This Windows namespace shortcut has no app target. Pin the application executable or use Nexus Files.");
        }
        if (target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) || target.StartsWith("::{", StringComparison.Ordinal))
            throw new NotSupportedException("This Windows desktop location is not available in Nexus. Use My files or PC controls.");
        var start = new ProcessStartInfo(target) { UseShellExecute = true, Arguments = arguments };
        if (Directory.Exists(working)) start.WorkingDirectory = working;
        else if (File.Exists(target)) start.WorkingDirectory = Path.GetDirectoryName(target)!;
        Process.Start(start);
    }
    internal async void OpenAdvancedWindowsSettings(string target = "ms-settings:")
    {
        try { await AppCatalog.OpenSettingsAsync(target); }
        catch (Exception error) { if (!IsStopping) Report("Windows did not open its advanced settings. Nexus Control Center remains available", error); }
    }
    internal async void ShowFiles(string? folder = null)
    {
        if (IsStopping) return; HideMenu();
        try
        {
            var opened = await _core.OpenFilesAsync(new(new(FileSelectionKind.Browse), folder), _cancel.Token);
            if (IsStopping) return;
            var window = new RunningWindow(new IntPtr(opened.Window), "Files", "Nexus.Shell", opened.ProcessId);
            if (NativeMethods.OwnsWindow(window)) NativeMethods.Activate(window);
            UpdateTaskbar();
        }
        catch (OperationCanceledException) when (IsStopping) { }
        catch (Exception ex) { if (!IsStopping) Report("Could not open Files; the desktop is still available", ex); }
    }
    internal void ReturnToFiles() => ShowFiles();
    internal async Task<IReadOnlyList<string>> PickAsync(FileSelectionRequest request, CancellationToken cancellation = default)
    {
        if (IsStopping) return [];
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _cancel.Token);
        try { var selected = await _core.OpenFilesAsync(new(request), linked.Token); return selected.Paths; }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { return []; }
        catch (Exception ex) { if (!IsStopping) Report("The file picker could not finish. You can open it again", ex); return []; }
        finally { if (!IsStopping) UpdateTaskbar(); }
    }
    private List<RunningWindow> AppWindows()
    {
        var windows = NativeMethods.RunningWindows(Desktop.Handle).ToList();
        if (_sections is not null) windows.Add(new(WinRT.Interop.WindowNative.GetWindowHandle(_sections), "Sections", "nexus", Environment.ProcessId));
        windows.AddRange(UtilityWindows());
        return windows;
    }
    internal void ShowWindowOverview()
    {
        if (IsStopping) return; HideMenu();
        try { IntPtr current = NativeMethods.ForegroundTaskWindow(); _switcher ??= new(this); _switcher.Show(AppWindows(), current); }
        catch (Exception ex) { Report("Could not switch windows", ex); }
    }
    internal void SwitcherClosed(SwitcherWindow window) { if (ReferenceEquals(window, _switcher)) _switcher = null; }
    internal void ShowDesktop()
    {
        if (IsStopping) return; HideMenu();
        try
        {
            _switcher?.Hide(); _desktopToggle.Toggle(AppWindows());
        }
        catch (Exception ex) { Report("Could not show the desktop", ex, false); }
        Desktop.ShowSurface(); Taskbar.ShowBar(); UpdateTaskbar();
    }
    internal void Report(string message, Exception? error = null, bool visible = true)
    {
        Log.Write(message, error);
        if (IsStopping || !visible) return;
        string detail = error is null ? message : message + ": " + error.Message;
        Notifications.Publish(error is null ? "Nexus" : "Nexus needs attention", detail, error is null ? NoticeKind.Information : NoticeKind.Error);
        if (!Notifications.Quiet && Desktop is not null) Desktop.Surface.Report(detail);
    }
    internal async void RequestExit()
    {
        if (IsStopping || _sessionDialog) return; HideMenu();
        if (Mode == DesktopSessionMode.Preview) { Shutdown(DesktopExitCode.Stop); return; }
        if (Mode == DesktopSessionMode.NexusSession) { Shutdown(DesktopExitCode.RestoreWindows); return; }
        _sessionDialog = true;
        try
        {
            var panel = new StackPanel { Spacing = 16 };
            panel.Children.Add(new TextBlock { Text = "Restart Nexus, return to the Windows desktop, or sign out of this session.", TextWrapping = TextWrapping.Wrap });
            var signOut = new Button { Content = "Sign out…" }; panel.Children.Add(signOut); bool signingOut = false;
            var dialog = new ContentDialog { XamlRoot = Desktop.Surface.XamlRoot, RequestedTheme = Theme.ElementTheme, Title = "Desktop session",
                Content = panel, PrimaryButtonText = "Restart Nexus", SecondaryButtonText = "Return to Windows", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
            signOut.Click += (_, _) => { signingOut = true; dialog.Hide(); };
            var choice = await dialog.ShowAsync();
            if (IsStopping) return;
            if (signingOut)
            {
                var confirm = new ContentDialog { XamlRoot = Desktop.Surface.XamlRoot, RequestedTheme = Theme.ElementTheme, Title = "Sign out?",
                    Content = "Save your work in other apps before signing out.", PrimaryButtonText = "Sign out", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
                if (await confirm.ShowAsync() == ContentDialogResult.Primary) Shutdown(DesktopExitCode.SignOut);
            }
            else if (choice == ContentDialogResult.Primary) Shutdown(DesktopExitCode.Restart);
            else if (choice == ContentDialogResult.Secondary) Shutdown(DesktopExitCode.RestoreWindows);
        }
        catch (Exception ex) { Report("Could not open session controls", ex); }
        finally { _sessionDialog = false; }
    }
    internal async void Shutdown(DesktopExitCode? exitCode = null)
    {
        if (IsStopping) return; IsStopping = true;
        _saveTimer.Stop(); _windowRefreshTimer.Stop(); Session.Changed -= SessionChanged;
        Notifications.Changed -= NotificationHistoryChanged; Session.State.NotificationHistory = Notifications.Items.ToList();
        _cancel.Cancel();
        Environment.ExitCode = (int)(exitCode ?? (IsManagedDesktop ? DesktopExitCode.RestoreWindows : DesktopExitCode.Stop));
        void Cleanup(Action action) { try { action(); } catch (Exception ex) { Log.Write("Desktop shutdown cleanup failed", ex); } }
        Cleanup(() => _dockPreview?.Close()); _dockPreview = null;
        Cleanup(() => _sections?.Close()); _sections = null;
        Cleanup(() => _menu?.Close()); _menu = null;
        Cleanup(() => _controlTimer.Stop());
        Cleanup(() => _notifications?.Close()); _notifications = null;
        Cleanup(() => _topBar?.Close()); _topBar = null;
        foreach (var utility in _utilities.Values.ToArray()) Cleanup(utility.Close); _utilities.Clear();
        Cleanup(() => _switcher?.Close()); _switcher = null;
        Cleanup(() => _windowEvents?.Dispose()); _windowEvents = null;
        Cleanup(() => _startKey?.Dispose()); _startKey = null;
        Cleanup(() => _integration?.Dispose()); _integration = null;
        // Keep the desktop alive until the bounded final save completes.
        try { await Session.SaveFinalAsync().WaitAsync(TimeSpan.FromSeconds(8)); }
        catch (Exception ex)
        {
            Log.Write("Final Core save failed; preserving an unsaved-session copy", ex);
            try
            {
                var snapshot = Session.Capture();
                string recovery = await Task.Run(() => Session.Store.SaveRecoverySnapshot(snapshot));
                NativeMethods.ShowStartupError("Nexus could not confirm the final settings save. Your current edits were preserved in:\n\n" + recovery);
            }
            catch (Exception recovery) { Log.Write("Could not preserve the unsaved-session copy", recovery); NativeMethods.ShowStartupError("Nexus could not save the latest settings or create a recovery copy. See nexus.log in the Nexus data folder."); }
        }
        try { await _core.DisposeAsync(); } catch (Exception ex) { Log.Write("Core shutdown failed", ex); }
        _timer.Stop();
        Cleanup(() => _pulse?.Dispose()); _pulse = null;
        Cleanup(() => _host?.Dispose()); _host = null;
        // Release the working-area reservation before destroying the desktop.
        Cleanup(() => Taskbar?.Dispose()); Cleanup(() => Taskbar?.Close());
        Cleanup(() => Desktop?.Dispose()); Cleanup(() => Desktop?.Close());
        _cancel.Dispose(); Stopped?.Invoke();
    }
}
