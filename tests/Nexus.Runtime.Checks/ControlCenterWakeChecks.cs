using Nexus.Runtime;
using Nexus.Shell.Models;

internal static class ControlCenterWakeChecks
{
    private sealed class Worker : IControlCenterProcess
    {
        public int Id => 501;
        public bool HasExited { get; private set; }
        public void Stop() => HasExited = true;
        public void Dispose() { }
    }
    internal static async Task RunAsync()
    {
        int checks = 0;
        void Check(bool condition, string message) { ++checks; if (!condition) throw new Exception(message); }
        async Task Reject(Func<Task> action)
        { try { await action(); } catch (RuntimeFailure) { ++checks; return; } throw new Exception("An invalid panel wait was accepted."); }
        var worker = new Worker(); using var coordinator = new ControlCenterCoordinator(_ => worker, (window, pid) => window == 5001 && pid == 501);
        var state = new ShellState { NativeGlass = true, SurfaceAnimations = false, QuickNote = "Keep my note" };
        var monitor = new PanelMonitor(-1920, -400, 1920, 1080, 1.5, 10, true);
        PanelSync Sync(long sequence, bool visible, string section = "Sound", bool compact = false)
            => new(new(sequence, visible, section, monitor, compact), ControlCenterPreferences.From(state), []);
        coordinator.Sync(Sync(1, true)); coordinator.Ready(501, new(5001));
        var current = coordinator.Pulse(501);
        Check(current.Preferences.NativeGlass && !current.Preferences.Animations, "Disabling motion must preserve glass across the panel contract.");
        Check(RuntimeOperations.Allows("controlcenter", RuntimeOperations.PanelWait)
            && !RuntimeOperations.Allows("desktop", RuntimeOperations.PanelWait) && !RuntimeOperations.Allows("files", RuntimeOperations.PanelWait), "Only the authenticated panel may wait for its state.");
        var waiting = coordinator.WaitAsync(501, current.Revision, CancellationToken.None);
        Check(!waiting.IsCompleted, "An unchanged snapshot must wait for a revision.");
        await Reject(() => coordinator.WaitAsync(501, current.Revision, CancellationToken.None));
        coordinator.Sync(Sync(2, true, "Network", compact: true));
        var compactState = await waiting.WaitAsync(TimeSpan.FromSeconds(3));
        Check(compactState.Desired.Compact && compactState.Desired.Section == "Network" && compactState.Desired.Sequence == 2, "A direct menu control must wake the panel with the new section and presentation.");
        Check((await coordinator.WaitAsync(501, current.Revision, CancellationToken.None)).Revision == compactState.Revision, "A missed change must return immediately on the next wait.");
        await Reject(() => coordinator.WaitAsync(999, compactState.Revision, CancellationToken.None));
        await Reject(() => coordinator.WaitAsync(501, -2, CancellationToken.None));
        await Reject(() => coordinator.WaitAsync(501, compactState.Revision + 1, CancellationToken.None));
        using (var cancellation = new CancellationTokenSource())
        {
            var cancelled = coordinator.WaitAsync(501, compactState.Revision, cancellation.Token); cancellation.Cancel();
            try { await cancelled; throw new Exception("Panel wait ignored cancellation."); } catch (OperationCanceledException) { ++checks; }
        }
        var hide = coordinator.WaitAsync(501, compactState.Revision, CancellationToken.None);
        coordinator.Hide(501, 2); var hidden = await hide.WaitAsync(TimeSpan.FromSeconds(3));
        Check(!hidden.Desired.Visible, "Worker dismissal must wake its state wait.");
        coordinator.Sync(Sync(3, true));
        var reopened = coordinator.Pulse(501); var preference = coordinator.WaitAsync(501, reopened.Revision, CancellationToken.None);
        state.SurfaceAnimations = true; coordinator.Sync(Sync(3, true));
        Check((await preference.WaitAsync(TimeSpan.FromSeconds(3))).Preferences.Animations, "Preference changes must wake the panel without a visibility change.");
        var applied = coordinator.Pulse(501);
        Check((await coordinator.WaitAsync(501, applied.Revision, CancellationToken.None)).Revision == applied.Revision, "Idle wait must return its bounded heartbeat snapshot without changing the revision.");
        ControlCenterPreferences.Validate(new(Guid.NewGuid(), "expand")); ++checks;
        ControlCenterPreferences.Set(state, new(Guid.NewGuid(), "preference", DesktopPreference.Animations, false));
        Check(!state.SurfaceAnimations && state.NativeGlass && state.QuickNote == "Keep my note", "Animation preference must preserve glass and unrelated state.");
        var restored = new ShellState(); ControlCenterPreferences.From(state).Apply(restored);
        Check(!restored.SurfaceAnimations && restored.NativeGlass, "Panel snapshot must preserve independent effects.");
        var shuttingDown = coordinator.WaitAsync(501, coordinator.Pulse(501).Revision, CancellationToken.None);
        coordinator.Dispose(); await Reject(async () => await shuttingDown);
        Check(worker.HasExited, "Shutdown must wake pending waits and retire the worker.");
        Console.WriteLine($"PASS: {checks} panel wake checks; revisions, compact presentation, cancellation, admission, heartbeat, shutdown and independent motion.");
    }
}
