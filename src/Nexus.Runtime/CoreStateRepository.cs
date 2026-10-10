using System.Security.Cryptography;
using System.Text.Json;
using Nexus.Shell.Models;
using Nexus.Shell.Services;

namespace Nexus.Runtime;

// Exactly one Core writer may own this profile, including across user sessions.
// The OS releases the lease on process failure; the file itself is retained.
public sealed class CoreStateRepository : IDisposable
{
    private readonly StateStore _store;
    private readonly FileStream _lease;
    private readonly object _gate = new();
    private ShellState _state;
    private long _revision;
    private AppearanceSettings _appearance;
    public string RecoveryMessage { get; }
    public long Revision => Interlocked.Read(ref _revision);
    public CoreStateRepository(StateStore store)
    {
        _store = store; Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        try { _lease = new FileStream(store.FilePath + ".writer.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new RuntimeFailure("writer", "Another Nexus session is already using this profile. Close it before starting this session."); }
        try
        {
            _state = store.Load(); _revision = _state.PersistenceRevision;
            _appearance = new(_state.Wallpaper, _state.NativeGlass, _state.ReducedEffects, _state.SurfaceAnimations);
            RecoveryMessage = store.RecoveryMessage;
            if (Directory.EnumerateFiles(Path.GetDirectoryName(store.FilePath)!, "settings.unsaved-*.json").Any())
                RecoveryMessage += (RecoveryMessage.Length == 0 ? "" : " ") + "An unsaved-session recovery copy is available in the Nexus data folder. It has not overwritten your current settings.";
        }
        catch { _lease.Dispose(); throw; }
    }
    public StateReadResult Read()
    { lock (_gate) return new(_state.Snapshot(), RecoveryMessage); }
    public AppearanceSettings Appearance()
        => Volatile.Read(ref _appearance);
    private static byte[] Content(ShellState state)
    {
        var copy = state.Snapshot(); copy.PersistenceRevision = 0; copy.PersistenceCommitId = "";
        return SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(copy));
    }
    public StateCommitResult Commit(StateCommit commit)
    {
        if (commit.State is null || commit.CommitId == Guid.Empty || commit.ExpectedRevision < 0)
            throw new InvalidDataException("The settings commit is invalid.");
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(commit.State);
        if (bytes.Length > StateStore.MaximumBytes) throw new InvalidDataException("Settings exceed the 2 MB limit. The previous save is intact.");
        var candidate = StateStore.NormalizeState(JsonSerializer.Deserialize<ShellState>(bytes)!);
        lock (_gate)
        {
            if (_state.PersistenceCommitId == commit.CommitId.ToString("N"))
            {
                if (!Content(candidate).SequenceEqual(Content(_state))) throw new RuntimeFailure("duplicate", "A commit identifier cannot be reused for different settings.");
                return new(_state.PersistenceRevision, commit.CommitId);
            }
            if (_state.PersistenceRevision != commit.ExpectedRevision)
                throw new RuntimeFailure("conflict", "Newer settings already exist. Nexus retained your current edits instead of overwriting them.");
            candidate.PersistenceRevision = checked(_state.PersistenceRevision + 1);
            candidate.PersistenceCommitId = commit.CommitId.ToString("N");
            _store.Save(candidate); // Flush + atomic replacement happen before acknowledgement.
            _state = candidate;
            Volatile.Write(ref _appearance, new(candidate.Wallpaper, candidate.NativeGlass, candidate.ReducedEffects, candidate.SurfaceAnimations));
            Interlocked.Exchange(ref _revision, candidate.PersistenceRevision);
            return new(candidate.PersistenceRevision, commit.CommitId);
        }
    }
    public void Dispose() => _lease.Dispose();
}
