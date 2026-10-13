using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

// A missing foreground HWND may indicate that its creation/show WinEvent was
// missed. Examine it on activation OR after a completed move/resize so the
// geometry fast path does not hide an otherwise missed create/show event.
// Ordinary activation and move/resize among known apps remain scan-free.
internal static class DockForegroundDiscoveryPolicy
{
    internal static bool IsUnknownForeground(bool shouldInspect,
        IntPtr foreground, IReadOnlyList<RunningWindow> knownWindows)
    {
        ArgumentNullException.ThrowIfNull(knownWindows);
        if (!shouldInspect || foreground == IntPtr.Zero) return false;
        return !knownWindows.Any(window => window.Handle == foreground);
    }
}
