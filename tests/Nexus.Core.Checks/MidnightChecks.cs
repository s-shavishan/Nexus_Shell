using Nexus.Shell.Models;
using Nexus.Shell.Services;

internal static class MidnightChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var calculator = new CalculatorEngine();
        void Keys(params string[] keys) { foreach (string key in keys) calculator.Input(key); }
        Keys("0", ".", "1", "+", "0", ".", "2", "="); check(calculator.Display == "0.3", "Decimal sums must not expose binary floating point error.");
        Keys("C", "5", "+", "2", "=", "="); check(calculator.Display == "9", "Repeated equals must repeat the last operand.");
        Keys("7"); check(calculator.Display == "7", "Typing after a result must start a new calculation.");
        Keys("C", "2", "+", "×", "3", "="); check(calculator.Display == "6", "Replacing a pending operator must not apply the previous one.");
        Keys("C", "2", "+", "3", "×", "4", "="); check(calculator.Display == "20", "Basic calculator chains must evaluate in entry order.");
        Keys("C", "2", "0", "0", "+", "1", "0", "%", "="); check(calculator.Display == "220", "Addition percent must use the first operand as its base.");
        Keys("C", "2", "0", "0", "×", "1", "0", "%", "="); check(calculator.Display == "20", "Multiplication percent must use a fractional operand.");
        Keys("C", "8", "÷", "0", "="); check(calculator.HasError, "Division by zero must show a recoverable error.");
        Keys("2", "Back", "3", "+/−"); check(!calculator.HasError && calculator.Display == "-3", "Digits and backspace must recover cleanly after an arithmetic error.");
        Keys("C", "1", ".", ".", "5"); check(calculator.Display == "1.5", "A second decimal separator must be ignored.");
        Keys("C"); foreach (char digit in new string('9', 28)) calculator.Input(digit.ToString()); Keys("×", "9", "="); check(calculator.HasError, "Decimal overflow must stay within the calculator error boundary.");

        var state = new ShellState { QuickNote = "Keep my existing note" }; ExploreWorkspace.Normalize(state);
        string id = NotesWorkspace.Add(state); check(NotesWorkspace.Read(state).Length == 2, "A native note must share the saved board while keeping the legacy quick note.");
        check(NotesWorkspace.Update(state, id, "Plan", "Study then code") && state.SavedItems.Single().Note == "Study then code", "Notes edits must update the same saved item used in Explore.");
        NotesWorkspace.TogglePin(state, id); check(NotesWorkspace.Read(state, "code", true).Single().Id == id, "Pinned search must match note contents.");
        var snapshot = state.Snapshot(); NotesWorkspace.Update(state, id, "Updated", "Different"); check(snapshot.SavedItems.Single().Title == "Plan", "A queued save must not change when the live note is edited.");
        NotesWorkspace.Update(state, NotesWorkspace.QuickId, "", "Shared quick note"); check(state.QuickNote == "Shared quick note", "The floating editor must use the Study quick-note field.");
        check(!NotesWorkspace.Remove(state, NotesWorkspace.QuickId), "Deleting a board note must not delete the quick note.");
        check(NotesWorkspace.Remove(state, id) && !NotesWorkspace.Update(state, id, "Lost", "Lost"), "Stale edits must not recreate a note removed in another window.");

        FileEntry[] entries = [new("b.jpg", "b.jpg", false, "") { Bytes = 20, ModifiedUtc = new(2026, 1, 2) }, new("a.zip", "a.zip", false, "") { Bytes = 100, ModifiedUtc = new(2026, 1, 1) }, new("folder", "folder", true, "")];
        check(FilePresentation.Filter(entries, "", FileSort.Size, true).Select(e => e.Name).SequenceEqual(new[] { "folder", "a.zip", "b.jpg" }), "Size sorting must retain folders first.");
        check(FilePresentation.Filter(entries, "", FileSort.Newest, true)[1].Name == "b.jpg", "Newest sorting must use metadata, not display strings.");
        check(FilePresentation.Filter(entries, "JPG", FileSort.Name, false).Single().Name == "b.jpg", "File filtering must ignore extension casing.");
        check(FilePresentation.CanPreview("a.JPEG") && !FilePresentation.CanPreview("a.svg") && FilePresentation.Icon("a.zip", false) == "Archive", "Preview eligibility and file artwork must not route executables or active SVG content through raster decoding.");
        var history = new NavigationTrail(); history.Visit("first"); history.Visit("second");
        check(history.BackTarget == "first" && history.Current == "second", "Checking a back target must not move history before folder I/O succeeds.");
        history.Back(); history.Visit("third"); check(!history.CanGoForward, "New folder navigation must truncate forward history.");

        var visibility = new DockVisibility();
        check(!visibility.Update(true, false, true, true, true, 1) && !visibility.Update(false, false, true, true, true, 2), "Fullscreen must take precedence over stale preview and context-menu interaction holds in both dock modes.");
        foreach (var monitor in new[] { new ShellRect(0, 0, 1920, 1080), new ShellRect(-2560, -140, 2560, 1440), new ShellRect(100, 200, 640, 480) })
        foreach (double scale in new[] { 1d, 1.5, 2, double.NaN })
        {
            var rect = DesktopLayout.SpotlightBounds(monitor, scale);
            check(rect.Width > 0 && rect.Height > 0 && rect.X >= monitor.X && rect.Y >= monitor.Y && rect.Right <= monitor.Right && rect.Bottom <= monitor.Bottom, "Spotlight must fit scaled and offset monitors.");
        }
        string directory = Path.Combine(Path.GetTempPath(), "Nexus-alias-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new StateStore(directory);
            foreach (var alias in new[] { ("Graphite", "Slate"), ("Lagoon", "Aurora"), ("Pearl", "Orbit") })
            { store.Save(new() { Wallpaper = alias.Item1, QuickNote = "Preserved" }); var saved = store.Load(); check(saved.Wallpaper == alias.Item2 && saved.QuickNote == "Preserved", "Theme aliases must round-trip into their canonical settings mood without resetting content."); }
        }
        finally { Directory.Delete(directory, true); }
        Console.WriteLine("PASS: decimal calculator/error recovery, shared notes/pins/stale edits, metadata file sorting, history commit, fullscreen priority, Spotlight placement and theme aliases.");
    }
}
