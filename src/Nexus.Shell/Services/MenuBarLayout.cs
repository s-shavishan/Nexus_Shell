namespace Nexus.Shell.Services;

// The work-area reservation stays constant while only the visible bar moves.
public static class MenuBarLayout
{
    public const int ReservationHeight = 38, FloatingHeight = 32;
    public static ShellRect Bounds(ShellRect monitor, double scale, bool attached)
    {
        scale = double.IsFinite(scale) ? Math.Clamp(scale, .5, 4) : 1;
        int band = Math.Min(Math.Max(1, monitor.Height), Math.Max(1, (int)Math.Round(ReservationHeight * scale)));
        if (attached) return new(monitor.X, monitor.Y, Math.Max(1, monitor.Width), band);
        int side = Math.Min((int)Math.Round(8 * scale), Math.Max(0, (monitor.Width - 1) / 2));
        int top = Math.Min((int)Math.Round(4 * scale), Math.Max(0, band - 1));
        return new(monitor.X + side, monitor.Y + top, Math.Max(1, monitor.Width - side * 2),
            Math.Min(Math.Max(1, band - top), Math.Max(1, (int)Math.Round(FloatingHeight * scale))));
    }
    public static bool Attached(ShellRect window, ShellRect work, bool maximized, double scale)
    {
        if (window.Width <= 0 || window.Height <= 0 || work.Width <= 0 || work.Height <= 0) return false;
        if (maximized) return true;
        int tolerance = Math.Max(2, (int)Math.Round(8 * (double.IsFinite(scale) ? Math.Clamp(scale, .5, 4) : 1)));
        bool top = Math.Abs((long)window.Y - work.Y) <= tolerance;
        bool side = Math.Abs((long)window.X - work.X) <= tolerance || Math.Abs((long)window.Right - work.Right) <= tolerance;
        bool fullHeight = Math.Abs((long)window.Bottom - work.Bottom) <= tolerance;
        bool fullWidth = Math.Abs((long)window.Width - work.Width) <= tolerance * 2;
        return top && side && (fullHeight || fullWidth);
    }
    public static ShellRect ClientBounds(ShellRect proposed, ShellRect work, bool maximized)
    {
        if (!maximized) return proposed;
        int left = Math.Max(proposed.X, work.X), top = Math.Max(proposed.Y, work.Y);
        int right = Math.Min(proposed.Right, work.Right), bottom = Math.Min(proposed.Bottom, work.Bottom);
        return right > left && bottom > top ? new(left, top, right - left, bottom - top) : proposed;
    }
}
