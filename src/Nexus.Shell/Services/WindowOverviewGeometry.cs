namespace Nexus.Shell.Services;

// Screen-coordinate geometry for the fullscreen overview. Native windows are
// never moved during pointer gestures; only the final staged rectangles are
// passed to WindowLayouts.Apply after explicit user confirmation.
public readonly record struct OverviewDrop(LayoutRect Bounds, string Zone);

public static class WindowOverviewGeometry
{
    public static OverviewDrop Drag(LayoutRect work, LayoutRect original,
        double dx, double dy, int cursorX, int cursorY, double dpi)
    {
        if (work.Width <= 0 || work.Height <= 0 || !double.IsFinite(dx) || !double.IsFinite(dy))
            throw new ArgumentException("A usable display and finite pointer movement are required.");
        dpi = double.IsFinite(dpi) ? Math.Clamp(dpi, .5, 4) : 1;
        int threshold = Math.Max(24, (int)Math.Round(48 * dpi));
        int width = Math.Clamp(original.Width, Math.Min(160, work.Width), work.Width);
        int height = Math.Clamp(original.Height, Math.Min(120, work.Height), work.Height);
        int x = (int)Math.Clamp(Math.Round((double)original.X + dx * dpi), work.X, (long)work.X + work.Width - width);
        int y = (int)Math.Clamp(Math.Round((double)original.Y + dy * dpi), work.Y, (long)work.Y + work.Height - height);

        bool left = cursorX <= work.X + threshold;
        bool right = cursorX >= work.X + work.Width - threshold;
        bool top = cursorY <= work.Y + threshold;
        bool bottom = cursorY >= work.Y + work.Height - threshold;
        if ((left || right) && (top || bottom))
        {
            int midX = work.X + work.Width / 2, midY = work.Y + work.Height / 2;
            return new(new LayoutRect(left ? work.X : midX, top ? work.Y : midY,
                left ? midX - work.X : work.X + work.Width - midX,
                top ? midY - work.Y : work.Y + work.Height - midY), "Quarter snap");
        }
        if (left || right)
        {
            int mid = work.X + work.Width / 2;
            return new(new LayoutRect(left ? work.X : mid, work.Y,
                left ? mid - work.X : work.X + work.Width - mid, work.Height),
                left ? "Left half" : "Right half");
        }
        if (top)
            return new(new LayoutRect(work.X, work.Y, work.Width, work.Height), "Fill display");
        return new(new LayoutRect(x, y, width, height), "Free position");
    }
}


public static class WindowOverviewInteractionPolicy
{
    // Prevent a card-click or Enter key from discarding uncommitted positions.
    public static bool CanSwitch(int stagedWindowCount) => stagedWindowCount == 0;

    // The synthetic click emitted after a drag belongs only to the card that
    // was dragged; blocking other cards for the same 400 ms makes the entire
    // overview feel unresponsive.
    public static bool SuppressClick(string? draggedKey, string clickedKey, long until, long now)
        => !string.IsNullOrEmpty(draggedKey)
            && string.Equals(draggedKey, clickedKey, StringComparison.Ordinal)
            && now < until;
}
