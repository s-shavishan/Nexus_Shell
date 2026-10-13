namespace Nexus.Shell.Services;

// Foreground switches and a completed interactive move/resize change selection
// or window geometry, but do not by themselves add/remove task-list entries.
// Taskbar.RefreshFromNativeEvent still observes current foreground, dock
// visibility and fullscreen/attachment on every delivered event. Keep full
// enumeration for lifecycle/title/minimize and unknown events. The bounded
// desktop recovery heartbeat still reconciles missed event notifications.
internal static class WindowEventScanPolicy
{
    internal const uint Foreground = 0x0003; // EVENT_SYSTEM_FOREGROUND
    internal const uint MoveSizeEnd = 0x000B; // EVENT_SYSTEM_MOVESIZEEND

    internal static bool RequiresEnumeration(uint kind)
        => kind is not (Foreground or MoveSizeEnd);
}
