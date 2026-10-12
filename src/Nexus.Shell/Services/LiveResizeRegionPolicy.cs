namespace Nexus.Shell.Services;

// A rounded native region can lag the HWND while the user drags a sizing edge.
// Only resizable, non-dock frames temporarily release their region during
// WM_SIZING, then restore the rounded region once on WM_EXITSIZEMOVE.
internal static class LiveResizeRegionPolicy
{
    internal static bool ShouldSuspend(bool interactive, bool resizable, bool customClip, bool systemFrame, bool suspended)
        => interactive && resizable && !customClip && !systemFrame && !suspended;
}
