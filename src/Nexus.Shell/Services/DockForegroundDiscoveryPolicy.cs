using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

// A missing foreground HWND may indicate that its creation/show WinEvent was
// missed. Ordinary activation among already-listed apps never needs a scan.
internal static class DockForegroundDiscoveryPolicy
{
    internal static bool IsUnknownForeground(bool foregroundChanged,
        IntPtr foreground, IReadOnlyList<RunningWindow> knownWindows)
    {
        ArgumentNullException.ThrowIfNull(knownWindows);
        if (!foregroundChanged || foreground == IntPtr.Zero) return false;
        return !knownWindows.Any(window => window.Handle == foreground);
    }
}
