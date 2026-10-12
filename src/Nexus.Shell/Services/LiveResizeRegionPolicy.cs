namespace Nexus.Shell.Services;

// A rounded native region can lag the HWND while the user drags a sizing edge.
// Only resizable, non-dock frames temporarily release their region during
// WM_SIZING. Always restore it on normal completion OR cancelled capture.
internal static class LiveResizeRegionPolicy
{
    internal static bool ShouldSuspend(bool interactive, bool resizable, bool customClip, bool systemFrame, bool suspended)
        => interactive && resizable && !customClip && !systemFrame && !suspended;
    // WM_SIZING is itself followed by native size/expose processing. Avoid
    // forcing another synchronous repaint merely to remove an obsolete GDI
    // clipping region. The final rounded region still requests a repaint.
    internal const bool RedrawWhileSuspending = false;
    internal const bool RedrawAfterRestore = true;

    internal static bool IsInterrupted(uint message)
        => message is 0x001F or 0x0215; // WM_CANCELMODE, WM_CAPTURECHANGED

    internal static bool ShouldRestore(uint message, bool unclipped)
        => unclipped && (message == 0x0232 || IsInterrupted(message)); // WM_EXITSIZEMOVE
}
