namespace Nexus.Shell.Services;

// UI-thread owned. The lease owns an OS power request, not a persisted power
// plan. A monotonic clock prevents clock/time-zone edits extending the timer.
public sealed class KeepAwakeSession(Func<bool, IDisposable> acquire, TimeProvider? clock = null) : IDisposable
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private IDisposable? _lease;
    private long _started;
    private TimeSpan _duration;
    private bool _disposed;
    public static IReadOnlyList<int> Durations { get; } = Array.AsReadOnly(new[] { 15, 30, 60, 120 });
    public bool Active => _lease is not null;
    public bool KeepDisplayOn { get; private set; }
    public string Status { get; private set; } = "Off · Windows manages sleep normally.";
    public TimeSpan Remaining => !Active ? TimeSpan.Zero
        : TimeSpan.FromTicks(Math.Clamp((_duration - _clock.GetElapsedTime(_started)).Ticks, 0, _duration.Ticks));

    public void Start(int minutes, bool keepDisplayOn)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Durations.Contains(minutes)) throw new ArgumentOutOfRangeException(nameof(minutes));
        // Keep an existing session intact if Windows declines its replacement.
        var next = acquire(keepDisplayOn) ?? throw new InvalidOperationException("Windows did not create a power request.");
        var previous = _lease;
        _lease = next; _started = _clock.GetTimestamp(); _duration = TimeSpan.FromMinutes(minutes);
        KeepDisplayOn = keepDisplayOn; UpdateStatus();
        previous?.Dispose();
    }
    public bool Tick()
    {
        if (!Active) return false;
        if (Remaining == TimeSpan.Zero) { Stop("Finished · Windows manages sleep normally."); return true; }
        string before = Status; UpdateStatus(); return before != Status;
    }
    private void UpdateStatus() => Status = $"Keep awake · {Math.Max(1, (int)Math.Ceiling(Remaining.TotalMinutes))} min left · "
        + (KeepDisplayOn ? "display stays on" : "display may turn off");
    public void Stop(string status = "Off · Windows manages sleep normally.")
    {
        var lease = _lease; _lease = null; KeepDisplayOn = false; Status = status;
        lease?.Dispose();
    }
    public void Dispose() { if (_disposed) return; _disposed = true; Stop(); }
}
