using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Interop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Nexus.Shell.UI;
using Nexus.Shell.UI.Menus;
using System.Diagnostics;

namespace Nexus.Shell.Desktop;

// The coordinator owns lifetime and shared data. Each surface owns its UI and
// its own HWND; Sections is created only after an explicit open action.
internal sealed class DesktopEnvironment
{
    internal ShellSession Session { get; } = new();
    internal ShellTheme Theme { get; } = new();
    internal DesktopMenus Menus { get; }
    internal DesktopWindow Desktop { get; private set; } = null!;
    internal TaskbarWindow Taskbar { get; private set; } = null!;
    internal bool IsStopping { get; private set; }
    internal bool SectionsOpen => _sections is not null;
    internal bool FilesOpen => _files is not null;
    internal DesktopSessionMode Mode { get; }
    internal event Action? Stopped;
    private MainWindow? _sections;
    private FilesWindow? _files;
    private SwitcherWindow? _switcher;
    private readonly HashSet<FilesWindow> _pickers = [];
    private readonly NexusDesktopToggle _desktopToggle = new();
    private ShellKeyboardHook? _keyboard;
    private EventWaitHandle? _pulse;
    private bool _sessionDialog;
    private readonly UsageTracker _usage;
    private int _usageTicks;
    private bool _usageTracking;
    private bool? _compact;
    private (string, bool, bool, bool, string)? _appearance;
    private MenuWindow? _menu;
    private DesktopIntegration? _integration;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly CancellationTokenSource _cancel = new();
    private Task<List<AppEntry>>? _catalog;
    private IReadOnlyList<RunningWindow> _windows = [];
    private bool _refreshing, _saving, _dirty;
    internal bool HotkeyAvailable => _integration?.HotkeyAvailable == true;

