namespace Nexus.Shell.Services;

// Foreground activation changes selection, menu/dock visibility and the
// active indicator, but does not itself alter the task-list membership.
// Structural WinEvents retain the existing 50 ms coalesced EnumWindows pass.
// The five-second desktop recovery scan still covers missing/lost events.
internal static class WindowEventScanPolicy
{
    internal const uint Foreground = 0x0003; // EVENT_SYSTEM_FOREGROUND

    internal static bool RequiresEnumeration(uint kind) => kind != Foreground;
}
