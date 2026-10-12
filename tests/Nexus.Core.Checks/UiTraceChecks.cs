using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using Nexus.Shell.Services;

internal static class UiTraceChecks
{
    private sealed class Listener : EventListener
    {
        internal readonly ConcurrentQueue<EventWrittenEventArgs> Records = new();
        protected override void OnEventSourceCreated(EventSource source)
        { if (source.Name == "Nexus-Shell-UI") EnableEvents(source, EventLevel.Verbose); }
        protected override void OnEventWritten(EventWrittenEventArgs args) => Records?.Enqueue(args);
    }
    internal static void Run()
    {
        _ = UiTrace.Source;
        using var listener = new Listener();
        UiTrace.Surface("native-size", "Files", new IntPtr(123), 800, 540, 1);
        UiTrace.MoveSize("exit", "Files", new IntPtr(123), false, 20, 19, 140.25);
        var events = listener.Records.ToArray();
        if (events.Any(e => e.EventId == 0)) throw new Exception("ETW schema registration failed: " + string.Join("; ", events.Where(e => e.EventId == 0).Select(e => e.Message)));
        if (events.Length != 2 || events[0].EventId != 1 || events[1].EventId != 2) throw new Exception("UI diagnostics must emit both typed events exactly once.");
        if (events[0].Payload?.Count != 7 || (int)events[0].Payload![3]! != Environment.ProcessId
            || (int)events[0].Payload![4]! != 800 || (long)events[0].Payload![2]! != 123) throw new Exception("The surface ETW event must retain native geometry and owner identity.");
        if (events[1].Payload?.Count != 8 || (bool)events[1].Payload![4]! || (int)events[1].Payload![5]! != 20
            || (double)events[1].Payload![7]! != 140.25) throw new Exception("The move-size event must retain actual release state, counts and duration.");
        listener.DisableEvents(UiTrace.Source);
        int before = listener.Records.Count;
        UiTrace.Surface("native-size", "Files", new IntPtr(123), 900, 600);
        if (listener.Records.Count != before) throw new Exception("Disabled UI diagnostics must stop recording events.");
        Console.WriteLine("PASS: UI ETW schema, owner/geometry payload, mouse-state/duration payload and disabled listener checks.");
    }
}
