namespace Nexus.Shell.Services;

// UI-thread-only scheduling gate. Repeated requests share one queued callback.
// Refusal, throwing enqueue, and a failed UI callback must all release the gate.
// Existing callers retain exception propagation unless an error reporter is set.
public sealed class CoalescedUiDispatch
{
    private bool _pending;

    public bool Request(Func<Action, bool> enqueue, Action callback, Action<Exception>? report = null)
    {
        ArgumentNullException.ThrowIfNull(enqueue);
        ArgumentNullException.ThrowIfNull(callback);
        if (_pending) return false;

        void Report(Exception error)
        {
            if (report is null) throw error;
            try { report(error); }
            catch { /* A diagnostic sink must not turn recovery into a UI crash. */ }
        }

        _pending = true;
        try
        {
            if (enqueue(() =>
            {
                _pending = false; // Release before delivery for reentrant requests.
                try { callback(); }
                catch (Exception error) { Report(error); }
            })) return true;
            _pending = false;
            return false;
        }
        catch (Exception error)
        {
            _pending = false;
            if (report is null) throw;
            Report(error);
            return false;
        }
    }
}
