namespace Nexus.Runtime;

// One in-flight operation per hardware section. A timed-out native call retains
// its lane until it actually ends, so repeated Refresh cannot pile up workers.
// Commands are never automatically replayed after a timeout or Core restart.
public sealed class SystemSettingsCoordinator(ISystemSettingsBackend backend, TimeSpan? deadline = null)
{
    private readonly Dictionary<string, SemaphoreSlim> _lanes = SettingsRules.Sections.ToDictionary(section => section, _ => new SemaphoreSlim(1, 1));
    public Guid Epoch { get; } = Guid.NewGuid();
    public async Task<SettingsSnapshot> ExecuteAsync(SettingsRequest request, CancellationToken cancellation)
    {
        SettingsRules.Validate(request, Epoch); cancellation.ThrowIfCancellationRequested();
        var lane = _lanes[request.Section];
        if (!await lane.WaitAsync(0, cancellation).ConfigureAwait(false)) throw new RuntimeFailure("settings-busy", "This device is still responding. Wait, then Refresh before trying again.");
        Task<SettingsSnapshot>? pending = null;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        stop.CancelAfter(deadline ?? TimeSpan.FromSeconds(8));
        try
        {
            pending = backend.ExecuteAsync(request, stop.Token);
            var result = await pending.WaitAsync(stop.Token).ConfigureAwait(false);
            if (result.Section != request.Section) throw new RuntimeFailure("settings-state", "The device service returned the wrong section. Refresh before changing a device.");
            return result with { Epoch = Epoch };
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { throw new RuntimeFailure("settings-timeout", "The device did not confirm this request. Refresh its actual state before making another change."); }
        finally
        {
            if (pending is { IsCompleted: false })
                _ = pending.ContinueWith(task => { _ = task.Exception; lane.Release(); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            else { if (pending?.IsFaulted == true) _ = pending.Exception; lane.Release(); }
        }
    }
}
