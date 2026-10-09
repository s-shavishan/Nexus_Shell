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
    public void Reconcile(IReadOnlyList<RunningWindow> snapshot)
    {
        var current = snapshot.Where(w => w.Handle != IntPtr.Zero && w.ProcessId > 0)
            .DistinctBy(DockWindowIdentity.Of).ToDictionary(DockWindowIdentity.Of);
        for (int i = _windows.Count - 1; i >= 0; i--)
        {
            var key = DockWindowIdentity.Of(_windows[i]);
            if (current.Remove(key, out var updated)) _windows[i] = updated;
            else _windows.RemoveAt(i);
        }
        foreach (var window in snapshot)
            if (current.Remove(DockWindowIdentity.Of(window))) _windows.Add(window);
    }
}
