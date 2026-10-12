namespace Nexus.Shell.Services;

// One pending UI repaint per native acrylic attachment. The system backdrop
// controller can raise events on multiple threads; use an admission ticket so
// stale completion/error paths never release a newer queued frame update.
internal sealed class GlassStateDispatchGate
{
    private int _pendingTicket;
    private int _nextTicket;

    internal bool Request(
        Func<Action, bool> enqueue,
        Func<bool> current,
        Action update,
        Action<Exception> report)
    {
        ArgumentNullException.ThrowIfNull(enqueue);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(report);

        int ticket = Interlocked.Increment(ref _nextTicket);
        if (ticket == 0) ticket = Interlocked.Increment(ref _nextTicket);
        if (Interlocked.CompareExchange(ref _pendingTicket, ticket, 0) != 0) return false;

        void Release() => Interlocked.CompareExchange(ref _pendingTicket, 0, ticket);

        bool accepted = GlassUiDispatch.TryPost(
            enqueue,
            () =>
            {
                // Release *this* admission before running user callbacks so
                // reentrant native state transitions can schedule new work.
                Release();
                return current();
            },
            update,
            error =>
            {
                Release();
                report(error);
            });
        if (!accepted) Release();
        return accepted;
    }
}
