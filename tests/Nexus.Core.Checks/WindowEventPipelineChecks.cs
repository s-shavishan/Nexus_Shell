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
