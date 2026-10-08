using Nexus.Shell.Services;
using System.Text.Json;

internal static class DesktopModeChecks
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Reject(Action action, string message)
    { bool failed = false; try { action(); } catch { failed = true; } Check(failed, message); }
    private sealed class Settings : IUserDesktopSettings
    {
        internal string BackupPath = "";
        internal bool FailStartup;
        internal ShellRegistryValue? ShellValue, StartupValue;
        public ShellRegistryValue? Shell
        {
            get => ShellValue;
            set { if (value?.Text.Contains("Nexus.DesktopHost", StringComparison.Ordinal) == true) Check(File.Exists(BackupPath), "Recovery must be durable before changing sign-in."); ShellValue = value; }
        }
        public ShellRegistryValue? NexusStartup
        {
            get => StartupValue;
            set { if (FailStartup) { FailStartup = false; throw new IOException("Injected registry write failure"); } StartupValue = value; }
        }
    }
    internal static void Run()
    {
        Check(new DesktopCapabilities(true, "Professional", 22631).SupportsCustomInterface, "Windows Pro must use the custom interface route.");
        Check(!new DesktopCapabilities(true, "Core", 22631).SupportsCustomInterface, "Windows Home cannot be selected as supported.");
        Check(!new DesktopCapabilities(false, "Professional", 22631).SupportsCustomInterface, "A Linux host cannot configure sign-in.");
        Check(DesktopWorkspace.IsAppTarget("nexus:files") && !DesktopWorkspace.IsAppTarget("nexus:unknown"), "Workspace plans must retain owned Nexus launchers and reject unknown commands.");
        Check(!new DesktopCapabilities(true, "Professional", 19041, 1201).SupportsCustomInterface && new DesktopCapabilities(true, "Professional", 19041, 1202).SupportsCustomInterface,
            "The documented Windows 10 policy patch floor must be checked.");
        string root = Path.Combine(Path.GetTempPath(), "Nexus-desktop-check-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string host = Path.Combine(root, "Nexus.DesktopHost.exe"), next = Path.Combine(root, "Next", "Nexus.DesktopHost.exe");
            File.WriteAllText(host, "fixture"); Directory.CreateDirectory(Path.GetDirectoryName(next)!); File.WriteAllText(next, "fixture");
            var settings = new Settings { BackupPath = Path.Combine(root, "recovery.json"), StartupValue = new("old preview command", ShellRegistryKind.ExpandString) };
            var originalRun = settings.StartupValue;
            var registration = new DesktopShellRegistration(settings, settings.BackupPath);
            Check(!registration.Restore(), "No backup must leave sign-in unchanged.");
            Reject(() => registration.Enable(host, new(true, "Core", 22631)), "Unsupported editions must not change sign-in.");
            Check(settings.Shell is null && !File.Exists(settings.BackupPath), "Unsupported setup must leave no recovery or policy changes.");
            registration.Enable(host, new(true, "Professional", 22631));
            using (var saved = JsonDocument.Parse(File.ReadAllText(settings.BackupPath)))
                Check(saved.RootElement.GetProperty("PreviousStartup").GetProperty("Kind").GetInt32() == 2,
                    "Expanded strings must retain the existing numeric recovery format.");
            Check(registration.Uses(host) && registration.OwnsCurrentSetting && settings.NexusStartup is null, "The host replaces the desktop and only Nexus's own Run entry is removed.");
            registration.Enable(next, new(true, "Professional", 22631));
            Check(registration.Uses(next), "A configured desktop must be able to move to a newer version folder.");
            settings.NexusStartup = new("new externally chosen preview command");
            registration.Restore();
            Check(settings.Shell is null && settings.NexusStartup?.Text == "new externally chosen preview command", "Recovery must preserve externally changed startup data.");
            settings.NexusStartup = originalRun; registration.Enable(host, new(true, "Professional", 22631));
            settings.FailStartup = true;
            Reject(() => registration.Enable(next, new(true, "Professional", 22631)), "A registry failure must be surfaced.");
            Check(registration.Uses(host), "A failed host path update must roll back to the active host.");
            Check(registration.Restore() && settings.Shell is null && settings.NexusStartup == originalRun, "Recovery after a failed update must still restore the original values and types.");
            settings.Shell = new("another-desktop.exe");
            Reject(() => registration.Enable(host, new(true, "Professional", 22631)), "Another custom desktop must not be replaced.");
            Check(settings.Shell.Text == "another-desktop.exe", "Foreign desktop policy must be preserved.");
            settings.Shell = new("explorer.exe", ShellRegistryKind.ExpandString); registration.Enable(host, new(true, "Professional", 22631));
            settings.Shell = new("policy-updated-externally.exe");
            Reject(() => registration.Restore(), "Recovery must refuse an externally replaced desktop policy.");
            settings.Shell = new(DesktopShellRegistration.CommandFor(host)); registration.Restore();
            Check(settings.Shell == new ShellRegistryValue("explorer.exe", ShellRegistryKind.ExpandString), "Recovery must restore an explicit desktop command without expanding it.");
            registration.Enable(host, new(true, "Professional", 22631));
            settings.FailStartup = true; Reject(() => registration.Restore(), "A partial recovery write failure must be reported.");
            Check(registration.Restore() && settings.NexusStartup == originalRun, "Retrying partial recovery must finish restoring the Run value.");
            string legacyStartup = "%LOCALAPPDATA%\\previous.exe";
            File.WriteAllText(settings.BackupPath, JsonSerializer.Serialize(new
            {
                Format = 1, Command = DesktopShellRegistration.CommandFor(host),
                PreviousShell = new { Text = "explorer.exe", Kind = 1 },
                PreviousStartup = new { Text = legacyStartup, Kind = 2 }
            }));
            settings.Shell = new(DesktopShellRegistration.CommandFor(host)); settings.NexusStartup = null;
            Check(registration.Restore() && settings.Shell == new ShellRegistryValue("explorer.exe")
                && settings.NexusStartup == new ShellRegistryValue(legacyStartup, ShellRegistryKind.ExpandString),
                "Existing numeric recovery records must restore string kinds and unexpanded text.");
            File.WriteAllText(settings.BackupPath, "{\"Format\":1,\"Command\":\"host\",\"PreviousShell\":{\"Text\":\"invalid\",\"Kind\":3}}");
            Reject(() => registration.Restore(), "Recovery must reject unsupported numeric registry kinds.");
            File.WriteAllText(settings.BackupPath, "{\"Format\":99,\"Command\":\"bad\"}");
            Reject(() => registration.Enable(host, new(true, "Professional", 22631)), "An invalid recovery record must block changes.");
            Reject(() => DesktopShellRegistration.CommandFor("relative.exe"), "The desktop command must use a full path.");
            Reject(() => DesktopShellRegistration.CommandFor(host + "\" argument"), "A desktop path must not inject command arguments.");
            string folder = Path.Combine(root, "Files"); Directory.CreateDirectory(folder); Directory.CreateDirectory(Path.Combine(folder, "Folder"));
            File.WriteAllText(Path.Combine(folder, "one.JSON"), "{}"); File.WriteAllText(Path.Combine(folder, "two.txt"), "text");
            var listing = FileCatalog.Read(folder, [".json"]);
            Check(listing.Entries.Count == 2 && listing.Entries[0].IsFolder && listing.Entries[1].Name == "one.JSON", "File selection must retain directories and filter extensions without case sensitivity.");
            Check(FileCatalog.SavePath(folder, "export", [".json"]) == Path.Combine(folder, "export.json"), "Saving must supply the requested file extension.");
            foreach (string invalid in new[] { "../escape", "..", "CON.txt", "LPT1", "x:y", "x\\y", "foo.", "line\nname" })
                Reject(() => FileCatalog.ChildPath(folder, invalid), "A new file/folder name must remain one Windows path segment: " + invalid);
            Reject(() => FileCatalog.SavePath(folder, "wrong.exe", [".json"]), "A save picker must reject an unsupported extension.");
            using var canceled = new CancellationTokenSource(); canceled.Cancel(); Reject(() => FileCatalog.Read(folder, null, canceled.Token), "Canceled reads must stop.");
            for (int i = 0; i < 1010; i++) File.WriteAllText(Path.Combine(folder, "entry-" + i), "");
            Check(FileCatalog.Read(folder).Limited && FileCatalog.Read(folder).Entries.Count <= FileCatalog.MaximumEntries, "Folder listing and realization must stay bounded.");
        }
        finally { Directory.Delete(root, true); }
        var keys = new ShellKeyboardState();
        Check(!keys.Process(0x5B, true).Consume && keys.Process(0x5B, false).Action == ShellKeyAction.Start, "Windows key alone must open Nexus Start on release.");
        keys.Process(0x5B, true); Check(keys.Process(0x45, true).Action == ShellKeyAction.Files && keys.Process(0x45, true).Action == ShellKeyAction.None, "Win+E must open Files once per press.");
        Check(keys.Process(0x45, false).Consume && keys.Process(0x5B, false).Action == ShellKeyAction.None, "Owned chord releases must not also open Start.");
        keys.Process(0x5B, true); Check(!keys.Process(0x4C, true).Consume && !keys.Process(0x4C, false).Consume && !keys.Process(0x5B, false).Consume, "Win+L and security shortcuts must pass to Windows.");
        keys.Process(0x5B, true, true); Check(!keys.Process(0x45, true, true).Consume && keys.Process(0x5B, false, true).Action == ShellKeyAction.None, "Ctrl/Alt/Shift chords must not be reinterpreted.");
        keys.Process(0x45, false); Check(!keys.Process(0x41, true).Consume, "Ordinary typing must pass through.");
        keys.Process(0xA4, true); Check(keys.Process(0x09, true).Action == ShellKeyAction.SwitchNext && keys.Process(0x09, true).Action == ShellKeyAction.None, "Alt+Tab must open the Nexus switcher once per press.");
        Check(keys.Process(0x09, false).Consume, "Tab release must remain owned while switching.");
        Check(keys.Process(0x09, true, true, true).Action == ShellKeyAction.SwitchPrevious, "Alt+Shift+Tab must cycle backwards.");
        keys.Process(0x09, false); Check(keys.Process(0xA4, false).Action == ShellKeyAction.SwitchCommit, "Releasing Alt must select the Nexus switcher window.");
        keys.Process(0xA4, true); Check(keys.Process(0x09, true, true, false, true).Action == ShellKeyAction.Overview, "Ctrl+Alt+Tab must open a persistent Nexus overview.");
        keys.Process(0x09, false); keys.Process(0xA4, false); keys.Process(0x5B, true);
        Check(keys.Process(0x09, true).Action == ShellKeyAction.Overview, "Win+Tab must open the Nexus overview."); keys.Process(0x09, false); keys.Process(0x5B, false);
        var budget = new ShellRestartBudget();
        Check(budget.MayRestart(TimeSpan.FromSeconds(2)) && budget.MayRestart(TimeSpan.FromSeconds(2)) && !budget.MayRestart(TimeSpan.FromSeconds(2)), "Recovery must allow two retries, then return to Windows.");
        Check(budget.MayRestart(TimeSpan.FromMinutes(5)), "A stable session must reset the crash budget.");
        Check(!budget.IsUnresponsive(TimeSpan.FromSeconds(44), TimeSpan.FromSeconds(44), false) && budget.IsUnresponsive(TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(45), false), "Startup heartbeat timeout must be bounded.");
        Check(!budget.IsUnresponsive(TimeSpan.FromHours(1), TimeSpan.FromSeconds(89), true) && budget.IsUnresponsive(TimeSpan.FromHours(1), TimeSpan.FromSeconds(90), true), "A missing UI heartbeat must trigger recovery after 90 seconds.");
        Console.WriteLine("PASS: Windows Pro capability, backup-before-policy, upgrade rollback, foreign-policy preservation, partial recovery retry, file I/O/filtering/bounds, shell shortcuts and restart/heartbeat policy.");
    }
}
