using System.Diagnostics;
using Nexus.Runtime;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;

namespace Nexus.Shell.Desktop;

internal sealed partial class DesktopEnvironment
{
    private readonly WindowsShellBridge _windowsShell = new();
    private bool _requestingWindowsShell;
    private AboutNexusWindow? _about;
    internal string WindowsShellStatus => _windowsShell.ShellAvailable ? "Explorer is running · Windows calendar, notifications and shell actions are available to request." : "Windows shell is unavailable · use Windows settings or start Explorer.";
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
            if (ClipboardSuspended) throw new InvalidOperationException("Windows panels cannot open while the session is locked or disconnected.");
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
    internal void StartWindowsShell()
    {
        if (IsStopping || _windowsShell.ShellAvailable) return;
        try { using var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { UseShellExecute = true }); }
        catch (Exception error) { Report("Explorer could not start", error); }
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
