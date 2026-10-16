// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// Screen-coordinate geometry for the fullscreen overview. Native windows are
// never moved during pointer gestures; only the final staged rectangles are
// passed to WindowLayouts.Apply after explicit user confirmation.
public readonly record struct OverviewDrop(LayoutRect Bounds, string Zone);

public static class WindowOverviewGeometry
{
    // Live HWND placement does not require the process's executable path.
    // Project a rectangle between monitor work areas, including negative
    // virtual-screen origins, without making a persistent saved layout.
    public static LayoutRect ProjectBetween(LayoutRect bounds, LayoutRect source, LayoutRect destination)
    {
        if (bounds.Width < 1 || bounds.Height < 1 || source.Width < 1 || source.Height < 1
            || destination.Width < 1 || destination.Height < 1)
            throw new ArgumentException("A window and two usable monitor work areas are required.");
        double left = Math.Clamp((bounds.X - (double)source.X) / source.Width, 0, 1);
        double top = Math.Clamp((bounds.Y - (double)source.Y) / source.Height, 0, 1);
        double widthFraction = Math.Clamp(bounds.Width / (double)source.Width, .05, 1);
        double heightFraction = Math.Clamp(bounds.Height / (double)source.Height, .05, 1);
        int width = Math.Clamp((int)Math.Round(widthFraction * destination.Width),
            Math.Min(160, destination.Width), destination.Width);
        int height = Math.Clamp((int)Math.Round(heightFraction * destination.Height),
            Math.Min(120, destination.Height), destination.Height);
        long maxX = (long)destination.X + destination.Width - width;
        long maxY = (long)destination.Y + destination.Height - height;
        int x = (int)Math.Clamp(destination.X + (long)Math.Round(left * destination.Width),
            destination.X, maxX);
        int y = (int)Math.Clamp(destination.Y + (long)Math.Round(top * destination.Height),
            destination.Y, maxY);
        return new(x, y, width, height);
    }

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
