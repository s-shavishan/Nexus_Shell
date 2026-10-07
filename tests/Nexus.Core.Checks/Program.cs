using Nexus.Shell.Models;
using Nexus.Shell.Services;
using System.Text.Json;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
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
