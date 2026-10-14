using Nexus.Shell.Models;
using Nexus.Shell.Services;
using Nexus.Shell.Interop;

internal static class DockChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    internal static void Run()
    {
        var stableViewport = new ShellRect(-1400, 40, 350, 210);
        Check(DockPreviewPolicy.ReuseThumbnailGeometry(stableViewport, stableViewport, true, 1000, 1190)
            && !DockPreviewPolicy.ReuseThumbnailGeometry(stableViewport, stableViewport, true, 1000, 1300)
            && !DockPreviewPolicy.ReuseThumbnailGeometry(stableViewport, stableViewport, false, 1000, 1100)
            && !DockPreviewPolicy.ReuseThumbnailGeometry(stableViewport, null, true, 1000, 1100)
            && !DockPreviewPolicy.ReuseThumbnailGeometry(stableViewport, new ShellRect(-1400, 40, 310, 210), true, 1000, 1100)
            && !DockPreviewPolicy.ReuseThumbnailGeometry(stableViewport, stableViewport, true, 1200, 1100),
            "A live DWM thumbnail should reuse native geometry only for identical short-lived viewport updates.");
        Check(DockPinnedWindowPolicy.IsShellArtifact("DWM Notification Window", "dwm")
            && !DockPinnedWindowPolicy.IsShellArtifact("Document", "dwm")
            && !DockPinnedWindowPolicy.IsShellArtifact("DWM Notification Window", "anotherapp"),
            "The DWM helper is a shell artifact, not a user dock window.");
        Check(DockPinnedWindowPolicy.Matches("nexus:calculator", "Calculator", "Calculator", "nexus")
            && DockPinnedWindowPolicy.Matches("nexus:notes", "Notes", "Notes", "nexus")
            && DockPinnedWindowPolicy.Matches(@"C:\Apps\calc.exe", "Calculator", "Unrelated title", "calc")
            && !DockPinnedWindowPolicy.Matches("nexus:calculator", "Calculator", "Notes", "nexus")
            && !DockPinnedWindowPolicy.Matches("Tool.lnk", "Calculator", "Calculator", "Calculator")
            && !DockPinnedWindowPolicy.Matches(@"C:\Apps\calc.exe", "Calculator", "Calculator", "explorer"),
            "Pinned apps must claim only their matching running window identities.");
        // Regression from P2.8/P2.9: Nexus Files is a supervised
        // Nexus.Shell worker and its title changes with folder navigation.
        // A pinned built-in Files icon must claim its ONE representative HWND,
        // not create an additional running icon on every directory change.
        Check(DockPinnedWindowPolicy.Matches("nexus:files", "Files",
                "Files · Nexus", "Nexus.Shell")
            && DockPinnedWindowPolicy.Matches("nexus:files", "Files",
                "Documents · Nexus Files", "Nexus.Shell")
            && DockPinnedWindowPolicy.Matches("nexus:files", "Files",
                @"C:\\Data · Nexus Files", "Nexus.Shell")
            && !DockPinnedWindowPolicy.Matches("nexus:files", "Files",
                "Choose files · Nexus", "Nexus.Shell")
            && !DockPinnedWindowPolicy.Matches("nexus:files", "Files",
                "Documents · Nexus Files", "explorer")
            && !DockPinnedWindowPolicy.Matches("nexus:calculator", "Calculator",
                "Documents · Nexus Files", "Nexus.Shell"),
            "Files pin must follow only the real Nexus Files worker and folder titles, not pickers, Explorer or other Nexus utilities.");

        Check(DockPinnedWindowPolicy.Matches(@"C:\\Windows\\System32\\calc.exe",
                "Calculator", "Calculator", "CalculatorApp")
            && DockPinnedWindowPolicy.Matches(@"C:\\Windows\\System32\\calc.exe",
                "Calculator", "Calculator", "calc")
            && !DockPinnedWindowPolicy.Matches(@"C:\\Apps\\calc.exe",
                "Calculator", "Calculator", "CalculatorApp")
            && !DockPinnedWindowPolicy.Matches(@"C:\\Windows\\System32\\calc.exe",
                "Unrelated", "Calculator", "CalculatorApp")
            && !DockPinnedWindowPolicy.Matches(@"C:\\Windows\\System32\\calc.exe",
                "Calculator", "Calculator", "explorer"),
            "Only the real Windows Calculator launcher may pair a packaged CalculatorApp window; never fuse unrelated processes.");
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

        // Destroy/create may reuse both HWND and PID between native scans.
        var oldWindow = new RunningWindow(new IntPtr(800), "First document", "editor", 4200);
        var otherWindow = new RunningWindow(new IntPtr(801), "Other document", "editor", 4200);
        var lifecycle = new DockWindowSet();
        Check(lifecycle.Reconcile([oldWindow, otherWindow]), "Initial dock snapshot must reconcile.");
        Check(lifecycle.Reconcile([otherWindow, oldWindow], new HashSet<IntPtr> { oldWindow.Handle }),
            "A recycled HWND in the same process must force a new dock identity.");
        Check(lifecycle.Windows.SequenceEqual([otherWindow, oldWindow]),
            "Recycled HWND must append while unaffected buttons retain their order.");
        Check(!lifecycle.Reconcile([oldWindow, otherWindow]),
            "Pure z-order changes must not recreate buttons after invalidation.");
        var reopened = oldWindow with { Title = "Second document" };
        Check(lifecycle.Reconcile([otherWindow, reopened], new HashSet<IntPtr> { oldWindow.Handle }),
            "Rapid close/reopen with the same HWND and PID must replace old metadata.");
        Check(lifecycle.Windows.Count == 2 && lifecycle.Windows[^1] == reopened,
            "Recycled identities must never leave duplicate buttons.");
        Check(!lifecycle.Reconcile([otherWindow, reopened], new HashSet<IntPtr> { new IntPtr(999) }),
            "Destroying an HWND outside the dock must not force layout work.");
        Check(lifecycle.Reconcile([otherWindow]) && lifecycle.Windows.Count == 1,
            "Closed app entries must disappear on structural scans.");
        Check(lifecycle.Reconcile([reopened, otherWindow]) && lifecycle.Windows.Count == 2,
            "Reopened apps must return without ghost window entries.");

        // Foreground-only events should inspect just two native HWNDs.
        var before = new IntPtr(10);
        var after = new IntPtr(12);
        Check(DockStateRefreshPolicy.ShouldInspect(before, before, after)
            && DockStateRefreshPolicy.ShouldInspect(after, before, after),
            "Foreground switches must refresh both old and new active buttons.");
        Check(!DockStateRefreshPolicy.ShouldInspect(new IntPtr(11), before, after),
            "Unrelated dock icons must not trigger native state checks.");
        Check(!DockStateRefreshPolicy.ShouldInspect(IntPtr.Zero, before, after),
            "Invalid HWNDs must never be inspected.");
        Check(DockStateRefreshPolicy.ShouldInspect(after, after, after),
            "Repeated foreground observations must still settle the target app state.");
        Check(!DockStateRefreshPolicy.ShouldInspect(new IntPtr(44), IntPtr.Zero, IntPtr.Zero),
            "No foreground must not inspect unrelated dock windows.");

        // A partially failed XAML update can leave DockWindowSet reconciled
        // while one button was never attached. The next identical snapshot
        // must force recovery rather than skipping the visual reconciliation.
        Check(!DockVisualRecoveryPolicy.RequiresFullReconcile(
                true, false, false, false, 3, 3, 3),
            "A stable, complete dock must keep its existing low-cost refresh path.");
        Check(DockVisualRecoveryPolicy.RequiresFullReconcile(
                true, false, false, true, 3, 3, 3),
            "An interrupted refresh must always receive a second full attempt.");
        Check(DockVisualRecoveryPolicy.RequiresFullReconcile(
                true, false, false, false, 3, 2, 2),
            "A missing recorded item must trigger reconstruction after a partial failure.");
        Check(DockVisualRecoveryPolicy.RequiresFullReconcile(
                true, false, false, false, 3, 3, 2),
            "A registered button that never reached the WinUI child list must be reattached.");
        Check(DockVisualRecoveryPolicy.RequiresFullReconcile(
                false, false, false, false, 0, 0, 0),
            "First dock initialization must always reconcile.");
        Check(DockVisualRecoveryPolicy.RequiresFullReconcile(
                true, true, false, false, 3, 3, 3)
            && DockVisualRecoveryPolicy.RequiresFullReconcile(
                true, false, true, false, 3, 3, 3),
            "Pin edits and real window lifecycle changes must keep full reconciliation.");
        Check(!DockVisualRecoveryPolicy.RequiresVisualRebuild(false, 3, 3),
            "An ordinary consistent scan must not detach and reattach WinUI dock buttons.");
        Check(DockVisualRecoveryPolicy.RequiresVisualRebuild(true, 3, 3),
            "A failed refresh must restore child order even when button counts happen to match.");
        Check(DockVisualRecoveryPolicy.RequiresVisualRebuild(false, 3, 2),
            "Missing visual controls must be repaired after the existing item dictionary stabilizes.");
        Check(DockVisualRecoveryPolicy.RequiresVisualRebuild(true, 0, 0),
            "A failed empty-dock refresh must also reset potentially orphaned children.");

        // Stage 13: a crashed WinUI render can leave exactly as many
        // orphaned buttons as current windows. Compare identities, not only
        // counts, and reject duplicate or missing dock entries.
        var observed = new[] {
            new RunningWindow(new IntPtr(91), "Editor", "editor", 701),
            new RunningWindow(new IntPtr(92), "Browser", "browser", 702)
        };
        var observedKeys = observed.Select(DockWindowIdentity.Of).ToArray();
        Check(DockVisualRecoveryPolicy.HasConsistentIdentities(observed, observedKeys),
            "An exact dock identity set should not force expensive repair.");
        Check(DockVisualRecoveryPolicy.HasConsistentIdentities(
                observed.Reverse().ToArray(), observedKeys),
            "A pure EnumWindows order change must not mark stable dock buttons as stale.");
        Check(!DockVisualRecoveryPolicy.HasConsistentIdentities(observed,
                [observedKeys[0], new DockWindowIdentity(new IntPtr(100), 703)]),
            "Equal counts with a stale HWND must still trigger recovery.");
        Check(!DockVisualRecoveryPolicy.HasConsistentIdentities(observed,
                [observedKeys[0], new DockWindowIdentity(observed[1].Handle, 999)]),
            "An equal-count reused HWND with the wrong PID must trigger recovery.");
        Check(!DockVisualRecoveryPolicy.HasConsistentIdentities(observed,
                [observedKeys[0], observedKeys[0]]),
            "Duplicate dock item keys must never pass a reconciliation consistency check.");
        Check(!DockVisualRecoveryPolicy.HasConsistentIdentities(observed,
                [observedKeys[0]]),
            "Missing dock buttons must be restored on the next scan.");
        Check(!DockVisualRecoveryPolicy.HasConsistentIdentities(
                [observed[0], observed[0]], [observedKeys[0], observedKeys[1]]),
            "Duplicate expected window identities must fail instead of hiding malformed snapshots.");
        Check(DockVisualRecoveryPolicy.HasConsistentIdentities(
                [observed[0] with { Title = "Renamed Editor" }, observed[1]], observedKeys),
            "A title-only update must keep the button identity stable; metadata is reconciled separately.");
        Check(DockVisualRecoveryPolicy.HasConsistentIdentities([], Array.Empty<DockWindowIdentity>()),
            "A genuinely empty dock is a consistent baseline, not a permanent recovery loop.");

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
        Check(!repair.ShouldSample(nextWindow, true, 1080),
            "Ordinary pointer polling must still obey the foreground repair throttle.");
        Check(repair.ShouldSample(nextWindow, true, 1081, force: true),
            "A completed native move/resize must force prompt geometry repair.");
        Check(!repair.ShouldSample(nextWindow, true, 1082)
            && !repair.ShouldSample(nextWindow, true, 2080)
            && repair.ShouldSample(nextWindow, true, 2081),
            "A forced repair must restart the bounded one-second fallback window.");
        Check(!repair.ShouldSample(IntPtr.Zero, false, 2082, force: true),
            "A forced geometry repair with no foreground must not run native window reconciliation.");

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
