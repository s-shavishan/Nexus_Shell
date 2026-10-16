// නමෝ බුද්ධාය 🤍
using System.Diagnostics;

namespace Nexus.Runtime.Storage;

public enum StorageScanState { Complete, Cancelled, Limited }
public sealed record StorageScanOptions(string Root, int MaximumEntries = 500_000, int MaximumFolders = 25_000,
    int MaximumDepth = 128, int LargestFiles = 100, bool IncludeHidden = true);
public sealed record StorageProgress(string Path, long Files, int Folders, long Bytes, long Skipped, TimeSpan Elapsed);
public sealed record StorageFolder(string Path, string? Parent, long Bytes, long Files, int Folders, bool Partial);
public sealed record StorageFile(string Path, long Bytes, DateTime ModifiedUtc);
public sealed record StorageFileType(string Extension, long Bytes, long Files);
public sealed record StorageSkip(string Path, string Reason);
public sealed record StorageScanResult(string Root, StorageScanState State, long Bytes, long Files, int Folders,
    long Skipped, long Examined, TimeSpan Elapsed, DateTimeOffset ObservedAt, StorageFolder[] Tree,
    StorageFile[] LargestFiles, StorageFileType[] FileTypes, StorageSkip[] SkippedPaths);

/// <summary>Read-only logical-size inventory. No file contents, links, elevation or deletion.</summary>
public static class StorageScanner
{
    public const FileAttributes RecallAttributes = (FileAttributes)(0x00040000 | 0x00400000);
    public const int MaximumSkipSamples = 100;
    private sealed class Folder(string path, string? parent, int depth)
    {
        internal readonly string Path = path;
        internal readonly string? Parent = parent;
        internal readonly int Depth = depth;
        internal long Bytes, Files;
        internal int Folders;
        internal bool Partial;
    }
    private sealed record Frame(Folder Folder, IEnumerator<FileSystemInfo> Items);

