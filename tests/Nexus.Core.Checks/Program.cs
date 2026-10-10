using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Nexus.Shell.Interop;
using System.Text.Json;

DesktopModeChecks.Run();
DesktopUiChecks.Run();

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
if (OperatingSystem.IsWindows())
{
    Check(NativeMethods.TryGetHighContrast(out _), "The desktop high-contrast query must succeed on Windows.");
    Check(NativeMethods.TryGetAnimationsEnabled(out _), "The desktop animation preference query must succeed on Windows.");
    Console.WriteLine("PASS: Win32 desktop accessibility queries (actual user32 calls).");
}
// Check small body text on the actual palette surfaces, including translucent cards.
foreach (string mood in AuraPalette.Moods)
{
    var palette = AuraPalette.For(mood);
    var canvas = AuraColor.Parse(palette.Canvas);
    var panel = AuraColor.Parse(palette.Panel).Over(canvas);
    var card = AuraColor.Parse(palette.Card).Over(panel);
    foreach (var background in new[] { panel, card, AuraColor.Parse(palette.HeroStart), AuraColor.Parse(palette.HeroEnd),
        AuraColor.Parse(palette.Tokens["NexusInput"]).Over(panel), AuraColor.Parse(palette.Tokens["NexusSidebar"]).Over(canvas),
        AuraColor.Parse(palette.Tokens["NexusSegment"]).Over(panel) })
    {
        Check(AuraColor.Contrast(AuraColor.Parse(palette.Tokens["NexusText"]), background) >= 4.5,
            palette.Name + " body text must meet 4.5:1 contrast.");
        Check(AuraColor.Contrast(AuraColor.Parse(palette.Muted), background) >= 4.5,
            palette.Name + " secondary text must meet 4.5:1 contrast.");
    }
    Check(AuraColor.Contrast(AuraColor.Parse(palette.Tokens["NexusAccentText"]), AuraColor.Parse(palette.Accent)) >= 4.5,
        palette.Name + " primary button text must meet 4.5:1 contrast.");
    var first = AuraColor.Parse(palette.Accent); var last = AuraColor.Parse(palette.AccentEnd);
    for (int step = 0; step <= 10; step++)
    {
        double amount = step / 10.0;
        byte Mix(byte a, byte b) => (byte)Math.Round(a + (b - a) * amount);
        var paint = new AuraColor(255, Mix(first.R, last.R), Mix(first.G, last.G), Mix(first.B, last.B));
        foreach (double opacity in new[] { 0d, .07, .13 })
        {
            var wash = AuraColor.Parse(palette.Tokens["NexusHover"]);
            var surface = (wash with { A = (byte)Math.Round(wash.A * opacity) }).Over(paint);
            Check(AuraColor.Contrast(AuraColor.Parse(palette.Tokens["NexusAccentText"]), surface) >= 4.5,
                palette.Name + " action text must meet 4.5:1 across the gradient, including hover and pressed washes.");
        }
    }
    foreach (var surface in new[] { panel, card, AuraColor.Parse(palette.Tokens["NexusSidebar"]).Over(canvas),
        AuraColor.Parse(palette.Tokens["NexusSidebar"]).Over(panel) })
    {
        var selected = AuraColor.Parse(palette.Tokens["NexusSelection"]).Over(surface);
        Check(AuraColor.Contrast(AuraColor.Parse(palette.Tokens["NexusSelectedText"]), selected) >= 4.5,
            palette.Name + " selected navigation and popup text must meet 4.5:1 contrast.");
    }
}
// Persist each new mood through the actual state store, and keep an existing
// chosen mood when loading settings from the previous release.
var moodDirectory = Path.Combine(Path.GetTempPath(), "Nexus-mood-" + Guid.NewGuid().ToString("N"));
try
{
    var moodStore = new StateStore(moodDirectory);
    foreach (string mood in AuraPalette.Moods)
    {
        var moodState = new ShellState { Wallpaper = mood, QuickNote = "My notes", CompactDock = true };
        moodStore.Save(moodState);
        var loadedMood = moodStore.Load();
        Check(loadedMood.Wallpaper == mood && loadedMood.QuickNote == "My notes" && loadedMood.CompactDock,
            "Mood changes must preserve content and dock preferences through the actual settings file.");
    }
}
finally { if (Directory.Exists(moodDirectory)) Directory.Delete(moodDirectory, true); }
DesktopFoundationChecks.Run(Check);
DesktopPerformanceChecks.Run(Check);
ExploreChecks.Run(Check);
ReliabilityChecks.Run(Check);
MidnightChecks.Run(Check);
PcControlChecks.Run(Check);
Check(AuraPalette.For("old-unknown").Name == "Pearl", "Unknown mood values should use the safe default palette.");
var glassState = new ShellState { NativeGlass = true };
Check(glassState.Snapshot().NativeGlass && JsonSerializer.Deserialize<ShellState>(JsonSerializer.Serialize(glassState))!.NativeGlass,
    "The native glass preference must survive snapshots and persistence.");
