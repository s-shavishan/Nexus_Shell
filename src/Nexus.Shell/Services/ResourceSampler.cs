using System.Diagnostics;

namespace Nexus.Shell.Services;

public sealed record ResourceSnapshot(double WorkingSetMB, double PrivateMB, double? CpuPercent);

public sealed class ResourceSampler : IDisposable
{
    private readonly Process _process = Process.GetCurrentProcess();
    private TimeSpan? _previousCpu;
    private long _previousTimestamp;

    public void Reset() { _previousCpu = null; _previousTimestamp = 0; }

    public ResourceSnapshot Sample()
    {
        _process.Refresh();
        var cpu = _process.TotalProcessorTime;
        long timestamp = Stopwatch.GetTimestamp();
        double elapsed = _previousTimestamp == 0 ? 0 : Stopwatch.GetElapsedTime(_previousTimestamp, timestamp).TotalSeconds;
        double? percent = _previousCpu is not null && elapsed is > 0 and < 15
            ? Math.Clamp((cpu - _previousCpu.Value).TotalSeconds / elapsed / Environment.ProcessorCount * 100, 0, 100) : null;
        _previousCpu = cpu; _previousTimestamp = timestamp;
        return new(_process.WorkingSet64 / 1048576d, _process.PrivateMemorySize64 / 1048576d, percent);
    }

    public void Dispose() => _process.Dispose();
}
