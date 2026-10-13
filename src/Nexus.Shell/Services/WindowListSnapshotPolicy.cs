using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

// Preserve a stable native window snapshot reference across identical scans.
// RunningWindow is an immutable record; compare metadata as well as HWND/PID.
// Order matters for the window board's current z-order, even though the dock
// separately retains its own lifecycle button order.
internal static class WindowListSnapshotPolicy
{
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
