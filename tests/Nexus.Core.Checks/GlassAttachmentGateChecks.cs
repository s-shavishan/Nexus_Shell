using Nexus.Shell.Services;

internal static class GlassAttachmentGateChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool actual, string message)
        { checks++; if (!actual) throw new Exception(message); }

        var gate = new GlassAttachmentGate<object>();
        var first = new object();
        var second = new object();
        Check(!gate.IsCurrent(first), "A backdrop must not accept callbacks before attachment.");
        gate.Attach(first);
        Check(gate.IsCurrent(first), "An attached native backdrop must own its callbacks.");
        gate.Attach(second);
        Check(!gate.IsCurrent(first) && gate.IsCurrent(second),
            "Replacement must reject state events from an older native backdrop.");
        Check(!gate.DetachFailed(first) && gate.IsCurrent(second),
            "An old failure callback must never detach a newly attached backdrop.");
        Check(gate.DetachFailed(second) && !gate.IsCurrent(second),
            "Active native failure must revoke the attachment before fallback.");
        Check(!gate.DetachFailed(second), "Duplicate failure notifications must be idempotent.");
        gate.Attach(first);
        Check(gate.IsCurrent(first), "Disabling then retrying must allow a fresh live attachment.");
        gate.Attach(null);
        Check(!gate.IsCurrent(first), "Disabling glass must reject any subsequently queued state events.");

        // Repeated palette/toggle lifecycle churn must not admit stale events.
        var previous = new object();
        for (int i = 0; i < 500; i++)
        {
            var current = new object();
            gate.Attach(current);
            Check(gate.IsCurrent(current) && !gate.IsCurrent(previous),
                "Rapid backdrop replacement must leave exactly one active generation.");
            Check(!gate.DetachFailed(previous) && gate.IsCurrent(current),
                "Delayed failures from earlier generations must not reset active glass.");
            previous = current;
        }
        gate.Close();
        Check(!gate.IsCurrent(previous) && !gate.DetachFailed(previous),
            "Closing the HWND must make all delayed callbacks inert.");
        gate.Attach(new object());
        Check(!gate.IsCurrent(previous), "Closed attachment gates must remain permanently detached.");
        Console.WriteLine($"PASS: {checks} Liquid Glass attachment checks; stale events, failure fallback, retry, 500 rapid replacements and post-close protection.");
    }
}
