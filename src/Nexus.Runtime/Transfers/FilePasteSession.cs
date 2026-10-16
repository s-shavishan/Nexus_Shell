using System.Security.Cryptography;
using System.Text.Json;

namespace Nexus.Runtime.Transfers;

public sealed record FileClipboardContent(uint Sequence, FileJobKind Kind, string[] Paths);

// A lost IPC reply retains the exact submission, including its ID. A cut also
// has a stable session-scoped ID across Files restarts and destination changes.
// Core therefore cannot admit the same cut twice to two different folders.
public sealed class FilePasteSession
{
    private (Guid Epoch, FileClipboardContent Clipboard, FileJobSpec Job)? _pending;
    private (Guid Epoch, uint Sequence)? _acceptedCut;
    public FileJobSpec Prepare(Guid epoch, FileClipboardContent clipboard, string destination)
    {
        if (epoch == Guid.Empty || clipboard is null || clipboard.Sequence == 0 || clipboard.Kind is not (FileJobKind.Copy or FileJobKind.Move))
            throw new RuntimeFailure("file-clipboard", "The file clipboard changed. Copy or cut the items again.");
        var job = FileJobRules.Validate(new(Guid.NewGuid(), clipboard.Kind, clipboard.Paths, destination));
        if (job.Kind == FileJobKind.Move && job.Sources.Any(path => FileJobRules.Paths.Equals(Path.GetDirectoryName(path), job.Destination)))
            throw new RuntimeFailure("file-clipboard", "A selected item is already in this folder. Cut items from another folder, or use Copy to make duplicates.");
        if (_acceptedCut == (epoch, clipboard.Sequence) && job.Kind == FileJobKind.Move)
            throw new RuntimeFailure("file-clipboard", "This cut is already in File Jobs. Cut the items again to start another move.");
        if (_pending is { } pending)
        {
            if (pending.Epoch != epoch)
                throw new RuntimeFailure("file-clipboard-restarted", "The transfer service restarted while a paste was unresolved. Review interrupted File Jobs and reopen Files before another paste.");
            if (pending.Clipboard.Sequence != clipboard.Sequence
                || pending.Job.Kind != job.Kind || !pending.Job.Sources.SequenceEqual(job.Sources, FileJobRules.Paths)
                || !FileJobRules.Paths.Equals(pending.Job.Destination, job.Destination))
                throw new RuntimeFailure("file-clipboard-pending", "A paste response was lost. Return to " + pending.Job.Destination + " and paste the original selection again to reconcile it, or review File Jobs before reopening Files.");
            return Clone(pending.Job);
        }
        if (job.Kind == FileJobKind.Move)
        {
            var identity = JsonSerializer.SerializeToUtf8Bytes(new { epoch, clipboard.Sequence,
                Sources = job.Sources.Select(path => OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path).ToArray() });
            job = job with { Id = new Guid(SHA256.HashData(identity).AsSpan(0, 16)) };
        }
        _pending = (epoch, new(clipboard.Sequence, clipboard.Kind, job.Sources.ToArray()), Clone(job));
        return Clone(job);
    }
    public void Accepted(Guid id)
    {
        if (_pending is not { } pending || pending.Job.Id != id) return;
        if (pending.Job.Kind == FileJobKind.Move) _acceptedCut = (pending.Epoch, pending.Clipboard.Sequence);
        _pending = null;
    }
    public void Rejected(Guid id) { if (_pending?.Job.Id == id) _pending = null; }
    private static FileJobSpec Clone(FileJobSpec job) => job with { Sources = job.Sources.ToArray() };
}
