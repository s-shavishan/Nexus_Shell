namespace Nexus.Runtime;

public sealed class CoreRouter(CoreStateRepository state, FilesCoordinator files, SystemSettingsCoordinator? settings = null)
{
    private int _stop;
    public bool StopRequested => Volatile.Read(ref _stop) != 0;
    public async Task<RuntimeResponse> DispatchAsync(RuntimeRequest request, CancellationToken cancellation)
    {
        RuntimeProtocol.Validate(request);
        cancellation.ThrowIfCancellationRequested();
        switch (request.Operation)
        {
            case RuntimeOperations.Health:
                var health = files.Health();
                return RuntimeProtocol.Success(request.Id, new RuntimeHealth(Environment.ProcessId, state.Revision, health.Count, health.IssueId, health.Issue));
            case RuntimeOperations.Stop:
                Interlocked.Exchange(ref _stop, 1); return RuntimeProtocol.Success(request.Id, new { Stopping = true });
            case RuntimeOperations.ReadState: return RuntimeProtocol.Success(request.Id, state.Read());
            case RuntimeOperations.CommitState:
                return RuntimeProtocol.Success(request.Id, state.Commit(RuntimeProtocol.Payload<StateCommit>(request.Payload)));
            case RuntimeOperations.OpenFiles:
                return RuntimeProtocol.Success(request.Id, await files.OpenAsync(request.Id, RuntimeProtocol.Payload<FilesLaunch>(request.Payload), cancellation).ConfigureAwait(false));
            case RuntimeOperations.Settings:
                if (settings is null) throw new RuntimeFailure("settings-unavailable", "This Core has no settings service. Keep the complete Nexus build together.");
                return RuntimeProtocol.Success(request.Id, await settings.ExecuteAsync(RuntimeProtocol.Payload<SettingsRequest>(request.Payload), cancellation).ConfigureAwait(false));
            case RuntimeOperations.Work: return RuntimeProtocol.Success(request.Id, files.Work(request.ProcessId));
            case RuntimeOperations.Next:
                // Wrap nullable commands in an object so every reply has an object body.
                return RuntimeProtocol.Success(request.Id, new FilesNext(await files.NextAsync(request.ProcessId, cancellation).ConfigureAwait(false)));
            case RuntimeOperations.Ready:
                files.Ready(request.ProcessId, RuntimeProtocol.Payload<FilesReady>(request.Payload)); return RuntimeProtocol.Success(request.Id, new { Ready = true });
            case RuntimeOperations.Complete:
                files.Complete(request.ProcessId, RuntimeProtocol.Payload<FilesCompletion>(request.Payload)); return RuntimeProtocol.Success(request.Id, new { Completed = true });
            case RuntimeOperations.Pulse:
                files.Pulse(request.ProcessId); return RuntimeProtocol.Success(request.Id, state.Appearance());
            default: throw new RuntimeFailure("operation", "This operation is unavailable.");
        }
    }
}
public sealed record FilesNext(FilesCommand? Command);
