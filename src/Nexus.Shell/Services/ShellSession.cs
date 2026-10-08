using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

// One live state and one ordered writer for all native shell windows.
public sealed class ShellSession
{
    public StateStore Store { get; }
    public ShellState State { get; }
    public event Action? Changed;
    private readonly object _queueLock = new();
    private Task _writes = Task.CompletedTask;
    private Func<ShellState>? _snapshot;
    private bool _finalized;

    public ShellSession(StateStore? store = null)
    { Store = store ?? new StateStore(); State = Store.Load(); }
    public void AttachSnapshot(Func<ShellState> snapshot) => _snapshot = snapshot;
    public void DetachSnapshot(Func<ShellState> snapshot)
    { if (_snapshot == snapshot) _snapshot = null; }
    public ShellState Capture() => (_snapshot?.Invoke() ?? State.Snapshot());
    public void NotifyChanged() { if (!_finalized) Changed?.Invoke(); }
    public Task SaveAsync()
    {
        // Capture on the UI thread, before sending immutable data to the writer.
        var snapshot = Capture();
        lock (_queueLock)
        {
            if (_finalized) return Task.CompletedTask;
            return _writes = _writes.ContinueWith(_ => Store.Save(snapshot), CancellationToken.None,
                TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }
    public void SaveFinal()
    {
        var snapshot = Capture();
        lock (_queueLock) { if (_finalized) return; _finalized = true; }
        // StateStore prevents older queued writes from replacing this snapshot.
        Store.SaveFinal(snapshot);
    }
}
