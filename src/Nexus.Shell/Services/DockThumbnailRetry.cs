namespace Nexus.Shell.Services;

// Per-preview, UI-thread-only backoff for transient DWM registration failures.
// Foreground/window lifecycle polling can be as fast as 80 ms; do not retry
// native DWM operations on every tick, but never strand an open preview either.
internal sealed class DockThumbnailRetry
{
    internal const int MaxDelayMilliseconds = 4000;
    private long _nextAttempt;
    private int _failures;
    private bool _waiting;

    internal bool ShouldAttempt(bool eligible, bool alreadyAttached, long now)
    {
        if (!eligible || alreadyAttached)
        {
            Reset();
            return false;
        }
        if (_waiting && now >= 0 && now < _nextAttempt) return false;
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
        _failures = 0;
    }
}
