// නමෝ බුද්ධාය 🤍
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using System.Text.Json;

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
        StateCommitResult response;
        try { response = await commit(request).ConfigureAwait(false); }
        catch (RuntimeFailure error) when (error.Code == "state-rejected")
        {
            // Core explicitly rejected the contents before writing. Do not keep
            // replaying that bad snapshot after the user corrects their settings.
            _pending = null; throw;
        }
        if (response.CommitId != request.CommitId || response.Revision != checked(request.ExpectedRevision + 1))
            throw new InvalidDataException("The settings acknowledgement does not match its commit.");
        Interlocked.Exchange(ref _revision, response.Revision); _pending = null;
    }
    public async Task SaveAsync(ShellState snapshot)
    {
        // Freeze at enqueue time, before another save can hold this caller at the
        // gate. Reject oversized content locally before it can become uncertain.
        var captured = snapshot.Snapshot();
        if (JsonSerializer.SerializeToUtf8Bytes(captured).Length > StateStore.MaximumBytes)
            throw new InvalidDataException("Settings exceed the 2 MB limit. The previous save is intact.");
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
            _pending = new(captured, _revision, Guid.NewGuid());
            await SendAsync(_pending).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
}
