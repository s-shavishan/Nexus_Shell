namespace Nexus.Shell.Services;

// Native acrylic controllers may send multiple state notifications before WinUI
// processes its dispatcher queue. The callback reads the controller's latest
// state, so only one queued frame update per attachment is required.
// Each WindowMaterial attachment owns its own gate: replacing a backdrop does
// not let an obsolete queued update suppress the next controller's repaint.
internal sealed class GlassStateDispatchGate
{
    private bool _pending;

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

        if (_pending) return false;

        _pending = true;
        bool accepted = GlassUiDispatch.TryPost(
            enqueue,
            () =>
            {
                // Clear before evaluating the generation and updating XAML:
                // exceptions and reentrant notifications must not strand us.
                _pending = false;
                return current();
            },
            update,
            error =>
            {
                _pending = false;
                report(error);
            });
        if (!accepted) _pending = false;
        return accepted;
    }
}
