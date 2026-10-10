namespace Nexus.Runtime;

public sealed class CoreRouter(CoreStateRepository state, FilesCoordinator files, SystemSettingsCoordinator? settings = null, ControlCenterCoordinator? panels = null)
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
                var settingsRequest = RuntimeProtocol.Payload<SettingsRequest>(request.Payload);
                if (request.Role == "controlcenter") settingsRequest = settingsRequest with { Window = PanelService().DisplayWindow(request.ProcessId) };
                return RuntimeProtocol.Success(request.Id, await settings.ExecuteAsync(settingsRequest, cancellation).ConfigureAwait(false));
            case RuntimeOperations.PanelSync: return RuntimeProtocol.Success(request.Id, PanelService().Sync(RuntimeProtocol.Payload<PanelSync>(request.Payload)));
            case RuntimeOperations.PanelPulse: return RuntimeProtocol.Success(request.Id, PanelService().Pulse(request.ProcessId));
            case RuntimeOperations.PanelWait: return RuntimeProtocol.Success(request.Id, await PanelService().WaitAsync(request.ProcessId, RuntimeProtocol.Payload<PanelWait>(request.Payload).Revision, cancellation).ConfigureAwait(false));
            case RuntimeOperations.PanelReady: return RuntimeProtocol.Success(request.Id, PanelService().Ready(request.ProcessId, RuntimeProtocol.Payload<PanelReady>(request.Payload)));
            case RuntimeOperations.PanelHide: return RuntimeProtocol.Success(request.Id, PanelService().Hide(request.ProcessId, RuntimeProtocol.Payload<PanelHidden>(request.Payload).Sequence));
            case RuntimeOperations.PanelAction: return RuntimeProtocol.Success(request.Id, await PanelService().ActionAsync(request.ProcessId, RuntimeProtocol.Payload<PanelAction>(request.Payload), cancellation).ConfigureAwait(false));
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
    private ControlCenterCoordinator PanelService() => panels ?? throw new RuntimeFailure("panel-unavailable", "This Core has no panel supervisor. Keep the complete build together.");
}
public sealed record FilesNext(FilesCommand? Command);
