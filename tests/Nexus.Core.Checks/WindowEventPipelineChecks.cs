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

        // A foreground-only activation must never enumerate every open window.
        // Other, including unexpected, native event kinds remain conservative.
        Check(!WindowEventScanPolicy.RequiresEnumeration(0x0003),
            "Foreground activation should update the dock without a complete EnumWindows pass.");
        foreach (uint kind in new uint[] { 0x000B, 0x0016, 0x0017, 0x8000, 0x8001, 0x8002, 0x8003, 0x800C, 0x8017, 0x8018, 0xDEAD })
            Check(WindowEventScanPolicy.RequiresEnumeration(kind),
                "Structural and unknown events must retain a full task-list recovery scan.");

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
