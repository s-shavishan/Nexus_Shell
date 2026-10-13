namespace Nexus.Shell.Services;

// Native WinEvent notifications can arrive concurrently. Keep only one queued
// UI reconciliation, but never let a late enqueue failure or stale callback
// release/deliver a different generation. A refused batch retains foreground
// priority for the next successful dispatcher admission.
public sealed class WindowEventDispatchGate
{
    private readonly object _gate = new();
    private long _nextTicket, _pendingTicket;
    private bool _foreground;

    public bool Request(bool foreground, Func<Action, bool> enqueue, Action<bool> deliver, Action<Exception>? report = null)
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
                bool sawForeground;
                lock (_gate)
                {
                    // A queue may refuse admission after retaining a callback.
                    // Such a stale callback must not consume a newer batch.
                    if (_pendingTicket != ticket) return;
                    sawForeground = _foreground;
                    _foreground = false;
                    _pendingTicket = 0; // Release before reentrant delivery.
                }
                try { deliver(sawForeground); }
                catch (Exception error) { Report(error); }
            })) return true;

            Release();
            return false;
        }
        catch (Exception error)
        {
            Release(); // Never release a newer ticket admitted inline.
            if (report is null) throw;
            Report(error);
            return false;
        }
    }
}
