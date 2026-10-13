using System.Collections.Concurrent;
using Nexus.Shell.Services;

internal static class GlassStateDispatchChecks
{
    internal static void Run()
    {
        int assertions = 0;
        void Check(bool condition, string failure)
        {
            assertions++;
            if (!condition) throw new Exception(failure);
        }

        var queue = new Queue<Action>();
        bool Enqueue(Action work) { queue.Enqueue(work); return true; }
        var errors = new List<string>();
        void Report(Exception error) => errors.Add(error.Message);
        var gate = new GlassStateDispatchGate();
        int paint = 0;
        int latest = 0;
        int paintedState = -1;
        bool live = true;

        Check(gate.Request(Enqueue, () => live, () => { paint++; paintedState = latest; }, Report),
            "A live native backdrop must queue its first state repaint.");
        for (int i = 1; i <= 1000; i++)
        {
            latest = i;
            Check(!gate.Request(Enqueue, () => live, () => { paint++; paintedState = latest; }, Report),
                "Repeated native state changes must not flood the WinUI dispatcher.");
        }
        Check(queue.Count == 1 && paint == 0,
            "A thousand rapid native state notifications must retain exactly one pending callback.");
        queue.Dequeue()();
        Check(paint == 1 && paintedState == 1000,
            "The single queued callback must repaint the latest controller state, not the first notification.");

        Check(gate.Request(Enqueue, () => live, () => { paint++; paintedState = latest; }, Report),
            "After dispatch completes, later native state changes must be accepted.");
        live = false; queue.Dequeue()();
        Check(paint == 1, "A detached native controller must not repaint a new frame.");

        // A new backdrop has its own gate, so a stale callback cannot suppress
        // an immediate update from the replacement controller.
        var replacement = new GlassStateDispatchGate();
        Check(gate.Request(Enqueue, () => live, () => paint++, Report),
            "The old gate may still have queued work during controller replacement.");
        Check(replacement.Request(Enqueue, () => true, () => paint++, Report),
            "The replacement controller must be able to schedule independently.");
        queue.Dequeue()(); queue.Dequeue()();
        Check(paint == 2, "Only the new native controller may repaint an attached surface.");

        Check(!gate.Request(_ => false, () => true, () => paint++, Report),
            "Rejected dispatcher admission must not enqueue a state update.");
        live = true;
        Check(gate.Request(Enqueue, () => live, () => paint++, Report),
            "A rejected dispatch must leave the gate reusable.");
        queue.Dequeue()();
        Check(paint == 3, "Recovery after enqueue rejection must perform a repaint.");

        Check(!gate.Request(_ => throw new InvalidOperationException("dispatcher closed"),
                () => true, () => paint++, Report),
            "A throwing WinUI queue must be contained.");
        Check(errors[^1] == "dispatcher closed", "Dispatcher closure must remain diagnosable.");
        Check(gate.Request(Enqueue, () => true,
                () => throw new InvalidOperationException("frame unloaded"), Report),
            "Callbacks can become invalid after they are queued.");
        queue.Dequeue()();
        Check(errors[^1] == "frame unloaded", "A throwing UI mutation must be contained and logged.");
        Check(gate.Request(Enqueue, () => true, () => paint++, Report),
            "A failed deferred paint must not permanently block glass updates.");
        queue.Dequeue()();
        Check(paint == 4, "An error recovery must deliver the next valid state.");

        int nested = 0;
        Check(gate.Request(Enqueue, () => true, () =>
        {
            nested++;
            Check(gate.Request(Enqueue, () => true, () => nested++, Report),
                "Reentrant native state changes must schedule the next generation.");
        }, Report), "First reentrant native state update must enqueue.");
        queue.Dequeue()();
        queue.Dequeue()();
        Check(nested == 2 && queue.Count == 0,
            "Reentrant acrylic events must not lose updates or leave excess callbacks.");

        Check(gate.Request(Enqueue, () => true,
                () => throw new InvalidOperationException("surface gone"),
                _ => throw new IOException("logger closed")),
            "Failing diagnostics must not reject the native callback itself.");
        queue.Dequeue()();
        Check(gate.Request(Enqueue, () => true, () => paint++, Report),
            "A broken diagnostic sink must not strand the native event gate.");
        queue.Dequeue()();
        Check(paint == 5, "The paint path must recover despite a failing logger.");

        // Actual cross-thread contention: all native notifications race
        // against one gate while the UI queue is intentionally not drained.
        var concurrentGate = new GlassStateDispatchGate();
        var concurrentQueue = new ConcurrentQueue<Action>();
        int admissions = 0, concurrentPaints = 0;
        Parallel.For(0, 1500, _ =>
        {
            if (concurrentGate.Request(work => { concurrentQueue.Enqueue(work); return true; },
                    () => true, () => Interlocked.Increment(ref concurrentPaints), Report))
                Interlocked.Increment(ref admissions);
        });
        Check(admissions == 1 && concurrentQueue.Count == 1,
            "Concurrent native callbacks must admit exactly one pending UI frame.");
        Check(concurrentQueue.TryDequeue(out var concurrentWork), "Admitted frame update was lost.");
        concurrentWork!();
        Check(concurrentPaints == 1
            && concurrentGate.Request(work => { concurrentQueue.Enqueue(work); return true; },
                () => true, () => Interlocked.Increment(ref concurrentPaints), Report),
            "Draining a contended native event gate must permit a subsequent repaint.");
        Check(concurrentQueue.TryDequeue(out concurrentWork), "Subsequent glass repaint was lost.");
        concurrentWork!();
        Check(concurrentPaints == 2, "Concurrent gate recovery must repaint exactly once per admitted callback.");

        // A queued callback may run inline, release the old admission, and
        // admit a replacement before the original enqueue reports failure.
        // The original failure path must not clear the newer admission.
        var racingGate = new GlassStateDispatchGate();
        var raceQueue = new Queue<Action>();
        bool replacementAdmitted = false;
        bool outerAdmitted = racingGate.Request(first =>
        {
            first();
            replacementAdmitted = racingGate.Request(next => { raceQueue.Enqueue(next); return true; },
                () => true, () => paint++, Report);
            return false;
        }, () => true, () => paint++, Report);
        Check(!outerAdmitted && replacementAdmitted && raceQueue.Count == 1,
            "Inline callback completion must allow an independently queued replacement.");
        Check(!racingGate.Request(next => { raceQueue.Enqueue(next); return true; },
                () => true, () => paint++, Report),
            "An obsolete enqueue rejection must not clear the replacement's pending ticket.");
        raceQueue.Dequeue()();
        Check(racingGate.Request(next => { raceQueue.Enqueue(next); return true; },
                () => true, () => paint++, Report),
            "After replacement completes, a new acrylic update must be admitted.");
        raceQueue.Dequeue()();

        Console.WriteLine($"PASS: {assertions} coalesced glass-state assertions; 1000 event burst, 1500 concurrent events, stale-ticket races, detach, replacement, UI failures and recovery.");
    }
}
