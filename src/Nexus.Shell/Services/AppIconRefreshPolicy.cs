namespace Nexus.Shell.Services;

// The WinUI icon cache is owned by the UI thread. An explicit user refresh
// should discard completed thumbnails, even successful ones, because desktop
// shortcut targets/icons may have changed since the original lookup.
// Leave active loads alone to avoid scheduling duplicate filesystem requests.
public static class AppIconRefreshPolicy
{
    public static int EvictCompleted<T>(Dictionary<string, Task<T>> cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        int removed = 0;
        foreach (var key in cache.Where(entry => entry.Value.IsCompleted).Select(entry => entry.Key).ToArray())
            if (cache.Remove(key)) removed++;
        return removed;
    }
}
