using System.Text.Json;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Nexus.Shell.Interop;

internal static class DesktopCommandChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        var left = new LayoutRect(-1920, 44, 1920, 1036);
        var original = new LayoutRect(-1600, 140, 800, 600);
        var saved = SavedWindowLayouts.Capture(@"C:\Apps\editor.exe", "Notes", "DISPLAY1", original, left, false);
        Check(SavedWindowLayouts.Project(saved, left) == original, "Layout proportions must round-trip on a monitor left of primary.");
        foreach (var work in new[] { new LayoutRect(0, 46, 1366, 674), new LayoutRect(-1280, -900, 1280, 850), new LayoutRect(1920, 80, 2560, 1400), new LayoutRect(0, 0, 180, 100) })
        foreach (var position in new[] { saved, saved with { Left = 1, Top = 1 }, saved with { Left = -3, Top = 4, Width = 12, Height = 5 }, saved with { Width = .01, Height = .01 } })
        {
            var projected = SavedWindowLayouts.Project(position, work);
            Check(projected.X >= work.X && projected.Y >= work.Y, "Restored windows must not cross the usable area's origin.");
            Check((long)projected.X + projected.Width <= (long)work.X + work.Width && (long)projected.Y + projected.Height <= (long)work.Y + work.Height, "Restored windows must fit the destination display.");
            Check(projected.Width > 0 && projected.Height > 0, "Small displays must retain a usable positive window size.");
        }
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            bool rejected = false; try { SavedWindowLayouts.Project(saved with { Left = value }, left); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Non-finite saved geometry must be rejected.");
        }
        var candidates = new[] { new LayoutCandidate("one", @"c:\apps\EDITOR.exe", "Notes") };
        Check(SavedWindowLayouts.Match([saved], candidates).Single().CandidateKey == "one", "Process path matching must ignore Windows path case.");
        Check(SavedWindowLayouts.Match([saved], [candidates[0] with { Title = "Changed title" }]).Count == 1, "A uniquely identifiable app can survive a changed title.");
        Check(SavedWindowLayouts.Match([saved], [candidates[0], candidates[0] with { Key = "two" }]).Count == 0, "Duplicate exact titles must never be guessed.");
        Check(SavedWindowLayouts.Match([saved, saved], candidates).Count == 0, "Duplicate saved identities must never target one app twice.");
        Check(SavedWindowLayouts.Match([saved], [candidates[0] with { Title = "One" }, candidates[0] with { Key = "two", Title = "Two" }]).Count == 0, "A title change with multiple same-app windows is ambiguous.");
        Check(SavedWindowLayouts.Match([saved], [candidates[0] with { ProcessPath = @"C:\Other\editor.exe" }]).Count == 0, "A same-named executable in a different location must not match.");
        Check(SavedWindowLayouts.Match([saved], [new("one", "", "Notes")]).Count == 0, "Unavailable process identity must be skipped.");
        var second = saved with { Title = "Two" };
        Check(SavedWindowLayouts.Match([saved, second], [candidates[0], candidates[0] with { Key = "two", Title = "Two" }]).Count == 2, "Distinct exact titles must match independently.");
        string id = Guid.NewGuid().ToString("N");
        var state = new ShellState { ClipboardShelf = true, WindowLayouts = [new(id, "  Study  ", [saved]), new(id.ToUpperInvariant(), "duplicate", [saved]), new("invalid", "discard", [saved])] };
        StateStore.NormalizeState(state);
        Check(state.WindowLayouts.Count == 1 && state.WindowLayouts[0].Name == "Study", "Layout identity and names must normalize and deduplicate.");
        var copy = state.Snapshot(); copy.WindowLayouts[0].Windows.Clear();
        Check(state.WindowLayouts[0].Windows.Count == 1, "Settings snapshots must deep-copy saved position lists.");
        state.WindowLayouts = Enumerable.Range(0, 20).Select(i => new SavedWindowLayout(Guid.NewGuid().ToString("N"), "Layout " + i, Enumerable.Repeat(saved, 30).ToList())).ToList();
        StateStore.NormalizeState(state);
        Check(state.WindowLayouts.Count == 8 && state.WindowLayouts.All(layout => layout.Windows.Count == 12), "Layout and per-layout capacity must be bounded.");
        var directory = Path.Combine(Path.GetTempPath(), "Nexus-command-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new StateStore(directory); store.Save(state); var loaded = store.Load();
            Check(loaded.ClipboardShelf && loaded.WindowLayouts.Count == 8 && loaded.WindowLayouts[0].Windows[0] == saved, "Layouts and the opt-in flag must survive actual persistence.");
            string json = File.ReadAllText(store.FilePath);
            Check(!json.Contains("Handle", StringComparison.Ordinal) && !json.Contains("ProcessId", StringComparison.Ordinal), "Persistent layouts must not contain stale native handles or process identifiers.");
            File.WriteAllText(store.FilePath, "{\"Profiles\":[],\"ExploreSpaces\":[],\"Tasks\":[],\"Wallpaper\":\"Midnight\",\"QuickNote\":\"keep my note\"}");
            var migrated = store.Load(); Check(!migrated.ClipboardShelf && migrated.WindowLayouts.Count == 0 && migrated.QuickNote == "keep my note", "Older settings must default capture off and retain existing data.");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        var history = new ClipboardHistory();
        Check(!history.Add("not opted in", DateTimeOffset.Now) && history.Items.Count == 0, "The clipboard must start off.");
        history.SetEnabled(true); var now = DateTimeOffset.Now;
        Check(!history.Add("", now) && !history.Add("  ", now) && !history.Add("bad\0text", now) && !history.Add(new string('x', 16385), now), "Empty, malformed and oversized text must not enter history.");
        Check(history.Add(new string('x', 16384), now), "The documented size boundary must be accepted.");
        history.Clear();
        for (int i = 0; i < 8; i++) { history.Add("pinned " + i, now.AddSeconds(i)); Check(history.TogglePin(history.Items[0].Id), "The first eight pins must be accepted."); }
        history.Add("ninth pin", now.AddSeconds(9)); Check(!history.TogglePin(history.Items[0].Id), "The ninth pin must be refused.");
        for (int i = 0; i < 200; i++) history.Add("ordinary " + i, now.AddMinutes(i));
        Check(history.Items.Count == 32 && history.Items.Count(item => item.Pinned) == 8, "Eviction must keep eight pins inside the 32-entry budget.");
        var pin = history.Items.First(item => item.Pinned); history.Add(pin.Text, now.AddDays(1));
        Check(history.Items.Count == 32 && history.Items.Single(item => item.Text == pin.Text).Id == pin.Id && history.Items[0].Pinned, "Copy deduplication must retain stable identity and pin state.");
        Check(history.Search("PINNED").Count == 8 && history.Search("not present").Count == 0, "Search must be case-insensitive and not introduce entries.");
        history.Remove(pin.Id); Check(history.Items.All(item => item.Id != pin.Id), "Removing an entry must release it from history.");
        history.SetEnabled(false); Check(history.Items.Count == 0 && !history.Add("late", now), "Turning off must clear pins and refuse late text.");
        history.SetEnabled(true); history.Add("session-secret-check", now);
        Check(!JsonSerializer.Serialize(state).Contains("session-secret-check", StringComparison.Ordinal), "Clipboard text must be absent from serialized settings.");
        history.Clear(); Check(history.Items.Count == 0, "Explicit clear must remove all entries.");
        var gate = new ClipboardCaptureGate(); var first = gate.Begin(1)!.Value;
        Check(gate.Begin(2) is null && gate.Busy, "A delayed clipboard source must allow only one outstanding read.");
        gate.Invalidate(); Check(!gate.Current(first) && gate.Begin(3) is null, "Clear or lock must invalidate results without spawning a replacement blocked reader.");
        gate.End(first); var next = gate.Begin(4)!.Value; gate.End(first);
        Check(gate.Busy && gate.Current(next), "An older completion must not release a newer reader.");
        gate.End(next); Check(!gate.Busy, "Completed reads must release their permit.");
        Check(ClipboardCapturePolicy.Allows(false, false, null) && ClipboardCapturePolicy.Allows(false, true, 1), "Unmarked and expressly allowed text can be captured after opt-in.");
        Check(!ClipboardCapturePolicy.Allows(true, false, null) && !ClipboardCapturePolicy.Allows(false, true, 0) && !ClipboardCapturePolicy.Allows(false, true, null) && !ClipboardCapturePolicy.Allows(false, true, 2), "Private, denied and malformed permission markers must be skipped.");
        Check(!ClipboardCapturePolicy.PayloadSize(0) && !ClipboardCapturePolicy.PayloadSize(3) && !ClipboardCapturePolicy.PayloadSize(ulong.MaxValue) && ClipboardCapturePolicy.PayloadSize(32770), "Native payload size must be bounded before reading its memory.");
        foreach (int count in new[] { 0, 1, 4, 6, 7, 199, 200 })
        foreach (int index in new[] { -4, 0, 1, 1000 })
        foreach (var size in new[] { (Width: 1060d, Height: 740d), (Width: 800d, Height: 540d), (Width: 550d, Height: 500d) })
        {
            var page = WindowBoardPolicy.Page(count, index, size.Width, size.Height);
            Check(page.Count is >= 0 and <= 6 && page.Index >= 0 && page.Index < page.Pages && page.Offset + page.Count <= count, "Paging must stay bounded at empty, last and small-window pages.");
        }
        Check(WindowBoardPolicy.Page(7, 1, 1060, 740).Count == 1, "The seventh window must appear on the last page.");
        Check(WindowBoardPolicy.Page(7, 0, 550, 500).Count == 2, "A small VM viewport must show one row of two windows.");
        Check(DesktopWindowCatalog.MonitorInfoSize == 104 && DesktopWindowCatalog.PlacementSize == 44, "MONITORINFOEXW and WINDOWPLACEMENT must match the Windows ABI.");
        Console.WriteLine($"PASS: {checks} desktop command checks (geometry, matching, migration, clipboard privacy/budgets and responsive paging).");
    }
}
