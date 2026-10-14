using Nexus.Shell.Models;
using Nexus.Shell.Services;

internal static class MidnightChecks
{
    internal static void Run(Action<bool, string> check)
    {
        // Reference-fidelity Midnight glass must be coherent across the
        // dock, menu bar, search and app chrome without degrading fallback.
        var midnight = AuraPalette.For("Midnight");
        check(MidnightGlassMetrics.ReferenceEnabled(midnight, false)
            && !MidnightGlassMetrics.ReferenceEnabled(midnight, true)
            && !MidnightGlassMetrics.ReferenceEnabled(AuraPalette.For("Solstice"), false),
            "Reference styling must be isolated to Midnight and respect High Contrast.");
        check(MidnightGlassMetrics.DockRadius > MidnightGlassMetrics.ControlRadius
            && MidnightGlassMetrics.FrameRadius > MidnightGlassMetrics.CardRadius
            && MidnightGlassMetrics.DockButtonWidth > MidnightGlassMetrics.DockCompactButtonWidth,
            "Midnight visual geometry must preserve intentional corner and density hierarchy.");
        check(AuraColor.Contrast(AuraColor.Parse(midnight.Tokens["NexusText"]), AuraColor.Parse(midnight.Canvas)) > 7,
            "Midnight text must remain legible against the dark opaque fallback.");
        check(midnight.Tokens["NexusSelection"] != midnight.Tokens["NexusInput"]
            && midnight.Tokens["NexusBorder"] != midnight.Tokens["NexusSidebar"],
            "Reference selection, input, edge and sidebar surfaces need distinct visual hierarchy.");
        foreach (var role in new[] { "MenuBar", "Dock", "Frame", "Card", "Input" })
        {
            var glass = GlassRecipe.For(midnight, role, "Clear");
            check(glass.TintOpacity is > 0 and < 1 && glass.LuminosityOpacity is > 0 and < 1
                && AuraColor.Parse(glass.Fallback).A == 255,
                "Reference glass must have finite translucent parameters and a fully opaque safety fallback.");
        }
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

        check(MidnightGlassMetrics.DockButtonWidth == 64
            && MidnightGlassMetrics.DockButtonHeight == 68
            && DesktopLayout.TaskbarHeight(false) >= MidnightGlassMetrics.DockButtonHeight + 20
            && DesktopLayout.TaskbarHeight(true) == 56,
            "Floating dock must reserve room for the real icon size and preserve compact mode.");
        // Screenshot-driven Files pane sizing: preserve the primary file list
        // under tight widths and do not add a noninteractive inspector to dialogs.
        foreach (double width in new[] { 320d, 590d, 859d, 860d, 940d, 1180d, double.NaN })
        {
            var browse = FilesPaneLayoutPolicy.Resolve(width, browse: true);
            bool expectedInspector = double.IsFinite(width) && width >= 860;
            double expectedWidth = expectedInspector ? width >= 1170 ? 256 : 228 : 0;
            check(browse.SidebarWidth is >= 136 and <= 206
                && browse.InspectorVisible == expectedInspector
                && browse.InspectorWidth == expectedWidth,
                "Responsive Files panes must preserve their width hierarchy and hide narrow inspectors.");
            var picker = FilesPaneLayoutPolicy.Resolve(width, browse: false);
            check(!picker.InspectorVisible && picker.InspectorWidth == 0,
                "File selection dialogs must not allocate content width to an inspector.");
        }
        check(LaunchpadCatalog.PageSize == 18 && 6 * 3 == LaunchpadCatalog.PageSize,
            "Launchpad must show three complete six-column rows at reference size.");
        check(LaunchpadArtworkPolicy.UsesNexusArtwork("sections")
            && !LaunchpadArtworkPolicy.UsesNexusArtwork("discovered-app"),
            "Only Nexus-owned builtins should keep fixed vector app artwork.");
        check(LaunchpadArtworkPolicy.Monogram("Administrative Tools") == "AT"
            && LaunchpadArtworkPolicy.Monogram("Event Viewer") == "EV"
            && LaunchpadArtworkPolicy.Monogram("Everything") == "EV"
            && LaunchpadArtworkPolicy.Monogram("7-Zip") == "7Z"
            && LaunchpadArtworkPolicy.Monogram("") == "APP",
            "Discovered apps need distinct, stable monograms instead of repeated placeholders.");
        check(LaunchpadArtworkPolicy.BackgroundForContrast("Everything", true) == "00000000"
            && LaunchpadArtworkPolicy.BackgroundForContrast("Everything", false) == LaunchpadArtworkPolicy.Background("Everything"),
            "Switching High Contrast must clear generated artwork without reloading native icons.");
        check(ControlCenterTilePolicy.IsPrimary("Network") && ControlCenterTilePolicy.IsPrimary("Display")
            && !ControlCenterTilePolicy.IsPrimary("Power") && !ControlCenterTilePolicy.IsPrimary("Desktop")
            && ControlCenterTilePolicy.Height("Network") > ControlCenterTilePolicy.Height("Power")
            && ControlCenterTilePolicy.Radius("Network") > ControlCenterTilePolicy.Radius("Desktop"),
            "Control Center primary modules must remain visibly distinct from secondary actions.");
        check(LaunchpadArtworkPolicy.Background("Event Viewer") == LaunchpadArtworkPolicy.Background("Event Viewer")
            && LaunchpadArtworkPolicy.Background("Event Viewer").Length == 8,
            "Fallback application tile color must be stable and fully opaque.");
        // The launcher must fit the same real, negatively offset monitors as
        // the expanded search without ever stealing focus or going off screen.
        foreach (var monitor in new[] { new ShellRect(0, 0, 1649, 928), new ShellRect(-2560, -140, 2560, 1440), new ShellRect(100, 200, 640, 480) })
        foreach (var scale in new[] { 1d, 1.5, 2d, double.NaN })
        {
            var compact = DesktopLayout.SpotlightPanelBounds(monitor, scale, false);
            var expanded = DesktopLayout.SpotlightPanelBounds(monitor, scale, true);
            check(compact.Width > 0 && compact.Height > 0
                && compact.X >= monitor.X && compact.Y >= monitor.Y
                && compact.Right <= monitor.Right && compact.Bottom <= monitor.Bottom,
                "Compact Spotlight must fit available desktop space.");
            check(expanded.Width == compact.Width && expanded.Height >= compact.Height
                && expanded.X >= monitor.X && expanded.Y >= monitor.Y
                && expanded.Right <= monitor.Right && expanded.Bottom <= monitor.Bottom,
                "Expanded search must keep the Spotlight width, stay on screen and grow only vertically.");
        }
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
