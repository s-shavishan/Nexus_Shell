using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

public readonly record struct DockWindowIdentity(IntPtr Handle, int ProcessId)
{
    public static DockWindowIdentity Of(RunningWindow window) => new(window.Handle, window.ProcessId);
}

// EnumWindows is in z-order, which changes on every activation. Dock order is
// lifecycle order: existing buttons stay in place; newly opened windows append.
public sealed class DockWindowSet
{
    private readonly List<RunningWindow> _windows = [];
    public IReadOnlyList<RunningWindow> Windows => _windows;
    public bool Reconcile(IReadOnlyList<RunningWindow> snapshot, IReadOnlySet<IntPtr>? destroyedHandles = null)
    {
        var current = snapshot.Where(w => w.Handle != IntPtr.Zero && w.ProcessId > 0)
            .DistinctBy(DockWindowIdentity.Of).ToDictionary(DockWindowIdentity.Of);
        bool changed = false;
        for (int i = _windows.Count - 1; i >= 0; i--)
        {
            var key = DockWindowIdentity.Of(_windows[i]);
            // Destroy followed by create can reuse the same HWND and PID
            // between scans. Treat the new instance as a fresh dock entry.
            if (destroyedHandles?.Contains(key.Handle) == true)
            {
                _windows.RemoveAt(i);
                changed = true;
                continue;
            }
            if (current.Remove(key, out var updated))
            {
                if (_windows[i] != updated) { _windows[i] = updated; changed = true; }
            }
            else { _windows.RemoveAt(i); changed = true; }
        }
        foreach (var window in snapshot)
        {
            if (current.Remove(DockWindowIdentity.Of(window)))
            { _windows.Add(window); changed = true; }
        }
        return changed;
    }
}
