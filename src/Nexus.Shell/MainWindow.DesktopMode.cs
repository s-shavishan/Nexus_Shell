using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Nexus.Shell.Services;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    private StackPanel DesktopModeCard()
    {
        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(Text("Your desktop at sign-in", 21));
        var capabilities = WindowsDesktopSettings.Capabilities();
        string host = Path.Combine(AppContext.BaseDirectory, "Nexus.DesktopHost.exe");
        bool configured = false; string status;
        try
        {
            configured = new DesktopShellRegistration(new WindowsDesktopSettings(), DesktopShellRegistration.DefaultBackupPath).Uses(host);
            status = _environment.Mode == DesktopSessionMode.DesktopShell ? "Nexus is your desktop for this session." : "You’re previewing Nexus alongside the Windows desktop.";
            status += configured ? " This version is selected for your next sign-in." : " This version is not selected for sign-in.";
        }
        catch (Exception ex) { status = "Desktop sign-in setting could not be read: " + ex.Message; }
        panel.Children.Add(Text(status, 13, true));
        panel.Children.Add(Text("Nexus provides the desktop, taskbar, Start, folders and file pickers. Windows continues to run your apps and system services. Settings, Control Panel and secure system prompts remain available.", 13, true));
        var enable = AsyncButton(configured ? "Use this version at sign-in" : "Use Nexus at sign-in…", EnableDesktopModeAsync);
        enable.IsEnabled = capabilities.SupportsCustomInterface && File.Exists(host);
        panel.Children.Add(enable);
        if (!capabilities.SupportsCustomInterface) panel.Children.Add(Text("Requires a supported Windows Pro, Enterprise or Education build.", 12, true));
        else if (!File.Exists(host)) panel.Children.Add(Text("Build or extract the complete release folder, including Nexus.DesktopHost.exe, first.", 12, true));
        panel.Children.Add(AsyncButton("Restore Windows desktop at sign-in…", RestoreDesktopModeAsync));
        panel.Children.Add(Text("The change applies to this user after sign-out. Keep the full Nexus folder in its current location. Your previous sign-in setting is saved for recovery.", 12, true));
        if (_environment.Mode == DesktopSessionMode.DesktopShell) panel.Children.Add(ActionButton("Desktop session controls…", _environment.RequestExit));
        return panel;
    }
    private async Task EnableDesktopModeAsync()
    {
        if (_dialogOpen || !_ready) return; _dialogOpen = true;
        try
        {
            string host = Path.Combine(AppContext.BaseDirectory, "Nexus.DesktopHost.exe");
            foreach (var name in new[] { "Nexus.DesktopHost.exe", "Nexus.DesktopHost.dll", "Nexus.DesktopHost.deps.json", "Nexus.DesktopHost.runtimeconfig.json" })
                if (!File.Exists(Path.Combine(AppContext.BaseDirectory, name))) throw new FileNotFoundException("The published desktop host is incomplete.", name);
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
        finally { _dialogOpen = false; }
    }
}
