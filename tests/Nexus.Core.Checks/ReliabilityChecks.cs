using Nexus.Shell.Models;
using Nexus.Shell.Services;
using System.Text.Json;

internal static class ReliabilityChecks
{
    public static void Run(Action<bool, string> check)
    {
        var state = new ShellState(); ExploreWorkspace.Normalize(state);
        var link = ExploreWorkspace.Capture("https://example.com/Lesson", "study");
        check(ExploreWorkspace.Add(state, link), "A new reference must be captured.");
        check(ExploreWorkspace.Add(state, link with { Id = "build-copy", SpaceId = "build" }),
            "A reference must be usable in more than one space.");
        check(!ExploreWorkspace.Add(state, link with { Id = "duplicate", Target = "https://EXAMPLE.COM/Lesson" }),
            "Host casing must not create duplicate links within a space.");
        check(ExploreWorkspace.Add(state, link with { Id = "case-distinct", Target = "https://example.com/lesson" }),
            "Case-sensitive web paths must remain distinct.");
        var nulls = JsonSerializer.Deserialize<ShellState>("""
            {"ExploreQuery":null,"ExploreCollection":null,"ActiveExploreSpaceId":null,
             "ExploreSpaces":[{"Id":"personal","Name":"Personal","Description":null,"Accent":null}],
             "SavedItems":[{"Id":"n","Title":"Note","Kind":"Note","Target":null,"Note":null,"Tags":null,"Collection":null}]}
            """)!;
        ExploreWorkspace.Normalize(nulls);
        check(ExploreWorkspace.Filter(nulls).Single().Note == "" && nulls.SavedItems[0].Tags == "",
            "Null optional imported fields must not break rendering or search.");

        string directory = Path.Combine(Path.GetTempPath(), "Nexus-recovery-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new StateStore(directory);
            store.Save(new() { QuickNote = "first", SavedItems = [link], ExploreSpaces = state.ExploreSpaces });
            store.Save(new() { QuickNote = "second", SavedItems = [link], ExploreSpaces = state.ExploreSpaces });
            check(File.Exists(store.BackupPath), "A replacement save must preserve the previous readable workspace.");
            File.WriteAllText(store.FilePath, "{broken");
            var recovered = store.Load();
            check(recovered.QuickNote == "first" && recovered.SavedItems.Single().Id == link.Id && store.RecoveryMessage.Length > 0,
                "Damaged settings must recover notes and cards and report the recovery.");
            store.Save(recovered);
            check(JsonSerializer.Deserialize<ShellState>(File.ReadAllText(store.BackupPath))!.QuickNote == "first",
                "Saving after recovery must never replace the valid backup with the damaged file.");
            store.SaveFinal(new() { QuickNote = "final" });
            store.Save(new() { QuickNote = "stale worker" });
            check(store.Load().QuickNote == "final", "An older queued save must not overwrite the final snapshot.");

            var restarted = new StateStore(directory);
            string previous = File.ReadAllText(restarted.FilePath);
            var oversized = new ShellState { QuickNote = new string('x', 2 * 1024 * 1024) };
            bool rejected = false;
            try { restarted.Save(oversized); } catch (InvalidDataException) { rejected = true; }
            check(rejected && File.ReadAllText(restarted.FilePath) == previous,
                "An oversized save must fail before replacing existing data.");
            File.Delete(restarted.FilePath);
            check(restarted.Load().QuickNote == "first" && restarted.RecoveryMessage.Length > 0,
                "A missing main save must recover from the readable backup.");
            File.WriteAllText(restarted.FilePath, "null"); File.WriteAllText(restarted.BackupPath, "[]");
            check(restarted.Load().ExploreSpaces.Count > 0 && restarted.RecoveryMessage.Length > 0 &&
                Directory.EnumerateFiles(directory, "*.unreadable-*").Any(),
                "Invalid workspace roots must open safely while retaining the unreadable copy.");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
        Console.WriteLine("PASS: separate-space references, case-sensitive URLs, null imported fields, damaged/missing settings recovery, final-save ordering and atomic oversized-save rejection.");
    }
}
