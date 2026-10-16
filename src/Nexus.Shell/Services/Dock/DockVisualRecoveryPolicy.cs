// නමෝ බුද්ධාය 🤍
using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

// A native snapshot can match the last completed scan even when the previous
// WinUI update threw halfway through removing or adding dock buttons.
// Keep normal activation fast; recover incomplete XAML tree mutations on the
// next full reconciliation attempt.
internal static class DockVisualRecoveryPolicy
{
    // Missing and incorrect button keys are more dangerous than a count
    // mismatch: a stopped/failed render can leave N stale buttons for N
    // entirely different native windows.
    internal static bool HasConsistentIdentities(
        IReadOnlyList<RunningWindow> expected, IReadOnlyCollection<DockWindowIdentity> actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        if (expected.Count != actual.Count) return false;
        var pending = new HashSet<DockWindowIdentity>(actual);
        if (pending.Count != actual.Count) return false;
        foreach (var window in expected)
            if (!pending.Remove(DockWindowIdentity.Of(window))) return false;
        return pending.Count == 0;
    }

    internal static bool RequiresFullReconcile(
        bool baselineReconciled, bool pinsChanged, bool windowsChanged,
        bool previousRefreshIncomplete, int expectedWindows, int items, int visualChildren)
        => !baselineReconciled || pinsChanged || windowsChanged || previousRefreshIncomplete
            || expectedWindows != items || items != visualChildren;

    internal static bool RequiresVisualRebuild(
        bool previousRefreshIncomplete, int expectedWindows, int visualChildren)
        => previousRefreshIncomplete || expectedWindows != visualChildren;
}
