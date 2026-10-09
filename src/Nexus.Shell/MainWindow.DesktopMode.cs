using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Services;
using System.Diagnostics;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    private StackPanel DesktopModeCard()
    {
        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(Text("Your Nexus desktop", 21));
        var capabilities = WindowsDesktopSettings.Capabilities();
        string host = Path.Combine(AppContext.BaseDirectory, "Nexus.DesktopHost.exe");
        bool configured = false;
        string status = _environment.Mode == DesktopSessionMode.NexusSession ? "Nexus owns the desktop for this session. Windows returns when you exit."
            : _environment.Mode == DesktopSessionMode.DesktopShell ? "Nexus is your desktop at sign-in." : "You’re previewing Nexus alongside the Windows desktop.";
        try
        {
            configured = new DesktopShellRegistration(new WindowsDesktopSettings(), DesktopShellRegistration.DefaultBackupPath).Uses(host);
            status += configured ? " This version is selected for your next sign-in." : " This version is not selected for sign-in.";
        }
        catch (Exception ex) { Log.Write("Desktop sign-in setting could not be read.", ex); status += " Sign-in policy is unavailable for this account. Session mode can still return to Windows when you exit."; }
        panel.Children.Add(Text(status, 13, true));
        panel.Children.Add(Text("Nexus provides the desktop, taskbar, Start, folders and file pickers. Windows continues to run your apps and system services. Settings, Control Panel and secure system prompts remain available.", 13, true));
        if (_environment.Mode == DesktopSessionMode.Preview)
        {
            var session = AsyncButton("Use Nexus for this session…", StartNexusSessionAsync);
            session.IsEnabled = File.Exists(host); panel.Children.Add(session);
            panel.Children.Add(Text("Use the Nexus desktop and taskbar for this session. Exit Nexus to return to Windows. Your sign-in settings stay as they are.", 12, true));
        }
        var enable = AsyncButton(configured ? "Use this version at sign-in" : "Use Nexus at sign-in…", EnableDesktopModeAsync);
        enable.IsEnabled = capabilities.SupportsCustomInterface && File.Exists(host);
        panel.Children.Add(enable);
        if (!capabilities.SupportsCustomInterface) panel.Children.Add(Text("Requires a supported Windows Pro, Enterprise or Education build.", 12, true));
        else if (!File.Exists(host)) panel.Children.Add(Text("Build or extract the complete release folder, including Nexus.DesktopHost.exe, first.", 12, true));
        panel.Children.Add(AsyncButton("Restore Windows desktop at sign-in…", RestoreDesktopModeAsync));
        panel.Children.Add(Text("Changing sign-in applies after sign-out and requires permission to write Windows desktop policy. Keep the full Nexus folder in its selected location. Your previous setting is saved for recovery.", 12, true));
        if (_environment.IsManagedDesktop) panel.Children.Add(ActionButton(_environment.Mode == DesktopSessionMode.NexusSession ? "Return to Windows" : "Desktop session controls…", _environment.RequestExit));
        return panel;
    }
    private static string CheckDesktopHost()
    {
        foreach (var name in new[] { "Nexus.DesktopHost.exe", "Nexus.DesktopHost.dll", "Nexus.DesktopHost.deps.json", "Nexus.DesktopHost.runtimeconfig.json" })
            if (!File.Exists(Path.Combine(AppContext.BaseDirectory, name))) throw new FileNotFoundException("The published desktop host is incomplete.", name);
        return Path.Combine(AppContext.BaseDirectory, "Nexus.DesktopHost.exe");
    }
    private async Task StartNexusSessionAsync()
    {
        if (_dialogOpen || !_ready || _environment.IsManagedDesktop) return; _dialogOpen = true;
        try
        {
            string host = CheckDesktopHost();
            var dialog = new ContentDialog { XamlRoot = DesktopRoot.XamlRoot, RequestedTheme = DesktopRoot.RequestedTheme,
                Title = "Use Nexus for this session?", Content = "Nexus will reopen as your desktop with its own taskbar. Your other apps and Windows services stay available.\n\nExit Nexus to return to the Windows desktop. Your sign-in settings will stay as they are.",
                PrimaryButtonText = "Start Nexus desktop", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
            PolishDialog(dialog);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || !_ready) return;
            var start = new ProcessStartInfo(host) { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory };
            start.ArgumentList.Add("--nexus-session"); start.ArgumentList.Add("--wait-for-preview"); start.ArgumentList.Add(Environment.ProcessId.ToString());
            using var launched = Process.Start(start) ?? throw new InvalidOperationException("The Nexus desktop host did not start.");
            _environment.Shutdown(DesktopExitCode.Stop);
        }
        finally { _dialogOpen = false; }
    }
    private async Task EnableDesktopModeAsync()
    {
        if (_dialogOpen || !_ready) return; _dialogOpen = true;
        try
        {
            string host = CheckDesktopHost();
            var dialog = new ContentDialog { XamlRoot = DesktopRoot.XamlRoot, RequestedTheme = DesktopRoot.RequestedTheme,
                Title = "Use Nexus as your desktop?", Content = "At your next sign-in, Nexus will start in place of the Windows desktop for this user. Windows services, apps, Settings and Control Panel remain available.\n\nCheck that this build opens correctly before changing sign-in. Keep this full folder in place:\n" + AppContext.BaseDirectory + "\n\nYour previous setting will be saved. Restore Windows from Personalize or run Restore-Windows-Desktop.bat if needed.",
                PrimaryButtonText = "Use Nexus at sign-in", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
            PolishDialog(dialog);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || !_ready) return;
            new DesktopShellRegistration(new WindowsDesktopSettings(), DesktopShellRegistration.DefaultBackupPath).Enable(host, WindowsDesktopSettings.Capabilities());
            RefreshStartupRepair();
            ShowStatus("Nexus is selected for your next sign-in. Save your work, then sign out when you’re ready.");
            Navigate("Personalize", false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Write("Windows denied desktop sign-in setup.", ex);
            ShowStatus("Windows denied access to desktop sign-in policy. Use Nexus for this session to run the Nexus desktop without changing sign-in settings.");
        }
        finally { _dialogOpen = false; }
    }
    private async Task RestoreDesktopModeAsync()
    {
        if (_dialogOpen || !_ready) return; _dialogOpen = true;
        try
        {
            var dialog = new ContentDialog { XamlRoot = DesktopRoot.XamlRoot, RequestedTheme = DesktopRoot.RequestedTheme,
                Title = "Restore your previous desktop?", Content = "Your saved desktop sign-in setting will be restored for this user. The current session will stay open. In a Nexus desktop session, use Session → Return to Windows to switch immediately.",
                PrimaryButtonText = "Restore sign-in setting", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
            PolishDialog(dialog);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || !_ready) return;
            bool restored = new DesktopShellRegistration(new WindowsDesktopSettings(), DesktopShellRegistration.DefaultBackupPath).Restore();
            RefreshStartupRepair();
            ShowStatus(restored ? "Your previous desktop sign-in setting is restored." : "No saved Nexus desktop setup was found. No sign-in setting changed.");
            Navigate("Personalize", false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Write("Windows denied desktop sign-in restoration.", ex);
            ShowStatus("Windows denied the sign-in policy change. You can still exit Nexus and restore Windows for the current session; the saved recovery record is retained.");
        }
        finally { _dialogOpen = false; }
    }
}
