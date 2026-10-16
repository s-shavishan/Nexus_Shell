// නමෝ බුද්ධාය 🤍
using System.Text.Json;

namespace Nexus.Runtime.Transfers;

public sealed class FileJobsJournal(string directory) : IFileJobsJournal
{
    private string PathName => Path.Combine(directory, "file-jobs.json");
    // Leave ample room for the RPC envelope and progress fields.
    private const int MaximumBytes = 2 * 1024 * 1024;
    public string Notice { get; private set; } = "";
    public FileJobRecord[] Read()
    {
        if (!File.Exists(PathName) && !File.Exists(PathName + ".backup")) return [];
        foreach (string path in new[] { PathName, PathName + ".backup" })
        {
            try
            {
                using var stream = File.OpenRead(path); if (stream.Length > MaximumBytes) throw new InvalidDataException("Job history is too large.");
                var jobs = JsonSerializer.Deserialize<FileJobRecord[]>(stream) ?? throw new InvalidDataException("Job history is empty.");
                if (jobs.Length > FileJobRules.MaximumJobs) throw new InvalidDataException("Job history exceeds its limit.");
                if (path.EndsWith(".backup", StringComparison.Ordinal)) Notice = "Job history was recovered from its backup. Interrupted jobs require your review.";
                return jobs.Where(j => j is not null && j.Job is not null && j.Progress is not null && Enum.IsDefined(j.State)).ToArray();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            { Notice = "Some job history could not be read. Review the destination before creating a replacement job."; }
        }
        return [];
    }
    public void Write(IReadOnlyList<FileJobRecord> jobs)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(jobs);
        if (jobs.Count > FileJobRules.MaximumJobs || bytes.Length > MaximumBytes) throw new InvalidDataException("File Jobs history exceeds its limit.");
        Directory.CreateDirectory(directory); string temporary = PathName + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            if (File.Exists(PathName))
            {
                bool readable = false;
                try { using var prior = File.OpenRead(PathName); readable = prior.Length <= MaximumBytes && JsonSerializer.Deserialize<FileJobRecord[]>(prior) is not null; }
                catch (Exception error) when (error is IOException or JsonException) { }
                if (readable) File.Replace(temporary, PathName, PathName + ".backup");
                else { File.Copy(PathName, PathName + ".unreadable-" + Guid.NewGuid().ToString("N")); File.Move(temporary, PathName, true); }
            }
            else File.Move(temporary, PathName);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
