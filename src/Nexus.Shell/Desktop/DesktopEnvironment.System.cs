using System.Diagnostics;
using Microsoft.UI.Xaml;
using Nexus.Shell.Interop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;

namespace Nexus.Shell.Desktop;

internal sealed partial class DesktopEnvironment
{
    internal KeepAwakeSession KeepAwake { get; } = new(WindowsPowerRequest.Acquire);
    internal event Action? SystemStatusChanged;
    private WindowsToolsWindow? _windowsTools;
    private WindowsDesktopChange _pendingSystemChanges;
    private readonly DispatcherTimer _systemRefreshTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool _systemSleeping, _openingWindowsTool;
    private DateTimeOffset? _lastWindowsResume;
    internal string WindowsIntegrationStatus => "Windows event bridge " + (_integration is null ? "unavailable" : "connected")
        + (_integration?.SleepNotificationsAvailable == true ? " · wake recovery registered" : " · standard sleep broadcasts only")
        + (_lastWindowsResume is { } at ? " · last recovery " + at.ToLocalTime().ToString("t") : " · waiting for sleep/display changes")
        + ". " + KeepAwake.Status;

    private void StartSystemIntegration()
    {
        _systemRefreshTimer.Tick += FlushSystemChanges;
        ActiveWindowChanged += RefreshSystemStatus;
    }
    private void RefreshSystemStatus() { if (!IsStopping) SystemStatusChanged?.Invoke(); }
    // Called on the owning HWND thread. Immediate suspend cleanup is small;
    // device, layout and service work is coalesced outside the native callback.
    private void QueueSystemChange(WindowsDesktopChange change)
    {
        if (IsStopping) return;
        if (change.HasFlag(WindowsDesktopChange.Suspend))
        {
            _systemSleeping = true; _systemRefreshTimer.Stop();
            KeepAwake.Stop("Stopped for sleep · start a new timer when needed.");
            ResetUsageSample();
            return;
        }
        if (change.HasFlag(WindowsDesktopChange.Resume))
        {
            _systemSleeping = false;
            // Explicit sleep also terminates Windows power requests. Never
            // display a timer as active after the OS has removed its request.
            KeepAwake.Stop("Off after sleep · Windows manages sleep normally.");
        }
        _pendingSystemChanges |= change;
        // Start once, not on every message: a sustained device burst cannot
        // keep postponing the next refresh forever.
        if (!_systemSleeping && !_systemRefreshTimer.IsEnabled) _systemRefreshTimer.Start();
    }
    private void FlushSystemChanges(object? sender, object args)
    {
        _systemRefreshTimer.Stop();
        if (IsStopping || _systemSleeping) return;
        var changes = _pendingSystemChanges; _pendingSystemChanges = WindowsDesktopChange.None;
        try
        {
            if (changes.HasFlag(WindowsDesktopChange.Resume))
            {
                _lastWindowsResume = DateTimeOffset.Now;
                ResetUsageSample(); HideMenu();
                RefreshDesktop(); CheckDesktopServices();
            }
            if ((changes & (WindowsDesktopChange.Resume | WindowsDesktopChange.Displays)) != 0)
            {
                _controlAnchor = null; // A saved screen coordinate may belong to a removed display.
                Taskbar.Position(); PositionDesktopPanels(); UpdateTaskbar();
            }
            if (changes.HasFlag(WindowsDesktopChange.Devices))
            { RefreshDesktop(); CheckDesktopServices(); }
            if (changes.HasFlag(WindowsDesktopChange.Power)) CheckDesktopServices();
            if ((changes & (WindowsDesktopChange.Resume | WindowsDesktopChange.Appearance)) != 0) RefreshAppearance();
            if ((changes & (WindowsDesktopChange.Resume | WindowsDesktopChange.Clock)) != 0)
            { Notifications.RefreshDue(); Taskbar.View.RefreshClock(); }
            RefreshSystemStatus();
        }
        catch (Exception error)
        {
            _pendingSystemChanges |= changes;
            Log.Write("Windows integration refresh deferred to the next desktop heartbeat", error);
        }
    }
    private void SessionAvailabilityChanged(bool suspended)
    {
        if (IsStopping) return;
        ResetUsageSample();
        if (suspended)
        { StopKeepAwake("Stopped while locked or disconnected."); HideMenu(); }
        else QueueSystemChange(WindowsDesktopChange.Displays | WindowsDesktopChange.Power | WindowsDesktopChange.Clock);
    }
    private void SystemIntegrationTick()
    {
        KeepAwake.Tick();
        if (_pendingSystemChanges != WindowsDesktopChange.None && !_systemSleeping && !_systemRefreshTimer.IsEnabled)
            _systemRefreshTimer.Start();
        // Uses the existing desktop heartbeat; no input hook or new poll loop.
        RefreshSystemStatus();
    }
    internal void StartKeepAwake(int minutes, bool display)
    {
        if (IsStopping || _systemSleeping) return;
        try
        {
            if (_clipboardFeed?.Available != true || ClipboardSuspended)
                throw new InvalidOperationException("Session notifications are unavailable or the session is locked. Use Session status to check services.");
            KeepAwake.Start(minutes, display); RefreshSystemStatus();
        }
        catch (Exception error) { Report("Keep awake could not start", error); }
    }
    internal void StopKeepAwake(string reason = "Off · Windows manages sleep normally.")
    { KeepAwake.Stop(reason); RefreshSystemStatus(); }
    internal void ShowWindowsTools()
    {
        if (IsStopping) return; HideMenu();
        try { _windowsTools ??= new(this); _windowsTools.Show(); }
        catch (Exception error) { Report("Windows tools could not open", error); }
    }
    internal void WindowsToolsClosed(WindowsToolsWindow window)
    { if (ReferenceEquals(_windowsTools, window)) _windowsTools = null; }
    internal async void RunWindowsTool(string id)
    {
        if (IsStopping || _openingWindowsTool) return;
        var tool = WindowsToolCatalog.Find(id); if (tool is null) return;
        _openingWindowsTool = true;
        try
        {
            HideMenu();
            if (tool.Kind == WindowsToolKind.Settings) await AppCatalog.OpenSettingsAsync(tool.Target);
            else if (tool.Kind == WindowsToolKind.ScreenSnip)
            {
                // Let panel hiding reach the compositor before asking Windows
                // to choose/capture an area. No image data is captured by Nexus.
                await Task.Delay(180, _cancel.Token);
                if (!await Windows.System.Launcher.LaunchUriAsync(new Uri(tool.Target)))
                    StartSystemExecutable("SnippingTool.exe");
            }
            else StartSystemExecutable(tool.Target);
        }
        catch (OperationCanceledException) when (IsStopping) { }
        catch (Exception error) { if (!IsStopping) Report(tool.Name + " could not open. It may be unavailable in this Windows installation", error); }
        finally { _openingWindowsTool = false; }
    }
    private static void StartSystemExecutable(string name)
    {
        string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (!Path.IsPathFullyQualified(system)) throw new DirectoryNotFoundException("The Windows system directory is unavailable.");
        string path = Path.Combine(system, name);
        if (!File.Exists(path)) throw new FileNotFoundException("The Windows tool is not installed.", path);
        using var process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path)! });
    }
    internal WindowsInputLayout CurrentInputLanguage() => WindowsInputLanguage.Current(ActiveWindow);
    internal void RequestInputLanguage(RunningWindow target, IntPtr layout)
    {
        if (IsStopping) return;
        try
        {
            if (!WindowsInputLanguage.Request(target, layout))
                Report("The input language could not be requested for this window. Try Win+Space in the app.");
            // The indicator remains observed state, never the requested value.
            RefreshSystemStatus();
        }
        catch (Exception error) { Report("Input language selection is unavailable. Try Win+Space in the app", error); }
    }
    private void StopSystemIntegration()
    {
        _systemRefreshTimer.Stop(); _systemRefreshTimer.Tick -= FlushSystemChanges;
        _pendingSystemChanges = WindowsDesktopChange.None; ActiveWindowChanged -= RefreshSystemStatus;
        KeepAwake.Dispose();
        _windowsTools?.Close(); _windowsTools = null;
    }
}
