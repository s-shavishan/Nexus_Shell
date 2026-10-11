namespace Nexus.Shell.Services;

// Align the visible frame, preserving the native invisible resize borders.
// Only the native adapter decides whether a window is an eligible maximized app.
public static class MaximizedWindowLayout
{
    public static ShellRect? Repair(ShellRect outer, ShellRect visible, ShellRect work, double scale)
    {
        static bool Valid(ShellRect r) => r.Width > 0 && r.Height > 0 && (long)r.X + r.Width is >= int.MinValue and <= int.MaxValue
            && (long)r.Y + r.Height is >= int.MinValue and <= int.MaxValue;
        if (!Valid(outer) || !Valid(visible) || !Valid(work)) return null;
        if (Math.Abs((long)visible.X - work.X) <= 2 && Math.Abs((long)visible.Y - work.Y) <= 2
            && Math.Abs((long)visible.Right - work.Right) <= 2 && Math.Abs((long)visible.Bottom - work.Bottom) <= 2) return null;
        int limit = (int)Math.Round(48 * (double.IsFinite(scale) ? Math.Clamp(scale, .5, 4) : 1));
        long left = (long)visible.X - outer.X, top = (long)visible.Y - outer.Y;
        long right = (long)outer.Right - visible.Right, bottom = (long)outer.Bottom - visible.Bottom;
        if (new[] { left, top, right, bottom }.Any(inset => inset < 0 || inset > limit)) return null;
        long x = (long)work.X - left, y = (long)work.Y - top;
        long width = (long)work.Width + left + right, height = (long)work.Height + top + bottom;
        if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue
            || width > int.MaxValue || height > int.MaxValue || x + width > int.MaxValue || y + height > int.MaxValue) return null;
        return new((int)x, (int)y, (int)width, (int)height);
    }
}

// A resistant app must never create an endless geometry fight with the shell.
public sealed class WindowRepairBudget
{
    private sealed record Entry(int Process, ShellRect Work, int Attempts, long Last);
    private readonly Dictionary<long, Entry> _entries = [];
    public bool Admit(long handle, int process, ShellRect work, long now)
    {
        if (handle == 0 || process <= 0) return false;
        if (!_entries.TryGetValue(handle, out var entry) || entry.Process != process || entry.Work != work)
        {
            // Evicting a resistant live window would renew its retry budget on
            // every scan of a large desktop. Prune closed windows separately.
            if (_entries.Count >= 64 && !_entries.ContainsKey(handle)) return false;
            _entries[handle] = new(process, work, 1, now); return true;
        }
        if (entry.Attempts >= 2 || now < entry.Last || now - entry.Last < 1000) return false;
        _entries[handle] = entry with { Attempts = entry.Attempts + 1, Last = now }; return true;
    }
    public void Forget(long handle) => _entries.Remove(handle);
    public void Prune(IEnumerable<long> liveHandles)
    { var live = liveHandles.ToHashSet(); foreach (long handle in _entries.Keys.Where(handle => !live.Contains(handle)).ToArray()) _entries.Remove(handle); }
    public void Reset() => _entries.Clear();
}
