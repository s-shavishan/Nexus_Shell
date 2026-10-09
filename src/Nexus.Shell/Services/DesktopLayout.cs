namespace Nexus.Shell.Services;

public readonly record struct ShellRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

public static class DesktopLayout
{
    public static int TaskbarHeight(bool compact) => compact ? 56 : 68;
    public static ShellRect QuickSettingsBounds(ShellRect monitor, ShellRect bar, double scale)
    {
        var rect = MenuBounds(monitor, bar, scale, 392, 650);
        scale = double.IsFinite(scale) ? Math.Clamp(scale, .5, 4) : 1;
        int gap = Math.Max(4, (int)Math.Round(12 * scale));
        return rect with { X = Math.Max(monitor.X, monitor.Right - gap - rect.Width) };
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
