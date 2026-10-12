namespace Nexus.Shell.Services;

// Native acrylic controllers may send multiple state notifications before WinUI
// processes its dispatcher queue. The callback reads the controller's latest
// state, so only one queued frame update per attachment is required.
// Each WindowMaterial attachment owns its own gate: replacing a backdrop does
// not let an obsolete queued update suppress the next controller's repaint.
internal sealed class GlassStateDispatchGate
{
    private int _pending;

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

        // Win32/WinRT notifications are not guaranteed to arrive on the XAML
        // thread. Only the winner may enqueue a frame update.
        if (Interlocked.CompareExchange(ref _pending, 1, 0) != 0) return false;
        bool accepted = GlassUiDispatch.TryPost(
            enqueue,
            () =>
            {
                // Clear before evaluating the generation and updating XAML:
                // exceptions and reentrant notifications must not strand us.
                Interlocked.Exchange(ref _pending, 0);
                return current();
            },
            update,
            error =>
            {
                Interlocked.Exchange(ref _pending, 0);
                report(error);
            });
        if (!accepted) Interlocked.Exchange(ref _pending, 0);
        return accepted;
    }
}
