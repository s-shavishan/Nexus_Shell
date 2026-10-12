namespace Nexus.Shell.Services;

// UI-thread-owned memoization of foreground-window identity for one immutable
// native enumeration snapshot. Normal dock visibility polls can run every
// 80 ms; don't rebuild pin/utility candidates if the HWND and snapshot are
// unchanged. Invalid handles and new snapshots always force re-resolution.
internal sealed class ForegroundLookupCache<T> where T : class
{
    private object? _snapshot;
    private IntPtr _handle;
    private T? _selection;
    private bool _known;

    internal T? Resolve(IntPtr foreground, object snapshot, Func<T?> discover,
        Func<T, bool> mayReuse, bool safeEmpty = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(discover);
        ArgumentNullException.ThrowIfNull(mayReuse);

        if (_known && ReferenceEquals(_snapshot, snapshot) && _handle == foreground)
        {
            if (_selection is { } previous)
            {
                if (mayReuse(previous)) return previous;
            }
            else if (safeEmpty) return null;
        }

        // Discovery may query Win32 and should not poison the cache on failure.
        var resolved = discover();
        _snapshot = snapshot;
        _handle = foreground;
        _selection = resolved;
        _known = true;
        return resolved;
    }

    internal void Reset()
    {
        _known = false;
        _snapshot = null;
        _handle = IntPtr.Zero;
        _selection = null;
    }
}
