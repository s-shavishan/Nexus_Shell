namespace Nexus.Shell.Services;

// UI-thread-owned dispatcher gate for native layout notifications. Each accepted
// callback owns its own ticket; a late refusal/exception from an earlier enqueue
// must never unlock the newly queued reposition. Retained callbacks from rejected
// admissions are ignored rather than executed against a stale WinUI surface.
public sealed class CoalescedUiDispatch
{
    private uint _nextTicket, _pendingTicket;

    public bool Request(Func<Action, bool> enqueue, Action callback, Action<Exception>? report = null)
    {
        ArgumentNullException.ThrowIfNull(enqueue);
        ArgumentNullException.ThrowIfNull(callback);
        if (_pendingTicket != 0) return false;

        void Report(Exception error)
        {
            if (report is null) throw error;
            try { report(error); }
            catch { /* A diagnostic sink must not turn recovery into a UI crash. */ }
        }

        uint ticket = unchecked(++_nextTicket);
        if (ticket == 0) ticket = unchecked(++_nextTicket);
        _pendingTicket = ticket;

        void Release()
        {
            if (_pendingTicket == ticket) _pendingTicket = 0;
        }

        try
        {
            if (enqueue(() =>
            {
                if (_pendingTicket != ticket) return; // Rejected/stale callback.
                _pendingTicket = 0; // Admit a new request during delivery.
                try { callback(); }
                catch (Exception error) { Report(error); }
            })) return true;

            Release();
            return false;
        }
        catch (Exception error)
        {
            Release();
            if (report is null) throw;
            Report(error);
            return false;
        }
    }
}
