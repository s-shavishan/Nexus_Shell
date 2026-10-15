namespace Nexus.Runtime.Transfers;

// Cooperative checkpoints keep the original streams/staging and worker slot.
// Pause never cancels/requeues a job. Cancellation still interrupts a paused
// waiter, allowing Core shutdown and explicit cancellation to clean staging.
public sealed class FileTransferFlow(Action<bool>? changed = null)
{
    private readonly object _gate = new();
    private TaskCompletionSource? _resume;
    public bool PauseRequested { get { lock (_gate) return _resume is not null; } }
    public bool Pause()
    {
        lock (_gate)
        {
            if (_resume is not null) return false;
            _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return true;
        }
    }
    public bool Resume()
    {
        TaskCompletionSource? resume;
        lock (_gate) { resume = _resume; _resume = null; }
        resume?.TrySetResult();
        return resume is not null;
    }
    public async ValueTask WaitAsync(CancellationToken cancellation)
    {
        Task? acknowledged = null;
        try
        {
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                Task? resume;
                lock (_gate) resume = _resume?.Task;
                if (resume is null) return;
                // Resume followed immediately by another Pause creates a new
                // generation even if this waiter never left its checkpoint.
                if (!ReferenceEquals(acknowledged, resume))
                { acknowledged = resume; changed?.Invoke(true); }
                await resume.WaitAsync(cancellation).ConfigureAwait(false);
            }
        }
        finally { if (acknowledged is not null) changed?.Invoke(false); }
    }
}
