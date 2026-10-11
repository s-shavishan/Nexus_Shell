namespace Nexus.Shell.Services;

// Root-only observation; callers coalesce callbacks before refreshing icons.
public sealed class DesktopFileMonitor : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = [];
    private int _disposed, _failed;
    public int FolderCount => _watchers.Count;
    public bool HadError => Volatile.Read(ref _failed) != 0;
    public DesktopFileMonitor(IEnumerable<string> folders, Action changed, Action<Exception> failed)
    {
        void Changed(object sender, FileSystemEventArgs args) { if (Volatile.Read(ref _disposed) == 0) changed(); }
        void Error(Exception error)
        { if (Volatile.Read(ref _disposed) != 0) return; if (Interlocked.Exchange(ref _failed, 1) == 0) failed(error); changed(); }
        foreach (string path in folders.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).Take(2))
        {
            FileSystemWatcher? watcher = null;
            try
            {
                watcher = new(path) { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite, InternalBufferSize = 8192 };
                watcher.Created += Changed; watcher.Deleted += Changed; watcher.Renamed += Changed; watcher.Changed += Changed;
                watcher.Error += (_, args) => Error(args.GetException()); watcher.EnableRaisingEvents = true; _watchers.Add(watcher);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            { watcher?.Dispose(); Error(error); }
        }
    }
    public void Dispose()
    { if (Interlocked.Exchange(ref _disposed, 1) != 0) return; foreach (var watcher in _watchers) watcher.Dispose(); }
}
