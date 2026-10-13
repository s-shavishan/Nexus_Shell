using Nexus.Shell.Services;

internal static class NativeUiDispatchChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }

        var gate = new CoalescedUiDispatch();
        var pending = new Queue<Action>();
        var errors = new List<string>();
        int deliveries = 0;
        bool Queue(Action callback) { pending.Enqueue(callback); return true; }
        void Report(Exception error) => errors.Add(error.Message);

        Check(gate.Request(Queue, () => deliveries++, Report),
            "First native reposition should schedule a UI callback.");
        for (int i = 0; i < 300; i++)
            Check(!gate.Request(Queue, () => deliveries++, Report),
                "Native sizing bursts must not queue duplicate WinUI callbacks.");
        Check(pending.Count == 1 && deliveries == 0,
            "The queue must retain one pending reposition without running it prematurely.");
        pending.Dequeue()();
        Check(deliveries == 1, "The accepted reposition must execute once.");
        Check(gate.Request(Queue, () => deliveries++, Report),
            "Dispatch completion must release the gate for a future resize.");
        pending.Dequeue()();
        Check(deliveries == 2, "A later position callback must run normally.");

        Check(!gate.Request(_ => false, () => deliveries++, Report),
            "WinUI shutdown may refuse enqueue without leaving a stuck gate.");
        Check(gate.Request(Queue, () => deliveries++, Report),
            "An enqueue refusal must permit the next desktop layout request.");
        pending.Dequeue()();
        Check(deliveries == 3, "A retried callback must execute.");

        Check(!gate.Request(_ => throw new InvalidOperationException("dispatcher closed"),
            () => deliveries++, Report),
            "Throwing dispatcher admission must be contained with an error reporter.");
        Check(errors[^1] == "dispatcher closed",
            "Dispatcher admission failure must remain visible in diagnostics.");
        Check(gate.Request(Queue, () => throw new InvalidOperationException("XAML surface disposed"), Report),
            "The native event can be queued before a WinUI surface closes.");
        pending.Dequeue()();
        Check(errors[^1] == "XAML surface disposed" && deliveries == 3,
            "Delayed XAML callback exceptions must not escape dispatcher execution.");
        Check(gate.Request(Queue, () => deliveries++, Report),
            "An exception from an earlier callback must not poison future repositioning.");
        pending.Dequeue()();
        Check(deliveries == 4, "The recovered queue must deliver later valid updates.");

        Check(gate.Request(Queue, () => throw new InvalidOperationException("failed layout"),
            _ => throw new IOException("log file unavailable")),
            "Log failure must not prevent a queued callback from being delivered safely.");
        pending.Dequeue()();
        Check(gate.Request(Queue, () => deliveries++, Report),
            "A broken exception logger must not block subsequent native work.");
        pending.Dequeue()();
        Check(deliveries == 5, "Native UI must remain responsive when logging is unavailable.");

        int nested = 0;
        Check(gate.Request(Queue, () =>
        {
            nested++;
            Check(gate.Request(Queue, () => nested++, Report),
                "Reentrant native window notifications must enqueue a fresh generation.");
        }, Report), "Initial reentrant request must be accepted.");
        pending.Dequeue()();
        pending.Dequeue()();
        Check(nested == 2 && pending.Count == 0,
            "Reentrant layout dispatch must execute exactly twice with a clean queue.");

        // Refusing an enqueue does not guarantee that the queue discarded the
        // callback. A late invocation must be harmless to an accepted successor.
        var rejected = new CoalescedUiDispatch();
        Action? rejectedCallback = null;
        int rejectedDeliveries = 0;
        var retryCallbacks = new Queue<Action>();
        Check(!rejected.Request(
            callback => { rejectedCallback = callback; return false; },
            () => rejectedDeliveries++),
            "A rejected callback must be marked obsolete.");
        Check(rejected.Request(
            callback => { retryCallbacks.Enqueue(callback); return true; },
            () => rejectedDeliveries += 10),
            "A new request should be admitted after a refused native repaint.");
        rejectedCallback!();
        Check(!rejected.Request(
            callback => { retryCallbacks.Enqueue(callback); return true; },
            () => rejectedDeliveries += 100),
            "An obsolete repaint callback must not unlock the replacement.");
        retryCallbacks.Dequeue()();
        Check(rejectedDeliveries == 10 && retryCallbacks.Count == 0,
            "Only the accepted repaint generation may be delivered.");

        var inline = new CoalescedUiDispatch();
        int inlineDeliveries = 0;
        var inlinePending = new Queue<Action>();
        Check(!inline.Request(callback =>
        {
            callback();
            Check(inline.Request(
                next => { inlinePending.Enqueue(next); return true; },
                () => inlineDeliveries += 10),
                "An inline finished update must allow a new layout generation.");
            return false;
        }, () => inlineDeliveries++),
            "Late refusal after inline execution must still return failure.");
        Check(!inline.Request(
            next => { inlinePending.Enqueue(next); return true; },
            () => inlineDeliveries += 100),
            "A previous generation's refusal must not unlock the newly pending update.");
        inlinePending.Dequeue()();
        Check(inlineDeliveries == 11 && inlinePending.Count == 0,
            "The two inline/reentrant generations must each deliver exactly once.");

        var legacy = new CoalescedUiDispatch();
        bool threw = false;
        try { legacy.Request(_ => throw new InvalidOperationException("expected"), () => { }); }
        catch (InvalidOperationException) { threw = true; }
        Check(threw && legacy.Request(Queue, () => deliveries++),
            "Callers without an error handler must preserve the original exception contract and recovery.");
        pending.Dequeue()();
        Check(deliveries == 6, "Legacy exception recovery must leave the gate reusable.");

        for (int i = 0; i < 500; i++)
        {
            Check(gate.Request(Queue, () => throw new InvalidOperationException("closing"), Report),
                "Rapid closing/restarting windows must be able to post safely.");
            pending.Dequeue()();
        }
        Check(deliveries == 6 && pending.Count == 0,
            "Five hundred failed deferred UI updates must not lock the dispatch gate.");
        Console.WriteLine($"PASS: {checks} native UI reposition dispatch assertions; refusal, deferred exceptions, logging, reentrancy and recovery.");
    }
}
