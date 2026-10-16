// නමෝ බුද්ධාය 🤍
namespace Nexus.Runtime.Transfers;

public enum FileJobKind { Copy, Move, CreateZip, ExtractZip }
public enum FileCollision { KeepBoth, Skip }
public enum FileJobState { Queued, Planning, Running, Complete, Cancelled, Failed, Interrupted, Pausing, Paused }
public sealed record FileJobSpec(Guid Id, FileJobKind Kind, string[] Sources, string Destination, FileCollision Collision = FileCollision.KeepBoth, bool Verify = true, string OutputName = "");
public sealed record FileJobSubmit(Guid Epoch, FileJobSpec Job);
public sealed record FileJobControl(Guid Epoch, Guid JobId, string Action);
public sealed record FileJobProgress(string Phase, string Current, long BytesCopied, long TotalBytes, int Completed, int Skipped, int TotalFiles,
    double BytesPerSecond = 0, double? SecondsRemaining = null);
public sealed record FileJobRecord(FileJobSpec Job, FileJobState State, DateTimeOffset Created, string Detail, FileJobProgress Progress);
public sealed record FileJobsSnapshot(Guid Epoch, long Revision, bool Paused, FileJobRecord[] Jobs, string Notice = "");
public interface IFileTransferBackend
{ Task RunAsync(FileJobSpec job, Action<FileJobProgress> progress, FileTransferFlow flow, CancellationToken cancellation); }
public interface IFileJobsJournal
{ FileJobRecord[] Read(); void Write(IReadOnlyList<FileJobRecord> jobs); string Notice { get; } }

public static class FileJobRules
{
    public const int MaximumJobs = 24, MaximumSources = 24, MaximumEntries = 10000, MaximumDepth = 64;
    public static StringComparer Paths => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    public static bool Archive(FileJobKind kind) => kind is FileJobKind.CreateZip or FileJobKind.ExtractZip;
    public static bool MeasuredPhase(string phase) => phase is "Copying" or "Compressing" or "Extracting";
    public static string KindLabel(FileJobKind kind) => kind switch { FileJobKind.CreateZip => "Create ZIP", FileJobKind.ExtractZip => "Extract ZIP", _ => kind.ToString() };
    public static bool Terminal(FileJobState state) => state is FileJobState.Complete or FileJobState.Cancelled or FileJobState.Failed or FileJobState.Interrupted;
    public static string PathName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || value.Any(char.IsControl) || !Path.IsPathFullyQualified(value))
            throw new RuntimeFailure("file-path", "Choose a full file or folder path.");
        string windows = value.Replace('/', '\\');
        if (windows.StartsWith("\\\\?\\", StringComparison.Ordinal) || windows.StartsWith("\\\\.\\", StringComparison.Ordinal) || windows.StartsWith("\\??\\", StringComparison.Ordinal))
            throw new RuntimeFailure("file-path", "Windows device paths are unavailable in File Jobs.");
        for (int i = 0; i < value.Length; i++)
            if (value[i] == ':' && (i != 1 || !char.IsAsciiLetter(value[0]))) throw new RuntimeFailure("file-path", "Alternate streams and URI paths are unavailable in File Jobs.");
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        if (OperatingSystem.IsWindows())
        {
            foreach (string part in full[Path.GetPathRoot(full)!.Length..].Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries))
            {
                string leaf = part.Split('.')[0].ToUpperInvariant();
                if (part.EndsWith('.') || part.EndsWith(' ') || part.IndexOfAny(['<', '>', '|', '?', '*', '"']) >= 0
                    || leaf is "CON" or "PRN" or "AUX" or "NUL" || leaf.Length == 4 && (leaf.StartsWith("COM") || leaf.StartsWith("LPT")) && leaf[3] is >= '1' and <= '9')
                    throw new RuntimeFailure("file-path", "This Windows filename is not supported by File Jobs.");
            }
        }
        return full;
    }
    public static bool Within(string child, string parent) => Paths.Equals(child, parent)
        || child.StartsWith(Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    public static FileJobSpec Validate(FileJobSpec job)
    {
        if (job is null || job.Id == Guid.Empty || !Enum.IsDefined(job.Kind) || !Enum.IsDefined(job.Collision) || job.Sources is null || job.Sources.Length is < 1 or > MaximumSources)
            throw new RuntimeFailure("file-job", "Choose one to 24 sources and a valid file operation.");
        var sources = job.Sources.Select(PathName).Distinct(Paths).ToArray(); string destination = PathName(job.Destination);
        foreach (string source in sources)
        {
            if (Paths.Equals(source, Path.GetPathRoot(source))) throw new RuntimeFailure("file-job", "Choose a folder inside the drive, rather than an entire drive.");
            if (Within(destination, source)) throw new RuntimeFailure("file-job", "The destination cannot be inside a selected source.");
            if (sources.Any(other => !Paths.Equals(source, other) && Within(source, other))) throw new RuntimeFailure("file-job", "Select the parent folder once instead of also selecting its contents.");
        }
        string outputName = job.OutputName ?? "";
        if (Archive(job.Kind))
        {
            outputName = ArchiveJobRules.Leaf(outputName);
            if (job.Kind == FileJobKind.CreateZip && !outputName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                throw new RuntimeFailure("file-archive", "Use a .zip extension for the archive name.");
            if (job.Kind == FileJobKind.ExtractZip && (sources.Length != 1 || !Path.GetExtension(sources[0]).Equals(".zip", StringComparison.OrdinalIgnoreCase)))
                throw new RuntimeFailure("file-archive", "Choose one ZIP file to extract.");
            _ = PathName(Path.Combine(destination, outputName));
        }
        else if (outputName.Length != 0) throw new RuntimeFailure("file-job", "Output names apply only to archive jobs.");
        return job with { Sources = sources, Destination = destination, OutputName = outputName };
    }
}
