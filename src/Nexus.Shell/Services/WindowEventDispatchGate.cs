namespace Nexus.Shell.Services;

// Concurrent native WinEvents share one UI delivery. Foreground priority and
// structural window-list invalidation are independent: combining the two must
// not lose a create/destroy/title change during a foreground burst. Per-ticket
// ownership rejects late callbacks and never unlocks a successor.
public sealed class WindowEventDispatchGate
{
    private readonly object _gate = new();
    private long _nextTicket, _pendingTicket;
    private bool _foreground, _enumerate;

    // Preserve the original entry point for callers that always need a scan.
    public bool Request(bool foreground, Func<Action, bool> enqueue, Action<bool> deliver, Action<Exception>? report = null)
    {
        ArgumentNullException.ThrowIfNull(deliver);
        return Request(foreground, true, enqueue, (observed, _) => deliver(observed), report);
    }

    public bool Request(bool foreground, bool enumerate, Func<Action, bool> enqueue,
        Action<bool, bool> deliver, Action<Exception>? report = null)
    {
        ArgumentNullException.ThrowIfNull(enqueue);
        ArgumentNullException.ThrowIfNull(deliver);

        void Report(Exception error)
        {
            if (report is null) throw error;
            try { report(error); }
            catch { /* Error reporting cannot crash a native event bridge. */ }
        }

        long ticket;
        lock (_gate)
        {
            _foreground |= foreground;
            _enumerate |= enumerate;
            if (_pendingTicket != 0) return false;
            ticket = unchecked(++_nextTicket);
            if (ticket == 0) ticket = unchecked(++_nextTicket);
            _pendingTicket = ticket;
        }

        void Release()
        {
            lock (_gate)
            {
                if (_pendingTicket == ticket) _pendingTicket = 0;
            }
        }

        try
        {
            if (enqueue(() =>
            {
                bool sawForeground, needsEnumeration;
                lock (_gate)
                {
                    // A refused enqueue may still invoke the callback later.
                    if (_pendingTicket != ticket) return;
                    sawForeground = _foreground;
                    needsEnumeration = _enumerate;
                    _foreground = _enumerate = false;
                    _pendingTicket = 0; // Admit reentrant requests.
                }
                try { deliver(sawForeground, needsEnumeration); }
                catch (Exception error) { Report(error); }
            })) return true;

            Release();
            return false;
        }
        catch (Exception error)
        {
            Release(); // Cannot clear a newer admission.
            if (report is null) throw;
            Report(error);
            return false;
        }
    }
}
