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
        bool configured = false, shellOwned = false, conflict = false, inspectionFailed = false;
        DesktopSignInStatus? signIn = null;
        string status = _environment.Mode == DesktopSessionMode.NexusSession ? "Nexus owns the desktop for this session. Windows returns when you exit."
            : _environment.Mode == DesktopSessionMode.DesktopShell ? "Nexus is your desktop at sign-in." : "You’re previewing Nexus alongside the Windows desktop.";
        try
        {
            var registration = new DesktopShellRegistration(new WindowsDesktopSettings(), DesktopShellRegistration.DefaultBackupPath);
            signIn = registration.Inspect(host); configured = signIn.Selected; shellOwned = signIn.Owned; conflict = signIn.Conflict;
            status += configured ? " This version is selected for your next sign-in." : " This version is not selected for sign-in.";
        }
        catch (Exception ex) { inspectionFailed = true; Log.Write("Desktop sign-in setting could not be read.", ex); status += " Sign-in policy or its recovery record could not be read. Use Inspect-Nexus-Startup.bat to review it."; }
        panel.Children.Add(Text(status, 13, true));
        panel.Children.Add(Text("Choose sign-in replacement to open Nexus after Windows authentication without first loading Explorer's desktop. Automatic startup alongside Windows opens Windows first, then Nexus.", 13, true));
        if (signIn?.Current is { } current)
        { var command = Text("Current sign-in command:\n" + current.Text, 12, true); command.IsTextSelectionEnabled = true; panel.Children.Add(command); }
        if (conflict) panel.Children.Add(Text(signIn?.Selected == true
            ? "This Nexus folder is selected, but its recovery record is missing or belongs to another command. Restore the matching backup before reconfiguring sign-in."
            : "Another sign-in command is present and does not match the Nexus recovery record. Use its original restore/uninstall option, or review Custom User Interface in your Windows user policy. Nexus keeps that command in place.", 12, true));
        panel.Children.Add(Text("Nexus provides the desktop, taskbar, Start, folders and file pickers. Windows continues to run your apps and system services. Settings, Control Panel and secure system prompts remain available.", 13, true));
        if (_environment.Mode == DesktopSessionMode.Preview)
        {
            var session = AsyncButton("Use Nexus for this session…", StartNexusSessionAsync);
            session.IsEnabled = File.Exists(host); panel.Children.Add(session);
            panel.Children.Add(Text("Use the Nexus desktop and taskbar for this session. Exit Nexus to return to Windows. Your sign-in settings stay as they are.", 12, true));
        }
        bool startupHere = false;
        try { startupHere = StartupRegistration.IsDesktopEnabled() && StartupRegistration.UsesCurrentVersion(); }
        catch (Exception error) { Log.Write("Desktop startup status unavailable", error); }
        var startup = AsyncButton(startupHere ? "Disable desktop startup after sign-in…" : "Start Nexus desktop after Windows sign-in…", ChangeDesktopStartupAsync);
        startup.IsEnabled = File.Exists(host) && !shellOwned; panel.Children.Add(startup);
        if (shellOwned) panel.Children.Add(Text("Restore the existing Nexus sign-in shell below before enabling startup alongside Windows.", 12, true));
        panel.Children.Add(Text("Start the supervised Nexus desktop after Windows signs you in. Windows remains available for recovery. Startup timing is controlled by Windows.", 12, true));
        var enable = AsyncButton(configured ? "Use this version as the sign-in shell" : "Start Nexus instead of the Windows desktop at sign-in…", EnableDesktopModeAsync);
        enable.IsEnabled = capabilities.SupportsCustomInterface && File.Exists(host) && !conflict && !inspectionFailed;
        panel.Children.Add(enable);
        if (!capabilities.SupportsCustomInterface) panel.Children.Add(Text("Requires a supported Windows Pro, Enterprise or Education build.", 12, true));
        else if (!File.Exists(host)) panel.Children.Add(Text("Build or extract the complete release folder, including Nexus.DesktopHost.exe, first.", 12, true));
        panel.Children.Add(AsyncButton("Restore Windows desktop at sign-in…", RestoreDesktopModeAsync));
        panel.Children.Add(Text("Shell replacement is a separate option on supported editions. Windows may request administrator permission for policy changes. Keep the full folder in its selected location; the previous setting is saved for recovery.", 12, true));
        if (_environment.IsManagedDesktop) panel.Children.Add(ActionButton(_environment.Mode == DesktopSessionMode.NexusSession ? "Return to Windows" : "Desktop session controls…", _environment.RequestExit));
        return panel;
    }
    private static string CheckDesktopHost()
    {
        foreach (var name in new[] { "Nexus.DesktopHost.exe", "Nexus.DesktopHost.dll", "Nexus.DesktopHost.deps.json", "Nexus.DesktopHost.runtimeconfig.json", "Nexus.Core.exe", "Nexus.Core.dll", "Nexus.Core.deps.json", "Nexus.Core.runtimeconfig.json", "Nexus.Runtime.dll" })
            if (!File.Exists(Path.Combine(AppContext.BaseDirectory, name))) throw new FileNotFoundException("The published desktop host is incomplete.", name);
        return Path.Combine(AppContext.BaseDirectory, "Nexus.DesktopHost.exe");
    }
    private async Task ChangeDesktopStartupAsync()
    {
        if (_dialogOpen || !_ready) return; _dialogOpen = true;
        try
        {
            bool enable = !(StartupRegistration.IsDesktopEnabled() && StartupRegistration.UsesCurrentVersion());
            if (enable) CheckDesktopHost();
            var dialog = new ContentDialog { XamlRoot = DesktopRoot.XamlRoot, RequestedTheme = DesktopRoot.RequestedTheme,
                Title = enable ? "Start Nexus after Windows sign-in?" : "Disable Nexus desktop startup?",
                Content = enable ? "Windows will sign you in normally, then Nexus will start its supervised desktop. Exit Nexus to return to Windows. Keep this complete folder in place:\n\n" + AppContext.BaseDirectory
                    : "Nexus will stop starting automatically. This session stays open, and your Windows desktop sign-in setting stays as it is.",
                PrimaryButtonText = enable ? "Enable desktop startup" : "Disable startup", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
            PolishDialog(dialog);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || !_ready) return;
            StartupRegistration.SetDesktopEnabled(enable); RefreshStartupRepair();
            _syncingPersonalization = true;
            try { StartupSwitch.IsOn = StartupRegistration.IsEnabled(); }
            finally { _syncingPersonalization = false; }
            ShowStatus(enable ? "Nexus desktop will start after Windows signs you in." : "Nexus automatic startup is disabled.");
            Navigate("Personalize", false);
        }
        finally { _dialogOpen = false; }
    }
    private static async Task<bool?> ApplyDesktopPolicyAsync(bool enable)
    {
        try
        {
            var registration = new DesktopShellRegistration(new WindowsDesktopSettings(), DesktopShellRegistration.DefaultBackupPath);
            if (!enable) return registration.Restore();
            registration.Enable(CheckDesktopHost(), WindowsDesktopSettings.Capabilities()); return true;
        }
        catch (Exception error) when (error is UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Write("Desktop policy requires Windows permission", error);
            return await DesktopPolicyElevation.ApplyAsync(enable) ? true : (bool?)null;
        }
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
                Title = "Use Nexus as your desktop?", Content = "Windows will boot and authenticate you normally. At your next sign-in, Nexus will start instead of Explorer's desktop for this user.\n\nCheck that this build opens correctly before changing sign-in. Keep this full folder in place:\n" + AppContext.BaseDirectory + "\n\nYour previous setting will be saved. Restore sign-in using Restore-Nexus-SignIn.bat; Restore-Windows-Desktop.bat restores the current desktop session.",
                PrimaryButtonText = "Use Nexus at sign-in", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
            PolishDialog(dialog);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || !_ready) return;
            if (await ApplyDesktopPolicyAsync(true) is not true) { ShowStatus("The Windows permission request was cancelled. Desktop policy was not changed by the helper."); return; }
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
            bool? restored = await ApplyDesktopPolicyAsync(false);
            RefreshStartupRepair();
            ShowStatus(restored switch { true => "Your previous desktop sign-in setting is restored.", false => "No saved Nexus desktop setup was found. No sign-in setting changed.", null => "The Windows permission request was cancelled. The saved recovery record is retained." });
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
