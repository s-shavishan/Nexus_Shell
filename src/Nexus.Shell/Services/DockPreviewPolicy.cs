namespace Nexus.Shell.Services;

// Screen pixels, including negative monitor origins. The short corridor lets a
// pointer cross the space between an icon and its card without an instant close.
public static class DockPreviewPolicy
{
    public const int HoverDelayMilliseconds = 280;
    public const int LeaveDelayMilliseconds = 240;
    public static bool Contains(ShellRect rectangle, int x, int y) => rectangle.Width > 0 && rectangle.Height > 0
        && x >= rectangle.X && x < rectangle.Right && y >= rectangle.Y && y < rectangle.Bottom;
    public static bool InInteractionArea(ShellRect anchor, ShellRect preview, int x, int y)
    {
        if (Contains(anchor, x, y) || Contains(preview, x, y)) return true;
        int top = Math.Min(preview.Bottom, anchor.Bottom), bottom = Math.Max(preview.Y, anchor.Y);
        if (top >= bottom) return false;
        int left = Math.Min(anchor.X, preview.X), right = Math.Max(anchor.Right, preview.Right);
        return x >= left && x < right && y >= top && y < bottom;
    }
    public static ShellRect FitSource(ShellRect viewport, int sourceWidth, int sourceHeight)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0 || sourceWidth <= 0 || sourceHeight <= 0) return default;
        double ratio = Math.Min((double)viewport.Width / sourceWidth, (double)viewport.Height / sourceHeight);
        int width = Math.Clamp((int)Math.Round(sourceWidth * ratio), 1, viewport.Width);
        int height = Math.Clamp((int)Math.Round(sourceHeight * ratio), 1, viewport.Height);
        return new(viewport.X + (viewport.Width - width) / 2, viewport.Y + (viewport.Height - height) / 2, width, height);
    }
}
