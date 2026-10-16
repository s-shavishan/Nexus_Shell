// නමෝ බුද්ධාය 🤍
using System.Security.Cryptography;

namespace Nexus.Runtime.Transfers;

public interface ITransferSource : IDisposable
{ Stream Content { get; } void CommitMove(); }

// File content runs in Core's queue worker. Every copy is staged beside its
// final destination, then committed without replacing an existing item.
public sealed partial class FileTransferEngine(Func<string, bool, ITransferSource>? openSource = null) : IFileTransferBackend
{
    private sealed class ReadSource(string path) : ITransferSource
    {
        public Stream Content { get; } = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        public void CommitMove() => throw new PlatformNotSupportedException("A Windows source handle is required for file moves.");
        public void Dispose() => Content.Dispose();
    }
    private sealed record Item(string Path, string Relative, long Bytes);
    private sealed record Root(string Path, bool Directory, Item[] Files, string[] Directories);
    public static void RejectLinks(string path)
    {
        string full = FileJobRules.PathName(path);
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            if (!File.Exists(current) && !Directory.Exists(current)) continue;
            var attributes = File.GetAttributes(current);
            if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Encrypted | FileAttributes.Offline)) != 0)
                throw new IOException("Linked, encrypted or offline items are unavailable in File Jobs: " + current);
        }
    }
    private static Root Plan(string source, CancellationToken cancellation)
    {
        RejectLinks(source); var attributes = File.GetAttributes(source);
        if ((attributes & FileAttributes.Directory) == 0) return new(source, false, [new(source, "", new FileInfo(source).Length)], []);
        var files = new List<Item>(); var directories = new List<string>(); var pending = new Stack<(string Path, int Depth)>(); pending.Push((source, 0)); int entries = 0;
        while (pending.TryPop(out var folder))
        {
            cancellation.ThrowIfCancellationRequested();
            if (folder.Depth > FileJobRules.MaximumDepth) throw new IOException("This folder exceeds File Jobs' 64-level limit.");
            RejectLinks(folder.Path);
            foreach (string entry in Directory.EnumerateFileSystemEntries(folder.Path))
            {
                cancellation.ThrowIfCancellationRequested();
                if (++entries > FileJobRules.MaximumEntries) throw new IOException("This folder exceeds the 10,000-entry job limit.");
                RejectLinks(entry); var kind = File.GetAttributes(entry); string relative = Path.GetRelativePath(source, entry);
                if ((kind & FileAttributes.Directory) != 0) { directories.Add(relative); pending.Push((entry, folder.Depth + 1)); }
                else files.Add(new(entry, relative, new FileInfo(entry).Length));
            }
        }
        return new(source, true, files.ToArray(), directories.ToArray());
    }
    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
    private static string Candidate(string path, int number, bool folder)
    {
        if (number == 0) return path;
        string extension = folder ? "" : Path.GetExtension(path); string stem = folder ? Path.GetFileName(path) : Path.GetFileNameWithoutExtension(path);
        return Path.Combine(Path.GetDirectoryName(path)!, stem + " (" + (number + 1) + ")" + extension);
    }
    private static string? Commit(string staged, string requested, bool folder, FileCollision collision)
    {
        for (int number = 0; number < 1000; number++)
        {
            string target = Candidate(requested, number, folder);
            RejectLinks(Path.GetDirectoryName(target)!);
            if (Exists(target)) { if (collision == FileCollision.Skip) return null; continue; }
            try { if (folder) Directory.Move(staged, target); else File.Move(staged, target, false); return target; }
            catch (IOException) when (Exists(target)) { if (collision == FileCollision.Skip) return null; }
        }
        throw new IOException("No free destination name was found after 1,000 collision checks.");
    }
    private static void CleanStage(string path, bool folder)
    {
        if (!Exists(path)) return; RejectLinks(path);
        if (folder)
        {
            // Only our uniquely named staging tree is removed. Never follow
            // a link planted into it by another process during cancellation.
            var plan = Plan(path, CancellationToken.None);
            foreach (var file in plan.Files) File.Delete(file.Path);
            foreach (string directory in plan.Directories.OrderByDescending(d => d.Length)) Directory.Delete(Path.Combine(path, directory), false);
            Directory.Delete(path, false);
        }
        else File.Delete(path);
    }
    public Task RunAsync(FileJobSpec request, Action<FileJobProgress> report, CancellationToken cancellation)
        => RunAsync(request, report, new FileTransferFlow(), cancellation);
    public async Task RunAsync(FileJobSpec request, Action<FileJobProgress> report, FileTransferFlow flow, CancellationToken cancellation)
    {
        await flow.WaitAsync(cancellation).ConfigureAwait(false);
        var job = FileJobRules.Validate(request); cancellation.ThrowIfCancellationRequested(); RejectLinks(job.Destination);
        if (!Directory.Exists(job.Destination)) throw new DirectoryNotFoundException("The destination folder is unavailable.");
        if (FileJobRules.Archive(job.Kind))
        { await ArchiveAsync(job, report, flow, cancellation).ConfigureAwait(false); return; }
        var roots = job.Sources.Select(path => Plan(path, cancellation)).ToArray();
        int count = roots.Sum(root => root.Files.Length + root.Directories.Length);
        if (count > FileJobRules.MaximumEntries) throw new IOException("This job exceeds 10,000 file/folder entries. Choose smaller sources.");
        long total = roots.SelectMany(root => root.Files).Aggregate(0L, (sum, file) => checked(sum + file.Bytes));
        int totalFiles = roots.Sum(root => root.Files.Length), completed = 0, skipped = 0; long copied = 0;
        void Progress(string phase, string current) => report(new(phase, current, copied, total, completed, skipped, totalFiles));
        Progress("Ready", "");
        foreach (var root in roots)
        {
            await flow.WaitAsync(cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested(); RejectLinks(root.Path); RejectLinks(job.Destination);
            string requested = Path.Combine(job.Destination, Path.GetFileName(root.Path));
            if (job.Collision == FileCollision.Skip && Exists(requested)) { skipped += root.Files.Length; Progress("Skipped", root.Path); continue; }
            if (root.Directory && job.Kind == FileJobKind.Move)
            {
                // Directory.Move is an atomic same-volume rename. Cross-volume
                // failures keep the entire source tree; no recursive deletion.
                Progress("Moving folder", root.Path); await flow.WaitAsync(cancellation).ConfigureAwait(false);
                string? result = Commit(root.Path, requested, true, job.Collision);
                if (result is null) skipped += root.Files.Length; else { completed += root.Files.Length; copied += root.Files.Sum(f => f.Bytes); }
                Progress("Committed", result ?? root.Path); continue;
            }
            string staged = Path.Combine(job.Destination, ".nexus-job-" + job.Id.ToString("N") + "-" + Guid.NewGuid().ToString("N") + ".partial");
            bool committed = false;
            try
            {
                if (root.Directory)
                {
                    Directory.CreateDirectory(staged);
                    foreach (string directory in root.Directories) { await flow.WaitAsync(cancellation).ConfigureAwait(false); Directory.CreateDirectory(Path.Combine(staged, directory)); }
                    foreach (var item in root.Files)
                    {
                        await CopyAsync(item.Path, Path.Combine(staged, item.Relative), false, job.Verify, bytes => { copied += bytes; Progress("Copying", item.Path); }, () => Progress("Verifying", item.Path), flow, cancellation).ConfigureAwait(false);
                    }
                    await flow.WaitAsync(cancellation).ConfigureAwait(false); string? result = Commit(staged, requested, true, job.Collision); committed = result is not null;
                    if (committed) completed += root.Files.Length; else skipped += root.Files.Length;
                    Progress("Committed", result ?? root.Path);
                }
                else
                {
                    using var source = (openSource ?? ((path, _) => new ReadSource(path)))(root.Path, job.Kind == FileJobKind.Move);
                    await CopyStreamAsync(source.Content, staged, job.Verify, bytes => { copied += bytes; Progress("Copying", root.Path); }, () => Progress("Verifying", root.Path), flow, cancellation).ConfigureAwait(false);
                    File.SetLastWriteTimeUtc(staged, File.GetLastWriteTimeUtc(root.Path));
                    await flow.WaitAsync(cancellation).ConfigureAwait(false); string? result = Commit(staged, requested, false, job.Collision); committed = result is not null;
                    if (!committed) { skipped++; Progress("Skipped", root.Path); continue; }
                    completed++; Progress("Committed", result!);
                    if (job.Kind == FileJobKind.Move)
                    {
                        await flow.WaitAsync(cancellation).ConfigureAwait(false);
                        try { source.CommitMove(); }
                        catch (Exception error) { throw new IOException("Copied successfully, but the original could not be removed. Both copies remain: " + error.Message, error); }
                    }
                }
            }
            finally
            {
                if (!committed && Exists(staged))
                    try { CleanStage(staged, root.Directory); }
                    catch (Exception error) { throw new IOException("Unfinished staging could not be removed. Review " + staged + ". " + error.Message, error); }
            }
        }
        Progress("Finished", "");
    }
    private async Task CopyAsync(string path, string staged, bool move, bool verify, Action<int> copied, Action verifying, FileTransferFlow flow, CancellationToken cancellation)
    {
        await flow.WaitAsync(cancellation).ConfigureAwait(false);
        RejectLinks(path); using var source = (openSource ?? ((name, _) => new ReadSource(name)))(path, move);
        await CopyStreamAsync(source.Content, staged, verify, copied, verifying, flow, cancellation).ConfigureAwait(false);
        File.SetLastWriteTimeUtc(staged, File.GetLastWriteTimeUtc(path));
    }
    private static async Task CopyStreamAsync(Stream input, string staged, bool verify, Action<int> copied, Action verifying, FileTransferFlow flow, CancellationToken cancellation)
    {
        RejectLinks(Path.GetDirectoryName(staged)!); long length = input.Length, read = 0;
        using var hash = verify ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;
        await using var output = new FileStream(staged, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] buffer = new byte[128 * 1024];
        while (true)
        {
            await flow.WaitAsync(cancellation).ConfigureAwait(false); int size = await input.ReadAsync(buffer, cancellation).ConfigureAwait(false); if (size == 0) break;
            await output.WriteAsync(buffer.AsMemory(0, size), cancellation).ConfigureAwait(false); hash?.AppendData(buffer, 0, size); read += size; copied(size);
        }
        if (read != length || input.Length != length) throw new IOException("The source changed size while it was being copied.");
        await flow.WaitAsync(cancellation).ConfigureAwait(false);
        await output.FlushAsync(cancellation).ConfigureAwait(false); output.Flush(true);
        if (verify)
        {
            verifying(); output.Position = 0;
            using var verification = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            while (true)
            {
                await flow.WaitAsync(cancellation).ConfigureAwait(false);
                int size = await output.ReadAsync(buffer, cancellation).ConfigureAwait(false); if (size == 0) break;
                verification.AppendData(buffer, 0, size);
            }
            byte[] actual = verification.GetHashAndReset();
            if (!CryptographicOperations.FixedTimeEquals(hash!.GetHashAndReset(), actual)) throw new IOException("The copied content failed SHA-256 verification. The original is retained.");
        }
    }
}
