// නමෝ බුද්ධාය 🤍
namespace Nexus.Runtime.Startup;

public sealed class StartupCoordinator(IStartupBackend backend, IStartupJournal journal)
{
    public StartupSnapshot Read(CancellationToken cancellation = default)
    {
        using var lease = journal.Acquire();
        var document = journal.Load(backend.OwnerId);
        var inventory = backend.Read(cancellation);
        var saved = document.Backups.Select(b => b.Original.Id).ToHashSet();
        var items = new List<StartupItem>();
        foreach (var entry in inventory.Entries.Where(e => !saved.Contains(e.Id)))
        {
            cancellation.ThrowIfCancellationRequested();
            string refusal = StartupRules.Refusal(entry);
            items.Add(new(entry, null, "Registered", refusal.Length > 0 ? refusal : "Pause removes this sign-in registration after saving its exact original. Running apps are not stopped.", refusal.Length == 0, false));
        }
        foreach (var backup in document.Backups)
        {
            cancellation.ThrowIfCancellationRequested();
            var current = backend.ReadManaged(backup.Original.Name);
            bool same = current?.Fingerprint == backup.Original.Fingerprint;
            bool conflict = current is not null && !same;
            string status = conflict ? "Conflict · original saved" : same ? "Already registered · finish restore"
                : backup.Phase == StartupBackupPhase.Paused ? "Paused by Nexus" : "Interrupted change · original saved";
            string detail = conflict ? "A newer registration uses this name. Restore will not overwrite it; review both commands."
                : same ? "The original is already present. Restore will only finish removing the saved record."
                : "Restore puts back the exact original name, command and value type. Windows startup approval still applies.";
            items.Add(new(backup.Original, backup.Id, status, detail, false, !conflict, current?.Command ?? ""));
        }
        return new(DateTimeOffset.Now, items.OrderBy(i => i.Entry.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(), inventory.Notices, document.Backups.Count);
    }

    public void Pause(StartupEntry expected)
    {
        StartupRules.ValidateManaged(expected);
        using var lease = journal.Acquire();
        var document = journal.Load(backend.OwnerId);
        if (document.Backups.Any(b => b.Original.Id == expected.Id))
            throw new InvalidOperationException("This startup name already has a saved original. Refresh and review it before making another change.");
        if (document.Backups.Count >= StartupJournal.MaximumBackups)
            throw new InvalidOperationException("128 originals are already saved. Restore an existing item before pausing another.");
        var current = backend.ReadManaged(expected.Name) ?? throw new InvalidOperationException("The registration is no longer present. Refresh the list.");
        if (current.Fingerprint != expected.Fingerprint) throw new InvalidOperationException("The registration changed since this list was read. Refresh before changing it.");
        var backup = new StartupBackup(Guid.NewGuid(), current, StartupBackupPhase.Prepared, DateTimeOffset.Now);
        var prepared = document with { Backups = [.. document.Backups, backup] };
        journal.Save(prepared); // The exact original is durable before registry mutation.
        try
        {
            backend.RemoveExpected(current); // Native backend compares and removes in one registry transaction.
            journal.Save(prepared with { Backups = prepared.Backups.Select(b => b.Id == backup.Id ? b with { Phase = StartupBackupPhase.Paused } : b).ToList() });
        }
        catch (Exception error)
        { throw new IOException("The startup change was not fully acknowledged. Its original is saved. Refresh and review before retrying. " + error.Message, error); }
    }

    public void Restore(Guid backupId)
    {
        using var lease = journal.Acquire();
        var document = journal.Load(backend.OwnerId);
        var backup = document.Backups.FirstOrDefault(b => b.Id == backupId) ?? throw new InvalidOperationException("This saved original has already been restored or removed. Refresh the list.");
        StartupRules.ValidateManaged(backup.Original);
        var current = backend.ReadManaged(backup.Original.Name);
        if (current is not null && current.Fingerprint != backup.Original.Fingerprint)
            throw new InvalidOperationException("Another command now uses this startup name. The saved original and current registration were both retained.");
        var pending = document with { Backups = document.Backups.Select(b => b.Id == backup.Id ? b with { Phase = StartupBackupPhase.RestorePending } : b).ToList() };
        journal.Save(pending);
        try
        {
            if (current is null) backend.RestoreIfMissing(backup.Original);
            journal.Save(pending with { Backups = pending.Backups.Where(b => b.Id != backup.Id).ToList() });
        }
        catch (Exception error)
        { throw new IOException("Restore was not fully acknowledged. The saved original is retained. Refresh to check whether Windows already has it. " + error.Message, error); }
    }
}

public static class StartupWork
{
    private static int _running;
    public static Task<T>? TryRun<T>(Func<T> operation)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return null;
        return Run();
        async Task<T> Run()
        {
            try { return await Task.Run(operation).ConfigureAwait(false); }
            finally { Volatile.Write(ref _running, 0); }
        }
    }
}
