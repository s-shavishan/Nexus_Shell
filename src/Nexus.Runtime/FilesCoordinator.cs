using System.Threading.Channels;
using Nexus.Shell.Services;

namespace Nexus.Runtime;

public interface IFilesProcess : IDisposable
{
    int Id { get; }
    bool HasExited { get; }
    int ExitCode { get; }
    void Stop();
}

public sealed class FilesCoordinator(Func<Guid, IFilesProcess> start, Func<long, int, bool> ownsWindow, TimeProvider? clock = null) : IDisposable
{
    public const int MaximumProcesses = 8;
    public static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(15), PulseTimeout = TimeSpan.FromSeconds(20);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Tool> _tools = [];
    private Tool? _browser;
    private bool _disposed;
    private Guid _issueId;
    private string _issue = "";
    private sealed class Pending(Guid id, bool browse)
    {
        internal Guid Id { get; } = id;
        internal bool Browse { get; } = browse;
        internal TaskCompletionSource<FilesResult> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed class Tool(Guid id, Guid operationId, FilesLaunch launch, IFilesProcess process, long now)
    {
        internal Guid Id { get; } = id;
        internal FilesWork Work { get; } = new(id, operationId, launch);
        internal IFilesProcess Process { get; } = process;
        internal Channel<FilesCommand> Commands { get; } = Channel.CreateBounded<FilesCommand>(new BoundedChannelOptions(16) { FullMode = BoundedChannelFullMode.Wait });
        internal Dictionary<Guid, Pending> Pending { get; } = [];
        internal long Started { get; } = now;
        internal long Pulse = now;
        internal bool Ready, Completed;
        internal long CompletedAt, Window;
    }
    public (int Count, Guid IssueId, string Issue) Health()
    { lock (_gate) return (_tools.Count, _issueId, _issue); }
    public bool IsWorker(int processId)
    { lock (_gate) return _tools.Values.Any(t => t.Process.Id == processId && !t.Process.HasExited); }
    private Tool Worker(int processId) => _tools.Values.FirstOrDefault(t => t.Process.Id == processId && !t.Process.HasExited)
        ?? throw new RuntimeFailure("worker", "This Files process is no longer part of the session.");
    private static void Validate(FilesLaunch launch)
    {
        if (launch.Request is null || !Enum.IsDefined(launch.Request.Kind) || launch.Request.Title is null || launch.Request.Title.Length > 200
            || launch.Request.SuggestedName is null || launch.Request.SuggestedName.Length > 256 || launch.Folder?.Length > 32767
            || launch.Request.Extensions is { Length: > 32 } || launch.Request.Extensions?.Any(x => x is null || x.Length > 24) == true)
            throw new InvalidDataException("The Files request is invalid.");
    }
    public async Task<FilesResult> OpenAsync(Guid operationId, FilesLaunch launch, CancellationToken cancellation)
    {
        Validate(launch); cancellation.ThrowIfCancellationRequested();
        Pending pending; Tool tool;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            pending = new(operationId, launch.Request.Kind == FileSelectionKind.Browse);
            if (pending.Browse && _browser is { Completed: false } existing && !existing.Process.HasExited)
            {
                tool = existing;
                if (tool.Pending.Count >= 8 || !tool.Commands.Writer.TryWrite(new(operationId, launch.Folder)))
                    throw new RuntimeFailure("busy", "Files is busy opening another request. Try again shortly.");
            }
            else
            {
                if (_tools.Count >= MaximumProcesses) throw new RuntimeFailure("busy", "Close an existing file picker before opening another.");
                Guid id = Guid.NewGuid();
                var process = start(id); tool = new(id, operationId, launch, process, _clock.GetTimestamp());
                _tools.Add(id, tool);
                if (pending.Browse) _browser = tool;
            }
            tool.Pending.Add(operationId, pending);
        }
        try { return await pending.Result.Task.WaitAsync(cancellation).ConfigureAwait(false); }
        finally
        {
            lock (_gate)
            {
                tool.Pending.Remove(operationId);
                // A caller exit cancels only its picker. Browsing keeps its
                // window, except when the first launch never reached readiness.
                if (!pending.Result.Task.IsCompleted && (!pending.Browse || !tool.Ready))
                    Finish(tool, "", intentional: true);
            }
        }
    }
    public FilesWork Work(int processId)
    { lock (_gate) return Worker(processId).Work; }
    public async Task<FilesCommand?> NextAsync(int processId, CancellationToken cancellation)
    {
        Tool tool; lock (_gate) tool = Worker(processId);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try { return await tool.Commands.Reader.ReadAsync(deadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { return null; }
        catch (ChannelClosedException) { return null; }
    }
    public void Pulse(int processId)
    { lock (_gate) Worker(processId).Pulse = _clock.GetTimestamp(); }
    public void Ready(int processId, FilesReady ready)
    {
        lock (_gate)
        {
            var tool = Worker(processId);
            if (ready.Window == 0 || !ownsWindow(ready.Window, processId)) throw new InvalidDataException("The Files window does not belong to this worker.");
            tool.Ready = true; tool.Window = ready.Window; tool.Pulse = _clock.GetTimestamp();
            if (tool.Pending.TryGetValue(ready.OperationId, out var pending) && pending.Browse)
                pending.Result.TrySetResult(new(processId, ready.Window, []));
        }
    }
    public void Complete(int processId, FilesCompletion completion)
    {
        lock (_gate)
        {
            var tool = Worker(processId);
            if (completion.OperationId != tool.Work.OperationId || completion.Paths is null || completion.Paths.Length > 1000
                || completion.Paths.Any(p => string.IsNullOrWhiteSpace(p) || p.Length > 32767)) throw new InvalidDataException("The picker result is invalid.");
            var kind = tool.Work.Launch.Request.Kind;
            if (kind is FileSelectionKind.OpenFile or FileSelectionKind.Folder or FileSelectionKind.SaveFile && completion.Paths.Length > 1)
                throw new InvalidDataException("This picker accepts one selection.");
            if (kind == FileSelectionKind.Browse && completion.Paths.Length != 0) throw new InvalidDataException("Browsing cannot return picker selections.");
            if (tool.Pending.TryGetValue(completion.OperationId, out var pending))
                pending.Result.TrySetResult(new(processId, tool.Window, completion.Paths));
            tool.Completed = true; tool.CompletedAt = _clock.GetTimestamp();
            foreach (var other in tool.Pending.Values.Where(p => p.Id != completion.OperationId))
                other.Result.TrySetException(new RuntimeFailure("closed", "The Files window closed before this request completed."));
            tool.Commands.Writer.TryComplete();
            if (ReferenceEquals(_browser, tool)) _browser = null;
        }
    }
    private void Finish(Tool tool, string message, bool intentional = false)
    {
        tool.Completed = true; tool.CompletedAt = _clock.GetTimestamp();
        if (message.Length > 0 && !intentional) { _issueId = Guid.NewGuid(); _issue = message; }
        foreach (var pending in tool.Pending.Values)
        {
            if (intentional) pending.Result.TrySetCanceled();
            else pending.Result.TrySetException(new RuntimeFailure("files", message.Length > 0 ? message : "The file selection could not finish."));
        }
        tool.Commands.Writer.TryComplete();
        try { if (!tool.Process.HasExited) tool.Process.Stop(); } catch { /* Recheck on the next monitor pass. */ }
        if (ReferenceEquals(_browser, tool)) _browser = null;
    }
    public void Monitor()
    {
        lock (_gate)
        {
            long now = _clock.GetTimestamp();
            foreach (var tool in _tools.Values.ToArray())
            {
                if (tool.Process.HasExited)
                {
                    if (!tool.Completed) Finish(tool, "Files stopped unexpectedly. Open it again to continue.");
                    _tools.Remove(tool.Id); tool.Process.Dispose(); continue;
                }
                if (tool.Completed)
                {
                    if (_clock.GetElapsedTime(tool.CompletedAt, now) >= TimeSpan.FromSeconds(5))
                        try { tool.Process.Stop(); } catch { }
                }
                else if (!tool.Ready && _clock.GetElapsedTime(tool.Started, now) >= ReadyTimeout)
                    Finish(tool, "Files did not become ready. The desktop is still available; try opening Files again.");
                else if (tool.Ready && _clock.GetElapsedTime(tool.Pulse, now) >= PulseTimeout)
                    Finish(tool, "Files stopped responding. The desktop is still available; open Files again to continue.");
            }
        }
    }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return; _disposed = true;
            foreach (var tool in _tools.Values) { Finish(tool, "", intentional: true); tool.Process.Dispose(); }
            _tools.Clear(); _browser = null;
        }
    }
}
