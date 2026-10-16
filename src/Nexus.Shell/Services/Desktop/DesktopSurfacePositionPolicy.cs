// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// The Windows desktop layer must re-anchor after shell or monitor events, but
// only a true monitor-rectangle difference needs an AppWindow.MoveAndResize.
// Query the real HWND on every positioning event so external moves recover.
internal static class DesktopSurfacePositionPolicy
{
    internal static bool RequiresMove(bool hasActual, ShellRect actual, ShellRect monitor)
        => !hasActual || actual != monitor;
}
