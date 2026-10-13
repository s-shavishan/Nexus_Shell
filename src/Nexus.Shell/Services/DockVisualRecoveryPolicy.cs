namespace Nexus.Shell.Services;

// A native snapshot can match the last completed scan even when the previous
// WinUI update threw halfway through removing or adding dock buttons.
// Keep normal activation fast; recover incomplete XAML tree mutations on the
// next full reconciliation attempt.
internal static class DockVisualRecoveryPolicy
{
    internal static bool RequiresFullReconcile(
        bool baselineReconciled, bool pinsChanged, bool windowsChanged,
        bool previousRefreshIncomplete, int expectedWindows, int items, int visualChildren)
        => !baselineReconciled || pinsChanged || windowsChanged || previousRefreshIncomplete
            || expectedWindows != items || items != visualChildren;

    internal static bool RequiresVisualRebuild(
        bool previousRefreshIncomplete, int expectedWindows, int visualChildren)
        => previousRefreshIncomplete || expectedWindows != visualChildren;
}
