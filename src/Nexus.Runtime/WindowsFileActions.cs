using Nexus.Runtime.Transfers;

namespace Nexus.Runtime;

public sealed record WindowsFileAction(Guid Id, string Kind, string Path, long Owner, int X, int Y, int OwnerProcessId = 0);
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
        if (request is null || request.Id == Guid.Empty || request.Kind is not ("menu" or "properties" or "open-with" or "reveal") || request.Owner == 0 || request.X is < -100000 or > 100000 || request.Y is < -100000 or > 100000)
            throw new RuntimeFailure("windows-file", "This Windows file action is invalid.");
        return request with { Path = FileJobRules.PathName(request.Path) };
    }
    public WindowsFileActionStatus Submit(WindowsFileAction request, int caller)
    {
        request = Validate(request) with { OwnerProcessId = caller };
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this); Monitor();
            if (_history.TryGetValue(request.Id, out var known))
            { if (known.Request != request || known.Caller != caller) throw new RuntimeFailure("windows-file-id", "This identifier belongs to another Windows file action."); return known.Status; }
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
