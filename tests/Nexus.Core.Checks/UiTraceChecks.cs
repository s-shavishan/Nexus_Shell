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
        using (UiTrace.Measure("launchpad-render", "Launchpad", 24))
        { GC.KeepAlive(new byte[256]); Thread.SpinWait(1000); }
        var work = listener.Records.Last();
        if (work.EventId != 3 || work.Payload?.Count != 7 || (int)work.Payload![2]! != Environment.ProcessId
            || (int)work.Payload[3]! != Environment.CurrentManagedThreadId || (int)work.Payload[4]! != 24
            || (double)work.Payload[5]! < 0 || (long)work.Payload[6]! < 256)
            throw new Exception("UI work tracing lost its owner, item count, duration or actual allocation payload.");
        UiTrace.Quality("Dock", new("Ultra", "Solid", "Windows backdrop fallback"));
        var quality = listener.Records.Last();
        if (quality.EventId != 4 || quality.Payload?.Count != 5 || (string)quality.Payload![0]! != "Dock"
            || (string)quality.Payload[1]! != "Ultra" || (string)quality.Payload[2]! != "Solid"
            || (string)quality.Payload[3]! != "Windows backdrop fallback" || (int)quality.Payload[4]! != Environment.ProcessId)
            throw new Exception("Quality diagnostics lost the requested/effective tier or owner.");
        UiTrace.WindowDispatch(foreground: false, enumeration: false, discovery: false);
        var geometry = listener.Records.Last();
        if (geometry.EventId != 5 || geometry.Payload?.Count != 4
            || (int)geometry.Payload![0]! != Environment.ProcessId
            || (bool)geometry.Payload[1]! || (bool)geometry.Payload[2]! || (bool)geometry.Payload[3]!)
            throw new Exception("The no-enumeration native event trace must retain its coalesced geometry-only classification.");
        UiTrace.WindowDispatch(foreground: true, enumeration: true, discovery: false);
        var structural = listener.Records.Last();
        if (structural.EventId != 5 || structural.Payload?.Count != 4
            || !(bool)structural.Payload![1]! || !(bool)structural.Payload[2]! || (bool)structural.Payload[3]!)
            throw new Exception("The native event trace must retain foreground and structural invalidation independently.");
        UiTrace.WindowDispatch(foreground: true, enumeration: false, discovery: true);
        var fallback = listener.Records.Last();
        if (fallback.EventId != 5 || fallback.Payload?.Count != 4
            || !(bool)fallback.Payload![1]! || (bool)fallback.Payload[2]! || !(bool)fallback.Payload[3]!)
            throw new Exception("Missed-create foreground discovery must remain observable in the native event trace.");
        listener.DisableEvents(UiTrace.Source);
        int before = listener.Records.Count;
        UiTrace.Surface("native-size", "Files", new IntPtr(123), 900, 600);
        UiTrace.Quality("Dock", new("Ultra", "Solid", "Windows backdrop fallback"));
        UiTrace.WindowDispatch(foreground: false, enumeration: false, discovery: false);
        if (listener.Records.Count != before) throw new Exception("Disabled UI diagnostics must stop recording events.");
        // Warm the disabled path before measuring its per-call cost.
        for (int i = 0; i < 10; i++) { using var scope = UiTrace.Measure("disabled", "Probe"); }
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { using var scope = UiTrace.Measure("disabled", "Probe"); }
        if (GC.GetAllocatedBytesForCurrentThread() != allocated || listener.Records.Count != before)
            throw new Exception("Disabled work tracing allocated memory or emitted events.");
        Console.WriteLine("PASS: UI ETW schema, owner/geometry/release payload, synchronous work timing/allocations and 1000 allocation-free disabled scopes.");
    }
}
