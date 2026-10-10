namespace Nexus.Runtime;

public interface IControlCenterProcess : IDisposable { int Id { get; } bool HasExited { get; } void Stop(); }

// A panel can request allowlisted actions, never a whole-state replacement.
// The desktop remains the sole preference writer. Device mutations travel
// directly to the settings service and are never replayed during recovery.
public sealed class ControlCenterCoordinator(Func<Guid, IControlCenterProcess> launch, Func<long, int, bool> ownsWindow, TimeProvider? clock = null) : IDisposable
{
    private sealed record Pending(int ProcessId, PanelAction Action, long Created, TaskCompletionSource<PanelSnapshot> Reply);
    private readonly object _gate = new();
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly RuntimeRestartBudget _budget = new(clock);
    private readonly Dictionary<Guid, Pending> _actions = [];
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly HashSet<int> _waiting = [];
    private IControlCenterProcess? _process;
    private Guid _toolId, _issueId;
    private string _issue = "";
    private long _window, _started, _pulse, _revision;
    private bool _disposed, _paused, _retiring;
    private PanelDesired _desired = new(0, false, "Sound", new(0, 0, 1, 1, 1, 0, false));
    private ControlCenterPreferences _preferences = ControlCenterPreferences.From(new());
    private bool Worker(int pid) => !_disposed && !_retiring && _process is { HasExited: false } && _process.Id == pid;
    public bool IsWorker(int pid) { lock (_gate) return Worker(pid); }
    private void Verify(int pid) { if (!Worker(pid)) throw new RuntimeFailure("panel-peer", "This Control Center instance no longer owns the session."); }
    private PanelSnapshot Snapshot() => new(_toolId, _revision, _preferences, _desired);
    private PanelStatus Status() => new(_process is { HasExited: false } ? _process.Id : 0, _window, _desired.Visible,
        _desired.Visible && _window == 0 && !_paused, _paused, _issueId, _issue);
    public PanelSyncResult Sync(PanelSync sync)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var monitor = sync.Desired.Monitor;
            if (sync.Desired.Sequence < 0 || !ControlCenterPreferences.Sections.Contains(sync.Desired.Section) || sync.Acknowledged.Length > 64
                || monitor.Width is < 1 or > 100_000 || monitor.Height is < 1 or > 100_000 || Math.Abs((long)monitor.X) > 1_000_000 || Math.Abs((long)monitor.Y) > 1_000_000
                || !double.IsFinite(monitor.Scale) || monitor.Scale is < .5 or > 4 || !Nexus.Shell.Services.AuraPalette.Moods.Contains(sync.Preferences.Wallpaper))
                throw new RuntimeFailure("panel-sync", "The desktop panel state is invalid.");
            bool changed = _preferences != sync.Preferences;
            _preferences = sync.Preferences;
            // A worker hide wins over repeated publication of the same sequence.
            if (sync.Desired.Sequence > _desired.Sequence) { _desired = sync.Desired; changed = true; }
            else if (sync.Desired.Sequence == _desired.Sequence && _desired.Monitor != monitor) { _desired = _desired with { Monitor = monitor }; changed = true; }
            if (changed) NotifyChanged();
            foreach (var id in sync.Acknowledged)
                if (_actions.Remove(id, out var command)) command.Reply.TrySetResult(Snapshot());
            MonitorCore();
            ExpireActions();
            return new(Status(), _actions.Values.Select(value => value.Action).ToArray());
        }
    }
    public PanelSnapshot Pulse(int pid) { lock (_gate) { Verify(pid); _pulse = _clock.GetTimestamp(); return Snapshot(); } }
    // One authenticated wait per worker. Changes wake it immediately; the
    // one-second bound also supplies the UI heartbeat without idle polling.
    public async Task<PanelSnapshot> WaitAsync(int pid, long revision, CancellationToken cancellation)
    {
        Task change;
        lock (_gate)
        {
            Verify(pid); cancellation.ThrowIfCancellationRequested();
            if (revision < -1 || revision > _revision) throw new RuntimeFailure("panel-revision", "The panel revision is invalid.");
            _pulse = _clock.GetTimestamp();
            if (revision != _revision) return Snapshot();
            if (!_waiting.Add(pid)) throw new RuntimeFailure("panel-busy", "This panel already has a pending state wait.");
            change = _changed.Task;
        }
        try
        {
            try { await change.WaitAsync(TimeSpan.FromSeconds(1), cancellation).ConfigureAwait(false); }
            catch (TimeoutException) { }
            lock (_gate) { Verify(pid); _pulse = _clock.GetTimestamp(); return Snapshot(); }
        }
        finally { lock (_gate) _waiting.Remove(pid); }
    }
    private void NotifyChanged()
    { ++_revision; var previous = _changed; _changed = new(TaskCreationOptions.RunContinuationsAsynchronously); previous.TrySetResult(); }
    public PanelSnapshot Ready(int pid, PanelReady ready)
    {
        lock (_gate)
        {
            Verify(pid);
            if (ready.Window == 0 || !ownsWindow(ready.Window, pid)) throw new RuntimeFailure("panel-window", "The panel window does not belong to this worker.");
            _window = ready.Window; _pulse = _clock.GetTimestamp(); return Snapshot();
        }
    }
    public PanelSnapshot Hide(int pid, long sequence) { lock (_gate) { Verify(pid); if (_desired.Sequence == sequence && _desired.Visible) { _desired = _desired with { Visible = false }; NotifyChanged(); } return Snapshot(); } }
    public long DisplayWindow(int pid) { lock (_gate) { Verify(pid); return _desired.Monitor.DisplayWindow; } }
    public async Task<PanelSnapshot> ActionAsync(int pid, PanelAction command, CancellationToken cancellation)
    {
        ControlCenterPreferences.Validate(command);
        Pending pending;
        lock (_gate)
        {
            Verify(pid); ExpireActions();
            if (_actions.ContainsKey(command.Id)) throw new RuntimeFailure("panel-action", "This panel action was already submitted.");
            if (_actions.Count >= 64) throw new RuntimeFailure("panel-busy", "Desktop actions are busy. Wait for the current changes.");
            pending = new(pid, command, _clock.GetTimestamp(), new(TaskCreationOptions.RunContinuationsAsynchronously));
            _actions.Add(command.Id, pending);
        }
        try { return await pending.Reply.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellation).ConfigureAwait(false); }
        finally { lock (_gate) _actions.Remove(command.Id); }
    }
    private void ExpireActions()
    {
        foreach (var (id, command) in _actions.ToArray())
            if (_clock.GetElapsedTime(command.Created) >= TimeSpan.FromSeconds(5))
            { _actions.Remove(id); command.Reply.TrySetException(new RuntimeFailure("panel-timeout", "The desktop did not acknowledge this action. Check its current state before trying again.")); }
    }
    public void Monitor() { lock (_gate) { if (!_disposed) { MonitorCore(); ExpireActions(); } } }
    private void MonitorCore()
    {
        if (_process is { } process && (_retiring || process.HasExited || _clock.GetElapsedTime(_window == 0 ? _started : _pulse) >= TimeSpan.FromSeconds(_window == 0 ? 12 : 10)))
        { if (!_retiring) Issue("Control Center stopped responding. Its panel is being recovered; the desktop remains available."); StopWorker(); }
        if (!_desired.Visible || _process is not null) return;
        if (!_budget.TryStart()) { if (!_paused) Issue("Control Center stopped repeatedly. Automatic recovery is paused for up to one minute."); _paused = true; return; }
        _paused = false; _toolId = Guid.NewGuid(); _started = _pulse = _clock.GetTimestamp();
        try { _process = launch(_toolId); if (_process.Id <= 0) throw new InvalidOperationException("The panel process has no identity."); }
        catch { StopWorker(); Issue("Control Center could not start. Check core.log; the desktop remains available."); }
    }
    private void Issue(string message) { if (_issue == message) return; _issueId = Guid.NewGuid(); _issue = message; }
    private void StopWorker()
    {
        var process = _process; _window = 0; _retiring = true;
        NotifyChanged();
        foreach (var command in _actions.Values) command.Reply.TrySetException(new RuntimeFailure("panel-restarted", "The panel was recovered. Its unfinished action was not replayed."));
        _actions.Clear();
        if (process is not null)
        {
            try { if (!process.HasExited) process.Stop(); }
            catch { Issue("Control Center could not be stopped. A replacement will wait for its previous instance to exit."); if (!_disposed) return; }
            process.Dispose();
        }
        _process = null; _retiring = false;
    }
    public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; StopWorker(); } }
}
