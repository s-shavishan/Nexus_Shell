// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// Frequent native polling is only needed while an auto-hidden floating dock
// must respond to edge reveal or while its preview/menu interaction is open.
// Foreground/window lifecycle events trigger immediate refresh independently.
internal static class DockRefreshCadence
{
    internal const int ActiveMilliseconds = 80;
    internal const int IdleMilliseconds = 250;

    internal static int Interval(bool floating, bool maximizedOrAttached, bool interacting)
        => (floating && maximizedOrAttached) || interacting ? ActiveMilliseconds : IdleMilliseconds;
}
