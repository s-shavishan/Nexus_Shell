using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace Nexus.Shell.Services;

// Opt-in ETW only: no disk I/O, polling, window titles, document paths or
// hardware identifiers on the UI thread. A trace records actual native events.
[EventSource(Name = "Nexus-Shell-UI", Guid = "7450d6b3-7228-4b28-b6cf-97df652c72d1")]
internal sealed class UiTrace : EventSource
{
    internal static readonly UiTrace Source = new();
    [NonEvent]
    internal static void Surface(string stage, string role, IntPtr handle, int width = 0, int height = 0, int value = 0)
    {
        if (Source.IsEnabled()) Source.SurfaceEvent(stage, role, handle.ToInt64(), Environment.ProcessId, width, height, value);
    }
    [Event(1, Level = EventLevel.Informational)]
    public void SurfaceEvent(string stage, string role, long handle, int processId, int width, int height, int value)
        => WriteEvent(1, stage, role, handle, processId, width, height, value);
    [NonEvent]
    internal static void MoveSize(string stage, string role, IntPtr handle, bool leftDown, int sizes, int regions, double durationMs)
    {
        if (Source.IsEnabled()) Source.MoveSizeEvent(stage, role, handle.ToInt64(), Environment.ProcessId, leftDown, sizes, regions, durationMs);
    }
    [Event(2, Level = EventLevel.Informational)]
    public void MoveSizeEvent(string stage, string role, long handle, int processId, bool leftDown, int sizes, int regions, double durationMs)
        => WriteEvent(2, stage, role, handle, processId, leftDown, sizes, regions, durationMs);
    [NonEvent]
    internal static WorkScope Measure(string stage, string role, int items = 0)
        => Source.IsEnabled() ? new(stage, role, items) : default;
    [Event(3, Level = EventLevel.Informational)]
    public void WorkEvent(string stage, string role, int processId, int threadId, int items, double durationMs, long allocatedBytes)
        => WriteEvent(3, stage, role, processId, threadId, items, durationMs, allocatedBytes);
    [NonEvent]
    internal static void Quality(string role, GlassQualityDecision decision)
    {
        if (Source.IsEnabled()) Source.QualityEvent(role, decision.Requested, decision.Effective, decision.Reason, Environment.ProcessId);
    }
    [Event(4, Level = EventLevel.Informational)]
    public void QualityEvent(string role, string requested, string effective, string reason, int processId)
        => WriteEvent(4, role, requested, effective, reason, processId);

    // Synchronous work only. Disabled tracing has no stopwatch, allocations,
    // timers or disk writes. A cross-thread disposal marks allocation unknown.
    internal readonly struct WorkScope : IDisposable
    {
        private readonly string? _stage, _role;
        private readonly int _items, _thread;
        private readonly long _started, _allocated;
        internal WorkScope(string stage, string role, int items)
        {
            _stage = stage; _role = role; _items = items;
            _thread = Environment.CurrentManagedThreadId;
            _allocated = GC.GetAllocatedBytesForCurrentThread(); _started = Stopwatch.GetTimestamp();
        }
        public void Dispose()
        {
            if (_stage is null || !Source.IsEnabled()) return;
            long bytes = _thread == Environment.CurrentManagedThreadId ? GC.GetAllocatedBytesForCurrentThread() - _allocated : -1;
            Source.WorkEvent(_stage, _role!, Environment.ProcessId, _thread, _items, Stopwatch.GetElapsedTime(_started).TotalMilliseconds, bytes);
        }
    }
}
