namespace Nexus.Runtime.Transfers;

// One transfer worker belongs to Core. RPC disconnects do not duplicate or
// cancel accepted jobs; explicit controls and Core shutdown own cancellation.
public sealed class FileJobCoordinator : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly IFileTransferBackend _backend;
    private readonly IFileJobsJournal _journal;
    private readonly List<FileJobRecord> _jobs = [];
    private readonly Dictionary<Guid, string> _accepted = [];
    private readonly CancellationTokenSource _stop = new();
    private CancellationTokenSource? _active;
    private Guid _activeId;
    private Task? _worker;
    private bool _paused, _disposed;
    private long _revision;
    private DateTimeOffset _lastProgress;
    public Guid Epoch { get; } = Guid.NewGuid();
    public FileJobCoordinator(IFileTransferBackend backend, IFileJobsJournal journal)
    {
        _backend = backend; _journal = journal;
        foreach (var record in journal.Read().Take(FileJobRules.MaximumJobs))
        {
            if (record?.Job is null) continue;
            try
            {
                var safe = Sanitize(record with { Job = FileJobRules.Validate(record.Job) });
                if (!FileJobRules.Terminal(safe.State)) safe = safe with { State = FileJobState.Interrupted, Detail = "Core stopped before this job finished. Review the destination before retrying." };
                _jobs.Add(safe);
                _accepted[safe.Job.Id] = Fingerprint(safe.Job);
            }
            catch (Exception error) when (error is ArgumentException or RuntimeFailure or IOException) { }
        }
    }
    public FileJobsSnapshot Snapshot()
    { lock (_gate) return new(Epoch, _revision, _paused, _jobs.Select(Clone).Reverse().ToArray(), _journal.Notice); }
    private static FileJobRecord Clone(FileJobRecord job) => job with { Job = job.Job with { Sources = [.. job.Job.Sources] } };
    private static FileJobRecord Sanitize(FileJobRecord job)
    {
        static string Text(string? text, int maximum) => (text ?? "")[..Math.Min(maximum, text?.Length ?? 0)];
        var progress = job.Progress ?? new("", "", 0, 0, 0, 0, 0);
        return job with { Detail = Text(job.Detail, 600), Progress = progress with { Phase = Text(progress.Phase, 64), Current = Text(progress.Current, 4096),
            BytesCopied = Math.Max(0, progress.BytesCopied), TotalBytes = Math.Max(0, progress.TotalBytes), Completed = Math.Clamp(progress.Completed, 0, FileJobRules.MaximumEntries),
            Skipped = Math.Clamp(progress.Skipped, 0, FileJobRules.MaximumEntries), TotalFiles = Math.Clamp(progress.TotalFiles, 0, FileJobRules.MaximumEntries) } };
    }
    private static string Fingerprint(FileJobSpec spec) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(spec)));
    private void CheckEpoch(Guid epoch)
    { ObjectDisposedException.ThrowIf(_disposed, this); if (epoch != Epoch) throw new RuntimeFailure("file-epoch", "Core restarted. Refresh File Jobs before making changes."); }
    public FileJobsSnapshot Submit(FileJobSubmit request)
    {
        var spec = FileJobRules.Validate(request.Job);
        lock (_gate)
        {
            CheckEpoch(request.Epoch);
            string fingerprint = Fingerprint(spec);
            if (_accepted.TryGetValue(spec.Id, out string? accepted))
            {
                if (accepted != fingerprint)
                    throw new RuntimeFailure("file-id", "This job identifier already belongs to another transfer.");
                return Snapshot();
            }
            if (_accepted.Count >= 4096) throw new RuntimeFailure("file-budget", "This Core session has accepted 4,096 jobs. Finish current work and restart Nexus before adding more.");
            var next = _jobs.ToList();
            if (next.Count >= FileJobRules.MaximumJobs)
            {
                var old = next.FirstOrDefault(j => FileJobRules.Terminal(j.State));
                if (old is null) throw new RuntimeFailure("file-queue", "The queue is full. Wait for a job to finish or cancel one.");
                next.Remove(old);
            }
            next.Add(new(spec, FileJobState.Queued, DateTimeOffset.UtcNow, "Waiting in the Core queue.", new("Queued", "", 0, 0, 0, 0, 0)));
            // A refused journal write cannot start an unacknowledged transfer.
            _journal.Write(next); _jobs.Clear(); _jobs.AddRange(next); _accepted.Add(spec.Id, fingerprint); _revision++;
            StartWorker(); return Snapshot();
        }
    }
    public FileJobsSnapshot Control(FileJobControl request)
    {
        lock (_gate)
        {
            CheckEpoch(request.Epoch);
            switch (request.Action)
            {
                case "pause": _paused = true; _revision++; break;
                case "resume": _paused = false; _revision++; StartWorker(); break;
                case "clear":
                    var remaining = _jobs.Where(j => !FileJobRules.Terminal(j.State)).ToArray(); _journal.Write(remaining);
                    _jobs.Clear(); _jobs.AddRange(remaining); _revision++; break;
                case "cancel":
                    int index = _jobs.FindIndex(j => j.Job.Id == request.JobId);
                    if (index < 0) throw new RuntimeFailure("file-job", "This job is no longer in the queue.");
                    if (_activeId == request.JobId) { _active?.Cancel(); break; }
                    if (!FileJobRules.Terminal(_jobs[index].State))
                    {
                        var cancelled = _jobs[index] with { State = FileJobState.Cancelled, Detail = "Cancelled before transfer started." };
                        try { Set(index, cancelled, true); }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                        { Set(index, cancelled with { Detail = cancelled.Detail + " History could not be saved: " + error.Message }, false); }
                    }
                    break;
                default: throw new RuntimeFailure("file-action", "This queue action is unavailable.");
            }
            return Snapshot();
        }
    }
    private void StartWorker()
    {
        if (_disposed || _paused || _worker is { IsCompleted: false }) return;
        _worker = Task.Run(WorkAsync);
    }
    private void Set(int index, FileJobRecord record, bool durable)
    {
        record = Sanitize(record);
        if (durable)
        { var next = _jobs.ToArray(); next[index] = record; _journal.Write(next); }
        _jobs[index] = record; _revision++;
    }
    private async Task WorkAsync()
    {
        try
        {
            while (true)
            {
                FileJobRecord job;
                lock (_gate)
                {
                    if (_disposed || _paused) return;
                    int index = _jobs.FindIndex(j => j.State == FileJobState.Queued); if (index < 0) return;
                    job = _jobs[index];
                    try { Set(index, job with { State = FileJobState.Planning, Detail = "Checking sources and destination." }, true); }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    { Set(index, job with { State = FileJobState.Failed, Detail = "Job history could not be saved. No transfer started: " + error.Message }, false); _paused = true; return; }
                    _activeId = job.Job.Id; _active = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token); _lastProgress = DateTimeOffset.MinValue;
                }
                FileJobState final; string detail;
                try
                {
                    await _backend.RunAsync(job.Job, progress =>
                    {
                        lock (_gate)
                        {
                            int index = _jobs.FindIndex(j => j.Job.Id == job.Job.Id); if (index < 0 || _disposed) return;
                            // Paint is bounded to four updates per second. File
                            // and job commits still publish their latest totals.
                            if (progress.Phase == "Copying" && DateTimeOffset.UtcNow - _lastProgress < TimeSpan.FromMilliseconds(250)) return;
                            _lastProgress = DateTimeOffset.UtcNow;
                            Set(index, _jobs[index] with { State = FileJobState.Running, Progress = progress, Detail = progress.Phase }, false);
                        }
                    }, _active.Token).ConfigureAwait(false);
                    final = FileJobState.Complete; detail = "Finished. Existing destination items were never overwritten.";
                }
                catch (OperationCanceledException) { final = FileJobState.Cancelled; detail = "Cancelled. Completed items remain; unfinished staging is cleaned where possible."; }
                catch (Exception error) when (error is not OutOfMemoryException)
                { final = FileJobState.Failed; detail = error.Message[..Math.Min(400, error.Message.Length)]; }
                lock (_gate)
                {
                    int index = _jobs.FindIndex(j => j.Job.Id == job.Job.Id);
                    if (index >= 0)
                    {
                        var done = _jobs[index] with { State = final, Detail = detail };
                        try { Set(index, done, true); }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                        { Set(index, done with { Detail = detail + " Job history could not be saved: " + error.Message }, false); }
                    }
                    _active?.Dispose(); _active = null; _activeId = Guid.Empty;
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                _worker = null;
                // A new submission may arrive as the previous loop exits.
                if (!_disposed && !_paused && _jobs.Any(j => j.State == FileJobState.Queued)) StartWorker();
            }
        }
    }
    public async ValueTask DisposeAsync()
    {
        Task? worker;
        lock (_gate) { if (_disposed) return; _disposed = true; _stop.Cancel(); _active?.Cancel(); worker = _worker; }
        if (worker is not null) await worker.ConfigureAwait(false);
        _stop.Dispose();
    }
}
