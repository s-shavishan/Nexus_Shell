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
}