    internal DesktopEnvironment(DesktopSessionMode mode = DesktopSessionMode.Preview, string? hostToken = null)
    {
        Mode = mode; Menus = new(this); _usage = new(Session.State); _usageTracking = Session.State.UsageTracking;
        if (mode == DesktopSessionMode.DesktopShell)
        {
            if (!Guid.TryParseExact(hostToken, "N", out _)) throw new ArgumentException("Start desktop mode through Nexus.DesktopHost.exe.");
            _pulse = EventWaitHandle.OpenExisting(@"Local\WhiteDreams.Nexus.Pulse." + hostToken);
        }
    }
    internal void Start()
    {
        try
        {
            if (Mode == DesktopSessionMode.DesktopShell && ShellLayerInterop.ExplorerDesktopPresent())
                throw new InvalidOperationException("Windows Explorer is already providing this desktop. Use preview mode, or sign in after configuring Nexus in Personalize.");
            if (!Session.State.CatalogInitialized)
            { Session.State.PinnedApps = AppCatalog.Defaults(); Session.State.CatalogInitialized = true; _dirty = true; }
            for (int i = 0; i < Session.State.PinnedApps.Count; i++)
                if (IsExplorer(Session.State.PinnedApps[i].Target)) { Session.State.PinnedApps[i] = Session.State.PinnedApps[i] with { Target = "nexus:files" }; _dirty = true; }
            Theme.Apply(Session.State.Wallpaper);
            Desktop = new(this); Taskbar = new(this);
            Session.Changed += SessionChanged;
            _saveTimer.Tick += SaveTick; _timer.Tick += Tick;
            _integration = new(Desktop.Handle, command => Desktop.DispatcherQueue.TryEnqueue(() =>
            {
                if (IsStopping) return;
                if (command == "exit") RequestExit(); else if (command == "search") ShowMenu(true);
                else if (command == "show") ShowDesktop();
                else if (command == "tray-lost") Report("The Windows notification icon is unavailable. The Nexus taskbar is still running.");
            }));
            RefreshIntegration(); Desktop.ShowSurface(); Taskbar.ShowBar();
            if (Mode == DesktopSessionMode.DesktopShell)
                _keyboard = new(action => Desktop.DispatcherQueue.TryEnqueue(() => HandleShellKey(action)));
            RefreshDesktop(); UpdateTaskbar(); _timer.Start();
            _pulse?.Set();
            if (_dirty) _saveTimer.Start();
            if (Session.Store.RecoveryMessage.Length > 0) Report(Session.Store.RecoveryMessage);
        }
        catch { Shutdown(DesktopExitCode.Stop); throw; }
    }
    internal void SaveState() => Session.NotifyChanged();
    private void SessionChanged()
    {
        if (IsStopping) return;
        _dirty = true; _saveTimer.Stop(); _saveTimer.Start();
        RefreshAppearance(); RefreshIntegration();
        Taskbar.View.Refresh(_windows);
        if (_compact != Session.State.CompactDock) { _compact = Session.State.CompactDock; Taskbar.Position(); }
        if (_usageTracking != Session.State.UsageTracking) { _usageTracking = Session.State.UsageTracking; ResetUsageSample(); }
    }
    internal void RefreshAppearance()
    {
        if (IsStopping || Desktop is null) return;
        Theme.Apply(Session.State.Wallpaper);
        var appearance = (Theme.Palette.Name, Theme.HighContrast, Theme.Animations, Session.State.ReducedEffects, Session.State.DisplayName);
        if (_appearance == appearance) return;
        _appearance = appearance; Desktop.Surface.ApplyAppearance();
        Taskbar?.View.ApplyAppearance(); _menu?.ApplyAppearance(this);
        _files?.ApplyAppearance(); foreach (var picker in _pickers) picker.ApplyAppearance();
        _switcher?.ApplyAppearance();
    }
    internal void RefreshIntegration()
    {
        if (_integration is null || IsStopping) return;
        // Avoid re-registering hotkeys/tray icons for every notes keystroke.
        if (_lastShortcut != Session.State.GlobalShortcut)
        { _lastShortcut = Session.State.GlobalShortcut; if (!_integration.SetHotkey(_lastShortcut.Value)) Report("Ctrl+Alt+Space is already assigned. Use the taskbar search button."); }
        bool resident = Mode == DesktopSessionMode.Preview && Session.State.KeepAvailable;
        if (_lastResident != resident)
        { _lastResident = resident; if (!_integration.SetResident(resident)) Report("The notification icon is unavailable. Exit remains available in the desktop menu."); }
    }
    private bool? _lastShortcut, _lastResident;
    private async void SaveTick(object? sender, object args)
    {
        _saveTimer.Stop(); if (IsStopping || !_dirty || _saving) return;
        _saving = true; _dirty = false;
        try { await Session.SaveAsync(); }
        catch (Exception ex) { _dirty = true; Report("Could not save your desktop settings", ex); }
        finally { _saving = false; if (!IsStopping && _dirty) _saveTimer.Start(); }
    }
    internal void ResetUsageSample() { _usage.ResetSample(); _usageTicks = 0; }
    private void Tick(object? sender, object args)
    {
        if (IsStopping) return;
        _pulse?.Set();
        RefreshAppearance(); Taskbar.View.RefreshClock(); UpdateTaskbar();
        if (!Session.State.UsageTracking) return;
        try
        {
            _dirty |= _usage.Tick();
            if (++_usageTicks >= 12) { _usageTicks = 0; if (_dirty && !_saveTimer.IsEnabled) _saveTimer.Start(); }
        }
        catch (Exception ex) { Report("Usage sample skipped", ex, false); }
    }
    internal async void UpdateTaskbar()
    {
        if (IsStopping || Taskbar is null || _refreshing) return; _refreshing = true;
        try { var windows = await Task.Run(() => NativeMethods.RunningWindows(Desktop.Handle)); if (!IsStopping) { _windows = windows; Taskbar.View.Refresh(windows); } }
        catch (Exception ex) { Report("Could not refresh running windows", ex, false); }
        finally { _refreshing = false; }
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
        try { _menu ??= new(this); if (_menu.IsOpen && !search) _menu.HideMenu(); else _menu.ShowMenu(this, Taskbar.BarBounds, search); }
        catch (Exception ex) { Report("Could not open Start", ex); }
    }
    internal void HideMenu() => _menu?.HideMenu();
    internal void MenuClosed(MenuWindow window) { if (ReferenceEquals(_menu, window)) _menu = null; }
    internal Task<List<AppEntry>> GetCatalogAsync(bool refresh = false)
    {
        if (_catalog is null || _catalog.IsCanceled || _catalog.IsFaulted || (refresh && _catalog.IsCompleted))
            _catalog = Task.Run(() => AppCatalog.DiscoverShortcuts(_cancel.Token), _cancel.Token);
        return _catalog;
    }
    internal void OpenDesktopItem(DesktopShortcut item)
    { if (item.Id == "sections") ShowSections(); else OpenTarget(item.Target); }
    internal void Launch(AppEntry app)
    {
        try { OpenTargetChecked(app.Target); Session.State.Activity.Insert(0, new(DateTimeOffset.Now, "Opened " + app.Name)); if (Session.State.Activity.Count > 200) Session.State.Activity.RemoveRange(200, Session.State.Activity.Count - 200); SaveState(); }
        catch (Exception ex) { Report("Could not open " + app.Name, ex); }
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
        if (target == "nexus:sections") { ShowSections(); return; }
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
            else if (Mode == DesktopSessionMode.DesktopShell) throw new NotSupportedException("This Windows namespace shortcut has no app target. Pin the application executable or use Nexus Files.");
        }
        if (target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) || target.StartsWith("::{", StringComparison.Ordinal))
            throw new NotSupportedException("This Windows desktop location is not available in Nexus. Use My files or PC controls.");
        var start = new ProcessStartInfo(target) { UseShellExecute = true, Arguments = arguments };
        if (Directory.Exists(working)) start.WorkingDirectory = working;
        else if (File.Exists(target)) start.WorkingDirectory = Path.GetDirectoryName(target)!;
        Process.Start(start);
    }
    internal void ShowFiles(string? folder = null)
    {
        if (IsStopping) return; HideMenu(); folder ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (_files is null)
        {
            var window = new FilesWindow(this, new(FileSelectionKind.Browse), folder); _files = window;
            window.Closed += (_, _) => { if (ReferenceEquals(_files, window)) _files = null; if (!IsStopping) UpdateTaskbar(); };
        }
        else _files.ShowFolder(folder);
        UpdateTaskbar();
    }
    internal void ReturnToFiles() { if (_files is null) ShowFiles(); else _files.ReturnToWindow(); }
    internal async Task<IReadOnlyList<string>> PickAsync(FileSelectionRequest request)
    {
        if (IsStopping) return [];
        var window = new FilesWindow(this, request, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        _pickers.Add(window);
        try { return await window.Selection; }
        finally { _pickers.Remove(window); }
    }
    private void HandleShellKey(ShellKeyAction action)
    {
        if (IsStopping) return;
        switch (action)
        {
            case ShellKeyAction.Start: ShowMenu(); break;
            case ShellKeyAction.Files: ShowFiles(); break;
            case ShellKeyAction.ShowDesktop: ShowDesktop(); break;
            case ShellKeyAction.Settings: OpenTarget("ms-settings:"); break;
            case ShellKeyAction.Search: ShowMenu(true); break;
            case ShellKeyAction.Controls: ShowSections("PC controls"); break;
            case ShellKeyAction.Overview: ShowSwitcher(false, true); break;
            case ShellKeyAction.SwitchNext: ShowSwitcher(false, false); break;
            case ShellKeyAction.SwitchPrevious: ShowSwitcher(true, false); break;
            case ShellKeyAction.SwitchCommit: _switcher?.CommitSelection(); break;
        }
    }
    private List<RunningWindow> AppWindows()
    {
        var windows = NativeMethods.RunningWindows(Desktop.Handle).ToList();
        if (_sections is not null) windows.Add(new(WinRT.Interop.WindowNative.GetWindowHandle(_sections), "Sections", "nexus", Environment.ProcessId));
        if (_files is not null) windows.Add(new(_files.Handle, "Files", "nexus", Environment.ProcessId));
        foreach (var picker in _pickers) windows.Add(new(picker.Handle, picker.NativeWindow.Title, "nexus", Environment.ProcessId));
        return windows;
    }
    private void ShowSwitcher(bool reverse, bool overview)
    {
        if (IsStopping) return; HideMenu();
        try { IntPtr current = NativeMethods.GetForegroundWindow(); _switcher ??= new(this); _switcher.Show(AppWindows(), current, reverse, overview); }
        catch (Exception ex) { Report("Could not switch windows", ex); }
    }
    internal void ShowWindowOverview() => ShowSwitcher(false, true);
    internal void SwitcherClosed(SwitcherWindow window) { if (ReferenceEquals(window, _switcher)) _switcher = null; }
    internal void ShowDesktop()
    {
        if (IsStopping) return; HideMenu();
        try
        {
            _switcher?.Hide(); _desktopToggle.Toggle(AppWindows());
        }
        catch (Exception ex) { Report("Could not show the desktop", ex, false); }
        Desktop.ShowSurface(); Taskbar.ShowBar();
    }
    internal void Report(string message, Exception? error = null, bool visible = true)
    { Log.Write(message, error); if (!IsStopping && visible && Desktop is not null) Desktop.Surface.Report(error is null ? message : message + ": " + error.Message); }
    internal async void RequestExit()
    {
        if (IsStopping || _sessionDialog) return; HideMenu();
        if (Mode == DesktopSessionMode.Preview) { Shutdown(DesktopExitCode.Stop); return; }
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
    internal void Shutdown(DesktopExitCode? exitCode = null)
    {
        if (IsStopping) return; IsStopping = true;
        _timer.Stop(); _saveTimer.Stop(); Session.Changed -= SessionChanged;
        _cancel.Cancel();
        Environment.ExitCode = (int)(exitCode ?? (Mode == DesktopSessionMode.DesktopShell ? DesktopExitCode.RestoreWindows : DesktopExitCode.Stop));
        void Cleanup(Action action) { try { action(); } catch (Exception ex) { Log.Write("Desktop shutdown cleanup failed", ex); } }
        Cleanup(() => _sections?.Close()); _sections = null;
        Cleanup(() => _menu?.Close()); _menu = null;
        Cleanup(() => _files?.Close()); _files = null;
        Cleanup(() => _switcher?.Close()); _switcher = null;
        foreach (var picker in _pickers.ToArray()) Cleanup(picker.Close); _pickers.Clear();
        Cleanup(() => _keyboard?.Dispose()); _keyboard = null;
        Cleanup(() => _pulse?.Dispose()); _pulse = null;
        Cleanup(() => _integration?.Dispose()); _integration = null;
        // Release the working-area reservation before destroying any native window.
        Cleanup(() => Taskbar?.Dispose()); Cleanup(() => Taskbar?.Close());
        Cleanup(() => Desktop?.Dispose()); Cleanup(() => Desktop?.Close());
        Cleanup(Session.SaveFinal); _cancel.Dispose(); Stopped?.Invoke();
    }
}
