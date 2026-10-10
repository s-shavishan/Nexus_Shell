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
    public static ShellRect TaskbarWorkArea(ShellRect monitor, bool compact, bool floating, double scale)
        => floating ? monitor : monitor with { Height = Math.Max(1, monitor.Height - TaskbarReservationHeight(compact, false, scale)) };
    // The native window strip and the reserved work area are independent.
    // Floating mode clips this strip to the dock and reserves zero pixels.
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
        var rect = MenuBounds(monitor, bar, scale, 356, 630);
        int gap = Math.Max(4, (int)Math.Round(14 * Scale(scale)));
        return rect with { X = Math.Max(monitor.X, monitor.Right - rect.Width - gap), Y = Math.Clamp(monitor.Y + (int)Math.Round(50 * Scale(scale)), monitor.Y, Math.Max(monitor.Y, bar.Y - gap - rect.Height)) };
    }
    public static ShellRect SpotlightBounds(ShellRect monitor, double scale)
    {
        scale = Scale(scale); int gap = Math.Max(4, (int)Math.Round(16 * scale));
        int width = Math.Min(Math.Max(1, monitor.Width - gap * 2), (int)Math.Round(650 * scale));
        int height = Math.Min(Math.Max(1, monitor.Height - gap * 2), (int)Math.Round(470 * scale));
        int top = Math.Min(gap * 5, Math.Max(gap, (monitor.Height - height) / 3));
        return new(monitor.X + (monitor.Width - width) / 2, monitor.Y + Math.Min(top, Math.Max(0, monitor.Height - height)), width, height);
    }
    public static ShellRect PreviewBounds(ShellRect monitor, ShellRect bar, ShellRect anchor, double scale)
    {
        scale = Scale(scale);
        int gap = Math.Max(4, (int)Math.Round(12 * scale));
        int width = Math.Min(Math.Max(1, monitor.Width - gap * 2), (int)Math.Round(336 * scale));
        int height = Math.Min(Math.Max(1, bar.Y - monitor.Y - gap * 2), (int)Math.Round(268 * scale));
        int x = Math.Clamp(anchor.X + (anchor.Width - width) / 2, monitor.X, Math.Max(monitor.X, monitor.Right - width));
        int y = Math.Clamp(bar.Y - gap - height, monitor.Y, Math.Max(monitor.Y, monitor.Bottom - height));
        return new(x, y, width, height);
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
