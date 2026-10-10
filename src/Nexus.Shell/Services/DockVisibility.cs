namespace Nexus.Shell.Services;

// Floating docks overlay the desktop. Hold the reveal while crossing the gap
// from the bottom edge, and briefly after leaving it; no work-area writes here.
public sealed class DockVisibility
{
    private long _holdUntil;
    public bool Update(bool floating, bool maximized, bool fullscreen, bool interacting, bool pointerInRevealArea, long now)
    {
        // Presentation apps and borderless games must never be covered by a
        // stale preview/menu. Fullscreen always wins over an interaction hold.
        if (fullscreen) { _holdUntil = 0; return false; }
        if (!floating) { _holdUntil = 0; return !fullscreen || interacting; }
        if (interacting) { _holdUntil = now + 450; return true; }
        if (!maximized) { _holdUntil = 0; return true; }
        if (pointerInRevealArea) _holdUntil = now + 450;
        return now < _holdUntil;
    }
    public static bool InRevealArea(ShellRect monitor, ShellRect bar, int x, int y, bool revealed, double scale)
    {
        double dpi = double.IsFinite(scale) ? Math.Clamp(scale, .5, 4) : 1;
        int edge = Math.Max(2, (int)Math.Round(3 * dpi));
        if (x < monitor.X || x >= monitor.Right || y < monitor.Y || y >= monitor.Bottom) return false;
        if (y >= monitor.Bottom - edge) return true;
        int padding = (int)Math.Round(10 * dpi);
        return revealed && x >= bar.X - padding && x <= bar.Right + padding && y >= bar.Y - padding;
    }
}
