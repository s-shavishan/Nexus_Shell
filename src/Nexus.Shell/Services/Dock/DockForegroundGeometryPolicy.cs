// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// Classify both fullscreen suppression and attached menu-bar behavior from
// one native foreground rectangle, avoiding contradictory mid-poll snapshots.
internal readonly record struct DockForegroundGeometry(bool Fullscreen, bool Attached);

internal static class DockForegroundGeometryPolicy
{
    internal static DockForegroundGeometry Classify(
        bool hasBounds, ShellRect window, ShellRect monitor, ShellRect work,
        bool maximized, bool minimized, long windowStyle, double scale)
    {
        if (!hasBounds) return default;
        return new(
            DockFullscreenPolicy.IsFullscreen(window, monitor, maximized, windowStyle),
            !minimized && MenuBarLayout.Attached(window, work, maximized, scale));
    }
}
