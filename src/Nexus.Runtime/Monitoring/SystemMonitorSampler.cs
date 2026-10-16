namespace Nexus.Runtime.Monitoring;

// One view owns one sampler. No history, executable paths or counters are saved.
public sealed class SystemMonitorSampler(TimeProvider? time = null, int? processors = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly int _processors = Math.Max(1, processors ?? Environment.ProcessorCount);
    private Dictionary<ProcessIdentity, ProcessObservation> _previous = [];
    private SystemCpuCounters? _system;
    public void Reset() { _previous.Clear(); _system = null; }
    public SystemMonitorSnapshot Sample(SystemObservation input)
    {
        var current = new Dictionary<ProcessIdentity, ProcessObservation>();
        var rows = new List<MonitoredProcess>();
        foreach (var process in input.Processes.Take(8192))
        {
            if (!current.TryAdd(process.Identity, process)) continue;
            double? cpu = null;
            if (process.Identity.CreatedFileTime > 0 && _previous.TryGetValue(process.Identity, out var before)
                && before.CpuTicks is { } old && process.CpuTicks is { } next && next >= old
                && process.Timestamp > before.Timestamp)
            {
                double elapsed = _time.GetElapsedTime(before.Timestamp, process.Timestamp).TotalSeconds;
                if (elapsed is >= 0.01 and <= 10)
                    cpu = Math.Clamp((next - old) / (double)TimeSpan.TicksPerSecond / elapsed / _processors * 100, 0, 100);
            }
            rows.Add(new(process, cpu));
        }
        double? totalCpu = null;
        if (_system is { } prior && input.Cpu is { } now && now.Idle >= prior.Idle
            && now.Kernel >= prior.Kernel && now.User >= prior.User)
        {
            double total = (double)(now.Kernel - prior.Kernel) + (now.User - prior.User);
            double idle = now.Idle - prior.Idle;
            if (total > 0 && idle <= total) totalCpu = (total - idle) / total * 100;
        }
        _system = input.Cpu; _previous = current;
        return new(totalCpu, input.TotalMemory, Math.Min(input.TotalMemory, input.AvailableMemory),
            input.Uptime, rows.ToArray(), input.Notice);
    }
    public static MonitoredProcess[] Select(IEnumerable<MonitoredProcess> processes, string query, bool mine, ProcessSort sort)
    {
        string[] words = (query.Length > 160 ? query[..160] : query).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var selected = processes.Where(row => (!mine || row.Observation.SameUser && row.Observation.SameSession)
            && words.All(word => (row.Observation.Name + " " + row.Identity.Id + " " + row.Observation.ImagePath)
                .Contains(word, StringComparison.OrdinalIgnoreCase)));
        IOrderedEnumerable<MonitoredProcess> ordered = sort switch
        {
            ProcessSort.Memory => selected.OrderByDescending(row => row.Observation.WorkingSet),
            ProcessSort.Name => selected.OrderBy(row => row.Observation.Name, StringComparer.CurrentCultureIgnoreCase),
            ProcessSort.Id => selected.OrderBy(row => row.Identity.Id),
            _ => selected.OrderByDescending(row => row.CpuPercent)
        };
        return ordered.ThenBy(row => row.Observation.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(row => row.Identity.Id).ToArray();
    }
}
