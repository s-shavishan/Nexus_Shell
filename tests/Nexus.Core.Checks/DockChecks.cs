using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Nexus.Shell.Interop;

internal static class DockChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run()
    {
        var dock = new DockWindowSet();
        var windows = Enumerable.Range(1, 130).Select(i => new RunningWindow(new IntPtr(i), "App " + i, "app", i + 100)).ToArray();
        Check(dock.Reconcile(windows), "New app identities must change the dock snapshot.");
        Check(dock.Windows.Count == 130, "All windows must remain accessible beyond the old 20/80-window limits.");
        Check(!dock.Reconcile(windows.Reverse().ToArray()),
            "Z-order-only changes must not trigger layout reconciliation.");
        Check(dock.Windows.SequenceEqual(windows), "Foreground/z-order changes must not move dock buttons.");
        var renamed = windows[30] with { Title = "Renamed document" };
        Check(dock.Reconcile(windows.Select(w => w.Handle == renamed.Handle ? renamed : w).ToArray()),
            "Window title changes must invalidate the dock button metadata.");
        Check(dock.Windows[30] == renamed, "A title update must change its existing entry without moving it.");
        var replacement = windows[0] with { ProcessId = 9000, Title = "New owner" };
        Check(dock.Reconcile([replacement, .. windows.Skip(1)]),
            "PID changes on a reused HWND must count as lifecycle changes.");
        Check(dock.Windows.Count == 130 && dock.Windows[^1] == replacement && !dock.Windows.Contains(windows[0]),
            "Reused HWNDs must remove the previous process and append a new identity.");
        dock.Reconcile([replacement, replacement, new(IntPtr.Zero, "Invalid", "app", 100), new(new IntPtr(999), "Invalid owner", "app")]);
        Check(dock.Windows.SequenceEqual(new[] { replacement }), "Closed, duplicate and invalid identities must not leave ghost buttons.");
        Check(dock.Reconcile([]), "Closing remaining windows must invalidate the dock snapshot.");
        Check(!dock.Reconcile([]), "An unchanged empty dock must skip repeated layout work.");
        Check(dock.Windows.Count == 0, "Closing every app must clear the running dock.");

        Check(NativeMethods.DockWindowEligibleVisibility(true, false),
            "Normal visible app windows must remain in the dock.");
        Check(NativeMethods.DockWindowEligibleVisibility(true, true),
            "Visible minimized app windows must remain in the dock.");
        Check(NativeMethods.DockWindowEligibleVisibility(false, true),
            "An iconic app that temporarily clears WS_VISIBLE must retain its dock entry.");
        Check(!NativeMethods.DockWindowEligibleVisibility(false, false),
            "Ordinary hidden non-iconic app windows must not be added to the dock.");

        // A rejected DispatcherQueue.TryEnqueue used to strand the dock's
        // reposition flag permanently, blocking later width/position changes.
        var dispatch = new CoalescedUiDispatch();
        var work = new Queue<Action>();
        bool Enqueue(Action callback) { work.Enqueue(callback); return true; }
        int positions = 0;
        Check(dispatch.Request(Enqueue, () => positions++), "The first dock reposition request must be queued.");
        Check(!dispatch.Request(Enqueue, () => positions += 100) && work.Count == 1,
            "Rapid dock width changes must share one queued reposition.");
        work.Dequeue()();
        Check(positions == 1, "A coalesced reposition must execute exactly once.");
        Check(!dispatch.Request(_ => false, () => positions += 100),
            "A rejected dispatcher request must be reported as unscheduled.");
        Check(dispatch.Request(Enqueue, () => positions++),
            "A rejected enqueue must not block future dock reposition requests.");
        work.Dequeue()();
        Check(positions == 2, "The next accepted request must execute after rejection.");
        bool dispatcherThrew = false;
        try { dispatch.Request(_ => throw new InvalidOperationException("Dispatcher shut down"), () => positions++); }
        catch (InvalidOperationException) { dispatcherThrew = true; }
        Check(dispatcherThrew && dispatch.Request(Enqueue, () => positions++),
            "A throwing dispatcher must not permanently poison the scheduling gate.");
        work.Dequeue()();
        Check(positions == 3, "Scheduling must recover from a throwing dispatcher.");
        Check(dispatch.Request(Enqueue, () =>
        {
            positions++;
            Check(dispatch.Request(Enqueue, () => positions++),
                "The gate must release before invoking a reposition callback that schedules another pass.");
        }), "A normal dock position update must enqueue.");
        work.Dequeue()(); work.Dequeue()();
        Check(positions == 5 && work.Count == 0, "Reentrant dock reposition updates must settle without lost or duplicate callbacks.");

        // A logical hide can outlive the compositor animation that owns it.
        Check(DockVisibilityTransitionPolicy.Decide(false, true, true, false, false)
                == DockVisibilityAction.AnimateHide,
            "A visible dock leaving its reveal corridor should animate once.");
        Check(DockVisibilityTransitionPolicy.Decide(false, false, true, true, false)
                == DockVisibilityAction.None,
            "An in-flight hide must not cause another hide on an 80 ms poll.");
        Check(DockVisibilityTransitionPolicy.Decide(false, false, true, false, false)
                == DockVisibilityAction.HideImmediately,
            "External native visibility must be corrected while the dock is logically hidden.");
        Check(DockVisibilityTransitionPolicy.Decide(false, false, true, true, true)
                == DockVisibilityAction.HideImmediately,
            "Fullscreen must interrupt animation and hide the dock immediately.");
        Check(DockVisibilityTransitionPolicy.Decide(true, false, true, true, false)
                == DockVisibilityAction.Show,
            "Edge reveal must cancel a pending hide and restore the dock.");
        Check(DockVisibilityTransitionPolicy.Decide(true, true, false, false, false)
                == DockVisibilityAction.Show,
            "A native-hidden dock must be restored when it is required onscreen.");
        Check(DockVisibilityTransitionPolicy.Decide(true, true, true, false, false)
                == DockVisibilityAction.None
            && DockVisibilityTransitionPolicy.Decide(false, false, false, false, false)
                == DockVisibilityAction.None,
            "Stable HWND visibility must not enqueue native operations.");
        Check(DockVisibilityTransitionPolicy.Decide(false, true, false, false, false)
                == DockVisibilityAction.HideImmediately,
            "Externally hidden windows must settle an outdated shown flag.");

        // Avoid a permanent 80 ms native polling loop when the dock is idle,
        // but keep the original edge-reveal latency during maximized windows.
        Check(DockRefreshCadence.Interval(true, false, false) == DockRefreshCadence.IdleMilliseconds,
            "A visible floating dock should not poll the native foreground at interactive speed.");
        Check(DockRefreshCadence.Interval(false, false, false) == DockRefreshCadence.IdleMilliseconds
            && DockRefreshCadence.Interval(false, true, false) == DockRefreshCadence.IdleMilliseconds,
            "Edge-mode docks without interactions should use the relaxed fallback interval.");
        Check(DockRefreshCadence.Interval(true, true, false) == DockRefreshCadence.ActiveMilliseconds,
            "Floating dock edge reveal must retain its responsive 80 ms polling.");
        Check(DockRefreshCadence.Interval(true, false, true) == DockRefreshCadence.ActiveMilliseconds
            && DockRefreshCadence.Interval(false, false, true) == DockRefreshCadence.ActiveMilliseconds,
            "Open dock previews, menus and other interactions require fast native pointer sampling.");

        // Visibility edge polling is intentionally fast, but native maximize
        // repair should not issue duplicate DWM queries on every 80 ms tick.
        var repair = new ForegroundRepairCadence();
        var firstWindow = new IntPtr(101);
        var nextWindow = new IntPtr(102);
        Check(!repair.ShouldSample(IntPtr.Zero, false, 0),
            "No foreground window must not trigger a native repair pass.");
        Check(repair.ShouldSample(firstWindow, true, 0),
            "The initial maximized foreground window must receive an immediate repair.");
        Check(!repair.ShouldSample(firstWindow, true, 80)
            && !repair.ShouldSample(firstWindow, true, 999),
            "Repeated edge-reveal ticks must not repeat native DWM repair before the fallback interval.");
        Check(repair.ShouldSample(firstWindow, true, 1000),
            "A stable foreground window must receive bounded periodic reconciliation.");
        Check(repair.ShouldSample(nextWindow, true, 1001),
            "Switching foreground windows must never wait for the fallback interval.");
        Check(repair.ShouldSample(nextWindow, false, 1002)
            && repair.ShouldSample(nextWindow, true, 1003),
            "Maximize and restore transitions must trigger immediate native geometry checks.");
        Check(!repair.ShouldSample(nextWindow, true, 1004)
            && repair.ShouldSample(nextWindow, true, 999),
            "Clock rollback must recover the repair schedule instead of stranding updates.");
        Check(!repair.ShouldSample(IntPtr.Zero, false, 1000)
            && repair.ShouldSample(nextWindow, true, 1001),
            "Foreground loss and reacquisition must resample even the same HWND.");

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
        // Fullscreen detection must be driven by coverage + native frame style,
        // not solely by IsZoomed (some borderless games report maximized).
        var mainMonitor = new ShellRect(0, 0, 1920, 1080);
        var secondaryMonitor = new ShellRect(-2560, -100, 2560, 1440);
        const long captionedStyle = 0x00C00000;
        const long borderlessStyle = unchecked((long)0x80000000); // WS_POPUP
        Check(DockFullscreenPolicy.IsFullscreen(mainMonitor, mainMonitor, false, borderlessStyle),
            "A borderless full-monitor app must hide the dock.");
        Check(DockFullscreenPolicy.IsFullscreen(mainMonitor, mainMonitor, true, borderlessStyle),
            "A borderless game reporting maximized must still hide the dock.");
        Check(!DockFullscreenPolicy.IsFullscreen(mainMonitor, mainMonitor, true, captionedStyle),
            "A normal captioned maximized app must retain floating dock edge reveal.");
        Check(DockFullscreenPolicy.IsFullscreen(secondaryMonitor, secondaryMonitor, true, borderlessStyle),
            "Fullscreen detection must support negative-origin monitors.");
        Check(!DockFullscreenPolicy.IsFullscreen(new(0, 0, 1919, 1080), mainMonitor, false, borderlessStyle),
            "A nearly-fullscreen ordinary window must not suppress the dock.");
        Check(!DockFullscreenPolicy.IsFullscreen(mainMonitor, secondaryMonitor, false, borderlessStyle),
            "A window on another monitor must not suppress this dock.");
        Check(!DockFullscreenPolicy.IsFullscreen(default, mainMonitor, true, borderlessStyle),
            "Invalid app geometry must not trigger fullscreen.");
        Check(!DockFullscreenPolicy.IsFullscreen(mainMonitor, default, true, borderlessStyle),
            "Invalid monitor geometry must not trigger fullscreen.");

        var monitor = new ShellRect(-1920, -180, 1920, 1080);
        var bar = new ShellRect(-1500, 800, 900, 76);
        Check(DockVisibility.InRevealArea(monitor, bar, -1000, 899, false, 1), "Reveal edges must work on monitors with negative origins.");
        Check(!DockVisibility.InRevealArea(monitor, bar, -1000, 890, false, 1), "A hidden dock must only reveal from the edge.");
        Check(DockVisibility.InRevealArea(monitor, bar, -1000, 890, true, 1), "The gap between the dock and edge must keep a revealed dock open.");
        Check(!DockVisibility.InRevealArea(monitor, bar, 1, 899, true, 1), "Another monitor must not reveal this dock.");
        Console.WriteLine("PASS: dock identity/order, 130-window overflow, handle reuse, coalesced reposition recovery and auto-hide/reveal/fullscreen policy.");
    }
}
