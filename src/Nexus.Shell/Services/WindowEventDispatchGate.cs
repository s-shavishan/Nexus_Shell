namespace Nexus.Shell.Services;

// WinEvent callbacks may arrive in bursts or from a different thread.
// Atomically coalesce them into one dispatcher callback while retaining
// foreground priority. Never hold the lock while entering WinUI.
public sealed class WindowEventDispatchGate
{
    private readonly object _gate = new();
    private bool _queued, _foreground;

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

        lock (_gate)
        {
            _foreground |= foreground;
            if (_queued) return false;
            _queued = true;
        }
        try
        {
            if (enqueue(() =>
            {
                bool sawForeground;
                lock (_gate)
                {
                    sawForeground = _foreground;
                    _foreground = false;
                    _queued = false;
                }
                try { deliver(sawForeground); }
                catch (Exception error) { Report(error); }
            })) return true;
            lock (_gate) _queued = false;
            return false;
        }
        catch (Exception error)
        {
            lock (_gate) _queued = false;
            if (report is null) throw;
            Report(error);
            return false;
        }
    }
}
