using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

// One UI state, with ordered persistence through Core in the application.
// The local writer remains available for portable, UI-independent checks.
public sealed class ShellSession
{
    public StateStore Store { get; }
    public ShellState State { get; }
    public string RecoveryMessage { get; }
    public event Action? Changed;
    private readonly object _queueLock = new();
    private Task _writes = Task.CompletedTask;
    private Func<ShellState>? _snapshot;
    private bool _finalized;
    private readonly Func<ShellState, Task>? _persist;

    public ShellSession(StateStore? store = null, ShellState? initialState = null, Func<ShellState, Task>? persist = null, string? recoveryMessage = null)
    { Store = store ?? new StateStore(); State = initialState ?? Store.Load(); _persist = persist; RecoveryMessage = recoveryMessage ?? Store.RecoveryMessage; }
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
            return _writes = _writes.ContinueWith(previous => { _ = previous.Exception; return _persist is null ? Task.Run(() => Store.Save(snapshot)) : _persist(snapshot); }, CancellationToken.None,
                TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
        }
    }
    public Task SaveFinalAsync()
    {
        var snapshot = Capture();
        lock (_queueLock)
        {
            if (_finalized) return _writes; _finalized = true;
            return _writes = _writes.ContinueWith(previous => { _ = previous.Exception; return _persist is null ? Task.Run(() => Store.SaveFinal(snapshot)) : _persist(snapshot); }, CancellationToken.None,
                TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
        }
    }
    public void SaveFinal() => SaveFinalAsync().GetAwaiter().GetResult();
}