Console.WriteLine("PASS: Aura palette contrast, safe default and glass preference persistence.");
// Users can return through a sequence, branch it, and rebuild pages without duplicating history.
var trail = new NavigationTrail();
Check(trail.Back() is null && trail.Forward() is null, "Empty navigation must stay safe.");
trail.Visit("Home"); trail.Visit("Apps"); trail.Visit("Study"); trail.Visit("Study");
Check(trail.Count == 3 && trail.Back() == "Apps" && trail.Back() == "Home" && trail.Back() is null,
    "Back should visit each prior page once and stop at the start.");
Check(trail.Forward() == "Apps", "Forward should restore the next page.");
trail.Visit("Explore");
Check(!trail.CanGoForward && trail.Current == "Explore" && trail.Back() == "Apps",
    "Visiting a new page after Back must discard the abandoned forward branch.");
for (int i = 0; i < 100; i++) trail.Visit("page-" + i);
Check(trail.Count == NavigationTrail.Capacity && trail.Current == "page-99", "Navigation memory must stay bounded.");

var shellEntries = new[] {
    new CommandEntry("Apps", "Library", "", "Workspace", "Apps"),
    new CommandEntry("Study preset", "School work", "", "Profile", "study"),
    new CommandEntry("Firefox", "Windows app", "", "App", "firefox"),
    new CommandEntry("ICT notes", "Saved file", "", "Saved", "notes"),
    new CommandEntry("Circuit revision", "Task", "", "Task", "task"),
    new CommandEntry("Control center", "Action", "", "Action", "controls"),
    new CommandEntry("Notes - Notepad", "Open window", "", "Window", "123") };
Check(ShellExperience.Search(shellEntries, "", "Apps", null, true).Single().Kind == "App", "Apps category must exclude workspace pages.");
Check(ShellExperience.Search(shellEntries, "", "Workspaces", null, true).Length == 2, "Workspaces category includes pages and profiles.");
Check(ShellExperience.Search(shellEntries, "notes", "Windows", null, true).Single().Kind == "Window", "Open-window titles must be searchable within their category.");
Check(ShellExperience.Search(shellEntries, "nothing", "Saved", null, true).Length == 0, "No category match should produce an empty result.");
var recentState = new ShellState();
ShellExperience.Remember(recentState, shellEntries[2]); ShellExperience.Remember(recentState, shellEntries[3]);
ShellExperience.Remember(recentState, shellEntries[2]);
Check(recentState.RecentCommands.Count == 2 && recentState.RecentCommands[0].Target == "firefox", "Reusing a recent item should promote it without duplicates.");
ShellExperience.Remember(recentState, shellEntries[5]); ShellExperience.Remember(recentState, shellEntries[6]);
Check(recentState.RecentCommands.Count == 2, "Actions and ephemeral window handles must never enter recent-item persistence.");
recentState.RecentCommands.Insert(0, new("App", "no-longer-installed"));
var recentResults = ShellExperience.Search(shellEntries.Concat(shellEntries), "", "All", recentState.RecentCommands, true);
Check(recentResults[0].Title == "Firefox" && recentResults.Length == shellEntries.Length && recentResults.Count(e => e.Target == "firefox") == 1,
    "Recent references must resolve against current entries, omit stale targets and avoid duplicate results.");
Check(ShellExperience.Search(shellEntries, "ICT", "All", recentState.RecentCommands, true).Single().Target == "notes",
    "Typing must search relevant matches instead of preferring unrelated recent items.");
