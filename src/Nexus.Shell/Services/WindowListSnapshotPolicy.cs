using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

// Preserve a stable native window snapshot reference across identical scans.
// RunningWindow is an immutable record; compare metadata as well as HWND/PID.
// Order matters for the window board's current z-order, even though the dock
// separately retains its own lifecycle button order.
internal static class WindowListSnapshotPolicy
{
    // Destroying a tooltip or a non-tasklist tool HWND should not rebuild
    // the dock when its actual window set and metadata have not changed.
    // Explicit reset always wins, including same-HWND/same-PID recreation.
    internal static bool RequiresDockRefresh(
        IReadOnlyList<RunningWindow> previous,
        IReadOnlyList<RunningWindow> next,
        IReadOnlySet<IntPtr> destroyedHandles,
        bool resetAll)
    {
        ArgumentNullException.ThrowIfNull(destroyedHandles);
        if (resetAll || !EquivalentDockEntries(previous, next)) return true;
        foreach (var window in previous)
            if (destroyedHandles.Contains(window.Handle)) return true;
        return false;
    }

    // The window board needs the latest native z-order, but the dock keeps
    // its own stable lifecycle order. Moving an already-known window to the
    // foreground must not invoke TaskbarView.Refresh just to reshuffle a
    // structurally identical task list. Compare identities AND metadata.
    internal static bool EquivalentDockEntries(
        IReadOnlyList<RunningWindow> previous,
        IReadOnlyList<RunningWindow> next)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(next);
        if (ReferenceEquals(previous, next)) return true;
        if (previous.Count != next.Count) return false;

        var byIdentity = new Dictionary<DockWindowIdentity, RunningWindow>(previous.Count);
        foreach (var window in previous)
            if (!byIdentity.TryAdd(DockWindowIdentity.Of(window), window)) return false;
        foreach (var window in next)
        {
            var key = DockWindowIdentity.Of(window);
            if (!byIdentity.Remove(key, out var prior) || prior != window) return false;
        }
        return byIdentity.Count == 0;
    }

    internal static bool Equivalent(IReadOnlyList<RunningWindow> previous, IReadOnlyList<RunningWindow> next)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(next);
        if (ReferenceEquals(previous, next)) return true;
        if (previous.Count != next.Count) return false;
        for (int index = 0; index < previous.Count; index++)
            if (previous[index] != next[index]) return false;
        return true;
    }
}
