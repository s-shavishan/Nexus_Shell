using Nexus.Shell.Models;
using Nexus.Shell.Services;
using System.Text.Json;

internal static class NexusControlsChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
        Check(NexusControlsCatalog.Pages.Select(p => p.Id).Distinct().Count() == 6, "Controls requires six distinct, bounded destinations.");
        foreach (var setting in NexusControlsCatalog.Settings)
        {
            Check(NexusControlsCatalog.TryRoute(NexusControlsCatalog.Target(setting), out var page, out var anchor)
                && page == setting.Page && anchor == setting.Id, "Every setting must resolve to its own page and anchor.");
            Check(DesktopSearchCatalog.Find(setting.Title).Any(command => command.Id == "nexus-controls:" + setting.Id), "Spotlight must discover individual Controls settings.");
        }
        foreach (string old in new[] { "nexus:sections", "NEXUS:SECTIONS", "nexus:settings", "nexus:controls" })
            Check(NexusControlsCatalog.TryRoute(old, out var page, out var setting) && page == "home" && setting is null, "Legacy shortcuts must open Controls home.");
        Check(NexusControlsCatalog.TryRoute("NEXUS:CONTROLS/CORE/SERVICES", out var core, out var service) && core == "core" && service == "services", "Controls routes must accept URI casing.");
        foreach (string invalid in new[] { "nexus:controls-other", "nexus:controls/unknown", "nexus:controls/core/clipboard", "nexus:controls/core/services/extra", "nexus:controls/../startup", "file:///nexus:controls", "nexus:controls/core/" })
            Check(!NexusControlsCatalog.TryRoute(invalid, out _, out _), "Unknown, cross-page and path-like routes must be rejected.");
        Check(NexusControlsCatalog.Find("  GLASS  motion ").Any(s => s.Id == "glass") && NexusControlsCatalog.Find("no-such-setting").Count == 0
            && NexusControlsCatalog.Find(new string('x', 10000)).Count == 0 && NexusControlsCatalog.Find("").Count <= 20, "Settings search must be literal, useful and bounded.");

        var legacy = new AppEntry("sections", "Sections", "NEXUS:SECTIONS", "", "System");
        var unrelated = new AppEntry("sections", "Sections editor", "/apps/sections-editor", "", "App");
        var state = StateStore.NormalizeState(new ShellState {
            PinnedApps = [legacy, unrelated, new("controls", "Nexus Controls", "nexus:controls", "")], RecentApps = [legacy],
            AppCollections = [new(Guid.NewGuid().ToString("N"), "Tools", ["nexus:sections", "nexus:files"])], HiddenAppTargets = ["nexus:sections"],
            RecentCommands = [new("App", "nexus:sections"), new("Saved", "nexus:sections")],
            QuickNote = "Keep this note", SavedItems = [new("kept-note", "Saved note", "", "Note", Note: "Do not delete"), new("kept-link", "Docs", "https://example.com", "Link")],
            UsageTracking = true, ClipboardShelf = true, KeepAvailable = true, QuietNotifications = true,
            Tasks = [new("task", "Keep this task")], FocusMinutes = 25, FocusRemainingSeconds = 90,
            WindowLayouts = [new("layout", "My windows", [])], Places = [new("place", "Home", "/home")]
        });
        string saved = JsonSerializer.Serialize(state.SavedItems), tasks = JsonSerializer.Serialize(state.Tasks);
        var preservedApp = state.PinnedApps.Single(app => app.Target == unrelated.Target);
        Check(NexusControlsCatalog.MigrateShortcuts(state), "The legacy app must migrate.");
        Check(state.PinnedApps.Count == 2 && state.PinnedApps[0].Name == "Nexus Controls" && state.PinnedApps[1] == preservedApp, "Migration must deduplicate Controls and preserve unrelated applications.");
        Check(state.RecentApps.Single().Target == "nexus:controls" && state.HiddenAppTargets.Single() == "nexus:controls"
            && state.AppCollections.Single().Targets.Contains("nexus:controls"), "Pinned, recent, hidden, collection references must migrate together.");
        Check(state.RecentCommands.Contains(new("App", "nexus:controls")) && state.RecentCommands.Contains(new("Saved", "nexus:sections")), "Migration must not reinterpret saved-item IDs as app targets.");
        Check(!NexusControlsCatalog.MigrateShortcuts(state), "Migration must be idempotent.");
        var original = NexusAppearance.Capture(state);
        foreach (string id in new[] { "midnight", "quiet", "compact" })
        {
            NexusAppearance.Preset(id).Apply(state);
            Check(state.UsageTracking && state.ClipboardShelf && state.KeepAvailable && state.QuietNotifications && state.QuickNote == "Keep this note"
                && JsonSerializer.Serialize(state.SavedItems) == saved && JsonSerializer.Serialize(state.Tasks) == tasks && state.FocusRemainingSeconds == 90,
                "Presets must not alter privacy, startup, saved content or paused timers.");
            original.Apply(state); Check(NexusAppearance.Capture(state) == original, "Appearance undo must restore every field affected by presets.");
        }
        bool rejected = false; try { NexusAppearance.Preset("unknown").Apply(state); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected && NexusAppearance.Capture(state) == original, "Unknown presets must not partially change settings.");
        string folder = Path.Combine(Path.GetTempPath(), "Nexus-controls-" + Guid.NewGuid().ToString("N"));
        try
        {
            var session = new ShellSession(new StateStore(folder), state); NexusAppearance.Preset("compact").Apply(state);
            session.SaveAsync().GetAwaiter().GetResult(); var loaded = new StateStore(folder).Load();
            Check(loaded.CompactDock && loaded.NativeGlass && loaded.PinnedApps[0].Target == "nexus:controls" && JsonSerializer.Serialize(loaded.SavedItems) == saved, "Migrated links and Controls preferences must survive actual state persistence.");
            state.DisplayName = "Still running"; session.SaveAsync().GetAwaiter().GetResult();
            Check(new StateStore(folder).Load().DisplayName == "Still running", "Saving from Controls must not finalize the shared desktop writer.");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        Check(NexusControlsCatalog.CompactNavigation(480) && NexusControlsCatalog.CompactNavigation(779) && !NexusControlsCatalog.CompactNavigation(780)
            && !NexusControlsCatalog.CompactNavigation(double.NaN), "Compact navigation must have a stable finite-width boundary.");
        Console.WriteLine($"PASS: {checks} Nexus Controls checks; settings routes, shortcut migration, preset undo, preserved content/privacy and real persistence.");
    }
}
