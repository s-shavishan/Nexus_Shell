namespace Nexus.Runtime;

public sealed class RuntimeRestartBudget(TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly Queue<long> _starts = new();
    public bool TryStart()
    {
        long now = _clock.GetTimestamp();
        while (_starts.TryPeek(out long previous) && _clock.GetElapsedTime(previous, now) >= TimeSpan.FromMinutes(1)) _starts.Dequeue();
        if (_starts.Count >= 3) return false;
        _starts.Enqueue(now); return true;
    }
}
