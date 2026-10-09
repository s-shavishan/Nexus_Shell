using Nexus.Shell.Models;
using Nexus.Shell.Services;
using System.Text.Json;

internal static class DesktopUiChecks
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static bool Contains(ShellRect outer, ShellRect inner) => inner.Width > 0 && inner.Height > 0
        && inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;
    internal static void Run()
    {
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
            Check(Contains(monitor, reservation) && Contains(reservation, bar), "The dock and its reserved work area must fit the active monitor at every DPI/origin.");
            if (floating)
            {
                Check(bar.X > reservation.X && bar.Right < reservation.Right && bar.Y > reservation.Y && bar.Bottom < reservation.Bottom,
                    "A floating taskbar must leave visible and clickable desktop space around all four edges.");
                Check(Math.Abs((bar.X - reservation.X) - (reservation.Right - bar.Right)) <= 1, "The dock must stay centered as its contents change.");
            }
            else Check(bar == reservation, "Edge-to-edge mode must retain the complete appbar bounds.");
            Check(Contains(monitor, DesktopLayout.MenuBounds(monitor, bar, scale)), "Start must remain on-screen above the visible dock.");
            Check(Contains(monitor, DesktopLayout.QuickSettingsBounds(monitor, bar, scale)), "Quick Settings must remain on-screen at the dock's right edge.");
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
        Console.WriteLine($"PASS: {cases} floating/appbar/menu placement cases across monitor origins, DPI and overflow; settings migration and persistence.");
    }
}
