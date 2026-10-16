using System.Diagnostics;
using Nexus.Runtime.Monitoring;
using Nexus.Shell.Interop;

internal static class SystemMonitorChecks
{
    private static int _checks;
    private static void Check(bool value, string detail) { _checks++; if (!value) throw new Exception(detail); }
    private sealed class Clock : TimeProvider
    { public override long TimestampFrequency => TimeSpan.TicksPerSecond; }
    private static ProcessObservation Row(int id = 20, long created = 100, ulong? cpu = 100, long timestamp = 100,
        string name = "Editor.exe", bool mine = true, bool session = true, bool? critical = false)
        => new(new(id, created), name, @"C:\Apps\" + name, cpu, timestamp, 1000, 2000, mine, session, critical);
    private static SystemObservation Sample(params ProcessObservation[] rows) => new(new(100, 200, 100), 1000, 500, TimeSpan.FromHours(2), rows);
    internal static async Task RunAsync()
    {
        _checks = 0;
        var sampler = new SystemMonitorSampler(new Clock(), 4);
        var first = sampler.Sample(Sample(Row())); Check(first.CpuPercent is null && first.Processes[0].CpuPercent is null, "The first observation must not invent CPU percentages.");
        var second = sampler.Sample(Sample(Row(cpu: 10000100, timestamp: 10000100)) with { Cpu = new(150, 300, 200) });
        Check(second.Processes[0].CpuPercent == 25 && second.CpuPercent == 75, "Per-process CPU must be normalized across cores; system idle includes kernel time.");
        Check(sampler.Sample(Sample(Row(cpu: 0, timestamp: 20000100))).Processes[0].CpuPercent is null, "Backwards CPU counters cannot produce use.");
        Check(sampler.Sample(Sample(Row(cpu: 100, timestamp: 20000100))).Processes[0].CpuPercent is null, "Repeated timestamps cannot produce use.");
        Check(sampler.Sample(Sample(Row(cpu: 200, timestamp: 100))).Processes[0].CpuPercent is null, "Backwards monotonic time invalidates the baseline.");
        Check(sampler.Sample(Sample(Row(cpu: 100000000, timestamp: 200000000))).Processes[0].CpuPercent is null, "A sleep or stale sampling gap must not masquerade as a current rate.");
        sampler.Reset(); Check(sampler.Sample(Sample(Row())).Processes[0].CpuPercent is null, "Resume must start a fresh baseline.");
        Check(sampler.Sample(Sample(Row(created: 200, cpu: 900, timestamp: 10000100))).Processes[0].CpuPercent is null, "A reused PID is a distinct process with a new baseline.");
        sampler.Sample(Sample()); Check(sampler.Sample(Sample(Row(created: 200, cpu: 1000, timestamp: 20000100))).Processes[0].CpuPercent is null, "Exited process baselines must be removed.");
        sampler.Sample(Sample(Row(created: 0))); Check(sampler.Sample(Sample(Row(created: 0, timestamp: 10000100))).Processes[0].CpuPercent is null, "Unknown identities cannot accumulate CPU.");
        sampler.Reset(); sampler.Sample(Sample(Row()));
        Check(sampler.Sample(Sample(Row(cpu: ulong.MaxValue, timestamp: 10000100))).Processes[0].CpuPercent == 100, "Oversized deltas must be bounded.");
        Check(sampler.Sample(Sample(Row()) with { AvailableMemory = 2000 }).AvailableMemory == 1000, "Racing memory counters must not underflow used memory.");
        Check(sampler.Sample(Sample(Row()) with { Cpu = new(0, 0, 0) }).CpuPercent is null, "Reset system CPU counters must invalidate their baseline.");
        Check(sampler.Sample(Sample(Row()) with { Cpu = null }).CpuPercent is null, "Missing system CPU counters must remain unavailable.");
        Check(sampler.Sample(Sample(Row(), Row())).Processes.Length == 1, "Duplicate native entries must not duplicate rows.");
        Check(sampler.Sample(Sample(Enumerable.Range(10, 8300).Select(id => Row(id: id)).ToArray())).Processes.Length == 8192, "Process materialization must stay bounded.");

        MonitoredProcess[] rows = [new(Row(20, name: "Beta.exe"), null), new(Row(21, name: "Alpha.exe") with { WorkingSet = 2000 }, 30), new(Row(22, name: "Gamma.exe", mine: false), 10), new(Row(23, name: "Other.exe", session: false), 20)];
        Check(SystemMonitorSampler.Select(rows, "", false, ProcessSort.Cpu).Select(r => r.Identity.Id).SequenceEqual(new[] { 21, 23, 22, 20 }), "Unknown CPU values sort after measured values.");
        Check(SystemMonitorSampler.Select(rows, "", false, ProcessSort.Memory)[0].Identity.Id == 21, "Memory sorting uses working set.");
        Check(SystemMonitorSampler.Select(rows, "", false, ProcessSort.Name)[0].Identity.Id == 21, "Process names sort alphabetically.");
        Check(SystemMonitorSampler.Select(rows, "", false, ProcessSort.Id)[0].Identity.Id == 20, "PID sorting must use numeric IDs.");
        Check(SystemMonitorSampler.Select(rows, "", true, ProcessSort.Id).Length == 2, "My session requires both owner and session match.");
        foreach (string query in new[] { "alpha", "21", @"C:\Apps\Alpha", "alpha 21", "  ALPHA   exe " })
            Check(SystemMonitorSampler.Select(rows, query, false, ProcessSort.Name).Single().Identity.Id == 21, "Literal process filters must match name, PID and path.");
        Check(SystemMonitorSampler.Select(rows, "nothing", false, ProcessSort.Cpu).Length == 0, "No matching processes must not expose an old selection.");
        Check(SystemMonitorSampler.Select(rows, new string('x', 10000), false, ProcessSort.Cpu).Length == 0, "Large filter text must stay bounded.");
        Check(new StorageVolume("C:\\", "Local", 1000, 250, 300, "").AvailablePercent == 25, "Storage reports available-to-account percentage.");
        Check(new StorageVolume("C:\\", "Local", 1000, 2000, 2000, "").AvailablePercent == 100, "Racing/quota availability is bounded.");
        Check(new StorageVolume("E:\\", "Removable", 0, 0, 0, "").AvailablePercent is null && new StorageVolume("E:\\", "Removable", null, null, null, "").AvailablePercent is null, "Zero or unavailable drive capacity cannot look like a full drive.");
        Actions(); await ReadersAsync(); NativeReadOnly();
        Console.WriteLine($"PASS: {_checks} System Monitor assertions; CPU identities/counter resets, process search/sorting, guarded actions and bounded independent read lanes.");
    }
    private static void Actions()
    {
        var valid = Row(); Check(ProcessActionRules.Refusal(valid, 999) == "", "An identified noncritical process in this user's session is controllable.");
        foreach (var denied in new[] { Row(id: 0), Row(id: 4), Row(created: 0), Row(mine: false), Row(session: false), Row(critical: true), Row(critical: null), Row() with { ImagePath = "" }, Row() with { Unavailable = "Denied" } })
        {
            var source = new ActionSource(denied); var result = ProcessActions.Execute(source, denied.Identity, ProcessAction.EndProcess, 999);
            Check(!result.Requested && source.Calls == 0, "Denied metadata must never reach an OS mutation.");
        }
        Check(ProcessActionRules.Refusal(valid, 20).Length > 0, "The shell process cannot end itself.");
        foreach (string name in new[] { "EXPLORER.EXE", "dwm.exe", "svchost.exe", "Nexus.Shell.exe", "Nexus.Core", "Nexus.DesktopHost.exe", "Nexus", "winlogon.exe", "lsass.exe", "ShellExperienceHost.exe", "StartMenuExperienceHost.exe", "RuntimeBroker.exe", "csrss", "services", "fontdrvhost.exe" })
        {
            var source = new ActionSource(Row(name: name));
            Check(!ProcessActions.Execute(source, valid.Identity, ProcessAction.EndProcess, 999).Requested && source.Calls == 0 && source.Disposed, "Desktop infrastructure must not be controllable from this surface.");
        }
        var stale = new ActionSource(Row(created: 101)); Check(!ProcessActions.Execute(stale, valid.Identity, ProcessAction.EndProcess, 999).Requested && stale.Calls == 0 && stale.Disposed, "PID reuse during confirmation must refuse the action and release its handle.");
        foreach (var action in Enum.GetValues<ProcessAction>())
        {
            var source = new ActionSource(valid); var result = ProcessActions.Execute(source, valid.Identity, action, 999);
            Check(result.Requested && source.Calls == 1 && source.LastAction == action && source.Reads == 1 && source.Disposed, "Validate and act exactly once through the same retained target.");
        }
        var invalid = new ActionSource(valid); Check(!ProcessActions.Execute(invalid, valid.Identity, (ProcessAction)999, 999).Requested && invalid.Opens == 0, "Invalid actions must be refused before opening the OS target.");
        var fails = new ActionSource(valid) { ThrowRead = true };
        try { ProcessActions.Execute(fails, valid.Identity, ProcessAction.EndProcess, 999); throw new Exception("Expected a failed native query."); }
        catch (IOException) { Check(fails.Disposed && fails.Calls == 0, "An authoritative re-read failure must close the handle without action."); }
    }
    private sealed class ActionSource(ProcessObservation observed) : IProcessActionSource
    {
        private readonly ProcessObservation _observed = observed;
        public int Calls, Reads, Opens; public bool Disposed, ThrowRead; public ProcessAction LastAction;
        public IProcessActionTarget Open(ProcessIdentity identity, ProcessAction action) { Opens++; return new Target(this); }
        private sealed class Target(ActionSource owner) : IProcessActionTarget
        {
            public ProcessObservation Read() { owner.Reads++; if (owner.ThrowRead) throw new IOException("Unavailable"); return owner._observed; }
            public ProcessActionResult CloseWindows() { owner.LastAction = ProcessAction.CloseWindows; owner.Calls++; return new(true, false, "Requested only"); }
            public ProcessActionResult EndProcess() { owner.LastAction = ProcessAction.EndProcess; owner.Calls++; return new(true, true, "Ended"); }
            public void Dispose() => owner.Disposed = true;
        }
    }
    private sealed class Source : ISystemMonitorSource
    {
        public readonly ManualResetEventSlim Started = new(), Release = new(); public int Reads, StorageReads; public bool Fail;
        public SystemObservation Read(CancellationToken cancellation) { Interlocked.Increment(ref Reads); if (Fail) throw new IOException("Injected read failure"); return Sample(Row()); }
        public StorageSnapshot ReadStorage(CancellationToken cancellation) { Interlocked.Increment(ref StorageReads); Started.Set(); Release.Wait(TimeSpan.FromSeconds(5)); cancellation.ThrowIfCancellationRequested(); return new([]); }
    }
    private static async Task ReadersAsync()
    {
        var source = new Source(); var reader = new SystemMonitorReader(source); using var cancel = new CancellationTokenSource();
        var blocked = reader.ReadStorageAsync(cancel.Token); Check(source.Started.Wait(TimeSpan.FromSeconds(2)), "The first drive worker must start.");
        try
        {
            for (int i = 0; i < 30; i++) Check(await new SystemMonitorReader(source).ReadStorageAsync(CancellationToken.None) is null, "Reopening while a driver read is stuck must not accumulate workers.");
            Check((await reader.ReadAsync(CancellationToken.None))?.Processes.Length == 1 && source.Reads == 1, "A slow drive must not block the independent process lane.");
            cancel.Cancel(); Check(await reader.ReadStorageAsync(CancellationToken.None) is null, "Cancellation must not release a lane while native I/O is still running.");
        }
        finally { source.Release.Set(); }
        try { await blocked; throw new Exception("Expected a cancelled read."); } catch (OperationCanceledException) { Check(true, "An obsolete read discards its result."); }
        Check(await reader.ReadStorageAsync(CancellationToken.None) is not null && source.StorageReads == 2, "The lane is reusable after the driver returns.");
        source.Fail = true; try { await reader.ReadAsync(CancellationToken.None); throw new Exception("Expected read failure."); } catch (IOException) { Check(true, "Read failure is reported."); }
        source.Fail = false; Check(await reader.ReadAsync(CancellationToken.None) is not null, "A failed process read must release its lane.");
        using var preCancelled = new CancellationTokenSource(); preCancelled.Cancel(); int reads = source.Reads;
        try { await reader.ReadAsync(preCancelled.Token); throw new Exception("Expected cancellation."); } catch (OperationCanceledException) { Check(source.Reads == reads, "A pre-cancelled read must not enter the source."); }
    }
    private static void NativeReadOnly()
    {
        if (!OperatingSystem.IsWindows()) { Console.WriteLine("SKIP: native Windows System Monitor/process-token/drive checks require Windows."); return; }
        var source = new WindowsSystemMonitor(); var snapshot = source.Read(CancellationToken.None);
        var own = snapshot.Processes.Single(p => p.Identity.Id == Environment.ProcessId);
        Check(own.Identity.CreatedFileTime > 0 && own.ImagePath.Length > 0 && own.CpuTicks is not null && own.WorkingSet > 0, "Read real Windows process identity/path/times/memory from one handle.");
        Check(own.SameUser && own.SameSession && ProcessActionRules.Refusal(own, Environment.ProcessId).Length > 0, "Real token ownership/session works and self-control remains refused.");
        var storage = source.ReadStorage(CancellationToken.None); Check(storage.Volumes.Length > 0 && storage.Volumes.All(v => v.Kind != "Network" || v.Capacity is null), "Read-only Windows storage reports local drives and does not probe network capacity.");
    }
    internal static async Task NativeActionsAsync()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Disposable native process checks require Windows.");
        // Use the actual installed dotnet host rather than a Nexus-named
        // apphost, which the production infrastructure protection must refuse.
        string executable = Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", "dotnet.exe"));
        if (!File.Exists(executable)) throw new InvalidOperationException("The installed dotnet host is required for this disposable fixture.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(typeof(SystemMonitorChecks).Assembly.Location);
        start.ArgumentList.Add("--system-monitor-disposable-child"); start.ArgumentList.Add(Guid.NewGuid().ToString("N"));
        using var child = Process.Start(start) ?? throw new Exception("Could not start the disposable process.");
        try
        {
            await Task.Delay(200); var source = new WindowsSystemMonitor();
            var row = source.Read(CancellationToken.None).Processes.Single(p => p.Identity.Id == child.Id);
            Check(ProcessActionRules.Refusal(row, Environment.ProcessId) == "", "The test child must be positively identified as controllable.");
            var stale = ProcessActions.Execute(source, row.Identity with { CreatedFileTime = row.Identity.CreatedFileTime + 1 }, ProcessAction.EndProcess, Environment.ProcessId);
            Check(!stale.Requested && !child.HasExited, "Mismatched native creation time must leave the real child running.");
            var closed = ProcessActions.Execute(source, row.Identity, ProcessAction.CloseWindows, Environment.ProcessId);
            Check(!closed.Requested && !child.HasExited, "A windowless test child must not be killed by Close windows.");
            var ended = ProcessActions.Execute(source, row.Identity, ProcessAction.EndProcess, Environment.ProcessId);
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); Check(ended.Requested && child.HasExited, "Only the newly launched disposable child must end through the retained process handle.");
            Console.WriteLine("PASS: 4 opt-in Windows disposable-process identity/close/end assertions.");
        }
        finally { if (!child.HasExited) { child.Kill(); await child.WaitForExitAsync(); } }
    }
}
