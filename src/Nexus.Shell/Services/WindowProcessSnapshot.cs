namespace Nexus.Shell.Services;

// Window enumeration may encounter dozens of HWNDs belonging to one process.
// Resolve the process/session metadata once per PID, including failures.
// A fresh instance for each enumeration avoids stale PID-reuse assumptions.
public sealed class WindowProcessSnapshot
{
    private readonly int _session;
    private readonly Func<int, (int Session, string Name)?> _resolve;
    private readonly Dictionary<int, string?> _cache = [];

    public WindowProcessSnapshot(int session, Func<int, (int Session, string Name)?> resolve)
    {
        _session = session;
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
    }

    public bool TryGetName(int pid, out string name)
    {
        name = "";
        if (pid <= 0) return false;
        if (!_cache.TryGetValue(pid, out var cached))
        {
            var details = _resolve(pid);
            cached = details is { } value && value.Session == _session
                && !string.IsNullOrWhiteSpace(value.Name) ? value.Name : null;
            _cache.Add(pid, cached);
        }
        if (cached is null) return false;
        name = cached;
        return true;
    }
}
