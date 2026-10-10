using Nexus.Runtime;
using Nexus.Shell.Models;

internal static class ControlCenterChecks
{
    private sealed class Clock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        internal void Advance(int seconds) => _ticks += TimeSpan.FromSeconds(seconds).Ticks;
    }
    private sealed class Worker(int id) : IControlCenterProcess
    {
        public int Id => id;
        public bool HasExited { get; set; }
        internal bool DenyStop;
        internal bool Disposed;
        public void Stop() { if (DenyStop) throw new IOException("Injected termination failure"); HasExited = true; }
        public void Dispose() => Disposed = true;
    }
    internal static async Task RunAsync()
    {
        int checks = 0;
        void Check(bool value, string message) { ++checks; if (!value) throw new Exception(message); }
        void Reject(Action action) { try { action(); } catch (RuntimeFailure) { ++checks; return; } throw new Exception("Invalid panel operation was accepted."); }
        var clock = new Clock(); var workers = new List<Worker>(); var ids = new List<Guid>();
        using var panel = new ControlCenterCoordinator(id => { ids.Add(id); var process = new Worker(100 + workers.Count); workers.Add(process); return process; }, (window, pid) => window == pid * 10L, clock);
        var state = new ShellState { QuickNote = "Keep notes", DisplayName = "Keep name" };
        PanelSync Sync(long sequence, bool visible, Guid[]? ack = null) => new(new(sequence, visible, "Sound", new(-1920, -200, 1920, 1080, 1.5, 20, true)), ControlCenterPreferences.From(state), ack ?? []);
        Check(panel.Sync(Sync(0, false)).Status.ProcessId == 0 && workers.Count == 0, "Closed panel must not eagerly create a process.");
        var started = panel.Sync(Sync(1, true));
        Check(workers.Count == 1 && started.Status.Recovering && started.Status.Window == 0, "Opening must create one supervised worker.");
        Check(panel.Pulse(100).ToolId == ids[0], "Snapshot must carry this worker's launch identity.");
        Reject(() => panel.Pulse(99)); Reject(() => panel.Ready(100, new(999)));
        panel.Ready(100, new(1000));
        Check(panel.Sync(Sync(1, true)).Status.Window == 1000, "Only an owned ready window may be published.");
        Check(panel.DisplayWindow(100) == 20, "Device display HWND must come from the authenticated desktop.");
        foreach (string operation in new[] { RuntimeOperations.ReadState, RuntimeOperations.CommitState, RuntimeOperations.Stop, RuntimeOperations.OpenFiles, RuntimeOperations.PanelSync })
            Check(!RuntimeOperations.Allows("controlcenter", operation), "Panel must not gain whole-state, shutdown, Files or supervisor privileges.");
        Check(RuntimeOperations.Allows("controlcenter", RuntimeOperations.Settings), "Panel needs typed device controls.");
        Check(!RuntimeOperations.Allows("files", RuntimeOperations.PanelPulse), "Files must not claim a panel heartbeat.");
        var command = new PanelAction(Guid.NewGuid(), "preference", DesktopPreference.CompactDock, true);
        var applying = panel.ActionAsync(100, command, CancellationToken.None);
        var delivered = panel.Sync(Sync(1, true));
        Check(delivered.Actions.Single() == command && !applying.IsCompleted, "Preference must wait for desktop acknowledgment.");
        Check(panel.Sync(Sync(1, true)).Actions.Single() == command, "A lost acknowledgment must retain the original command identity.");
        ControlCenterPreferences.Set(state, command);
        panel.Sync(Sync(1, true, [command.Id]));
        var receipt = await applying;
        Check(receipt.Preferences.CompactDock && receipt.Revision > 0, "Acknowledged preference must return the actual desktop state.");
        Check(state.QuickNote == "Keep notes" && state.DisplayName == "Keep name", "Panel preference must preserve unrelated desktop edits.");
        Check(panel.Sync(Sync(1, true)).Actions.Length == 0, "Acknowledged action must leave the queue.");
        foreach (var invalid in new[] { new PanelAction(Guid.Empty, "lock"), new PanelAction(Guid.NewGuid(), "run", Value: "cmd.exe"),
            new PanelAction(Guid.NewGuid(), "preference", (DesktopPreference)999, true), new PanelAction(Guid.NewGuid(), "preference", DesktopPreference.Wallpaper, Value: "unknown"),
            new PanelAction(Guid.NewGuid(), "preference", DesktopPreference.NativeGlass, Value: "false"), new PanelAction(Guid.NewGuid(), "advanced", Value: "file:///cmd.exe") })
            Reject(() => ControlCenterPreferences.Validate(invalid));
        Reject(() => panel.Sync(Sync(1, true) with { Desired = Sync(1, true).Desired with { Monitor = new(0, 0, 0, 1080, double.NaN, 20, true) } }));
        Check(panel.Hide(100, 0).Desired.Visible, "Stale hide must not dismiss a newer opening.");
        Check(!panel.Hide(100, 1).Desired.Visible && !panel.Sync(Sync(1, true)).Status.Visible, "Worker dismissal must win over a repeated desired-state publication.");
        panel.Sync(Sync(2, true)); Check(panel.Hide(100, 1).Desired.Visible, "Reopening needs a new sequence and must survive the old hide response.");
        using (var cancel = new CancellationTokenSource())
        {
            var cancelled = panel.ActionAsync(100, new(Guid.NewGuid(), "lock"), cancel.Token); cancel.Cancel();
            try { await cancelled; throw new Exception("Cancellation was ignored."); } catch (OperationCanceledException) { ++checks; }
            Check(panel.Sync(Sync(2, true)).Actions.Length == 0, "Cancelled undispatched action must be removed.");
        }
        var unfinished = panel.ActionAsync(100, new(Guid.NewGuid(), "preference", DesktopPreference.NativeGlass, false), CancellationToken.None);
        workers[0].HasExited = true; panel.Monitor();
        try { await unfinished; throw new Exception("Recovery replayed an unfinished action."); } catch (RuntimeFailure) { ++checks; }
        Check(workers.Count == 2 && workers[0].Disposed && ids[0] != ids[1], "Recovery must dispose the old process and replace its identity.");
        Reject(() => panel.Pulse(100)); panel.Ready(101, new(1010));
        Check(panel.Sync(Sync(2, true)).Actions.Length == 0, "Restart must not replay preferences or device operations.");
        clock.Advance(10); panel.Monitor(); Check(workers.Count == 3 && workers[1].HasExited, "Missing UI heartbeat must recover a hung ready worker.");
        clock.Advance(12); panel.Monitor();
        var paused = panel.Sync(Sync(2, true)).Status;
        Check(paused.Paused && paused.ProcessId == 0 && workers.Count == 3, "Repeated startup failure must pause after three launches.");
        for (int n = 0; n < 20; n++) panel.Monitor();
        Check(workers.Count == 3 && panel.Sync(Sync(2, true)).Status.IssueId == paused.IssueId, "Pause must not spin up processes or flood alerts.");
        clock.Advance(60); panel.Monitor(); Check(workers.Count == 4, "Recovery must resume after its bounded cooldown.");
        panel.Ready(103, new(1030)); workers[3].DenyStop = true; clock.Advance(10); panel.Monitor();
        Check(workers.Count == 4 && !panel.IsWorker(103), "Failed termination must revoke the old peer without launching a duplicate.");
        workers[3].DenyStop = false; panel.Monitor(); Check(workers.Count == 5 && workers[3].Disposed, "Replacement may start only after the old instance has stopped.");
        panel.Ready(104, new(1040)); panel.Hide(104, 2); workers[4].HasExited = true; panel.Monitor();
        Check(workers.Count == 5, "A hidden crashed panel must wait until it is requested.");
        clock.Advance(61); panel.Sync(Sync(3, true)); int queuePid = workers[^1].Id; panel.Ready(queuePid, new(queuePid * 10L));
        var queued = Enumerable.Range(0, 64).Select(_ => panel.ActionAsync(queuePid, new(Guid.NewGuid(), "lock"), CancellationToken.None)).ToArray();
        try { await panel.ActionAsync(queuePid, new(Guid.NewGuid(), "lock"), CancellationToken.None); throw new Exception("Unbounded panel actions were accepted."); } catch (RuntimeFailure) { ++checks; }
        clock.Advance(5); Check(panel.Sync(Sync(3, true)).Actions.Length == 0, "Expired actions must disappear before desktop dispatch.");
        foreach (var expired in queued) { try { await expired; throw new Exception("Expired action succeeded."); } catch (RuntimeFailure) { ++checks; } }
        panel.Hide(queuePid, 3); workers[^1].HasExited = true; panel.Monitor();
        // Recover repeatedly with a fresh budget interval; the desktop state stays intact.
        for (int n = 0; n < 12; n++)
        {
            clock.Advance(61); var reopened = panel.Sync(Sync(4 + n, true)); int pid = reopened.Status.ProcessId;
            panel.Ready(pid, new(pid * 10L)); workers[^1].HasExited = true; panel.Monitor();
            Check(state.CompactDock && state.QuickNote == "Keep notes" && panel.Sync(Sync(4 + n, true)).Actions.Length == 0, "Repeated panel crashes must preserve state without stale actions.");
        }
        panel.Dispose(); Check(workers.All(worker => worker.HasExited && worker.Disposed), "Session shutdown must stop and dispose every worker.");
        Console.WriteLine($"PASS: {checks} Control Center checks; peer/window authorization, sole-writer preferences, ordered hide/reopen, lost acknowledgments, cancelled actions, heartbeat/startup recovery, cooldown, termination failure and repeated crashes.");
    }
}
