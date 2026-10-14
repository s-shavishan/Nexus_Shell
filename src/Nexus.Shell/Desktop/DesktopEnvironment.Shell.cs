using System.Diagnostics;
using System.Runtime.InteropServices;
using Nexus.Runtime;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;

namespace Nexus.Shell.Desktop;

internal sealed partial class DesktopEnvironment
{
    private readonly WindowsShellBridge _windowsShell = new();
    private readonly WindowsShellRecoveryGate _shellRecovery = new();
    private string _windowsShellDetail = "";
    private DateTimeOffset? _lastWindowsShellRecovery;
    private bool _requestingWindowsShell;
    private AboutNexusWindow? _about;
    private bool WindowsSessionAvailable => !IsStopping && !_systemSleeping && ClipboardAvailable && !ClipboardSuspended;
    internal bool CanStartWindowsShell => WindowsSessionAvailable && _shellRecovery.Observed && _shellRecovery.Presence.Absent && !_shellRecovery.Starting;
    internal string WindowsShellStatus
    {
        get
        {
            string state = !_shellRecovery.Observed ? "Windows shell has not been checked yet."
                : _shellRecovery.Starting ? "Starting Windows shell · waiting for its desktop and taskbar."
                : _shellRecovery.Presence.Ready ? "Windows desktop and taskbar observed · native panels can be requested."
                : !_shellRecovery.Presence.Absent ? "Windows shell is partially available · another Explorer will not be started."
                : "Windows shell is unavailable · Windows settings remain accessible.";
            if (IsManagedDesktop && _shellRecovery.AutomaticPaused && !_shellRecovery.Presence.Ready) state += " Automatic recovery is paused; you can retry below.";
            if (_lastWindowsShellRecovery is { } at) state += " Last reconnection " + at.ToLocalTime().ToString("t") + ".";
            return _windowsShellDetail.Length == 0 ? state : state + " " + _windowsShellDetail;
        }
    }
    internal WindowsShellRoute WindowsRoute(WindowsShellSurface surface) => WindowsShellPolicy.Route(surface, Environment.OSVersion.Version.Build);
    internal void ShowAbout()
    { if (IsStopping) return; HideMenu(); try { _about ??= new(this); _about.Show(); } catch (Exception error) { Report("About Nexus could not open", error); } }
    internal void AboutClosed(AboutNexusWindow window) { if (ReferenceEquals(_about, window)) _about = null; }
    internal async void OpenWindowsSurface(WindowsShellSurface surface)
    {
        if (IsStopping || _requestingWindowsShell) return; _requestingWindowsShell = true;
        var route = WindowsRoute(surface);
        try
        {
            HideMenu(); await Task.Delay(120, _cancel.Token);
            if (!WindowsSessionAvailable) throw new InvalidOperationException("Windows panels need an active, unlocked session with session notifications connected.");
            var state = WindowsShellPolicy.Request(route, _windowsShell);
            if (state == WindowsShellRequestState.ShellUnavailable)
            { Report("Windows shell is unavailable. Opening " + route.Name + " settings."); await AppCatalog.OpenSettingsAsync(route.Fallback); }
            else if (state != WindowsShellRequestState.Requested)
                Report(state == WindowsShellRequestState.KeysHeld ? "Release held keys, then try again. " + route.Shortcut + " opens " + route.Name + "." : "Windows did not accept the panel shortcut. Use " + route.Shortcut + " or its settings menu.");
            await Task.Delay(400, _cancel.Token);
        }
        catch (OperationCanceledException) when (IsStopping) { }
        catch (Exception error) { if (!IsStopping) Report(route.Name + " could not be requested", error); }
        finally { _requestingWindowsShell = false; }
    }
    private void ObserveWindowsShell()
    {
        bool observed = _shellRecovery.Observed;
        bool changed = _shellRecovery.Observe(_windowsShell.Observe(), Environment.TickCount64);
        if (!changed || !observed) return;
        if (_shellRecovery.Presence.Ready) { _lastWindowsShellRecovery = DateTimeOffset.Now; _windowsShellDetail = ""; }
        QueueSystemChange(WindowsDesktopChange.Shell);
    }
    internal string CheckWindowsShellConnection()
    {
        if (IsStopping) return "The desktop is closing.";
        ObserveWindowsShell();
        QueueSystemChange(WindowsDesktopChange.Shell);
        RefreshSystemStatus();
        return WindowsShellStatus;
    }
    internal async Task<string> StartWindowsShellAsync(bool automatic = false)
    {
        if (IsStopping) return "The desktop is closing.";
        ObserveWindowsShell();
        var decision = _shellRecovery.Begin(Environment.TickCount64, WindowsSessionAvailable, automatic);
        if (decision != WindowsShellStartDecision.Start) return decision switch
        {
            WindowsShellStartDecision.Ready => "Windows shell is already available.",
            WindowsShellStartDecision.InProgress => "A Windows shell start is already being checked.",
            WindowsShellStartDecision.SessionUnavailable => "Unlock this session and check services before starting Windows shell.",
            WindowsShellStartDecision.Incomplete => "Windows shell is partially available. Let Windows finish starting; Nexus will not launch a duplicate Explorer.",
            WindowsShellStartDecision.Cooldown => "Please wait ten seconds between Windows shell start requests.",
            WindowsShellStartDecision.Paused => "Automatic Windows shell recovery is paused after three attempts. Use Start Windows shell to retry.",
            _ => "Waiting for Windows to recover its shell first."
        };
        RefreshSystemStatus();
        var token = _cancel.Token;
        try
        {
            // Fixed Windows image only. Never stop Explorer or a Windows app.
            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                using var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { UseShellExecute = false });
                if (process is null) throw new InvalidOperationException("Windows did not accept the Explorer start request.");
            }, token);
            long started = Environment.TickCount64;
            while (!IsStopping && Environment.TickCount64 - started < 8000)
            {
                ObserveWindowsShell();
                if (_shellRecovery.Presence.Ready) return "Windows shell reconnected. Calendar and notifications can be requested.";
                if (!WindowsSessionAvailable) return "The shell start was requested; further checks paused while this session is unavailable.";
                await Task.Delay(250, token);
            }
            _windowsShellDetail = "Explorer start was requested, but both shell surfaces were not observed within eight seconds. Check Windows settings or retry later.";
            if (automatic) Log.Write(_windowsShellDetail);
            return _windowsShellDetail;
        }
        catch (OperationCanceledException) when (IsStopping) { return "The desktop is closing."; }
        catch (Exception error)
        {
            _windowsShellDetail = "Explorer could not start: " + error.Message;
            Log.Write("Windows shell recovery failed", error);
            return _windowsShellDetail;
        }
        finally { _shellRecovery.Complete(); RefreshSystemStatus(); }
    }
    internal string WindowsShellDiagnostics()
    {
        string geometry = "";
        if (Taskbar is not null)
        {
            var monitor = ShellLayerInterop.Monitor(Taskbar.Handle);
            geometry = "\nMonitor: " + monitor.Monitor.Bounds + "\nObserved work area: " + monitor.Work.Bounds + "\nNexus work area: " + Taskbar.DesiredWorkArea;
        }
        return "Nexus Shell " + AboutNexusWindow.Version + "\n" + RuntimeInformation.OSDescription + " · " + RuntimeInformation.ProcessArchitecture
            + "\nSession: " + Mode + "\n" + WindowsShellStatus
            + "\nShell start attempts: " + _shellRecovery.Attempts
            + "\nCalendar: " + WindowsRoute(WindowsShellSurface.Calendar).Shortcut
            + "\nNotifications: " + WindowsRoute(WindowsShellSurface.Notifications).Shortcut
            + geometry;
    }
    internal async void WindowsFileAction(string path, string kind = "menu")
    {
        if (IsStopping) return; HideMenu();
        var (x, y) = WindowsShellBridge.Cursor();
        try
        {
            var action = new WindowsFileAction(Guid.NewGuid(), kind, path, Desktop.Handle.ToInt64(), x, y);
            var status = await _core.WindowsFileAsync(RuntimeOperations.WindowsFileAction, action, _cancel.Token);
            WindowsShellBridge.AllowForeground(status.ProcessId);
            // Detect prompt startup failures without blocking on a native dialog.
            for (int i = 0; i < 8 && !IsStopping; i++)
            {
                await Task.Delay(250, _cancel.Token);
                status = await _core.WindowsFileAsync(RuntimeOperations.WindowsFileStatus, new WindowsFileStatusRequest(action.Id), _cancel.Token);
                if (status.State == "Failed") { Report(status.Detail); break; }
                if (status.State == "Finished") break;
            }
        }
        catch (OperationCanceledException) when (IsStopping) { }
        catch (Exception error) { if (!IsStopping) Report("Windows file action unavailable. Review its menu/dialog before retrying", error); }
    }
}
