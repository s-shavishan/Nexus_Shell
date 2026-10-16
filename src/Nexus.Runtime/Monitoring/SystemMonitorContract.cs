// නමෝ බුද්ධාය 🤍
namespace Nexus.Runtime.Monitoring;

public readonly record struct ProcessIdentity(int Id, long CreatedFileTime);
public sealed record ProcessObservation(ProcessIdentity Identity, string Name, string ImagePath,
    ulong? CpuTicks, long Timestamp, ulong? WorkingSet, ulong? PrivateBytes,
    bool SameUser, bool SameSession, bool? Critical, string Unavailable = "");
public sealed record SystemCpuCounters(ulong Idle, ulong Kernel, ulong User);
public sealed record SystemObservation(SystemCpuCounters? Cpu, ulong TotalMemory, ulong AvailableMemory,
    TimeSpan Uptime, ProcessObservation[] Processes, string Notice = "");
public sealed record MonitoredProcess(ProcessObservation Observation, double? CpuPercent)
{
    public ProcessIdentity Identity => Observation.Identity;
}
public sealed record SystemMonitorSnapshot(double? CpuPercent, ulong TotalMemory, ulong AvailableMemory,
    TimeSpan Uptime, MonitoredProcess[] Processes, string Notice);
public sealed record StorageVolume(string Root, string Kind, ulong? Capacity, ulong? Available,
    ulong? VolumeFree, string Detail)
{
    // GetDiskFreeSpaceEx capacity/availability are for the calling account,
    // which can have a quota. Do not call the complement physical disk use.
    public double? AvailablePercent => Capacity is > 0 && Available is { } free
        ? Math.Clamp((double)free / Capacity.Value * 100, 0, 100) : null;
}
public sealed record StorageSnapshot(StorageVolume[] Volumes, string Notice = "");
public enum ProcessSort { Cpu, Memory, Name, Id }
public enum ProcessAction { CloseWindows, EndProcess }
public sealed record ProcessActionResult(bool Requested, bool Exited, string Detail);

public interface ISystemMonitorSource
{
    SystemObservation Read(CancellationToken cancellation);
    StorageSnapshot ReadStorage(CancellationToken cancellation);
}
public interface IProcessActionTarget : IDisposable
{
    ProcessObservation Read();
    ProcessActionResult CloseWindows();
    ProcessActionResult EndProcess();
}
public interface IProcessActionSource
{
    IProcessActionTarget Open(ProcessIdentity identity, ProcessAction action);
}
