using Nexus.Shell.Models;
using Nexus.Shell.Services;

internal static class PcControlChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var before = new CpuTimes(100, 200, 100);
        check(PcMetrics.CpuPercent(before, new(180, 310, 150)) == 50, "Kernel time includes idle; a sample must report the busy fraction.");
        check(PcMetrics.CpuPercent(null, before) is null && PcMetrics.CpuPercent(before, before) is null, "First and zero-duration CPU samples must not invent a reading.");
        check(PcMetrics.CpuPercent(before, new(1, 1, 1)) is null, "Reset counters must discard the CPU baseline.");
        foreach (var area in new[] { new WindowRect(0, 40, 1919, 1039), new WindowRect(-1920, -400, 1919, 1079) })
        foreach (string layout in new[] { "Columns", "Stack", "Grid" })
        foreach (int count in new[] { 1, 2, 3, 4 })
        {
            var plan = WindowLayouts.Plan(area, count, layout);
            check(plan.Length == count && plan.All(r => r.Width > 0 && r.Height > 0 && r.X >= area.X && r.Y >= area.Y
                && r.X + r.Width <= area.X + area.Width && r.Y + r.Height <= area.Y + area.Height), "Layouts must stay inside the monitor work area, including negative origins.");
            for (int i = 0; i < count; i++) for (int j = i + 1; j < count; j++)
                check(plan[i].X + plan[i].Width <= plan[j].X || plan[j].X + plan[j].Width <= plan[i].X
                    || plan[i].Y + plan[i].Height <= plan[j].Y || plan[j].Y + plan[j].Height <= plan[i].Y, "Selected windows must not overlap.");
            if (layout != "Grid" || count != 3)
                check(plan.Sum(r => (long)r.Width * r.Height) == (long)area.Width * area.Height, "A full layout must cover the available work area without rounding gaps.");
        }
        var service = new WindowLayouts();
        bool rejected = false;
        try { service.Arrange([new RunningWindow(IntPtr.Zero, "Closed app", "old", 123)], "Columns"); }
        catch (InvalidOperationException) { rejected = true; }
        check(rejected && !service.CanUndo, "A stale window must be rejected before any native placement or undo mutation.");
        var state = new ShellState { LastPage = "PC controls" }; DesktopWorkspace.Normalize(state);
        check(state.LastPage == "PC controls" && state.Snapshot().LastPage == "PC controls", "The new workspace must survive normalization and snapshots.");
        Console.WriteLine("PASS: PC CPU counter semantics, bounded monitor layouts, stale-window rejection and workspace recovery.");
        if (OperatingSystem.IsWindows()) NativeReadChecks(check);
    }
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void NativeReadChecks(Action<bool, string> check)
    {
        CpuTimes? previous = null;
        var first = PcMetrics.Read(ref previous); var second = PcMetrics.Read(ref previous);
        check(first.TotalMemory > 0 && second.AvailableMemory <= second.TotalMemory, "The real Windows memory query must return physical memory bounds.");
        var audio = new AudioController();
        try
        {
            var snapshot = audio.ReadAsync().WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            if (snapshot.Available)
                check(snapshot.DeviceId.Length > 0 && snapshot.Device.Length > 0 && float.IsFinite(snapshot.Volume)
                    && snapshot.Volume is >= 0 and <= 1 && snapshot.Apps.Length <= 32, "Read-only Core Audio must return bounded output/session data.");
            Console.WriteLine(snapshot.Available ? "PASS: actual Core Audio read on the Windows build host (no levels changed)."
                : "SKIP: build host has no available audio output; unavailable-state response checked, device acceptance still required.");
            check(snapshot.Available || snapshot.Message.Length > 0, "Unavailable audio must carry a usable explanation.");
            audio.SuspendAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
        finally { audio.Dispose(); }
        audio.Completion.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
    }
}
