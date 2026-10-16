// නමෝ බුද්ධාය 🤍
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nexus.Runtime.Startup;

/// <summary>Independent per-account original registrations; never auto-restores or executes commands.</summary>
public sealed class StartupJournal(string directory) : IStartupJournal
{
    public const int MaximumBytes = 2 * 1024 * 1024, MaximumBackups = 128;
    public string FilePath => Path.Combine(directory, "startup-apps.json");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public IDisposable Acquire()
    {
        Directory.CreateDirectory(directory);
        try { return new FileStream(FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException error) { throw new IOException("Another Nexus startup change is in progress. Retry after it finishes.", error); }
    }
    public StartupJournalDocument Load(string ownerId)
    {
        if (!File.Exists(FilePath)) return new(1, ownerId, []);
        try
        {
            using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumBytes) throw new InvalidDataException("Startup originals exceed the 2 MiB limit.");
            var document = JsonSerializer.Deserialize<StartupJournalDocument>(stream, Json) ?? throw new InvalidDataException("The startup journal is empty.");
            Validate(document, ownerId); return document;
        }
        catch (Exception error) when (error is JsonException or InvalidDataException)
        { throw new InvalidDataException("Saved startup originals cannot be read. No registration was changed. Preserve startup-apps.json and its .backup before manual recovery. " + error.Message, error); }
    }
    public static void Validate(StartupJournalDocument document, string ownerId)
    {
        if (document.Schema != 1 || string.IsNullOrWhiteSpace(ownerId) || document.OwnerId != ownerId || document.Backups is null
            || document.Backups.Count > MaximumBackups || document.Backups.Any(b => b is null || b.Id == Guid.Empty || b.Original is null || !Enum.IsDefined(b.Phase))
            || document.Backups.Select(b => b.Id).Distinct().Count() != document.Backups.Count)
            throw new InvalidDataException("The startup-originals schema, account or identities are invalid.");
        foreach (var backup in document.Backups)
        {
            try { StartupRules.ValidateManaged(backup.Original); }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException)
            { throw new InvalidDataException("A saved startup registration is invalid; it was not applied.", error); }
        }
        if (document.Backups.Select(b => b.Original.Id).Distinct().Count() != document.Backups.Count)
            throw new InvalidDataException("Duplicate startup originals require manual review.");
    }
    public void Save(StartupJournalDocument document)
    {
        Validate(document, document.OwnerId);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, Json);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Saved startup originals reached 2 MiB. No additional change was applied.");
        Directory.CreateDirectory(directory);
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".backup");
            else File.Move(temporary, FilePath, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
