using System.Diagnostics;

namespace Nexus.Shell.Services;

// A monotonic clock keeps a session correct across wall-clock changes and delayed
// UI ticks. The tick only paints the remaining time; it does not count seconds.
public sealed class FocusSession
{
    private readonly Func<long> _timestamp;
    private readonly double _frequency;
    private long _started;
    private TimeSpan _elapsed;
    public TimeSpan Duration { get; private set; } = TimeSpan.FromMinutes(25);
    public bool IsRunning { get; private set; }
    public bool IsComplete => Remaining == TimeSpan.Zero;
    public TimeSpan Remaining
    {
        get
        {
            var elapsed = _elapsed + (IsRunning
                ? TimeSpan.FromSeconds(Math.Max(0, (_timestamp() - _started) / _frequency)) : TimeSpan.Zero);
            return elapsed >= Duration ? TimeSpan.Zero : Duration - elapsed;
        }
    }

    public FocusSession(Func<long>? timestamp = null, long? frequency = null)
    {
        _timestamp = timestamp ?? (() => Stopwatch.GetTimestamp());
        _frequency = frequency ?? Stopwatch.Frequency;
        if (_frequency <= 0) throw new ArgumentOutOfRangeException(nameof(frequency));
    }
    public void Reset(int minutes)
    {
        if (minutes is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(minutes));
        IsRunning = false; _elapsed = TimeSpan.Zero; Duration = TimeSpan.FromMinutes(minutes);
    }
    public void Start()
    {
        if (IsRunning) return;
        if (IsComplete) _elapsed = TimeSpan.Zero;
        _started = _timestamp(); IsRunning = true;
    }
    // Restoring never starts a clock. Time away from Nexus is not counted as focus.
    public void Restore(int minutes, TimeSpan remaining)
    {
        if (minutes is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(minutes));
        if (remaining < TimeSpan.Zero || remaining > TimeSpan.FromMinutes(minutes))
            throw new ArgumentOutOfRangeException(nameof(remaining));
        Reset(minutes);
        _elapsed = Duration - remaining;
    }
    public void Pause()
    {
        if (!IsRunning) return;
        _elapsed = Duration - Remaining; IsRunning = false;
    }
    public bool CompleteIfDue()
    {
        if (!IsRunning || !IsComplete) return false;
        IsRunning = false; _elapsed = Duration; return true;
    }
}
