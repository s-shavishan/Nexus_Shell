using Nexus.Shell.Models;
using Nexus.Shell.Services;

internal static class DockChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run()
    {
        var dock = new DockWindowSet();
        var windows = Enumerable.Range(1, 130).Select(i => new RunningWindow(new IntPtr(i), "App " + i, "app", i + 100)).ToArray();
        dock.Reconcile(windows);
        Check(dock.Windows.Count == 130, "All windows must remain accessible beyond the old 20/80-window limits.");
        dock.Reconcile(windows.Reverse().ToArray());
        Check(dock.Windows.SequenceEqual(windows), "Foreground/z-order changes must not move dock buttons.");
        var renamed = windows[30] with { Title = "Renamed document" };
        dock.Reconcile(windows.Select(w => w.Handle == renamed.Handle ? renamed : w).ToArray());
        Check(dock.Windows[30] == renamed, "A title update must change its existing entry without moving it.");
        var replacement = windows[0] with { ProcessId = 9000, Title = "New owner" };
        dock.Reconcile([replacement, .. windows.Skip(1)]);
        Check(dock.Windows.Count == 130 && dock.Windows[^1] == replacement && !dock.Windows.Contains(windows[0]),
            "Reused HWNDs must remove the previous process and append a new identity.");
        dock.Reconcile([replacement, replacement, new(IntPtr.Zero, "Invalid", "app", 100), new(new IntPtr(999), "Invalid owner", "app")]);
        Check(dock.Windows.SequenceEqual(new[] { replacement }), "Closed, duplicate and invalid identities must not leave ghost buttons.");
        dock.Reconcile([]); Check(dock.Windows.Count == 0, "Closing every app must clear the running dock.");

        var visibility = new DockVisibility();
        Check(visibility.Update(true, false, false, false, false, 0), "Floating dock must be visible on the desktop.");
        Check(!visibility.Update(true, true, false, false, false, 1), "A maximized app must hide the floating dock.");
        Check(visibility.Update(true, true, false, false, true, 100), "The bottom edge must reveal the dock.");
        Check(visibility.Update(true, true, false, false, false, 300), "Crossing the floating gap must retain the reveal.");
        Check(!visibility.Update(true, true, false, false, false, 551), "Leaving the reveal area must hide after the hold expires.");
        Check(visibility.Update(true, true, false, true, false, 600), "Start, Quick Settings and context menus must keep the dock reachable.");
        Check(!visibility.Update(true, false, true, false, true, 601), "Fullscreen must hide the dock even at the reveal edge.");
        Check(visibility.Update(false, true, false, false, false, 602), "Edge mode must stay visible for maximized windows.");
        Check(!visibility.Update(false, false, true, false, false, 603), "Fullscreen must also hide edge mode.");
        var monitor = new ShellRect(-1920, -180, 1920, 1080);
        var bar = new ShellRect(-1500, 800, 900, 76);
        Check(DockVisibility.InRevealArea(monitor, bar, -1000, 899, false, 1), "Reveal edges must work on monitors with negative origins.");
        Check(!DockVisibility.InRevealArea(monitor, bar, -1000, 890, false, 1), "A hidden dock must only reveal from the edge.");
        Check(DockVisibility.InRevealArea(monitor, bar, -1000, 890, true, 1), "The gap between the dock and edge must keep a revealed dock open.");
        Check(!DockVisibility.InRevealArea(monitor, bar, 1, 899, true, 1), "Another monitor must not reveal this dock.");
        Console.WriteLine("PASS: dock identity, stable order, title changes, 130-window overflow, handle reuse, removal and auto-hide/reveal/fullscreen policy.");
    }
}
