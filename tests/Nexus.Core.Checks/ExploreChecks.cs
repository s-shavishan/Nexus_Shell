using Nexus.Shell.Models;
using Nexus.Shell.Services;
using System.Text.Json;

internal static class ExploreChecks
{
    public static void Run(Action<bool, string> check)
    {
        var state = JsonSerializer.Deserialize<ShellState>("""
            {"SavedItems":[{"Id":"old","Title":"My lesson","Target":"https://example.com/lesson","Kind":"Link","Collection":"ICT","Favorite":true}],"QuickNote":"Keep my old notes"}
            """)!;
        WorkspaceState.Normalize(state); ExploreWorkspace.Normalize(state);
        check(state.ExploreSpaces.Count == 3 && state.SavedItems.Single().SpaceId == "personal" && state.SavedItems[0].Favorite &&
            state.SavedItems[0].Collection == "ICT" && state.QuickNote == "Keep my old notes", "Explore migration must preserve old saved data and add empty spaces.");
        var link = ExploreWorkspace.Capture(" https://example.org/reference ", "study");
        var note = ExploreWorkspace.Capture("One idea\nKeep a small prototype.", "study") with { Tags = "C#, coding, Coding, #winui" };
        check(link.Kind == "Link" && note.Kind == "Note" && note.Target == "" && note.Title == "One idea", "Capture must distinguish web links from local notes.");
        state.ActiveExploreSpaceId = "study";
        check(ExploreWorkspace.Add(state, link) && ExploreWorkspace.Add(state, note) &&
            !ExploreWorkspace.Add(state, link with { Id = "duplicate" }), "Capture must keep separate notes and deduplicate repeated links.");
        ExploreWorkspace.Normalize(state);
        check(state.SavedItems.First(a => a.Id == note.Id).Tags == "C#, coding, winui", "Tags must be bounded and deduplicated.");
        state.ExploreQuery = "prototype";
        check(ExploreWorkspace.Filter(state).Single().Id == note.Id, "Space search must find note bodies.");
        state.ExploreQuery = "winui";
        check(ExploreWorkspace.Filter(state).Single().Id == note.Id, "Space search must find tags.");
        state.ExploreQuery = ""; state.ExploreFavoritesOnly = true;
        check(ExploreWorkspace.Filter(state).Length == 0, "Favorites from another space must not leak into this one.");
        state.ExploreFavoritesOnly = false; state.ExploreSelectedItemId = note.Id; state.ExploreView = "List";
        var restored = JsonSerializer.Deserialize<ShellState>(JsonSerializer.Serialize(state))!;
        ExploreWorkspace.Normalize(restored);
        check(restored.ExploreSelectedItemId == note.Id && restored.ExploreView == "List" && restored.ActiveExploreSpaceId == "study",
            "The last space, view and selection must survive a restart.");
        var snapshot = state.Snapshot(); state.ExploreSpaces.Clear();
        check(snapshot.ExploreSpaces.Count == 3, "An asynchronous save snapshot must own its space list.");
        string exported = ExploreWorkspace.Export(restored);
        var incoming = ExploreWorkspace.ReadImport(exported);
        var target = new ShellState(); ExploreWorkspace.Normalize(target);
        check(ExploreWorkspace.Import(target, incoming) == 2 && target.SavedItems.All(a => a.SpaceId == target.ActiveExploreSpaceId) &&
            target.SavedItems.All(a => a.Id != note.Id && a.Id != link.Id), "Import must create a separate space with fresh identities.");
        int importedAgain = ExploreWorkspace.Import(target, incoming);
        check(importedAgain == 2 && target.SavedItems.Count(a => a.Kind == "Link") == 2,
            "The same reference must remain available in independent imported spaces.");
        string importedSpace = target.ActiveExploreSpaceId;
        ExploreWorkspace.RemoveSpace(target, importedSpace);
        check(target.SavedItems.All(a => a.SpaceId != importedSpace) && target.SavedItems.Count == 4,
            "Removing a space must rehome its cards without deleting content.");
        bool invalidRejected = false;
        try { ExploreWorkspace.ReadImport(exported.Replace("\"FormatVersion\": 1", "\"FormatVersion\": 99")); }
        catch (InvalidDataException) { invalidRejected = true; }
        check(invalidRejected, "Import must reject unknown archive formats.");
        invalidRejected = false;
        try { ExploreWorkspace.ReadImport(exported.Replace("https://example.org/reference", "javascript:alert(1)")); }
        catch (InvalidDataException) { invalidRejected = true; }
        check(invalidRejected, "Imported links must use http or https.");
        invalidRejected = false;
        try { ExploreWorkspace.Capture(new string('x', ExploreWorkspace.NoteLimit + 1), "personal"); }
        catch (InvalidDataException) { invalidRejected = true; }
        check(invalidRejected, "Oversized captured notes must be rejected.");
        var full = new ShellState(); ExploreWorkspace.Normalize(full);
        full.SavedItems = Enumerable.Range(0, WorkspaceState.MaximumItems).Select(i =>
            new SavedItem("note-" + i, "Note " + i, "", "Note", Note: "body " + i)).ToList();
        int oldSpaceCount = full.ExploreSpaces.Count;
        invalidRejected = false;
        try { ExploreWorkspace.Import(full, incoming); } catch (InvalidDataException) { invalidRejected = true; }
        check(invalidRejected && full.SavedItems.Count == WorkspaceState.MaximumItems && full.ExploreSpaces.Count == oldSpaceCount,
            "An import that exceeds capacity must leave the existing board untouched.");
        full.SavedItems = full.SavedItems.Select(a => a with { Note = new string('\u4E00', ExploreWorkspace.NoteLimit) }).ToList();
        string largeNotesArchive = ExploreWorkspace.Export(full);
        check(System.Text.Encoding.UTF8.GetByteCount(largeNotesArchive) < ExploreWorkspace.ImportByteLimit &&
            ExploreWorkspace.ReadImport(largeNotesArchive).Items.Count == WorkspaceState.MaximumItems,
            "A full board of maximum-length Unicode notes must fit its own export/import limit.");
        var profile = new WorkspaceProfile("p", "Study", "", "Explore", "x", [], [note.Id]);
        check(DesktopWorkspace.Plan(profile, restored.SavedItems).Length == 0, "Workspace launch plans must never treat a local note as an executable target.");
        var repaired = new ShellState { ExploreSpaces = [new("odd", "Custom", "", "unknown")], ActiveExploreSpaceId = "missing",
            ExploreSelectedItemId = "missing", SavedItems = [new("safe", "Link", "https://example.com", "Link", SpaceId: "missing")] };
        ExploreWorkspace.Normalize(repaired);
        check(repaired.ActiveExploreSpaceId == "personal" && repaired.ExploreSelectedItemId == "" && repaired.SavedItems.Single().SpaceId == "personal",
            "Invalid space references must recover into Personal without losing their cards.");
        var spaceCommand = new CommandEntry("Study space", "Explore", "x", "Space", "study");
        check(ShellExperience.MatchesCategory(spaceCommand, "Saved"), "Explore spaces must appear in categorized saved-item search.");
        Console.WriteLine("PASS: Explore migration, capture, note/tag search, persistent selection, independent snapshots, archive round-trip, reference deduplication, atomic import capacity and note launch exclusion.");
    }
}
