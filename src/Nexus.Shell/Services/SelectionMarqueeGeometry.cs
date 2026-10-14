namespace Nexus.Shell.Services;

// DIP-space hit-testing independent of the visual tree.  The same rules are
// shared by the desktop and Files and can be exercised on non-Windows hosts.
public readonly record struct SelectionMarqueeRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

public static class SelectionMarqueeGeometry
{
    public const double DragThreshold = 4;

    public static bool HasStarted(double startX, double startY, double currentX, double currentY)
        => double.IsFinite(startX) && double.IsFinite(startY)
        && double.IsFinite(currentX) && double.IsFinite(currentY)
        && (Math.Abs(currentX - startX) >= DragThreshold
            || Math.Abs(currentY - startY) >= DragThreshold);

    public static SelectionMarqueeRect Between(double startX, double startY, double endX, double endY)
        => new(Math.Min(startX, endX), Math.Min(startY, endY),
            Math.Max(1, Math.Abs(endX - startX)), Math.Max(1, Math.Abs(endY - startY)));

    public static bool Intersects(SelectionMarqueeRect selection, SelectionMarqueeRect item)
        => selection.Width > 0 && selection.Height > 0 && item.Width > 0 && item.Height > 0
        && selection.X < item.Right && item.X < selection.Right
        && selection.Y < item.Bottom && item.Y < selection.Bottom;
}