for (int i = 0; i < 20; i++) ShellExperience.Remember(recentState, new("App", "", "", "App", "target-" + i));
Check(recentState.RecentCommands.Count == ShellExperience.RecentLimit, "Recent storage must stay bounded.");
recentState.RememberRecentItems = false; ShellExperience.Normalize(recentState);
ShellExperience.Remember(recentState, shellEntries[2]);
Check(recentState.RecentCommands.Count == 0, "Disabling recent items must clear them and prevent new recording.");
Check(ShellExperience.NormalizeRecent(new[] { new RecentCommand("Action", "exit"), new RecentCommand("App", ""), new RecentCommand("App", new string('x', 4097)) }).Count == 0,
    "Invalid persisted references must be rejected.");
var personal = new ShellState { Clock24Hour = false, CompactDock = true, ShowHomeNotes = false, ShowClockWidget = false,
    LastPage = "Personalize", RecentCommands = [new("Saved", "notes")] };
var personalCopy = personal.Snapshot(); personal.RecentCommands.Clear();
Check(!personalCopy.Clock24Hour && personalCopy.CompactDock && !personalCopy.ShowHomeNotes && !personalCopy.ShowClockWidget && personalCopy.RecentCommands.Count == 1,
    "Personalization and recent references must survive an isolated snapshot.");
DesktopWorkspace.Normalize(personalCopy);
Check(personalCopy.LastPage == "Personalize", "Resume should preserve the Personalize page.");
var personalRoundTrip = JsonSerializer.Deserialize<ShellState>(JsonSerializer.Serialize(personalCopy))!;
Check(personalRoundTrip.CompactDock && !personalRoundTrip.Clock24Hour && personalRoundTrip.RecentCommands.Count == 1,
    "New shell preferences must round-trip through JSON.");
var oldSettings = JsonSerializer.Deserialize<ShellState>("{\"QuickNote\":\"keep\"}")!;
Check(oldSettings.ShowHomeNotes && oldSettings.ShowClockWidget && oldSettings.Clock24Hour && !oldSettings.CompactDock && oldSettings.QuickNote == "keep",
    "Old settings must receive compatible appearance defaults without losing notes.");
Console.WriteLine("PASS: navigation branching/bounds, categorized search, live recent resolution, privacy opt-out and personalization migration.");

long ticks = 0;
var focus = new FocusSession(() => ticks, 1000);
focus.Reset(1); focus.Start(); ticks = 10_000;
Check(focus.Remaining == TimeSpan.FromSeconds(50), "Elapsed time must come from the clock.");
focus.Start(); ticks = 20_000;
Check(focus.Remaining == TimeSpan.FromSeconds(40), "Starting twice must not reset the clock.");
focus.Pause(); ticks = 500_000;
Check(focus.Remaining == TimeSpan.FromSeconds(40), "Paused time must not reduce remaining time.");
focus.Start(); ticks = 550_000;
Check(focus.Remaining == TimeSpan.Zero && focus.CompleteIfDue(), "A delayed tick must still complete the session.");
Check(!focus.CompleteIfDue(), "A session must complete only once.");
focus.Start();
Check(focus.Remaining == TimeSpan.FromMinutes(1), "A completed session should restart cleanly.");
focus.Reset(25);
Check(!focus.IsRunning && focus.Remaining == TimeSpan.FromMinutes(25), "Changing a preset must stop and reset.");
bool rejected = false;
try { focus.Reset(0); } catch (ArgumentOutOfRangeException) { rejected = true; }
Check(rejected, "Invalid presets must be rejected.");
focus.Restore(25, TimeSpan.FromSeconds(470.25));
ticks += 1_000_000;
Check(!focus.IsRunning && focus.Remaining == TimeSpan.FromSeconds(470.25), "Restored sessions must stay paused, even as the clock advances.");
focus.Start(); ticks += 5_000;
Check(focus.Remaining == TimeSpan.FromSeconds(465.25), "A restored session must resume from its checkpoint.");
focus.Restore(25, TimeSpan.Zero);
Check(!focus.CompleteIfDue(), "Restoring a completed session must not award completion again.");
rejected = false;
try { focus.Restore(25, TimeSpan.FromMinutes(26)); } catch (ArgumentOutOfRangeException) { rejected = true; }
Check(rejected, "Checkpoints outside the session duration must be rejected.");

var entries = new[] {
    new CommandEntry("Home", "Personal desktop", "", "Workspace", "Home"),
    new CommandEntry("Study", "Focus timer and notes", "", "Workspace", "Study"),
    new CommandEntry("Focus music", "Saved study playlist", "", "Saved", "one"),
    new CommandEntry("Firefox", "Windows app", "", "App", "firefox") };
