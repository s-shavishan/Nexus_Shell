using Nexus.Shell.Services;

internal static class WindowsShellRecoveryChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool pass, string message) { checks++; if (!pass) throw new InvalidOperationException(message); }
        var ready = new WindowsShellPresence(10, 20);
        var absent = new WindowsShellPresence(0, 0);
        var gate = new WindowsShellRecoveryGate();
        Check(gate.Begin(0, true, false) == WindowsShellStartDecision.Incomplete, "An unobserved shell must not trigger a launch.");
        Check(gate.Observe(ready, 0) && !gate.Observe(ready, 100), "Steady native identity must not queue repeated layout recovery.");
        Check(gate.Begin(100, true, true) == WindowsShellStartDecision.Ready && gate.Attempts == 0, "An existing Explorer must never be started again.");
        Check(gate.Observe(new(11, 21), 200), "A changed desktop/taskbar identity must be detected even without a missing heartbeat.");
        gate.Observe(absent, 1000);
        Check(gate.Begin(5999, true, true) == WindowsShellStartDecision.Waiting, "Give Windows five seconds to restore its own shell.");
        Check(gate.Begin(6000, false, true) == WindowsShellStartDecision.SessionUnavailable && gate.Attempts == 0, "Locked, disconnected, sleeping or unobserved sessions cannot consume a recovery attempt.");
        Check(gate.Begin(6000, true, true) == WindowsShellStartDecision.Start && gate.Starting && gate.Attempts == 1, "A missing shell may start after grace expires.");
        for (int i = 0; i < 1000; i++) Check(gate.Begin(6001 + i, true, i % 2 == 0) == WindowsShellStartDecision.InProgress, "Heartbeat and manual requests must share one in-flight start.");
        gate.Complete();
        Check(gate.Begin(15999, true, false) == WindowsShellStartDecision.Cooldown && gate.Attempts == 1, "Manual retry must respect the same cooldown.");
        Check(gate.Begin(16000, true, true) == WindowsShellStartDecision.Start, "A later bounded automatic attempt may proceed.");
        gate.Complete();
        Check(gate.Begin(26000, true, true) == WindowsShellStartDecision.Start, "The third automatic attempt may proceed.");
        gate.Complete();
        Check(gate.Begin(36000, true, true) == WindowsShellStartDecision.Paused && gate.AutomaticPaused, "Repeated failures must pause automatically rather than spawn forever.");
        Check(gate.Begin(36000, true, false) == WindowsShellStartDecision.Start && gate.Attempts == 4, "An explicit manual retry remains available after the automatic circuit pauses.");
        gate.Complete();
        Check(gate.Begin(35999, true, false) == WindowsShellStartDecision.Cooldown, "A backward clock value cannot bypass cooldown.");
        gate.Observe(ready, 40000);
        gate.Observe(ready, 339999);
        Check(gate.AutomaticPaused, "Brief recovery cannot refill the automatic budget.");
        gate.Observe(new(12, 22), 340000);
        gate.Observe(new(12, 22), 639999);
        Check(gate.AutomaticPaused, "An Explorer identity change resets the stable interval.");
        gate.Observe(new(12, 22), 640000);
        Check(gate.Attempts == 0 && !gate.AutomaticPaused, "Five stable minutes restore automatic recovery capacity.");
        foreach (var partial in new[] { new WindowsShellPresence(10, 0), new WindowsShellPresence(0, 20) })
        {
            gate = new(); gate.Observe(partial, 0);
            Check(gate.Begin(10000, true, false) == WindowsShellStartDecision.Incomplete && gate.Begin(10000, true, true) == WindowsShellStartDecision.Incomplete && gate.Attempts == 0,
                "A partly initialized shell must never cause a duplicate Explorer process.");
        }
        gate = new(); gate.Observe(absent, 0); gate.Observe(new(10, 0), 4999); gate.Observe(absent, 5000);
        Check(gate.Begin(9999, true, true) == WindowsShellStartDecision.Waiting, "Incomplete startup interrupts the continuous absence grace interval.");
        Check(gate.Begin(5000, true, false) == WindowsShellStartDecision.Start, "A deliberate manual recovery does not wait for the automatic grace period.");
        gate.Complete();
        Check(NexusControlsCatalog.Find("Windows connection").Any(s => s.Id == "windows" && s.Page == "core"), "Windows recovery must be discoverable in Controls and Spotlight.");
        Console.WriteLine($"PASS: {checks} Windows shell recovery checks; restart identity, startup grace, single admission, cooldown, bounded automatic recovery, manual retry, session gating and stable budget reset. No Explorer process launched.");
    }
}
