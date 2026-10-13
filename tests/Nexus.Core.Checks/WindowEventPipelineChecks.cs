using Nexus.Shell.Services;

internal static class WindowEventPipelineChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }

        var gate = new WindowEventDispatchGate();
        var callbacks = new Queue<Action>();
        bool Enqueue(Action action) { callbacks.Enqueue(action); return true; }
        var notifications = new List<bool>();
        void Deliver(bool foreground) => notifications.Add(foreground);

        Check(gate.Request(false, Enqueue, Deliver), "First native window event must queue a UI refresh.");
        Check(!gate.Request(true, Enqueue, Deliver), "A foreground event during a queued refresh must coalesce.");
        for (int i = 0; i < 1000; i++)
            Check(!gate.Request(i % 7 == 0, Enqueue, Deliver), "Native event bursts must not queue thousands of UI callbacks.");
        Check(callbacks.Count == 1, "Only one UI dispatch is permitted per in-flight event batch.");
        callbacks.Dequeue()();
        Check(notifications.SequenceEqual([true]), "Coalesced events must preserve foreground priority.");
        Check(gate.Request(false, Enqueue, Deliver), "Dispatch completion must allow another batch.");
        callbacks.Dequeue()();
        Check(notifications.SequenceEqual([true, false]), "Ordinary lifecycle batches must not manufacture a foreground event.");

        Check(!gate.Request(true, _ => false, Deliver), "A rejected WinUI dispatch must not claim to queue work.");
        Check(gate.Request(false, Enqueue, Deliver), "Dispatch refusal must not permanently block the next request.");
        callbacks.Dequeue()();
        Check(notifications[^1], "A rejected dispatch must retain its foreground observation for retry.");
        bool threw = false;
        try { gate.Request(true, _ => throw new InvalidOperationException("dispatcher shutting down"), Deliver); }
        catch (InvalidOperationException) { threw = true; }
        Check(threw && gate.Request(false, Enqueue, Deliver),
            "Throwing dispatcher must release its queue gate and preserve pending foreground state.");
        callbacks.Dequeue()();
        Check(notifications[^1], "Foreground priority must survive a throwing enqueue.");

        int nested = 0;
        Check(gate.Request(false, Enqueue, foreground =>
        {
            nested++;
            Check(gate.Request(true, Enqueue, observed => { if (observed) nested++; }),
                "Reentrant native event callback must schedule a fresh batch.");
        }), "Normal initial request should be accepted.");
        callbacks.Dequeue()(); callbacks.Dequeue()();
        Check(nested == 2 && callbacks.Count == 0, "Reentrant delivery must complete both batches exactly once.");

        var concurrent = new WindowEventDispatchGate();
        var accepted = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        int dispatches = 0, foregroundBatches = 0;
        Parallel.For(0, 1500, i =>
        {
            concurrent.Request(i % 101 == 0, callback => { accepted.Enqueue(callback); return true; },
                foreground => { Interlocked.Increment(ref dispatches); if (foreground) Interlocked.Increment(ref foregroundBatches); });
        });
        Check(accepted.Count == 1, "Concurrent native events must admit exactly one dispatcher callback.");
        Check(accepted.TryDequeue(out var queued) && queued is not null, "The coalesced callback must be present.");
        queued!();
        Check(dispatches == 1 && foregroundBatches == 1, "A concurrent burst must retain its foreground signal.");

        var safeEvents = new WindowEventDispatchGate();
        var safeQueue = new Queue<Action>();
        var nativeErrors = new List<string>();
        bool SafeEnqueue(Action callback) { safeQueue.Enqueue(callback); return true; }
        int deliveredSafe = 0;
        Check(safeEvents.Request(true, SafeEnqueue,
            _ => throw new InvalidOperationException("WinUI view closed"),
            error => nativeErrors.Add(error.Message)),
            "A native foreground notification may queue before WinUI closes.");
        safeQueue.Dequeue()();
        Check(nativeErrors.SequenceEqual(["WinUI view closed"]),
            "A delayed WinEvent UI failure must be contained and diagnosed.");
        Check(safeEvents.Request(false, SafeEnqueue,
            sawForeground => { if (!sawForeground) deliveredSafe++; },
            error => nativeErrors.Add(error.Message)),
            "The event gate must recover after its consumer throws.");
        safeQueue.Dequeue()();
        Check(deliveredSafe == 1,
            "A later ordinary lifecycle event must be delivered after the failed UI callback.");
        Check(!safeEvents.Request(true,
            _ => throw new InvalidOperationException("queue unavailable"),
            _ => deliveredSafe++,
            error => nativeErrors.Add(error.Message)),
            "A throwing WinUI queue must fail admission without crashing the native observer.");
        Check(nativeErrors[^1] == "queue unavailable",
            "Rejected foreground dispatch must remain observable.");
        Check(safeEvents.Request(false, SafeEnqueue,
            foreground => { if (foreground) deliveredSafe++; },
            error => nativeErrors.Add(error.Message)),
            "Foreground priority must survive a contained enqueue failure.");
        safeQueue.Dequeue()();
        Check(deliveredSafe == 2, "Retried foreground event must preserve its high-priority state.");

        Check(safeEvents.Request(false, SafeEnqueue,
            _ => throw new InvalidOperationException("observer failed"),
            _ => throw new IOException("log writer failed")),
            "Diagnostics failures must not turn handled UI callbacks into native hook crashes.");
        safeQueue.Dequeue()();
        for (int i = 0; i < 500; i++)
        {
            Check(safeEvents.Request(false, SafeEnqueue,
                _ => throw new InvalidOperationException("stale window"),
                error => nativeErrors.Add(error.Message)),
                "Repeated stale native callbacks must not strand the event dispatch gate.");
            safeQueue.Dequeue()();
        }
        Check(safeQueue.Count == 0 && nativeErrors.Count == 502,
            "Contained callbacks must release the native-event queue for every subsequent generation.");

        // A rejecting dispatcher may have retained the callback already. If it
        // runs late, it cannot consume foreground priority or release its retry.
        var refused = new WindowEventDispatchGate();
        Action? refusedCallback = null;
        var validCallbacks = new Queue<Action>();
        var refusedObservations = new List<bool>();
        Check(!refused.Request(true,
            callback => { refusedCallback = callback; return false; },
            observed => refusedObservations.Add(observed)),
            "Rejected admission must not claim to have scheduled a foreground event.");
        Check(refused.Request(false,
            callback => { validCallbacks.Enqueue(callback); return true; },
            observed => refusedObservations.Add(observed)),
            "A valid retry must be admitted after refusal.");
        refusedCallback!();
        Check(!refused.Request(false,
            callback => { validCallbacks.Enqueue(callback); return true; },
            observed => refusedObservations.Add(observed)),
            "A rejected callback arriving late must not unlock the valid retry.");
        validCallbacks.Dequeue()();
        Check(refusedObservations.SequenceEqual([true]) && validCallbacks.Count == 0,
            "A stale callback must not deliver twice or steal the retained foreground flag.");

        // Some queue wrappers invoke inline, then report failure to their caller.
        // The old request's late rejection must not clear the newer generation.
        var overlap = new WindowEventDispatchGate();
        var overlapCallbacks = new Queue<Action>();
        var overlapObservations = new List<bool>();
        Check(!overlap.Request(false, callback =>
        {
            callback();
            Check(overlap.Request(true,
                next => { overlapCallbacks.Enqueue(next); return true; },
                observed => overlapObservations.Add(observed)),
                "A finished inline batch must admit its successor.");
            return false;
        }, observed => overlapObservations.Add(observed)),
            "An inline callback followed by refusal still reports refused admission.");
        Check(!overlap.Request(false,
            next => { overlapCallbacks.Enqueue(next); return true; },
            observed => overlapObservations.Add(observed)),
            "A late rejection from an old ticket cannot clear an in-flight successor.");
        overlapCallbacks.Dequeue()();
        Check(overlapObservations.SequenceEqual([false, true]) && overlapCallbacks.Count == 0,
            "The successor must retain foreground priority after inline completion and late refusal.");

        // Neither foreground switching nor completing an interactive
        // move/resize changes task-list membership by itself. Both still
        // deliver an immediate UI callback for native geometry/visibility.
        Check(!WindowEventScanPolicy.RequiresEnumeration(0x0003),
            "Foreground activation should update the dock without a complete EnumWindows pass.");
        Check(!WindowEventScanPolicy.RequiresEnumeration(0x000B),
            "Move/resize completion should refresh geometry without enumerating every open window.");
        foreach (uint kind in new uint[] { 0x0016, 0x0017, 0x8000, 0x8001, 0x8002, 0x8003, 0x800C, 0x8017, 0x8018, 0xDEAD })
            Check(WindowEventScanPolicy.RequiresEnumeration(kind),
                "Lifecycle, title, minimize and unknown events must retain full task-list recovery.");

        var geometryBatch = new WindowEventDispatchGate();
        var geometryQueue = new Queue<Action>();
        var geometryResults = new List<(bool Foreground, bool Enumerate)>();
        Check(geometryBatch.Request(false, WindowEventScanPolicy.RequiresEnumeration(0x000B),
                action => { geometryQueue.Enqueue(action); return true; },
                (foreground, enumerate) => geometryResults.Add((foreground, enumerate))),
            "A completed window move must still enqueue an immediate UI refresh.");
        for (int i = 0; i < 700; i++)
            Check(!geometryBatch.Request(false, WindowEventScanPolicy.RequiresEnumeration(0x000B),
                action => { geometryQueue.Enqueue(action); return true; },
                (foreground, enumerate) => geometryResults.Add((foreground, enumerate))),
                "Move/resize completion bursts must be coalesced into one delivery.");
        Check(geometryQueue.Count == 1,
            "Hundreds of redundant move/size notifications must not flood the dispatcher.");
        geometryQueue.Dequeue()();
        Check(geometryResults.SequenceEqual([(false, false)]),
            "A pure geometry batch must not schedule a full window enumeration.");
        Check(geometryBatch.Request(false, false,
                action => { geometryQueue.Enqueue(action); return true; },
                (foreground, enumerate) => geometryResults.Add((foreground, enumerate))),
            "A subsequent geometry callback must be admitted.");
        Check(!geometryBatch.Request(false, WindowEventScanPolicy.RequiresEnumeration(0x8001),
                action => { geometryQueue.Enqueue(action); return true; },
                (foreground, enumerate) => geometryResults.Add((foreground, enumerate))),
            "A destroy event must coalesce with the pending geometry update.");
        geometryQueue.Dequeue()();
        Check(geometryResults[^1] == (false, true),
            "Destroy events must upgrade an outstanding move/size batch to structural enumeration.");
        Check(geometryBatch.Request(false, false,
                action => { geometryQueue.Enqueue(action); return true; },
                (foreground, enumerate) => geometryResults.Add((foreground, enumerate)))
            && !geometryBatch.Request(true, false,
                action => { geometryQueue.Enqueue(action); return true; },
                (foreground, enumerate) => geometryResults.Add((foreground, enumerate))),
            "Concurrent move/size and foreground notifications must coalesce.");
        geometryQueue.Dequeue()();
        Check(geometryResults[^1] == (true, false),
            "Foreground observations must survive a geometry-only batch without forcing enumeration.");


        // Real move/resize completion must remain observable independently
        // of foreground and structural notifications inside the same gate.
        var mixedGate = new WindowEventDispatchGate();
        var mixedQueue = new Queue<Action>();
        var mixed = new List<(bool Foreground, bool Enumerate, bool Geometry)>();
        bool MixedEnqueue(Action action) { mixedQueue.Enqueue(action); return true; }
        void MixedDeliver(bool foreground, bool enumerate, bool geometry)
            => mixed.Add((foreground, enumerate, geometry));
        Check(mixedGate.Request(false, false, true, MixedEnqueue, MixedDeliver),
            "Move/size completion must schedule one geometry refresh.");
        for (int i = 0; i < 1000; i++)
            Check(!mixedGate.Request(false, false, true, MixedEnqueue, MixedDeliver),
                "A 1000-event geometry burst must retain one pending UI callback.");
        Check(!mixedGate.Request(true, false, false, MixedEnqueue, MixedDeliver)
            && !mixedGate.Request(false, true, false, MixedEnqueue, MixedDeliver),
            "Foreground and structural events must join the pending geometry batch.");
        Check(mixedQueue.Count == 1, "Three independent native flags must share one delivery.");
        mixedQueue.Dequeue()();
        Check(mixed.SequenceEqual([(true, true, true)]),
            "Coalesced geometry, selection and lifecycle invalidation must all survive.");
        Check(mixedGate.Request(false, false, false, MixedEnqueue, MixedDeliver),
            "After delivery an unchanged notification must be admitted normally.");
        mixedQueue.Dequeue()();
        Check(mixed[^1] == (false, false, false),
            "Old geometry flags must not leak into later batches.");
        Check(!mixedGate.Request(false, false, true, _ => false, MixedDeliver),
            "A refused geometry enqueue must report failure.");
        Check(mixedGate.Request(false, false, false, MixedEnqueue, MixedDeliver),
            "A rejected dispatcher admission must allow a new ticket.");
        mixedQueue.Dequeue()();
        Check(mixed[^1] == (false, false, true),
            "Geometry invalidation must survive a rejected WinUI enqueue.");
        var inlineGate = new WindowEventDispatchGate();
        var inline = new List<(bool Foreground, bool Enumerate, bool Geometry)>();
        var inlineAccepted = inlineGate.Request(false, false, true,
            callback => { callback(); return true; },
            (f, e, g) => inline.Add((f, e, g)));
        Check(inlineAccepted && inline.SequenceEqual([(false, false, true)]),
            "Inline callbacks must atomically consume geometry flags and admit later batches.");

        var nativeBatch = new WindowEventDispatchGate();
        var nativeBatchQueue = new Queue<Action>();
        var nativeBatchResults = new List<(bool Foreground, bool Enumerate)>();
        bool AddNative(Action callback) { nativeBatchQueue.Enqueue(callback); return true; }
        void ReceiveNative(bool foreground, bool enumerate) => nativeBatchResults.Add((foreground, enumerate));
        Check(nativeBatch.Request(true, false, AddNative, ReceiveNative),
            "A foreground-only batch should be immediately admitted.");
        for (int i = 0; i < 800; i++)
            Check(!nativeBatch.Request(true, false, AddNative, ReceiveNative),
                "Repeated task switches should not schedule duplicate UI work.");
        Check(nativeBatchQueue.Count == 1,
            "Foreground-only storms must retain exactly one queued callback.");
        nativeBatchQueue.Dequeue()();
        Check(nativeBatchResults.SequenceEqual([(true, false)]),
            "A foreground-only storm must not request the structural enumeration.");

        Check(nativeBatch.Request(true, false, AddNative, ReceiveNative),
            "A subsequent activation must schedule a fresh selection update.");
        Check(!nativeBatch.Request(false, true, AddNative, ReceiveNative),
            "A structural event should coalesce with an active foreground batch.");
        nativeBatchQueue.Dequeue()();
        Check(nativeBatchResults[^1] == (true, true),
            "Coalescing must preserve both foreground priority and structural invalidation.");
        Check(nativeBatch.Request(false, true, AddNative, ReceiveNative),
            "A structural-only batch must schedule its own reconciliation.");
        nativeBatchQueue.Dequeue()();
        Check(nativeBatchResults[^1] == (false, true),
            "A structural-only native event must refresh the task list.");

        var nativeRefusal = new WindowEventDispatchGate();
        Action? oldNative = null;
        var nativeRetries = new Queue<Action>();
        var nativeRetriesSeen = new List<(bool Foreground, bool Enumerate)>();
        void NativeRetry(bool foreground, bool enumerate) => nativeRetriesSeen.Add((foreground, enumerate));
        Check(!nativeRefusal.Request(true, true,
            callback => { oldNative = callback; return false; }, NativeRetry),
            "A refused structural foreground batch must remain pending for a retry.");
        Check(nativeRefusal.Request(false, false,
            callback => { nativeRetries.Enqueue(callback); return true; }, NativeRetry),
            "A later foreground-only request must rescue both invalidation flags.");
        oldNative!();
        nativeRetries.Dequeue()();
        Check(nativeRetriesSeen.SequenceEqual([(true, true)]),
            "Stale rejected events must not steal pending structural invalidation.");

        var knownForeground = new[] {
            new Nexus.Shell.Models.RunningWindow(new IntPtr(101), "Editor", "editor", 1001),
            new Nexus.Shell.Models.RunningWindow(new IntPtr(102), "Browser", "browser", 1002)
        };
        Check(!DockForegroundDiscoveryPolicy.IsUnknownForeground(true, IntPtr.Zero, knownForeground),
            "Null foreground must not trigger an unknown-app scan.");
        Check(!DockForegroundDiscoveryPolicy.IsUnknownForeground(false, new IntPtr(103), knownForeground),
            "Non-foreground events already have their own enumeration decision.");
        Check(!DockForegroundDiscoveryPolicy.IsUnknownForeground(true, new IntPtr(101), knownForeground),
            "Alt+Tab between known apps must remain enumeration-free.");
        Check(DockForegroundDiscoveryPolicy.IsUnknownForeground(true, new IntPtr(103), knownForeground),
            "Unknown focused apps must be eligible for native discovery checks.");
        Check(DockForegroundDiscoveryPolicy.IsUnknownForeground(true, new IntPtr(101),
            Array.Empty<Nexus.Shell.Models.RunningWindow>()),
            "Missed create/show events must be recoverable before the periodic fallback.");

        var previousList = new[] {
            new Nexus.Shell.Models.RunningWindow(new IntPtr(10), "Editor", "editor", 100),
            new Nexus.Shell.Models.RunningWindow(new IntPtr(11), "Browser", "browser", 101)
        };
        Check(WindowListSnapshotPolicy.Equivalent(previousList, previousList),
            "Identical references must preserve one snapshot identity.");
        Check(WindowListSnapshotPolicy.Equivalent(previousList, previousList.ToArray()),
            "Equivalent enumeration content must not invalidate the foreground lookup cache.");
        Check(!WindowListSnapshotPolicy.Equivalent(previousList, previousList.Reverse().ToArray()),
            "Z-order changes should still update the window board order.");
        Check(!WindowListSnapshotPolicy.Equivalent(previousList,
            [previousList[0] with { Title = "Editor — renamed" }, previousList[1]]),
            "Title updates must refresh the dock metadata without changing HWND identity.");
        Check(!WindowListSnapshotPolicy.Equivalent(previousList,
            [previousList[0] with { ProcessId = 999 }, previousList[1]]),
            "PID reuse must never be mistaken for an identical snapshot.");
        Check(!WindowListSnapshotPolicy.Equivalent(previousList,
            [previousList[0]]),
            "Closed windows must invalidate the previous snapshot.");

        // Stage 7: lifecycle tombstones are bound to a scan BEFORE its worker
        // begins enumeration. Notifications arriving mid-scan remain for the
        // successor, and a failed scan must restore its original events.
        var journal = new DockLifecycleJournal();
        Check(!journal.HasPending, "An empty lifecycle journal must not request a scan.");
        journal.RecordDestroy(IntPtr.Zero);
        Check(!journal.HasPending, "Invalid handles must never enter the journal.");
        journal.RecordDestroy(new IntPtr(41));
        journal.RecordDestroy(new IntPtr(41));
        var beforeScan = journal.BeginScan();
        Check(beforeScan.Destroyed.Count == 1 && beforeScan.Destroyed.Contains(new IntPtr(41))
            && !beforeScan.ResetAll && !journal.HasPending,
            "A scan must own one snapshot of deduplicated pre-enumeration destroys.");
        journal.RecordDestroy(new IntPtr(42));
        Check(journal.HasPending,
            "A destroy during enumeration must be retained for a later scan.");
        Check(journal.CompleteScan(beforeScan, applied: true),
            "Successful scan must release its lifecycle batch.");
        Check(!journal.CompleteScan(beforeScan, applied: false),
            "A stale completion must not restore or release a newer batch.");
        var afterScan = journal.BeginScan();
        Check(afterScan.Destroyed.SetEquals(new[] { new IntPtr(42) }),
            "Successful reconciliation must consume only the old batch.");
        journal.RecordDestroy(new IntPtr(43));
        Check(journal.CompleteScan(afterScan, applied: false),
            "Failed reconciliation must release its current batch for retry.");
        var retry = journal.BeginScan();
        Check(retry.Destroyed.SetEquals(new[] { new IntPtr(42), new IntPtr(43) }),
            "Failed scan destroys and newer native events must both survive retry.");
        Check(journal.CompleteScan(retry, applied: true) && !journal.HasPending,
            "Successful retry must drain all known window destructions.");

        journal.RecordDestroy(new IntPtr(44));
        var inFlight = journal.BeginScan();
        bool overlappingRejected = false;
        try { journal.BeginScan(); }
        catch (InvalidOperationException) { overlappingRejected = true; }
        Check(overlappingRejected, "A second scan may not take ownership of an active batch.");
        journal.RecordDestroy(new IntPtr(45));
        Check(journal.CompleteScan(inFlight, applied: false),
            "A failed active batch must remain recoverable after a refused overlap.");
        var recovered = journal.BeginScan();
        Check(recovered.Destroyed.SetEquals(new[] { new IntPtr(44), new IntPtr(45) }),
            "Overlapping scan attempts must never discard concurrent destroy events.");
        Check(journal.CompleteScan(recovered, applied: true), "Recovery must finish normally.");

        var overflow = new DockLifecycleJournal();
        for (int n = 1; n <= DockLifecycleJournal.MaxTrackedHandles + 1; n++)
            overflow.RecordDestroy(new IntPtr(n));
        var reset = overflow.BeginScan();
        Check(reset.ResetAll && reset.Destroyed.Count == 0,
            "Overflow must request a safe full dock identity rebuild, not drop tombstones silently.");
        overflow.RecordDestroy(new IntPtr(90000));
        Check(overflow.CompleteScan(reset, applied: false),
            "Failed full rebuilds must remain eligible for retry.");
        var resetRetry = overflow.BeginScan();
        Check(resetRetry.ResetAll && resetRetry.Destroyed.Count == 0,
            "Full-rebuild demand must survive scan failure and newer events.");
        Check(overflow.CompleteScan(resetRetry, applied: true) && !overflow.HasPending,
            "Successful full rebuild must release overflow mode.");

        var parallelJournal = new DockLifecycleJournal();
        Parallel.For(1, 1801, n => parallelJournal.RecordDestroy(new IntPtr(n)));
        var parallelBatch = parallelJournal.BeginScan();
        Check(!parallelBatch.ResetAll && parallelBatch.Destroyed.Count == 1800,
            "Parallel native hooks must not lose or duplicate lifecycle tombstones.");
        Check(parallelJournal.CompleteScan(parallelBatch, applied: true) && !parallelJournal.HasPending,
            "A parallel event burst must settle after a successful scan.");

        // Stage 12: the five-second heartbeat is redundant when a scan is
        // already running, but real native events and explicit user commands
        // must still queue a successor so their structural changes survive.
        Check(WindowScanAdmissionPolicy.Decide(false, false) == WindowScanAdmission.Start,
            "Explicit dock refresh with no scan running must start immediately.");
        Check(WindowScanAdmissionPolicy.Decide(false, true) == WindowScanAdmission.Start,
            "Periodic recovery must enumerate when the native worker is idle.");
        Check(WindowScanAdmissionPolicy.Decide(true, true)
                == WindowScanAdmission.CoveredByActiveScan,
            "A recovery heartbeat overlapping an active enumeration must not request a duplicate scan.");
        Check(WindowScanAdmissionPolicy.Decide(true, false)
                == WindowScanAdmission.QueueFollowUp,
            "A real window change or user action during enumeration must request one later scan.");
        var admissions = Enumerable.Range(0, 1000)
            .Select(_ => WindowScanAdmissionPolicy.Decide(true, true)).ToArray();
        Check(admissions.All(x => x == WindowScanAdmission.CoveredByActiveScan),
            "Repeated recovery heartbeats must never queue redundant follow-up native scans.");
        Check(WindowScanAdmissionPolicy.Decide(true, false) == WindowScanAdmission.QueueFollowUp
                && WindowScanAdmissionPolicy.Decide(false, false) == WindowScanAdmission.Start,
            "A pending structural notification must not be suppressed once the active scan completes.");

        // P2.4 shutdown/reentrancy audit: failed scan owns the retry
        // schedule even if a coalesced 50 ms callback is already armed.
        Check(WindowScanTimerPolicy.Decide(false, false, false, false)
                == WindowScanTimerAction.Start,
            "A native scan failure must retry even without a newer window event.");
        Check(WindowScanTimerPolicy.Decide(false, false, true, false)
                == WindowScanTimerAction.Start,
            "Failed scan with pending lifecycle work must schedule recovery.");
        Check(WindowScanTimerPolicy.Decide(false, false, false, true)
                == WindowScanTimerAction.Restart
            && WindowScanTimerPolicy.Decide(false, false, true, true)
                == WindowScanTimerAction.Restart,
            "An armed fast timer must be replaced by bounded failure backoff.");
        Check(WindowScanTimerPolicy.Decide(false, true, true, false)
                == WindowScanTimerAction.Start,
            "A successful scan must schedule events that arrived during enumeration.");
        Check(WindowScanTimerPolicy.Decide(false, true, true, true)
                == WindowScanTimerAction.None,
            "A successful scan must not restart an already-armed coalesced event timer.");
        Check(WindowScanTimerPolicy.Decide(false, true, false, false)
                == WindowScanTimerAction.None,
            "A successful idle scan must not schedule redundant enumeration.");
        foreach (bool applied in new[] { false, true })
            foreach (bool pending in new[] { false, true })
                foreach (bool armed in new[] { false, true })
                    Check(WindowScanTimerPolicy.Decide(true, applied, pending, armed)
                            == WindowScanTimerAction.None,
                        "Shutdown must prevent all new and restarted scan timers.");

        // P2.4 integration review: a fault without lifecycle tombstones
        // must still retry; failures back off, and success resets the cadence.
        var retryPolicy = new WindowScanRetryBackoff();
        Check(retryPolicy.ConsecutiveFailures == 0, "A new native scan should have no failures.");
        Check(retryPolicy.Complete(false) == 500
            && retryPolicy.Complete(false) == 1000
            && retryPolicy.Complete(false) == 2000
            && retryPolicy.Complete(false) == 5000,
            "Successive enumeration failures must use bounded exponential retry intervals.");
        for (int i = 0; i < 25; i++)
            Check(retryPolicy.Complete(false) == WindowScanRetryBackoff.MaxDelayMilliseconds,
                "Persistent faults must never create a 50 ms native enumeration spin loop.");
        Check(retryPolicy.Complete(true) == WindowScanRetryBackoff.NormalMilliseconds
            && retryPolicy.ConsecutiveFailures == 0,
            "A successful recovery must immediately return future window events to normal cadence.");
        Check(retryPolicy.Complete(false) == 500
            && retryPolicy.Complete(true) == WindowScanRetryBackoff.NormalMilliseconds,
            "The next transient fault must not inherit an old recovery backoff.");

        // Destroyed tool/pop-up HWNDs that were never part of the dock must
        // not force an expensive XAML button reconcile of unchanged apps.
        var taskList = new[] {
            new Nexus.Shell.Models.RunningWindow(new IntPtr(808), "IDE", "ide", 440),
            new Nexus.Shell.Models.RunningWindow(new IntPtr(809), "Browser", "browser", 441)
        };
        var destroyedPopup = new HashSet<IntPtr> { new IntPtr(999) };
        Check(!WindowListSnapshotPolicy.RequiresDockRefresh(
                taskList, taskList.ToArray(), destroyedPopup, resetAll: false),
            "An unrelated native popup destruction must leave the dock layout unchanged.");
        Check(WindowListSnapshotPolicy.RequiresDockRefresh(
                taskList, taskList.ToArray(), new HashSet<IntPtr> { taskList[0].Handle }, resetAll: false),
            "Destroy/recreate of the same dock HWND/PID must still recreate its button.");
        Check(WindowListSnapshotPolicy.RequiresDockRefresh(
                taskList, taskList.ToArray(), destroyedPopup, resetAll: true),
            "A lifecycle-overflow reset must always rebuild all dock identities.");
        Check(WindowListSnapshotPolicy.RequiresDockRefresh(
                taskList, [taskList[0] with { Title = "IDE: renamed" }, taskList[1]],
                new HashSet<IntPtr>(), resetAll: false),
            "Native title/metadata changes must trigger dock reconciliation.");
        Check(WindowListSnapshotPolicy.RequiresDockRefresh(
                taskList, [taskList[0]], new HashSet<IntPtr>(), resetAll: false),
            "Closed windows must trigger dock button removal.");
        Check(!WindowListSnapshotPolicy.RequiresDockRefresh(
                taskList, taskList.ToArray(), new HashSet<IntPtr>(), resetAll: false),
            "Ordinary identical recovery scans must preserve retained dock layout.");

        // Native EnumWindows follows z-order; dock entries intentionally do
        // not. Pure Alt+Tab reordering should never rebuild XAML controls.
        Check(WindowListSnapshotPolicy.EquivalentDockEntries(taskList, taskList.Reverse().ToArray()),
            "Dock identity/content equivalence must ignore native z-order.");
        Check(!WindowListSnapshotPolicy.Equivalent(taskList, taskList.Reverse().ToArray()),
            "Window board native snapshot equivalence must remain z-order-sensitive.");
        Check(!WindowListSnapshotPolicy.RequiresDockRefresh(taskList,
                taskList.Reverse().ToArray(), new HashSet<IntPtr>(), resetAll: false),
            "An Alt+Tab z-order change alone must not trigger dock reconciliation.");
        var duplicateDockIdentities = new[] { taskList[0], taskList[0] };
        Check(!WindowListSnapshotPolicy.EquivalentDockEntries(
                duplicateDockIdentities, duplicateDockIdentities),
            "Even reference-identical lists with duplicate HWNDs must not bypass identity checks.");
        Check(!WindowListSnapshotPolicy.EquivalentDockEntries(taskList,
                [taskList[0], taskList[0]]),
            "Duplicate window identities must never be treated as an equivalent dock snapshot.");
        Check(!WindowListSnapshotPolicy.EquivalentDockEntries(taskList,
                [taskList[0] with { ProcessId = 9999 }, taskList[1]]),
            "A reused native HWND with a new PID must invalidate the dock snapshot.");
        Check(!WindowListSnapshotPolicy.EquivalentDockEntries(taskList,
                [taskList[0] with { ProcessName = "another-process" }, taskList[1]]),
            "Process metadata changes must invalidate a retained dock entry.");
        Check(WindowListSnapshotPolicy.RequiresDockRefresh(taskList,
                taskList.Reverse().ToArray(), new HashSet<IntPtr> { taskList[0].Handle }, resetAll: false),
            "A true destroyed HWND must still invalidate a dock entry regardless of z-order.");
        var largeDock = Enumerable.Range(1, 180).Select(n =>
            new Nexus.Shell.Models.RunningWindow(new IntPtr(n), "Tool " + n, "tool", 4000 + n)).ToArray();
        Check(WindowListSnapshotPolicy.EquivalentDockEntries(largeDock, largeDock.Reverse().ToArray()),
            "A large reordered native window set must retain equivalent dock lifecycle identities.");

        int lookupCount = 0;
        (int Session, string Name)? Resolve(int pid)
        {
            lookupCount++;
            return pid switch { 101 => (7, "browser"), 102 => (8, "other-session"), 103 => null, _ => (7, "other") };
        }
        var snapshot = new WindowProcessSnapshot(7, Resolve);
        for (int i = 0; i < 600; i++)
            Check(snapshot.TryGetName(101, out var name) && name == "browser",
                "Multiple HWNDs in one process must share its resolved metadata.");
        Check(lookupCount == 1, "A running-window scan must inspect a multi-window process only once.");
        Check(!snapshot.TryGetName(102, out _) && !snapshot.TryGetName(102, out _),
            "Foreign-session processes must stay excluded and be negatively cached.");
        Check(!snapshot.TryGetName(103, out _) && !snapshot.TryGetName(103, out _),
            "Unavailable process metadata must not be retried once per HWND.");
        Check(!snapshot.TryGetName(0, out _) && !snapshot.TryGetName(-2, out _),
            "Invalid process IDs must not be resolved.");
        Check(lookupCount == 3, "Only one lookup per distinct valid PID is allowed within a scan.");
        var nextScan = new WindowProcessSnapshot(7, pid => pid == 101 ? (7, "browser-restarted") : null);
        Check(nextScan.TryGetName(101, out var restarted) && restarted == "browser-restarted",
            "A new snapshot must resolve PIDs again, avoiding stale PID-reuse metadata.");

        Console.WriteLine($"PASS: {checks} native event dispatch/process-cache checks; concurrent 1500-event burst, foreground retention, rejection/exception/reentrancy, multi-HWND reuse and process/session isolation.");
    }
}