Check(CommandSearch.Filter(entries, "  FOCUS  ")[0].Title == "Focus music", "Title prefixes should precede subtitle matches.");
Check(CommandSearch.Filter(entries, "study notes").Single().Title == "Study", "All query words must match.");
Check(CommandSearch.Filter(entries, "not-found").Length == 0, "No-match searches must be empty.");
Check(CommandSearch.Filter(entries, "", 2).Length == 2, "The palette must obey its result bound.");
Check(CommandSearch.Filter(entries, "", 0).Length == 0, "A zero result limit must be empty.");

var state = new ShellState { QuickNote = "remember", FocusCompleted = 1 };
state.SavedItems.Add(new("one", "A note", "https://example.com", "Link"));
var snapshot = state.Snapshot();
state.SavedItems.Clear(); state.FocusCompleted = 2; state.QuickNote = "changed";
Check(snapshot.SavedItems.Count == 1 && snapshot.FocusCompleted == 1 && snapshot.QuickNote == "remember",
    "Persistence snapshots must retain their lists and scalar values.");
var legacy = JsonSerializer.Deserialize<ShellState>("""
    {"QuickNote":"my old note","PinnedApps":[{"Id":"files","Name":"Files","Target":"explorer.exe","Glyph":"x"}],
     "SavedItems":[{"Id":"one","Title":"My link","Target":"https://example.com","Kind":"Link"}]}
    """)!;
WorkspaceState.Normalize(legacy);
Check(legacy.QuickNote == "my old note" && legacy.PinnedApps.Count == 1 && legacy.SavedItems.Single().Collection == "Personal",
    "Older settings must retain notes, pins, and saved items when adding collections and tasks.");
Check(legacy.Tasks.Count == 0 && legacy.FocusRemainingSeconds == -1 && !legacy.SavedItems[0].Favorite,
    "Missing new fields must migrate to a fresh timer and empty task list.");
legacy.Tasks = Enumerable.Range(0, 120).Select(i => new TaskEntry(i.ToString(), "Task " + i)).ToList();
legacy.Tasks.Insert(0, new("duplicate", "   "));
legacy.Tasks.Insert(1, new("0", "First task"));
legacy.FocusTaskId = "missing"; legacy.FocusRemainingSeconds = double.PositiveInfinity;
legacy.SavedItems.Add(new("bad", "Invalid link", "file:///C:/something", "Link"));
legacy.SavedItems.Add(new("one", "Duplicate identity", "https://example.org", "Link"));
WorkspaceState.Normalize(legacy);
Check(legacy.Tasks.Count == 100 && legacy.Tasks.Select(t => t.Id).Distinct().Count() == 100,
    "Tasks must remain bounded, nonblank, and uniquely identified.");
Check(legacy.FocusTaskId == "" && legacy.FocusRemainingSeconds == -1 && legacy.SavedItems.Count == 1,
    "Dangling focus tasks, non-finite checkpoints, duplicate saved identities and non-web links must be sanitized.");
legacy.FocusTaskId = legacy.Tasks[0].Id; legacy.FocusRemainingSeconds = 50_000;
WorkspaceState.Normalize(legacy);
Check(legacy.FocusRemainingSeconds == legacy.FocusMinutes * 60 && legacy.FocusTaskId == legacy.Tasks[0].Id,
    "Valid task selection must survive while oversized checkpoints are bounded.");
legacy.SavedItems[0] = legacy.SavedItems[0] with { Collection = "Study", Favorite = true };
var migrated = JsonSerializer.Deserialize<ShellState>(JsonSerializer.Serialize(legacy))!;
WorkspaceState.Normalize(migrated);
Check(migrated.SavedItems[0].Favorite && migrated.SavedItems[0].Collection == "Study" && migrated.Tasks.Count == 100,
    "New workspace data must round-trip through JSON.");
var taskSnapshot = migrated.Snapshot(); migrated.Tasks.Clear(); migrated.FocusTaskId = "";
Check(taskSnapshot.Tasks.Count == 100 && taskSnapshot.FocusTaskId.Length > 0,
    "A save snapshot must own its task list and focus selection.");
Console.WriteLine("PASS: timing, paused recovery, command search, legacy migration, bounded workspace data, JSON round-trip, and independent snapshots.");

