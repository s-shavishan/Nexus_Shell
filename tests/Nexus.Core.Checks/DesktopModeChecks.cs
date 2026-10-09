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
    private sealed class Surfaces : IWindowsDesktopSurfaces
    {
        internal readonly Dictionary<long, WindowsDesktopSurface> Values = [];
        internal long FailHandle;
        internal int VisibilityWrites;
        public IReadOnlyList<WindowsDesktopSurface> Read() => Values.Values.ToList();
        public bool Matches(WindowsDesktopSurface saved) => Values.TryGetValue(saved.Handle, out var now) && now.ProcessId == saved.ProcessId && now.ClassName == saved.ClassName;
        public void SetVisible(WindowsDesktopSurface saved, bool visible)
        { if (!Matches(saved)) return; if (saved.Handle == FailHandle) throw new IOException("Injected surface failure"); VisibilityWrites++; Values[saved.Handle] = Values[saved.Handle] with { Visible = visible }; }
    }
    internal static void Run()
    {
        var surfaces = new Surfaces();
        surfaces.Values[1] = new(1, 100, "Shell_TrayWnd", true);
        surfaces.Values[2] = new(2, 100, "WorkerW", true);
        surfaces.Values[4] = new(4, 100, "Progman", true);
        surfaces.Values[5] = new(5, 100, "WorkerW", false);
        surfaces.Values[6] = new(6, 100, "unrelated-app", true);
        var lease = new DesktopSurfaceLease(surfaces);
        lease.TakeOver(); Check(!surfaces.Values[1].Visible && surfaces.Values[2].Visible && surfaces.Values[4].Visible
            && !surfaces.Values[5].Visible && surfaces.Values[6].Visible,
            "Takeover must hide the taskbar while preserving visible/hidden Explorer desktop windows and unrelated apps.");
        for (int i = 0; i < 1000; i++) lease.Maintain();
        Check(surfaces.VisibilityWrites == 1, "Steady session maintenance must not repeatedly hide an unchanged taskbar or any desktop window.");
        surfaces.Values[3] = new(3, 101, "Shell_SecondaryTrayWnd", true); lease.Maintain();
        surfaces.Values[1] = surfaces.Values[1] with { Visible = true }; lease.Maintain();
        Check(!surfaces.Values[1].Visible && !surfaces.Values[3].Visible && surfaces.VisibilityWrites == 3,
            "A reappearing primary taskbar and newly created secondary taskbar must each be hidden once.");
        surfaces.Values[1] = new(1, 999, "unrelated-app", false);
        lease.Restore();
        Check(!surfaces.Values[1].Visible && surfaces.Values[2].Visible && surfaces.Values[3].Visible
            && surfaces.Values[4].Visible && !surfaces.Values[5].Visible && surfaces.Values[6].Visible,
            "Restoration must preserve original visibility, restore newly discovered taskbars and reject reused handles.");
        var partialSurfaces = new Surfaces { FailHandle = 8 };
        partialSurfaces.Values[8] = new(8, 100, "Shell_TrayWnd", false); partialSurfaces.Values[9] = new(9, 100, "Shell_SecondaryTrayWnd", false);
        Reject(() => DesktopSurfaceLease.RestoreSaved(partialSurfaces, partialSurfaces.Read().Select(s => s with { Visible = true })), "A failed native surface operation must be reported.");
        Check(partialSurfaces.Values[9].Visible, "One failed taskbar restore must not stop the other surface restorations.");
        bool shown = false, area = false, explorer = false;
        var recovery = DesktopSessionRecovery.Restore(() => throw new UnauthorizedAccessException("policy denied"), () => shown = true, () => area = true, () => explorer = true);
        Check(shown && area && explorer && recovery.WindowsDesktopRequested && !recovery.SignInRestored && recovery.Errors.Count == 1,
            "Denied policy permission must never stop surface, work-area or Explorer recovery.");
        recovery = DesktopSessionRecovery.Restore(null, () => shown = true, () => area = true, () => explorer = true);
        Check(recovery.SignInRestored && recovery.WindowsDesktopRequested && recovery.Errors.Count == 0, "Session-only recovery must complete with no sign-in policy operation.");
        explorer = false;
        recovery = DesktopSessionRecovery.Restore(null, () => throw new IOException("surface failure"), () => throw new IOException("area failure"), () => explorer = true);
        Check(explorer && recovery.Errors.Count == 2, "Explorer recovery must still run if either preceding desktop operation fails.");
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
            var record = new DesktopSessionRecord(Path.Combine(root, "session.json"));
            var snapshot = new DesktopSessionSnapshot(1, 7, host, new(0, 0, 1920, 1080), new(0, 0, 1920, 1032), []);
            Check(record.Read(7) is null, "No saved session must leave desktop state untouched.");
            var savedSurfaces = new Surfaces(); savedSurfaces.Values[4] = new(4, 100, "Shell_TrayWnd", true); savedSurfaces.Values[5] = new(5, 100, "WorkerW", false);
            var savedLease = new DesktopSurfaceLease(savedSurfaces, before =>
            {
                Check(savedSurfaces.Values[4].Visible, "The journal must be written before hiding the Windows taskbar.");
                record.Save(snapshot with { Surfaces = before });
            });
            savedLease.TakeOver();
            var savedSession = record.Read(7)!;
            Check(savedSession.Work == snapshot.Work && savedSession.Surfaces.Single().Handle == 4 && savedSession.Surfaces.Single().Visible,
                "New recovery journals must retain the original work area and taskbar visibility without owning Explorer desktop windows.");
            DesktopSurfaceLease.RestoreSaved(savedSurfaces, savedSession.Surfaces);
            Check(savedSurfaces.Values[4].Visible && !savedSurfaces.Values[5].Visible, "Independent recovery must restore the saved visibility after the host lease is lost.");
            var legacySnapshot = snapshot with { Surfaces = [new WindowsDesktopSurface(5, 100, "WorkerW", true)] };
            record.Save(legacySnapshot);
            DesktopSurfaceLease.RestoreSaved(savedSurfaces, record.Read(7)!.Surfaces);
            Check(savedSurfaces.Values[5].Visible, "Recovery must still restore Explorer desktop windows from interrupted 1.4.0 journals.");
            Reject(() => record.Read(8), "Session recovery must reject another interactive session's record.");
            Reject(() => record.Save(snapshot with { Work = new(0, 0, 2500, 1032) }), "The saved work area must fit its monitor.");
            var cannotSave = new DesktopSurfaceLease(savedSurfaces, _ => throw new UnauthorizedAccessException("journal denied"));
            Reject(cannotSave.TakeOver, "An unwritable recovery journal must reject takeover.");
            Reject(cannotSave.Maintain, "A journal failure must remain pending on a retry.");
            Check(savedSurfaces.Values[4].Visible, "A recovery-record permission failure must leave Windows visible.");
            File.WriteAllText(Path.Combine(root, "session.json"), "{\"Format\":99}");
            Reject(() => record.Read(7), "Invalid session state must not supply native window handles.");
            record.Delete(); Check(record.Read(7) is null, "Successful restoration must permit removing its session record.");
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
        var budget = new ShellRestartBudget();
        Check(budget.MayRestart(TimeSpan.FromSeconds(2)) && budget.MayRestart(TimeSpan.FromSeconds(2)) && !budget.MayRestart(TimeSpan.FromSeconds(2)), "Recovery must allow two retries, then return to Windows.");
        Check(budget.MayRestart(TimeSpan.FromMinutes(5)), "A stable session must reset the crash budget.");
        Check(!budget.IsUnresponsive(TimeSpan.FromSeconds(44), TimeSpan.FromSeconds(44), false) && budget.IsUnresponsive(TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(45), false), "Startup heartbeat timeout must be bounded.");
        Check(!budget.IsUnresponsive(TimeSpan.FromHours(1), TimeSpan.FromSeconds(89), true) && budget.IsUnresponsive(TimeSpan.FromHours(1), TimeSpan.FromSeconds(90), true), "A missing UI heartbeat must trigger recovery after 90 seconds.");
        Console.WriteLine("PASS: Windows Pro capability, backup-before-policy, upgrade rollback, foreign-policy preservation, partial recovery retry, file I/O/filtering/bounds, restart/heartbeat policy.");
        Console.WriteLine("PASS: session takeover/restoration, durable visibility and work-area recovery, refused journal writes, reused handles, denied-policy recovery and denied-policy handling.");
    }
}
