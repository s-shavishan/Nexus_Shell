// නමෝ බුද්ධාය 🤍
using Nexus.Runtime.Transfers;

namespace Nexus.Runtime;

public sealed record WindowsFileAction(Guid Id, string Kind, string Path, long Owner, int X, int Y, int OwnerProcessId = 0, string[]? AdditionalPaths = null);
public sealed record WindowsFileActionStatus(Guid Id, string State, string Detail, int ProcessId);
public interface IWindowsFileProcess : IDisposable { int Id { get; } bool HasExited { get; } int ExitCode { get; } bool Ready { get; } void Admit(); void Stop(); }

// Optional Windows shell handlers run outside Core and desktop. There is no
// retry or crash replay: a handler may already have performed an operation.
public sealed class WindowsFileActionCoordinator(Func<WindowsFileAction, int, IWindowsFileProcess> start, TimeProvider? time = null) : IDisposable
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, (WindowsFileAction Request, int Caller, WindowsFileActionStatus Status)> _history = [];
    private readonly Dictionary<Guid, (IWindowsFileProcess Process, long Started)> _workers = [];
    private bool _disposed;
    public Guid IssueId { get; private set; }
    public string Issue { get; private set; } = "";
    public int ActiveCount { get { lock (_gate) return _workers.Count; } }
    public (int Count, Guid IssueId, string Issue) Health() { lock (_gate) return (_workers.Count, IssueId, Issue); }
    public static WindowsFileAction Validate(WindowsFileAction request)
    {
        if (request is null || request.Id == Guid.Empty || request.Kind is not ("menu" or "properties" or "open-with" or "reveal" or "recycle") || request.Owner == 0 || request.X is < -100000 or > 100000 || request.Y is < -100000 or > 100000)
            throw new RuntimeFailure("windows-file", "This Windows file action is invalid.");
        if ((request.AdditionalPaths?.Length ?? 0) > (request.Kind == "recycle" ? FileJobRules.MaximumSources - 1 : 0))
            throw new RuntimeFailure("windows-file", "Recycle up to 24 items; other Windows file actions require one item.");
        string path = FileJobRules.PathName(request.Path);
        var additional = (request.AdditionalPaths ?? []).Select(FileJobRules.PathName).ToArray();
        if (request.Kind == "recycle")
        {
            string[] paths = [path, .. additional];
            if (paths.Distinct(FileJobRules.Paths).Count() != paths.Length
                || paths.Any(item => FileJobRules.Paths.Equals(item, System.IO.Path.GetPathRoot(item))
                    || paths.Any(other => !FileJobRules.Paths.Equals(item, other) && FileJobRules.Within(item, other))))
                throw new RuntimeFailure("windows-file", "Select separate files or folders, without drive roots or a folder and its contents together.");
        }
        return request with { Path = path, AdditionalPaths = additional };
    }
    private static bool Same(WindowsFileAction first, WindowsFileAction second)
        => first with { AdditionalPaths = null } == second with { AdditionalPaths = null }
            && (first.AdditionalPaths ?? []).SequenceEqual(second.AdditionalPaths ?? [], FileJobRules.Paths);
    private static IEnumerable<string> Targets(WindowsFileAction request)
        => new[] { request.Path }.Concat(request.AdditionalPaths ?? []);
    public WindowsFileActionStatus Submit(WindowsFileAction request, int caller)
    {
        request = Validate(request) with { OwnerProcessId = caller };
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this); Monitor();
            if (_history.TryGetValue(request.Id, out var known))
            { if (!Same(known.Request, request) || known.Caller != caller) throw new RuntimeFailure("windows-file-id", "This identifier belongs to another Windows file action."); return known.Status; }
            if (request.Kind == "recycle" && _workers.Keys.Select(id => _history[id].Request)
                .Where(active => active.Kind == "recycle")
                .Any(active => Targets(active).Any(first => Targets(request).Any(second => FileJobRules.Within(first, second) || FileJobRules.Within(second, first)))))
                throw new RuntimeFailure("windows-file-busy", "Windows is already recycling an overlapping selection. Finish or cancel its dialog before trying again.");
            if (_workers.Count >= 3) throw new RuntimeFailure("windows-file-busy", "Three Windows file dialogs are already active. Close one before opening another.");
            if (_history.Count >= 4096) throw new RuntimeFailure("windows-file-budget", "This Core session has reached its Windows file-action limit. Restart Nexus after finishing current work.");
            var worker = start(request, caller);
            var status = new WindowsFileActionStatus(request.Id, "Requested", "Windows owns this action. Finish or dismiss its menu/dialog.", worker.Id);
            _workers.Add(request.Id, (worker, _time.GetTimestamp())); _history.Add(request.Id, (request, caller, status)); return status;
        }
    }
    public WindowsFileActionStatus Status(Guid id, int caller)
    {
        lock (_gate)
        {
            Monitor();
            if (!_history.TryGetValue(id, out var value) || value.Caller != caller) throw new RuntimeFailure("windows-file-id", "This action does not belong to this component.");
            return value.Status;
        }
    }
    public void Monitor()
    {
        lock (_gate)
        {
            foreach (var (id, entry) in _workers.ToArray())
            {
                var process = entry.Process;
                bool ready = !process.HasExited && process.Ready;
                if (ready && _history[id].Status.State != "Failed") process.Admit();
                if (!process.HasExited && !ready && _history[id].Status.State != "Failed" && _time.GetElapsedTime(entry.Started) >= TimeSpan.FromSeconds(15))
                {
                    var pending = _history[id]; IssueId = Guid.NewGuid(); Issue = "A Windows file handler timed out before its command was admitted. Try Windows Explorer.";
                    _history[id] = pending with { Status = pending.Status with { State = "Failed", Detail = Issue } };
                    try { process.Stop(); } catch { }
                }
                if (!process.HasExited) continue;
                var value = _history[id]; bool success = process.ExitCode == 0;
                if (value.Status.State != "Failed")
                {
                    _history[id] = value with { Status = value.Status with { State = success ? "Finished" : "Failed", Detail = success ? "Windows action finished or was dismissed." : "Windows could not complete this action. See the Core log and try Windows Explorer." } };
                    if (!success) { IssueId = Guid.NewGuid(); Issue = "A Windows file handler failed or closed unexpectedly. Review its dialog/output before retrying; Core log has details."; }
                }
                process.Dispose(); _workers.Remove(id);
            }
        }
    }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return; _disposed = true;
            foreach (var entry in _workers.Values) { try { entry.Process.Stop(); } catch { } finally { entry.Process.Dispose(); } }
            _workers.Clear();
        }
    }
}
public sealed record WindowsFileStatusRequest(Guid Id);
