using System.Text.Json;
using Nexus.Shell.Models;
using Nexus.Shell.Services;

internal static class EverydayDesktopChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        void Reject(Action action, string message) { bool rejected = false; try { action(); } catch (Exception e) when (e is ArgumentException or IOException or InvalidOperationException) { rejected = true; } Check(rejected, message); }
        var state = new ShellState();
        var alpha = new AppEntry("one", "Alpha", "alpha.exe", "", "Development");
        var beta = new AppEntry("two", "Beta", "beta.exe", "", "Game");
        var zeta = new AppEntry("three", "Zeta", "zeta.exe", "", "Utility");
        var catalog = new[] { zeta, beta, alpha };
        var group = DesktopOrganization.CreateCollection(state, "  Work  ");
        Check(group.Name == "Work", "Collection names must be trimmed.");
        Reject(() => DesktopOrganization.CreateCollection(state, "work"), "Duplicate collection names must be rejected without adding another.");
        foreach (string name in new[] { "", "  ", "bad\nname", new string('x', 41) }) Reject(() => DesktopOrganization.CreateCollection(state, name), "Invalid collection names must not enter state.");
        DesktopOrganization.ToggleMember(state, group.Id, alpha.Target); DesktopOrganization.ToggleMember(state, group.Id, zeta.Target);
        Check(DesktopOrganization.Apps(catalog, state, "collection:" + group.Id, "").Select(a => a.Name).SequenceEqual(["Alpha", "Zeta"]), "A collection must resolve existing apps and obey A–Z order.");
        DesktopOrganization.ToggleMember(state, group.Id, "ALPHA.EXE");
        Check(group.Targets.SequenceEqual([zeta.Target]), "Collection membership must be case-insensitive and reversible.");
        DesktopOrganization.ToggleMember(state, group.Id, "missing.exe");
        Check(DesktopOrganization.Apps(catalog, state, "collection:" + group.Id, "").Count == 1, "An uninstalled collection target must not become a launchable ghost.");
        Check(DesktopOrganization.Apps(catalog, state, "collection:missing", "").Count == 0, "Removed collection selection must not unexpectedly show all apps.");
        DesktopOrganization.RenameCollection(state, group.Id, "Build");
        Check(state.AppCollections[0].Targets.Count == 2 && state.AppCollections[0].Name == "Build", "Renaming a collection must retain its members.");
        var snapshot = state.Snapshot(); snapshot.AppCollections[0].Targets.Clear();
        Check(state.AppCollections[0].Targets.Count == 2, "Queued snapshots must own independent collection lists.");
        state.PinnedApps = [beta, alpha]; state.RecentApps = [zeta, alpha];
        DesktopOrganization.SetHidden(state, alpha.Target, true); DesktopOrganization.SetHidden(state, "ALPHA.EXE", true);
        Check(state.HiddenAppTargets.Count == 1 && state.PinnedApps.Count == 2, "Hiding must deduplicate and preserve dock pins.");
        foreach (string section in new[] { "All", "Pinned", "Recent", "Development" }) Check(DesktopOrganization.Apps(catalog, state, section, "").All(a => a.Target != alpha.Target), "Hidden apps must stay out of normal Launchpad sections.");
        Check(DesktopOrganization.Apps(catalog, state, "Hidden", "ALP").Single() == alpha, "Hidden apps must remain searchable and recoverable.");
        DesktopOrganization.SetHidden(state, "ALPHA.EXE", false);
        Check(DesktopOrganization.Apps(catalog, state, "Hidden", "").Count == 0 && DesktopOrganization.Apps(catalog, state, "All", "").Count == 3, "Restoring hidden apps must restore their normal visibility.");
        Check(DesktopOrganization.Apps(catalog, state, "Pinned", "").SequenceEqual([beta, alpha]), "Pinned view must preserve dock order.");
        Check(DesktopOrganization.Apps(catalog, state, "Recent", "").SequenceEqual([zeta, alpha]), "Recent view must preserve recency order.");
        state.RememberRecentItems = false; Check(DesktopOrganization.Apps(catalog, state, "Recent", "").Count == 0, "Recent opt-out must remain effective.");
        Check(DesktopOrganization.MovePin(state.PinnedApps, alpha.Target, -1) && state.PinnedApps[0] == alpha, "Dock pin movement must change ordering.");
        Check(!DesktopOrganization.MovePin(state.PinnedApps, alpha.Target, -1) && !DesktopOrganization.MovePin(state.PinnedApps, "missing", 1), "Dock boundaries and missing pins must be harmless.");
        state.LaunchpadOrder = "Catalog"; Check(DesktopOrganization.Apps(catalog, state, "All", "").SequenceEqual(catalog), "Catalog order must be selectable.");
        Check(DesktopOrganization.Apps(catalog, state, "Games", "").Single() == beta && DesktopOrganization.Apps(catalog, state, "All", "tools").Count == 0, "Categories must use app metadata without inventing search matches.");
        for (int i = 1; i < 12; i++) DesktopOrganization.CreateCollection(state, "Group " + i);
        Reject(() => DesktopOrganization.CreateCollection(state, "Overflow"), "Collection capacity must be enforced before mutation.");
        var capped = state.AppCollections[0]; capped.Targets.Clear();
        for (int i = 0; i < 100; i++) DesktopOrganization.ToggleMember(state, capped.Id, "app-" + i);
        Reject(() => DesktopOrganization.ToggleMember(state, capped.Id, "one-too-many"), "Collection membership must stay bounded.");
        DesktopOrganization.ToggleMember(state, capped.Id, "app-0"); Check(capped.Targets.Count == 99, "Removing a member must still work at the limit.");
        state.HiddenAppTargets = Enumerable.Range(0, 600).Select(i => "hidden-" + i).ToList();
        Reject(() => DesktopOrganization.SetHidden(state, "overflow", true), "Hidden apps must remain bounded.");
        DesktopOrganization.SetHidden(state, "hidden-0", false); Check(state.HiddenAppTargets.Count == 599, "Hidden-app recovery must work at the limit.");
        var root = Path.Combine(Path.GetTempPath(), "Nexus-everyday-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var places = new ShellState(); var pin = DesktopOrganization.AddPlace(places, "Workspace", root);
            Check(DesktopOrganization.AddPlace(places, "Other label", root + Path.DirectorySeparatorChar).Id == pin.Id && places.Places.Count == 1, "Duplicate folder pins must canonicalize trailing separators.");
            DesktopOrganization.RenamePlace(places, pin.Id, "My work"); Check(places.Places[0].Path == root, "Renaming a place must not rename a filesystem folder.");
            Reject(() => DesktopOrganization.AddPlace(places, "Bad", "relative/folder"), "Relative favorite paths must be rejected.");
            Reject(() => DesktopOrganization.AddPlace(places, "Bad", "nexus:files"), "Places must contain folders, not arbitrary shell routes.");
            for (int i = 1; i < 24; i++) DesktopOrganization.AddPlace(places, "Folder " + i, Path.Combine(root, "offline-" + i));
            Reject(() => DesktopOrganization.AddPlace(places, "Overflow", Path.Combine(root, "overflow")), "Favorite folders must stay bounded, including unavailable locations.");
            Check(DesktopOrganization.MovePlace(places, pin.Id, 1) && places.Places[1].Id == pin.Id, "Place reordering must preserve stable identity.");
            places.AppCollections = state.AppCollections; places.ShowDesktopIcons = false; places.DesktopIconOrder = "Newest";
            var store = new StateStore(Path.Combine(root, "settings")); store.Save(places); var loaded = store.Load();
            Check(loaded.Places.Count == 24 && loaded.Places[1].Name == "My work" && loaded.AppCollections.Count == 12 && !loaded.ShowDesktopIcons && loaded.DesktopIconOrder == "Newest", "New organization choices must survive actual Core-format persistence.");
            var legacy = StateStore.NormalizeState(JsonSerializer.Deserialize<ShellState>("{\"QuickNote\":\"keep\",\"Wallpaper\":\"Midnight\"}")!);
            Check(legacy.ShowDesktopIcons && legacy.Places.Count == 0 && legacy.AppCollections.Count == 0 && legacy.QuickNote == "keep" && legacy.Wallpaper == "Midnight", "Older settings must preserve existing content with usable new defaults.");
            var malformed = new ShellState { AppCollections = [null!, new(capped.Id, "good", null!), new(capped.Id.ToUpperInvariant(), "duplicate id", []), new("bad", "bad", [])], HiddenAppTargets = [null!, "x", "X", "bad\0path"], Places = [new(pin.Id, "Home", root), new(pin.Id.ToUpperInvariant(), "Duplicate", Path.Combine(root, "other"))], LaunchpadOrder = "invalid", DesktopIconOrder = "invalid" };
            StateStore.NormalizeState(malformed);
            Check(malformed.AppCollections.Count == 1 && malformed.AppCollections[0].Targets.Count == 0 && malformed.HiddenAppTargets.SequenceEqual(["x"]) && malformed.Places.Count == 1 && malformed.LaunchpadOrder == "Name" && malformed.DesktopIconOrder == "Name", "Damaged settings must normalize nulls, duplicate identities and unknown modes.");
            var edits = new FileMutationHistory(); string source = Path.Combine(root, "report.txt"); File.WriteAllText(source, "original");
            string renamed = edits.Rename(source, "renamed.txt"); Check(!File.Exists(source) && File.ReadAllText(renamed) == "original" && edits.CanUndo, "Rename must move the original without changing file content.");
            Check(edits.Undo() == source && File.ReadAllText(source) == "original" && !edits.CanUndo, "Rename undo must restore the original path.");
            string occupied = Path.Combine(root, "occupied.txt"); File.WriteAllText(occupied, "existing");
            Reject(() => edits.Rename(source, "occupied.txt"), "Rename must reject existing destinations.");
            Check(File.ReadAllText(source) == "original" && File.ReadAllText(occupied) == "existing", "Conflicting rename must preserve both files.");
            foreach (string name in new[] { "../escape", "a/b", "NUL", "COM1.txt", "bad:name", "bad\0name", "same." }) Reject(() => edits.Rename(source, name), "Reserved names and traversal must be rejected.");
            renamed = edits.Rename(source, "renamed.txt"); File.WriteAllText(source, "replacement"); Reject(() => edits.Undo(), "Undo must refuse to overwrite a newly occupied original path.");
            Check(File.ReadAllText(renamed) == "original" && File.ReadAllText(source) == "replacement", "Undo conflicts must preserve current data.");
            File.Delete(source); edits.Undo();
            renamed = edits.Rename(source, "renamed.txt"); File.AppendAllText(renamed, " changed"); Reject(() => edits.Undo(), "Undo must leave edited files untouched.");
            Check(File.ReadAllText(renamed) == "original changed", "Edited file content must remain intact.");
            var folderEdits = new FileMutationHistory(); string folder = folderEdits.CreateFolder(root, "New folder");
            Check(Directory.Exists(folder) && folderEdits.CanUndo, "New folders must be recorded for undo.");
            folderEdits.Undo(); Check(!Directory.Exists(folder), "An unchanged empty created folder can be undone.");
            folder = folderEdits.CreateFolder(root, "New folder");
            File.WriteAllText(Path.Combine(folder, "keep.txt"), "keep"); Reject(() => folderEdits.Undo(), "New-folder undo must refuse a nonempty folder.");
            Check(File.ReadAllText(Path.Combine(folder, "keep.txt")) == "keep", "Folder undo must never remove user content.");
            Reject(() => folderEdits.CreateFolder(root, "New folder"), "Creation must not adopt an existing folder for undo.");
            Check(!Directory.EnumerateDirectories(root, ".nexus-folder-*").Any(), "Failed or successful folder creation must not leave temporary folders.");
            string movedFolder = folderEdits.Rename(folder, "Moved folder"); folderEdits.Undo(); Check(Directory.Exists(folder) && !Directory.Exists(movedFolder), "Folder rename must also be reversible.");
            using var eventSeen = new ManualResetEventSlim(); int errors = 0;
            using (var monitor = new DesktopFileMonitor([root, root], () => eventSeen.Set(), _ => Interlocked.Increment(ref errors)))
            {
                Check(monitor.FolderCount == 1, "Duplicate watcher roots must not allocate duplicate watchers.");
                File.WriteAllText(Path.Combine(root, "watcher-check.txt"), "event"); Check(eventSeen.Wait(TimeSpan.FromSeconds(5)), "A real folder creation must notify the desktop observer.");
                Check(!monitor.HadError && errors == 0, "A healthy local folder watcher must report healthy status.");
            }
            using var missing = new DesktopFileMonitor([Path.Combine(root, "does-not-exist")], () => { }, _ => { });
            Check(missing.HadError && missing.FolderCount == 0, "Missing folders must fail independently and leave manual refresh possible.");
        }
        finally { Directory.Delete(root, recursive: true); }
        var now = DateTimeOffset.Now; var inbox = new NotificationInbox(); inbox.Publish("Core", "Recovered", NoticeKind.Information, now); inbox.Publish("Files", "Disk unavailable", NoticeKind.Warning, now.AddSeconds(1));
        Check(inbox.Query("disk", "All", now).Single().Title == "Files" && inbox.Query("", "Issues", now).Count == 1, "Notification search and issue filters must preserve actual alerts.");
        var alert = inbox.Items.First(i => i.Title == "Files"); inbox.Snooze(alert.Id, TimeSpan.FromMinutes(15), now);
        Check(inbox.Query("", "All", now).Count == 1 && inbox.Query("", "Snoozed", now).Single().Id == alert.Id && inbox.Unread == 1, "Snoozed alerts must leave the active list and unread badge.");
        var notificationState = new ShellState { NotificationHistory = inbox.Items.ToList() }; var recovered = new NotificationInbox(JsonSerializer.Deserialize<ShellState>(JsonSerializer.Serialize(notificationState))!.NotificationHistory);
        Check(recovered.Query("", "Snoozed", now).Single().SnoozedUntil == now.AddMinutes(15), "Snooze deadlines must survive state serialization.");
        recovered.RefreshDue(now.AddMinutes(15)); Check(recovered.Query("", "Unread", now.AddMinutes(15)).Count == 2 && recovered.Query("", "Snoozed", now.AddMinutes(15)).Count == 0, "Due alerts must return unread after a clock or session advance.");
        recovered.MarkRead([alert.Id]); Check(recovered.Query("", "Unread", now).Count == 1, "Mark shown read must not mark unrelated alerts.");
        recovered.Dismiss(alert.Id); Check(recovered.CanUndoDismiss && recovered.Items.Count == 1, "Dismissal must retain one undo action."); recovered.UndoDismiss(); Check(recovered.Items.Count == 2 && !recovered.CanUndoDismiss, "Undo dismiss must restore the actual alert once.");
        recovered.Clear(); recovered.Publish("New", "Arrived after clear", now: now.AddSeconds(3)); recovered.UndoDismiss(); Check(recovered.Items.Count == 3, "Undo clear must preserve alerts received after clearing.");
        recovered.Snooze(alert.Id, TimeSpan.FromHours(1), now); recovered.Wake(alert.Id); Check(recovered.Items.Single(i => i.Id == alert.Id).SnoozedUntil is null, "Bring back now must clear the snooze deadline.");
        Reject(() => recovered.Snooze(alert.Id, TimeSpan.FromDays(2), now), "Snooze must have a bounded duration.");
        var icon = new DesktopShortcut("system", "My files", "", "nexus:files");
        var icons = new[] { new DesktopShortcut("z", "Zeta", "", "z.txt") { IsFileSystem = true, ModifiedUtc = DateTime.UtcNow }, icon, new DesktopShortcut("a", "Alpha", "", "a") { IsFileSystem = true, IsFolder = true } };
        Check(DesktopCatalog.Sort(icons, "Name").Select(i => i.Id).SequenceEqual(["system", "a", "z"]), "Desktop sort must keep system entries first and sort user items.");
        Check(DesktopCatalog.Sort(icons, "Newest")[1].Id == "z" && DesktopCatalog.Sort(icons, "Kind")[1].Id == "a", "Newest and kind sorting must use metadata rather than labels alone.");
        Console.WriteLine($"PASS: {checks} everyday-desktop checks (organization, persistence, real file edits/undo, watcher events and notification recovery).");
    }
}
