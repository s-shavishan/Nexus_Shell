using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Nexus.Runtime.Transfers;

public sealed partial class FileTransferEngine
{
    private sealed record PackItem(string Source, string Name, bool Directory, long Bytes, DateTime Modified);
    private sealed record UnpackItem(ZipArchiveEntry Entry, string Name, bool Directory);
    private async Task ArchiveAsync(FileJobSpec job, Action<FileJobProgress> report, FileTransferFlow flow, CancellationToken cancellation)
    {
        string requested = Path.Combine(job.Destination, job.OutputName);
        string staged = Path.Combine(job.Destination, ".nexus-job-" + job.Id.ToString("N") + "-" + Guid.NewGuid().ToString("N") + ".partial");
        bool directory = job.Kind == FileJobKind.ExtractZip, committed = false;
        long copied = 0, total = 0; int files = 0;
        void Progress(string phase, string path, int complete = 0, int skipped = 0)
            => report(new(phase, path, copied, total, complete, skipped, files));
        Progress("Planning archive", job.Sources[0]);
        try
        {
            if (!directory)
            {
                var plan = new List<PackItem>(); var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                long metadata = 0;
                foreach (string path in job.Sources)
                {
                    await flow.WaitAsync(cancellation).ConfigureAwait(false);
                    var root = Plan(path, cancellation);
                    string leaf = ArchiveJobRules.Leaf(Path.GetFileName(root.Path)), chosen = leaf;
                    for (int number = 1; !roots.Add(chosen); number++)
                    {
                        if (number >= 1000) throw new IOException("Too many identical archive root names.");
                        chosen = Path.GetFileName(Candidate(Path.Combine(job.Destination, leaf), number, root.Directory));
                    }
                    void Add(string source, string name, bool folder, long bytes)
                    {
                        name = ArchiveJobRules.EntryPath(name);
                        if (plan.Count >= FileJobRules.MaximumEntries) throw new IOException("This archive exceeds 10,000 file/folder entries.");
                        metadata += 46 + Encoding.UTF8.GetByteCount(name) + 1;
                        if (metadata > ArchiveJobRules.MaximumMetadataBytes) throw new IOException("This archive has too much filename metadata.");
                        total = checked(total + bytes);
                        if (total > ArchiveJobRules.MaximumBytes) throw new IOException("ZIP jobs support up to 64 GiB of file contents.");
                        plan.Add(new(source, name, folder, bytes, folder ? Directory.GetLastWriteTimeUtc(source) : File.GetLastWriteTimeUtc(source)));
                    }
                    if (root.Directory)
                    {
                        Add(root.Path, chosen, true, 0);
                        foreach (string relative in root.Directories)
                            Add(Path.Combine(root.Path, relative), chosen + "/" + relative.Replace(Path.DirectorySeparatorChar, '/'), true, 0);
                        foreach (var file in root.Files)
                            Add(file.Path, chosen + "/" + file.Relative.Replace(Path.DirectorySeparatorChar, '/'), false, file.Bytes);
                    }
                    else Add(root.Path, chosen, false, root.Files[0].Bytes);
                }
                // A Windows extraction must not contain case aliases, duplicate
                // names or a file standing where another entry needs a folder.
                ValidateArchiveTree(plan.Select(item => (item.Name, item.Directory)), staged);
                files = plan.Count(item => !item.Directory);
                if (job.Collision == FileCollision.Skip && Exists(requested)) { Progress("Skipped", requested, skipped: files); return; }
                var hashes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                int zone = 0;
                await using (var output = new FileStream(staged, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 128 * 1024, FileOptions.Asynchronous))
                {
                    using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
                    {
                        foreach (var item in plan)
                        {
                            await flow.WaitAsync(cancellation).ConfigureAwait(false);
                            RejectLinks(item.Source);
                            var entry = archive.CreateEntry(item.Name + (item.Directory ? "/" : ""), CompressionLevel.Fastest);
                            // ZIP stores local DOS time without an offset. Use
                            // local components so same-zone round trips retain
                            // the UTC instant within ZIP's two-second precision.
                            DateTimeOffset time = new DateTimeOffset(item.Modified).ToLocalTime();
                            entry.LastWriteTime = time.Year < 1980 ? new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero)
                                : time.Year > 2107 ? new(2107, 12, 31, 23, 59, 58, TimeSpan.Zero) : time;
                            if (item.Directory) continue;
                            zone = Math.Max(zone, ArchiveOriginZone.Read(item.Source));
                            using var source = (openSource ?? ((path, _) => new ReadSource(path)))(item.Source, false);
                            await using var target = entry.Open();
                            var digest = await PumpArchiveAsync(source.Content, target, item.Bytes, job.Verify, false,
                                bytes => { copied += bytes; Progress("Compressing", item.Source); }, flow, cancellation).ConfigureAwait(false);
                            if (source.Content.Length != item.Bytes) throw new IOException("An archive source changed size while being read.");
                            if (digest.Hash is { } hash) hashes.Add(item.Name, hash);
                        }
                        await flow.WaitAsync(cancellation).ConfigureAwait(false);
                    }
                    await output.FlushAsync(cancellation).ConfigureAwait(false); output.Flush(true);
                    if (output.Length > ArchiveJobRules.MaximumBytes) throw new IOException("The resulting ZIP exceeds the 64 GiB archive limit.");
                    if (job.Verify)
                    {
                        Progress("Verifying archive", job.OutputName);
                        await ZipArchiveEnvelope.ValidateAsync(output, flow, cancellation).ConfigureAwait(false);
                        using var verification = new ZipArchive(output, ZipArchiveMode.Read, true);
                        var expectedNames = plan.Select(item => item.Name + (item.Directory ? "/" : "")).ToHashSet(StringComparer.Ordinal);
                        foreach (var entry in verification.Entries)
                        {
                            if (!expectedNames.Remove(entry.FullName)) throw new IOException("ZIP entry names failed verification.");
                            if (entry.FullName.EndsWith('/')) continue;
                            await using var input = entry.Open();
                            var digest = await PumpArchiveAsync(input, Stream.Null, entry.Length, true, true, _ => { }, flow, cancellation).ConfigureAwait(false);
                            if (digest.Crc != entry.Crc32 || !hashes.TryGetValue(entry.FullName, out var expected)
                                || !CryptographicOperations.FixedTimeEquals(expected, digest.Hash!))
                                throw new IOException("The ZIP contents failed verification. Source files are retained.");
                        }
                        if (expectedNames.Count != 0) throw new IOException("The ZIP is missing expected entries. Source files are retained.");
                    }
                }
                ArchiveOriginZone.Write(staged, zone);
            }
            else
            {
                string sourcePath = job.Sources[0]; RejectLinks(sourcePath);
                using var source = (openSource ?? ((path, _) => new ReadSource(path)))(sourcePath, false);
                int declared = await ZipArchiveEnvelope.ValidateAsync(source.Content, flow, cancellation).ConfigureAwait(false);
                using var archive = new ZipArchive(source.Content, ZipArchiveMode.Read, true);
                if (archive.Entries.Count != declared) throw new InvalidDataException("ZIP entry counts do not match.");
                var plan = new List<UnpackItem>();
                foreach (var entry in archive.Entries)
                {
                    await flow.WaitAsync(cancellation).ConfigureAwait(false);
                    int mode = (entry.ExternalAttributes >> 16) & 0xF000;
                    bool folder = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\') || (entry.ExternalAttributes & 0x10) != 0 || mode == 0x4000;
                    if (entry.IsEncrypted || mode is not (0 or 0x4000 or 0x8000) || mode == 0x8000 && folder
                        || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0 || folder && (entry.Length != 0 || entry.Crc32 != 0))
                        throw new InvalidDataException("Linked, special or encrypted ZIP entries are unavailable.");
                    total = checked(total + entry.Length);
                    if (total > ArchiveJobRules.MaximumBytes || entry.Length < 0) throw new InvalidDataException("ZIP contents exceed the 64 GiB extraction limit.");
                    plan.Add(new(entry, ArchiveJobRules.EntryPath(entry.FullName), folder));
                }
                string[] folders = ValidateArchiveTree(plan.Select(item => (item.Name, item.Directory)), staged);
                files = plan.Count(item => !item.Directory);
                if (job.Collision == FileCollision.Skip && Exists(requested)) { Progress("Skipped", requested, skipped: files); return; }
                int zone = ArchiveOriginZone.Read(sourcePath);
                if (Exists(staged)) throw new IOException("The archive staging name is already in use.");
                Directory.CreateDirectory(staged); RejectLinks(staged);
                foreach (string folder in folders)
                {
                    await flow.WaitAsync(cancellation).ConfigureAwait(false);
                    string target = Path.Combine(staged, folder.Replace('/', Path.DirectorySeparatorChar));
                    RejectLinks(Path.GetDirectoryName(target)!); Directory.CreateDirectory(target); RejectLinks(target);
                }
                foreach (var item in plan.Where(item => !item.Directory))
                {
                    await flow.WaitAsync(cancellation).ConfigureAwait(false);
                    string target = Path.Combine(staged, item.Name.Replace('/', Path.DirectorySeparatorChar)); RejectLinks(Path.GetDirectoryName(target)!);
                    await using (var input = item.Entry.Open())
                    await using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 128 * 1024, FileOptions.Asynchronous))
                    {
                        var digest = await PumpArchiveAsync(input, output, item.Entry.Length, job.Verify, true,
                            bytes => { copied += bytes; Progress("Extracting", item.Name); }, flow, cancellation).ConfigureAwait(false);
                        await output.FlushAsync(cancellation).ConfigureAwait(false); output.Flush(true);
                        if (digest.Crc != item.Entry.Crc32) throw new InvalidDataException("A ZIP entry failed its CRC integrity check: " + item.Name);
                        if (job.Verify)
                        {
                            Progress("Verifying archive", item.Name); output.Position = 0;
                            var actual = await PumpArchiveAsync(output, Stream.Null, item.Entry.Length, true, false, _ => { }, flow, cancellation).ConfigureAwait(false);
                            if (!CryptographicOperations.FixedTimeEquals(digest.Hash!, actual.Hash!)) throw new IOException("Extracted contents failed SHA-256 verification.");
                        }
                    }
                    File.SetLastWriteTimeUtc(target, item.Entry.LastWriteTime.UtcDateTime);
                    ArchiveOriginZone.Write(target, zone);
                }
                foreach (var item in plan.Where(item => item.Directory).OrderByDescending(item => item.Name.Length))
                    Directory.SetLastWriteTimeUtc(Path.Combine(staged, item.Name.Replace('/', Path.DirectorySeparatorChar)), item.Entry.LastWriteTime.UtcDateTime);
                _ = Plan(staged, cancellation); // Recheck the entire fresh tree before publication.
            }
            Progress("Committing archive", requested);
            await flow.WaitAsync(cancellation).ConfigureAwait(false);
            string? result = Commit(staged, requested, directory, job.Collision); committed = result is not null;
            Progress(result is null ? "Skipped" : "Finished", result ?? requested, committed ? files : 0, committed ? 0 : files);
        }
        finally
        {
            if (!committed && Exists(staged))
                try { CleanStage(staged, directory); }
                catch (Exception error) { throw new IOException("Archive staging could not be removed. Review " + staged + ". " + error.Message, error); }
        }
    }
    private static string[] ValidateArchiveTree(IEnumerable<(string Name, bool Directory)> entries, string staged)
    {
        var nodes = new Dictionary<string, (bool Directory, string Name)>(StringComparer.OrdinalIgnoreCase);
        var explicitNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, directory) in entries)
        {
            if (!explicitNames.Add(name)) throw new InvalidDataException("ZIP entry names collide on Windows: " + name);
            string[] parts = name.Split('/'); string relative = "";
            for (int i = 0; i < parts.Length; i++)
            {
                relative = relative.Length == 0 ? parts[i] : relative + "/" + parts[i];
                bool folder = i < parts.Length - 1 || directory;
                if (nodes.TryGetValue(relative, out var known))
                { if (!known.Directory || !folder || known.Name != relative) throw new InvalidDataException("ZIP paths contain a case alias or file/folder conflict: " + relative); }
                else
                {
                    nodes.Add(relative, (folder, relative));
                    string full = FileJobRules.PathName(Path.Combine(staged, relative.Replace('/', Path.DirectorySeparatorChar)));
                    if (!FileJobRules.Within(full, staged) || FileJobRules.Paths.Equals(full, staged)) throw new InvalidDataException("A ZIP entry escapes its extraction folder.");
                }
                if (nodes.Count > FileJobRules.MaximumEntries) throw new InvalidDataException("ZIP contents exceed 10,000 files/folders, including implied directories.");
            }
        }
        return nodes.Where(node => node.Value.Directory).Select(node => node.Key).OrderBy(name => name.Count(ch => ch == '/')).ToArray();
    }
    private static readonly uint[] ArchiveCrcTable = Enumerable.Range(0, 256).Select(number =>
    {
        uint value = (uint)number;
        for (int i = 0; i < 8; i++) value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
        return value;
    }).ToArray();
    private static uint ArchiveCrc(uint crc, byte[] buffer, int count)
    { for (int i = 0; i < count; i++) crc = ArchiveCrcTable[(crc ^ buffer[i]) & 255] ^ (crc >> 8); return crc; }
    private static async Task<(byte[]? Hash, uint Crc)> PumpArchiveAsync(Stream input, Stream output, long expected, bool sha, bool crc,
        Action<int> report, FileTransferFlow flow, CancellationToken cancellation)
    {
        using var hash = sha ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(128 * 1024); long bytes = 0; uint checksum = uint.MaxValue;
        try
        {
            while (true)
            {
                await flow.WaitAsync(cancellation).ConfigureAwait(false);
                int count = await input.ReadAsync(buffer.AsMemory(0, 128 * 1024), cancellation).ConfigureAwait(false);
                if (count == 0) break;
                if (count > expected - bytes) throw new InvalidDataException("Archive content exceeds its declared size.");
                await output.WriteAsync(buffer.AsMemory(0, count), cancellation).ConfigureAwait(false);
                hash?.AppendData(buffer, 0, count); if (crc) checksum = ArchiveCrc(checksum, buffer, count);
                bytes += count; report(count);
            }
            if (bytes != expected) throw new InvalidDataException("Archive content does not match its declared size.");
            return (hash?.GetHashAndReset(), ~checksum);
        }
        finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
    }
}
