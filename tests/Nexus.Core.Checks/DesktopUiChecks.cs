using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Nexus.Runtime;
using System.Text.Json;

internal static class DesktopUiChecks
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static bool Contains(ShellRect outer, ShellRect inner) => inner.Width > 0 && inner.Height > 0
        && inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;
    internal static void Run()
    {
        Check(ControlCenterTilePolicy.RetainPanelAfterFailure("Network", true, true)
            && ControlCenterTilePolicy.RetainPanelAfterFailure("Bluetooth", true, true)
            && ControlCenterTilePolicy.RetainPanelAfterFailure("Power", true, true)
            && ControlCenterTilePolicy.RetainPanelAfterFailure("Display", false, true)
            && !ControlCenterTilePolicy.RetainPanelAfterFailure("Network", false, true)
            && !ControlCenterTilePolicy.RetainPanelAfterFailure("Network", true, false),
            "Failed device mutations retain usable controls without replay; failed first reads retain no stale panel.");
        var mixedLinks = new[]
        {
            new LinkState("VPN", "Tunnel", "Down", "", "", "", 0),
            new LinkState("Ethernet", "Ethernet", "Up", "192.168.1.5", "", "", 1000),
            new LinkState("Wi-Fi", "Wireless80211", "Up", "192.168.1.8", "", "", 300),
            new LinkState("Virtual adapter", "Virtual", "Down", "", "", "", 0)
        };
        var arrangedLinks = NetworkPresentationPolicy.OrderedLinks(mixedLinks);
        Check(arrangedLinks.Count == 4 && arrangedLinks[0].Name == "Ethernet"
            && arrangedLinks[1].Name == "Wi-Fi"
            && NetworkPresentationPolicy.InitiallyVisibleLinks(arrangedLinks) == 2,
            "The compact Network panel must show active adapters first and keep all remaining Windows adapters accessible.");
        var activeLinks = Enumerable.Range(0, 6)
            .Select(i => new LinkState("Active " + i, "Ethernet", "Up", "", "", "", 100))
            .Concat(mixedLinks).ToArray();
        var activeSorted = NetworkPresentationPolicy.OrderedLinks(activeLinks);
        Check(NetworkPresentationPolicy.InitiallyVisibleLinks(activeSorted) == 8
            && activeSorted.Take(8).All(NetworkPresentationPolicy.Connected)
            && !NetworkPresentationPolicy.Connected(mixedLinks[0]),
            "Network overflow cannot hide an active VPN, virtual interface or other connected adapter.");
        var wifiRows = new[]
        {
            new WifiNetwork("Weak", "", 20, false, true),
            new WifiNetwork("Live", "profile1", 10, true, true),
            new WifiNetwork("Strong", "", 90, false, true)
        };
        var rankedWifi = NetworkPresentationPolicy.OrderedWifi(wifiRows);
        Check(rankedWifi.Select(x => x.Name).SequenceEqual(["Live", "Strong", "Weak"])
            && NetworkPresentationPolicy.InitialWifiRows == 6,
            "Connected Wi-Fi must remain first while visible scan results are ordered by signal and collapsed after six.");
        Check(FileTypeContextCatalog.Initialize() && FileTypeContextCatalog.Reload(),
            "The shipped file context schema must parse before native UI builds.");
        string fileTypeJson = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Assets", "Configuration", "file_context_profiles.json"));
        Check(FileTypeContextCatalog.ValidateJson(fileTypeJson, out var schemaError) && schemaError.Length == 0,
            "The shipped file-type profiles must pass pure schema validation.");
        string duplicateExtension = fileTypeJson.Replace("\".mp4\"", "\".png\"", StringComparison.Ordinal);
        Check(!FileTypeContextCatalog.ValidateJson(duplicateExtension, out var duplicateError)
            && duplicateError.Contains("Ambiguous extension", StringComparison.Ordinal),
            "Conflicting extension ownership must fail before it reaches the live menu cache.");
        Check(!FileTypeContextCatalog.ValidateJson(fileTypeJson.Replace("\"primary\": \"open\"",
                "\"primary\": \"decrypt\"", StringComparison.Ordinal), out _)
            && !FileTypeContextCatalog.ValidateJson("{broken json", out _),
            "Unknown action handlers and malformed JSON must never become executable menu entries.");
        Check(FileTypeContextCatalog.Resolve(@"C:\Data\movie.mp4", false).Id == "video.general",
            "Rejecting an invalid proposed catalog must preserve the last known-good live resolution.");
        Check(FileTypeContextCatalog.Resolve(@"C:\Data\movie.MP4", false).Id == "video.general"
            && FileTypeContextCatalog.Resolve(@"C:\Data\archive.TAR.GZ", false).Id == "archive.generic"
            && FileTypeContextCatalog.Resolve(@"C:\Data\project.TS", false).Id == "code.source"
            && FileTypeContextCatalog.Resolve(@"C:\Data\unknown.MDF", false).Id == "fallback.unknown"
            && FileTypeContextCatalog.Resolve(@"C:\Data\images", true).Id == "folder",
            "Profiles must be deterministic, case-insensitive and support longest compound extensions with safe fallback.");
        var mixedFileContext = FileTypeContextCatalog.Plan([
            new FileTypeMenuTarget(@"C:\Data\photo.png", false),
            new FileTypeMenuTarget(@"C:\Data\project.cs", false)]);
        Check(mixedFileContext.SelectionCount == 2 && mixedFileContext.Primary.Length == 0
            && mixedFileContext.CommonActions.SequenceEqual(["open_with"]),
            "Mixed selections retain only common type capabilities and never mass-run applications.");
        Check(FileTypeContextCatalog.Resolve(@"C:\Data\shortcut.lnk", false).Id == "shortcuts.windows"
            && FileTypeContextCatalog.Resolve(@"C:\Data\demo.CUSTOM", false).Id == "fallback.unknown",
            "Shortcuts must not offer an unsuitable Open with command; unknown types remain usable.");
        Check(FileTypeContextCatalog.Resolve(@"C:\Data\source.cs", false).Actions.Contains("edit")
            && FileTypeContextCatalog.Resolve(@"C:\Data\settings.json", false).Actions.Contains("edit")
            && !FileTypeContextCatalog.Resolve(@"C:\Data\installer.exe", false).Actions.Contains("edit")
            && !FileTypeContextCatalog.Resolve(@"C:\Data\shortcut.lnk", false).Actions.Contains("edit")
            && !FileTypeContextCatalog.Resolve(@"C:\Data\photo.png", false).Actions.Contains("edit"),
            "Text editor fallback must only be offered for profiles with declared edit support.");
        Check(FileTypeContextCatalog.Plan([new(@"C:\Data\audio.mp3", false)]).Primary == "play"
            && FileContextPolicy.For(@"C:\Data\setup.exe", false).OpenLabel == "Run application"
            && FileContextPolicy.For(@"C:\Data\bundle.tar.gz", false).OpenLabel == "Open archive",
            "Native context labels reflect their file-profile default.");
        var displayEpoch = Guid.NewGuid();
        SettingsRules.Validate(new SettingsRequest("Display", 17,
            new SettingsChange("display-preview", "DISPLAY1", "1280x720@60", Epoch: displayEpoch)), displayEpoch);
        SettingsRules.Validate(new SettingsRequest("Display", 17,
            new SettingsChange("display-keep", "DISPLAY1", Epoch: displayEpoch)), displayEpoch);
        SettingsRules.Validate(new SettingsRequest("Display", 17,
            new SettingsChange("display-revert", "DISPLAY1", Epoch: displayEpoch)), displayEpoch);
        bool badDisplayRejected = false;
        try { SettingsRules.Validate(new SettingsRequest("Display", 17,
            new SettingsChange("display-preview", "DISPLAY1", "", Epoch: displayEpoch)), displayEpoch); }
        catch (RuntimeFailure) { badDisplayRejected = true; }
        Check(badDisplayRejected, "Mode changes must reject empty mode IDs.");
        badDisplayRejected = false;
        try { SettingsRules.Validate(new SettingsRequest("Display", 17,
            new SettingsChange("display-keep", "DISPLAY1", Enabled: true, Epoch: displayEpoch)), displayEpoch); }
        catch (RuntimeFailure) { badDisplayRejected = true; }
        Check(badDisplayRejected, "Confirmation cannot carry unvalidated toggle values.");
        Check(new[] { 0, 2, 55, 259, 433, 1168 }.All(BluetoothRadioAvailability.NoRadioError)
            && !BluetoothRadioAvailability.NoRadioError(5)
            && !BluetoothRadioAvailability.NoRadioError(31),
            "No-radio Win32 errors must hide Bluetooth but real access/driver failures must remain visible.");
        Check(!BluetoothRadioAvailability.ShowControls(false)
            && BluetoothRadioAvailability.ShowControls(true)
            && BluetoothRadioAvailability.ShowControls(null),
            "Bluetooth UI must disappear only for positively confirmed absent radios.");
        Check(WindowsToolCatalog.Find("night-light")?.Target == "ms-settings:nightlight"
            && WindowsToolCatalog.Find("network-adapters")?.Target == "ncpa.cpl"
            && WindowsToolCatalog.Find("project-display")?.Target == "DisplaySwitch.exe",
            "Advanced Control Center actions must use fixed audited Windows routes.");
        Check(WindowOverviewInteractionPolicy.CanSwitch(0)
            && !WindowOverviewInteractionPolicy.CanSwitch(1)
            && !WindowOverviewInteractionPolicy.CanSwitch(12),
            "The overview cannot silently discard a staged arrangement when switching cards.");
        Check(WindowOverviewInteractionPolicy.SuppressClick("hwnd:1", "hwnd:1", 1400, 1300)
            && !WindowOverviewInteractionPolicy.SuppressClick("hwnd:1", "hwnd:2", 1400, 1300)
            && !WindowOverviewInteractionPolicy.SuppressClick("hwnd:1", "hwnd:1", 1400, 1400)
            && !WindowOverviewInteractionPolicy.SuppressClick(null, "hwnd:1", 1400, 1300),
            "Drag-click suppression must affect only the dragged card until its deadline.");
        // Live window dragging must not depend on a queryable process path.
        // Use native screen/work-area geometry, not persistent saved-layout
        // identity, when staging a move on another display.
        var fromDisplay = new LayoutRect(-1920, -100, 1920, 1080);
        var toDisplay = new LayoutRect(0, 38, 1920, 1042);
        var liveBounds = new LayoutRect(-1650, 0, 800, 600);
        var projected = WindowOverviewGeometry.ProjectBetween(liveBounds, fromDisplay, toDisplay);
        Check(projected.X == 270 && projected.Y >= 130 && projected.Y <= 135
            && projected.Width == 800 && projected.Height >= 575 && projected.Height <= 582,
            "Native live HWND positions must project across monitors without requiring a saved app identity.");
        var sameDisplay = WindowOverviewGeometry.ProjectBetween(liveBounds, fromDisplay, fromDisplay);
        Check(sameDisplay == liveBounds, "Projecting onto the same monitor must preserve an already valid window rectangle.");
        var smallDisplay = new LayoutRect(2400, 100, 120, 80);
        var constrained = WindowOverviewGeometry.ProjectBetween(liveBounds, fromDisplay, smallDisplay);
        Check(constrained.X >= smallDisplay.X && constrained.Y >= smallDisplay.Y
            && (long)constrained.X + constrained.Width <= (long)smallDisplay.X + smallDisplay.Width
            && (long)constrained.Y + constrained.Height <= (long)smallDisplay.Y + smallDisplay.Height,
            "A small secondary display must never receive an offscreen staged rectangle.");
        bool rejectedInvalidGeometry = false;
        try { _ = WindowOverviewGeometry.ProjectBetween(liveBounds, fromDisplay, default); }
        catch (ArgumentException) { rejectedInvalidGeometry = true; }
        Check(rejectedInvalidGeometry, "A disconnected or zero-area monitor must reject live window projection.");
        // Fullscreen overview moves are only geometry until the user applies.
        var workspace = new LayoutRect(-1920, -80, 1920, 1080);
        var beforeOverview = new LayoutRect(-1700, 0, 800, 600);
        var free = WindowOverviewGeometry.Drag(workspace, beforeOverview, 50, 90, -1200, 440, 1);
        Check(free.Zone == "Free position" && free.Bounds.X == -1650 && free.Bounds.Y == 90,
            "Overview should stage free movement using physical negative-origin geometry.");
        var leftSnap = WindowOverviewGeometry.Drag(workspace, beforeOverview, 0, 0, workspace.X + 10, 420, 1);
        Check(leftSnap.Zone == "Left half" && leftSnap.Bounds.X == -1920 && leftSnap.Bounds.Width == 960,
            "Overview left-edge drag should preview an exact half-screen snap.");
        var quarter = WindowOverviewGeometry.Drag(workspace, beforeOverview, 0, 0, workspace.X + 2, workspace.Y + 2, 1.5);
        Check(quarter.Zone == "Quarter snap" && quarter.Bounds.Width == 960 && quarter.Bounds.Height == 540,
            "Overview corner drag should stage a quarter-screen tile at scaled DPI.");
        var opposite = WindowOverviewGeometry.Drag(workspace, beforeOverview, 0, 0, workspace.X + workspace.Width - 1, 420, 1);
        Check(opposite.Zone == "Right half" && opposite.Bounds.X == -960,
            "Overview right-edge drop must position windows on the correct monitor half.");
        var edgeClamp = WindowOverviewGeometry.Drag(workspace, beforeOverview, 40000, 40000, -1200, 440, 1);
        Check(edgeClamp.Bounds.X + edgeClamp.Bounds.Width == workspace.X + workspace.Width
            && edgeClamp.Bounds.Y + edgeClamp.Bounds.Height == workspace.Y + workspace.Height,
            "Overview free placement must clamp off-screen drags to the monitor work area.");
        // A selection box must follow mouse motion in every direction, and
        // must never activate unrelated desktop items in empty space.
        Check(!SelectionMarqueeGeometry.HasStarted(10, 10, 13, 13)
            && SelectionMarqueeGeometry.HasStarted(10, 10, 14, 10)
            && SelectionMarqueeGeometry.HasStarted(10, 10, 10, 14),
            "Marquee should begin only after the mouse passes its drag threshold.");
        var backwards = SelectionMarqueeGeometry.Between(150, 170, 40, 60);
        Check(backwards == new SelectionMarqueeRect(40, 60, 110, 110),
            "Backwards mouse dragging must normalize selection geometry.");
        Check(SelectionMarqueeGeometry.Intersects(backwards, new(100, 100, 50, 50))
            && !SelectionMarqueeGeometry.Intersects(backwards, new(151, 175, 30, 30))
            && !SelectionMarqueeGeometry.Intersects(backwards, new(0, 0, 35, 35)),
            "Marquee selection should include overlapping icons and exclude non-overlapping ones.");
        var horizontal = SelectionMarqueeGeometry.Between(-400, -100, -120, -100);
        Check(horizontal.Width == 280 && horizontal.Height == 1
            && SelectionMarqueeGeometry.Intersects(horizontal, new(-200, -110, 40, 20)),
            "Horizontal/negative-origin drag boxes must remain selectable.");
        var monitors = new[] { new ShellRect(0, 0, 1920, 1080), new ShellRect(-1920, -180, 1920, 1080), new ShellRect(2400, 0, 2560, 1440), new ShellRect(0, 0, 320, 480) };
        var scales = new[] { .5, 1, 1.25, 1.5, 2, 4, double.NaN, double.PositiveInfinity, -1 };
        var widths = new[] { 280d, 640, 900, 2400, -100, double.NaN, double.PositiveInfinity };
        int cases = 0;
        foreach (var monitor in monitors)
        foreach (double scale in scales)
        foreach (bool compact in new[] { false, true })
        foreach (bool floating in new[] { false, true })
        foreach (double preferred in widths)
        {
            int height = Math.Min(monitor.Height, DesktopLayout.TaskbarReservationHeight(compact, floating, scale));
            var reservation = new ShellRect(monitor.X, monitor.Bottom - height, monitor.Width, height);
            var bar = DesktopLayout.TaskbarBounds(reservation, floating, preferred, scale);
            var work = DesktopLayout.TaskbarWorkArea(monitor, compact, floating, scale);
            Check(Contains(monitor, reservation) && Contains(reservation, bar), "The dock and its native window strip must fit the active monitor at every DPI/origin.");
            Check(Contains(monitor, work) && (floating ? work == monitor : work.Bottom == reservation.Y),
                "Floating mode must reserve zero space; only edge mode may reduce the work area.");
            if (floating)
            {
                Check(bar.X > reservation.X && bar.Right < reservation.Right && bar.Y > reservation.Y && bar.Bottom < reservation.Bottom,
                    "A floating taskbar must leave visible and clickable desktop space around all four edges.");
                Check(Math.Abs((bar.X - reservation.X) - (reservation.Right - bar.Right)) <= 1, "The dock must stay centered as its contents change.");
            }
            else Check(bar == reservation, "Edge-to-edge mode must retain the complete appbar bounds.");
            Check(Contains(monitor, DesktopLayout.MenuBounds(monitor, bar, scale)), "Start must remain on-screen above the visible dock.");
            Check(Contains(monitor, DesktopLayout.QuickSettingsBounds(monitor, bar, scale)), "Quick Settings must remain on-screen at the dock's right edge.");
            foreach (int x in new[] { monitor.X, bar.X + bar.Width / 2, monitor.Right - 1 })
                Check(Contains(monitor, DesktopLayout.PreviewBounds(monitor, bar, new(x, bar.Y, 48, bar.Height), scale)),
                    "A preview above the left, middle or right icon must stay within the active monitor at every origin/DPI.");
            cases++;
        }
        var state = JsonSerializer.Deserialize<ShellState>("{\"CompactDock\":true}")!;
        Check(state.FloatingTaskbar, "Existing settings must migrate to the requested floating taskbar.");
        state.FloatingTaskbar = false;
        Check(!state.Snapshot().FloatingTaskbar && !JsonSerializer.Deserialize<ShellState>(JsonSerializer.Serialize(state.Snapshot()))!.FloatingTaskbar,
            "An explicit edge-to-edge choice must survive snapshots and settings round trips.");
        foreach (var profile in Enum.GetValues<DesktopVisualProfile>())
        {
            DesktopVisuals.Apply(state, profile);
            Check(DesktopVisuals.Read(state) == profile && !state.FloatingTaskbar, "Visual quality must remain independent of the taskbar layout preference.");
        }
        DockChecks.Run();
        DockExperienceChecks.Run();
        Console.WriteLine($"PASS: {cases} floating/appbar/menu placement and work-area cases across monitor origins, DPI and overflow; settings migration and persistence.");
    }
}