var desktop = JsonSerializer.Deserialize<ShellState>("""
    {"QuickNote":"legacy notes","SavedItems":[{"Id":"lesson","Title":"Lesson","Target":"https://example.com","Kind":"Link"}]}
    """)!;
WorkspaceState.Normalize(desktop); DesktopWorkspace.Normalize(desktop);
Check(desktop.Profiles.Count == 4 && desktop.ActiveProfileId == "personal" && !desktop.KeepAvailable && desktop.DesktopLayout,
    "Older settings should get starter workspaces and desktop layout; resident mode must remain off.");
var profile = desktop.Profiles[1] with {
    Apps = [new("browser", "Lesson", "https://example.com", "x"), new("bad", "Unsafe scheme", "javascript:alert(1)", "x")],
    SavedItemIds = ["lesson", "missing", "lesson"] };
desktop.Profiles[1] = profile; desktop.ActiveProfileId = profile.Id;
DesktopWorkspace.Normalize(desktop);
profile = desktop.Profiles[1];
Check(profile.Apps.Count == 1 && profile.SavedItemIds.SequenceEqual(new[] {"lesson"}),
    "Workspace migration must reject unsafe app schemes and prune missing or duplicate saved IDs.");
Check(DesktopWorkspace.Plan(profile, desktop.SavedItems).Length == 1,
    "The launch plan must deduplicate a target shared by an app and a saved item.");
Check(desktop.Activity.Count == 0, "Planning a workspace must not record or launch activity.");
var profileSnapshot = desktop.Snapshot(); desktop.Profiles[1].Apps.Clear(); desktop.Profiles[1].SavedItemIds.Clear();
Check(profileSnapshot.Profiles[1].Apps.Count == 1 && profileSnapshot.Profiles[1].SavedItemIds.Count == 1,
    "An in-flight save must own each profile's nested collections.");
desktop.Profiles = Enumerable.Range(0, 20).Select(i => new WorkspaceProfile(i.ToString(), "Workspace", "", "bad-page", "x",
    Enumerable.Range(0, 20).Select(j => new AppEntry(j.ToString(), "App", "https://example.com/" + j, "x")).ToList(), [])).ToList();
desktop.LastPage = "unknown"; desktop.ActiveProfileId = "missing";
DesktopWorkspace.Normalize(desktop);
Check(desktop.Profiles.Count == 8 && desktop.Profiles[0].Apps.Count == 8 && desktop.Profiles.All(p => p.Page == "Home") && desktop.LastPage == "Home",
    "Malformed workspace data must remain bounded and return to valid navigation.");
Check(desktop.Profiles.Any(p => p.Id == desktop.ActiveProfileId), "An active workspace must always resolve.");
Check(DesktopIntegration.NotificationDataSize == (IntPtr.Size == 8 ? 976 : 956) &&
    DesktopIntegration.NotificationIconOffset == (IntPtr.Size == 8 ? 32 : 20),
    "The notification-area interop structure must use Windows Unicode buffers and correct pointer alignment.");
var settingsDirectory = Path.Combine(Path.GetTempPath(), "Nexus-check-" + Guid.NewGuid().ToString("N"));
try
{
    Directory.CreateDirectory(settingsDirectory);
    var store = new StateStore(settingsDirectory);
    File.WriteAllText(store.FilePath, "{\"QuickNote\":\"keep my notes\",\"PinnedApps\":[]}");
    var upgraded = store.Load();
    Check(upgraded.QuickNote == "keep my notes" && upgraded.Profiles.Count == 4 &&
        File.Exists(Path.Combine(settingsDirectory, "settings.before-0.5.0.json")),
        "Loading pre-desktop settings must preserve notes and create a migration backup.");
    store.Save(upgraded);
    var reloaded = store.Load();
    Check(reloaded.Profiles.Count == 4 && reloaded.QuickNote == "keep my notes", "Desktop settings must round-trip through actual atomic persistence.");
}
finally { if (Directory.Exists(settingsDirectory)) Directory.Delete(settingsDirectory, true); }
if (OperatingSystem.IsWindows()) DesktopNativeChecks.Run();
StartupChecks.Run();
Console.WriteLine("PASS: desktop migration, bounded profiles, launch-plan deduplication, nested snapshots, native structure layout, and atomic persistence.");
