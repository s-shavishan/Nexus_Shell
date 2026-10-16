namespace Nexus.Runtime.Monitoring;

// At most one OS read per lane across views, including a closed view whose
// driver call has not returned. Never queue an unbounded trail of Task.Run work.
public sealed class SystemMonitorReader(ISystemMonitorSource source)
{
    private static readonly SemaphoreSlim Processes = new(1, 1), Storage = new(1, 1);
    public Task<SystemObservation?> ReadAsync(CancellationToken cancellation)
        => TryRead(Processes, () => source.Read(cancellation), cancellation);
    public Task<StorageSnapshot?> ReadStorageAsync(CancellationToken cancellation)
        => TryRead(Storage, () => source.ReadStorage(cancellation), cancellation);
    private static async Task<T?> TryRead<T>(SemaphoreSlim lane, Func<T> read, CancellationToken cancellation) where T : class
    {
        cancellation.ThrowIfCancellationRequested();
        if (!lane.Wait(0)) return null;
        try
        {
            return await Task.Run(() => { cancellation.ThrowIfCancellationRequested(); var value = read(); cancellation.ThrowIfCancellationRequested(); return value; }).ConfigureAwait(false);
        }
        finally { lane.Release(); }
    }
}