    public static string Validate(StorageScanOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Root) || options.Root.Length > 32767 || options.Root.Any(char.IsControl)
            || !Path.IsPathFullyQualified(options.Root)) throw new ArgumentException("Choose a full folder path.");
        if (options.MaximumEntries is < 1 or > 1_000_000 || options.MaximumFolders is < 1 or > 50_000
            || options.MaximumDepth is < 1 or > 128 || options.LargestFiles is < 1 or > 500)
            throw new ArgumentException("The storage scan limits are invalid.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.Root));
    }

    public static string SkipReason(FileAttributes attributes, bool includeHidden)
        => (attributes & FileAttributes.ReparsePoint) != 0 ? "Link, junction or reparse point was not followed."
            : (attributes & (FileAttributes.Offline | RecallAttributes)) != 0 ? "Offline or cloud-recall item was not opened."
            : !includeHidden && (attributes & FileAttributes.Hidden) != 0 ? "Hidden item excluded by scan preference." : "";

    public static StorageScanResult Scan(StorageScanOptions options, Action<StorageProgress>? progress = null, CancellationToken cancellation = default)
    {
        string root = Validate(options);
        var watch = Stopwatch.StartNew();
        var tree = new List<Folder> { new(root, null, 0) };
        var frames = new Stack<Frame>();
        var largest = new PriorityQueue<StorageFile, long>();
        var types = new Dictionary<string, (long Bytes, long Files)>(StringComparer.OrdinalIgnoreCase);
        var skippedPaths = new List<StorageSkip>();
        long files = 0, bytes = 0, skipped = 0, examined = 0, lastProgress = 0;
        StorageScanState state = StorageScanState.Complete;
        void Skip(Folder folder, string path, string reason)
        {
            skipped++; folder.Partial = true;
            if (skippedPaths.Count < MaximumSkipSamples) skippedPaths.Add(new(path, reason));
        }
        void Report(string path, bool force = false)
        {
            if (!force && watch.ElapsedMilliseconds - lastProgress < 250) return;
            lastProgress = watch.ElapsedMilliseconds;
            progress?.Invoke(new(path, files, tree.Count - 1, bytes, skipped, watch.Elapsed));
        }
        void Push(Folder folder)
        {
            try
            {
                var items = new DirectoryInfo(folder.Path).EnumerateFileSystemInfos("*", new EnumerationOptions
                { RecurseSubdirectories = false, IgnoreInaccessible = false, AttributesToSkip = 0, ReturnSpecialDirectories = false, BufferSize = 16 * 1024 }).GetEnumerator();
                frames.Push(new(folder, items));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { Skip(folder, folder.Path, error.Message); }
        }
        try
        {
            if (!cancellation.IsCancellationRequested)
            {
                var info = new DirectoryInfo(root);
                if (!info.Exists) throw new DirectoryNotFoundException("This folder is unavailable or access is denied.");
                string refusal = SkipReason(info.Attributes, true);
                if (refusal.Length > 0) throw new IOException("Choose a local folder without a link/cloud-recall root. " + refusal);
                Push(tree[0]);
            }
            while (frames.Count > 0)
            {
                if (cancellation.IsCancellationRequested) { state = StorageScanState.Cancelled; break; }
                if (examined >= options.MaximumEntries) { state = StorageScanState.Limited; break; }
                var frame = frames.Peek();
                FileSystemInfo item;
                try
                {
                    if (!frame.Items.MoveNext()) { frames.Pop().Items.Dispose(); continue; }
                    item = frame.Items.Current;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                { Skip(frame.Folder, frame.Folder.Path, error.Message); frames.Pop().Items.Dispose(); continue; }
                examined++;
                try
                {
                    string reason = SkipReason(item.Attributes, options.IncludeHidden);
                    if (reason.Length > 0) { Skip(frame.Folder, item.FullName, reason); Report(item.FullName); continue; }
                    if ((item.Attributes & FileAttributes.Directory) != 0)
                    {
                        if (tree.Count >= options.MaximumFolders)
                        { state = StorageScanState.Limited; Skip(frame.Folder, item.FullName, "Folder limit reached."); Report(item.FullName); continue; }
                        var child = new Folder(item.FullName, frame.Folder.Path, frame.Folder.Depth + 1);
                        tree.Add(child);
                        if (child.Depth > options.MaximumDepth)
                        { state = StorageScanState.Limited; Skip(child, child.Path, "Depth limit reached."); }
                        else Push(child);
                    }
                    else
                    {
                        var file = item as FileInfo ?? new FileInfo(item.FullName);
                        long length = file.Length;
                        DateTime modified = file.LastWriteTimeUtc;
                        if (length < 0) throw new IOException("The file reported an invalid length.");
                        frame.Folder.Bytes = checked(frame.Folder.Bytes + length);
                        frame.Folder.Files++;
                        files++; bytes = checked(bytes + length);
                        largest.Enqueue(new(item.FullName, length, modified), length);
                        if (largest.Count > options.LargestFiles) largest.Dequeue();
                        string extension = Path.GetExtension(item.Name).ToLowerInvariant();
                        if (extension.Length == 0) extension = "(no extension)";
                        if (extension.Length > 40 || !types.ContainsKey(extension) && types.Count >= 255) extension = "(other)";
                        var type = types.GetValueOrDefault(extension);
                        types[extension] = (checked(type.Bytes + length), type.Files + 1);
                    }
                    Report(item.FullName);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                { Skip(frame.Folder, item.FullName, error.Message); }
            }
            if (cancellation.IsCancellationRequested) state = StorageScanState.Cancelled;
        }
        finally
        {
            while (frames.Count > 0) frames.Pop().Items.Dispose();
        }
        var byPath = tree.ToDictionary(f => f.Path, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        if (state != StorageScanState.Complete) tree[0].Partial = true;
        for (int i = tree.Count - 1; i > 0; i--)
        {
            var folder = tree[i]; var parent = byPath[folder.Parent!];
            parent.Bytes = checked(parent.Bytes + folder.Bytes); parent.Files += folder.Files;
            parent.Folders += folder.Folders + 1; parent.Partial |= folder.Partial;
        }
        Report(root, true);
        return new(root, state, bytes, files, tree.Count - 1, skipped, examined, watch.Elapsed, DateTimeOffset.Now,
            tree.Select(f => new StorageFolder(f.Path, f.Parent, f.Bytes, f.Files, f.Folders, f.Partial)).ToArray(),
            largest.UnorderedItems.Select(f => f.Element).OrderByDescending(f => f.Bytes).ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToArray(),
            types.Select(t => new StorageFileType(t.Key, t.Value.Bytes, t.Value.Files)).OrderByDescending(t => t.Bytes).ToArray(), skippedPaths.ToArray());
    }
}

/// <summary>Keep one physical reader across closed/reopened utility windows, even if an OS read stalls.</summary>
public static class StorageScanRunner
{
    private static int _running;
    public static Task<StorageScanResult>? TryStart(StorageScanOptions options, Action<StorageProgress>? progress, CancellationToken cancellation)
    {
        StorageScanner.Validate(options);
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return null;
        return Run();
        async Task<StorageScanResult> Run()
        {
            try { return await Task.Run(() => StorageScanner.Scan(options, progress, cancellation)).ConfigureAwait(false); }
            finally { Volatile.Write(ref _running, 0); }
        }
    }
}
