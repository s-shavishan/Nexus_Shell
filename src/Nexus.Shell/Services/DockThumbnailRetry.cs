namespace Nexus.Shell.Services;

// Per-preview, UI-thread-only backoff for transient DWM registration failures.
// Foreground/window lifecycle polling can be as fast as 80 ms; do not retry
// native DWM operations on every tick, but never strand an open preview either.
internal sealed class DockThumbnailRetry
{
    internal const int MaxDelayMilliseconds = 4000;
    private long _nextAttempt;
    private long _lastObserved;
    private int _failures;
    private bool _waiting;

    internal bool ShouldAttempt(bool eligible, bool alreadyAttached, long now)
    {
        if (!eligible || alreadyAttached)
        {
            Reset();
            return false;
        }
        // A restarted clock/invalidated time origin must never lock the card
        // out until an obsolete absolute deadline is reached.
        if (_waiting && now < _lastObserved) Reset();
        _lastObserved = now;
        if (_waiting && now < _nextAttempt) return false;
        int delay = Math.Min(MaxDelayMilliseconds, 250 << Math.Min(_failures, 4));
        _failures = Math.Min(_failures + 1, 5);
        _waiting = true;
        _nextAttempt = now > long.MaxValue - delay ? long.MaxValue : now + delay;
        return true;
    }

    internal void Reset()
    {
        _waiting = false;
        _nextAttempt = 0;
        _lastObserved = 0;
        _failures = 0;
    }
}
