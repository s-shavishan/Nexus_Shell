using Nexus.Shell.Models;

namespace Nexus.Runtime;

// Shared production logic: reconcile an uncertain acknowledgement before
// committing newer edits. A retry keeps the original commit ID and contents.
public sealed class RevisionedStateWriter(long revision, Func<Task<StateReadResult>> read, Func<StateCommit, Task<StateCommitResult>> commit)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _revision = revision;
    private StateCommit? _pending;
    public long Revision => Interlocked.Read(ref _revision);
    private async Task SendAsync(StateCommit request)
    {
        var response = await commit(request).ConfigureAwait(false);
        if (response.CommitId != request.CommitId || response.Revision != checked(request.ExpectedRevision + 1))
            throw new InvalidDataException("The settings acknowledgement does not match its commit.");
        Interlocked.Exchange(ref _revision, response.Revision); _pending = null;
    }
    public async Task SaveAsync(ShellState snapshot)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_pending is { } pending)
            {
                var current = await read().ConfigureAwait(false);
                if (current.State.PersistenceCommitId == pending.CommitId.ToString("N")
                    && current.State.PersistenceRevision == checked(pending.ExpectedRevision + 1))
                { Interlocked.Exchange(ref _revision, current.State.PersistenceRevision); _pending = null; }
                else if (current.State.PersistenceRevision == pending.ExpectedRevision)
                    await SendAsync(pending).ConfigureAwait(false);
                else throw new RuntimeFailure("conflict", "Settings changed while a save was interrupted. Your current edits have been retained instead of overwriting newer settings.");
            }
            _pending = new(snapshot.Snapshot(), _revision, Guid.NewGuid());
            await SendAsync(_pending).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
}
