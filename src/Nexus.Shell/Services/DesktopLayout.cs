namespace Nexus.Shell.Services;

public readonly record struct ShellRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

public static class DesktopLayout
{
    public static int TaskbarHeight(bool compact) => compact ? 56 : 68;
    private static double Scale(double value) => double.IsFinite(value) ? Math.Clamp(value, .5, 4) : 1;
    public static int TaskbarReservationHeight(bool compact, bool floating, double scale)
        => Math.Max(1, (int)Math.Round((TaskbarHeight(compact) + (floating ? 32 : 0)) * Scale(scale)));
    // Reserve a normal appbar strip; clip only its visible dock. The empty
    // margin stays outside the HWND's input/drawing region, not painted white.
    public static ShellRect TaskbarBounds(ShellRect reservation, bool floating, double preferredWidthDip, double scale)
    {
        if (!floating) return reservation;
        scale = Scale(scale);
        int gap = Math.Min(Math.Max(1, (int)Math.Round(12 * scale)), Math.Max(0, (reservation.Height - 1) / 2));
        int horizontal = Math.Min(Math.Max(1, (int)Math.Round(16 * scale)), Math.Max(0, (reservation.Width - 1) / 2));
        double preferred = double.IsFinite(preferredWidthDip) ? Math.Clamp(preferredWidthDip, 280, 2400) : 640;
        int width = Math.Min(Math.Max(1, reservation.Width - horizontal * 2), (int)Math.Round(preferred * scale));
        return new(reservation.X + (reservation.Width - width) / 2, reservation.Y + gap, width, Math.Max(1, reservation.Height - gap * 2));
    }
    public static ShellRect QuickSettingsBounds(ShellRect monitor, ShellRect bar, double scale)
    {
        var rect = MenuBounds(monitor, bar, scale, 392, 650);
        return rect with { X = Math.Clamp(bar.Right - rect.Width, monitor.X, Math.Max(monitor.X, monitor.Right - rect.Width)) };
    }
    public static ShellRect MenuBounds(ShellRect monitor, ShellRect bar, double scale, int widthDip = 440, int heightDip = 540)
    {
        scale = double.IsFinite(scale) ? Math.Clamp(scale, .5, 4) : 1;
        int gap = Math.Max(4, (int)Math.Round(12 * scale));
        int width = Math.Min(Math.Max(1, monitor.Width - gap * 2), (int)Math.Round(widthDip * scale));
        int available = Math.Max(1, bar.Y - monitor.Y - gap * 2);
        int height = Math.Min(available, (int)Math.Round(heightDip * scale));
        int x = Math.Clamp(bar.X + gap, monitor.X, monitor.Right - width);
        int y = Math.Clamp(bar.Y - gap - height, monitor.Y, monitor.Bottom - height);
        return new(x, y, width, height);
    }
}
