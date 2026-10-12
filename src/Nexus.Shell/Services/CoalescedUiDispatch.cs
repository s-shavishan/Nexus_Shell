namespace Nexus.Shell.Services;

// UI-thread-only scheduling gate. Repeated requests share one queued callback.
// A rejected or throwing dispatcher must never leave the gate permanently set.
public sealed class CoalescedUiDispatch
{
    private bool _pending;

    public bool Request(Func<Action, bool> enqueue, Action callback)
    {
        ArgumentNullException.ThrowIfNull(enqueue);
        ArgumentNullException.ThrowIfNull(callback);
        if (_pending) return false;
        _pending = true;
        try
        {
            if (enqueue(() =>
            {
                _pending = false;
                callback();
            })) return true;
            _pending = false;
            return false;
        }
        catch
        {
            _pending = false;
            throw;
        }
    }
}
