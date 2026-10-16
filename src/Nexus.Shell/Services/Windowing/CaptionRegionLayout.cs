// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// Native caption rectangles are physical client pixels. Keep interactive
// controls and resize edges out of them, including at fractional DPI.
internal static class CaptionRegionLayout
{
    internal static ShellRect? ToPixels(double x, double y, double width, double height, double scale, bool outward = false)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(width) || !double.IsFinite(height)
            || !double.IsFinite(scale) || width <= 0 || height <= 0 || scale <= 0) return null;
        double left = outward ? Math.Floor(x * scale) : Math.Ceiling(x * scale);
        double top = outward ? Math.Floor(y * scale) : Math.Ceiling(y * scale);
        double right = outward ? Math.Ceiling((x + width) * scale) : Math.Floor((x + width) * scale);
        double bottom = outward ? Math.Ceiling((y + height) * scale) : Math.Floor((y + height) * scale);
        if (left < int.MinValue || top < int.MinValue || right > int.MaxValue || bottom > int.MaxValue
            || !double.IsFinite(right) || !double.IsFinite(bottom) || right <= left || bottom <= top
            || right - left > int.MaxValue || bottom - top > int.MaxValue) return null;
        return new((int)left, (int)top, (int)(right - left), (int)(bottom - top));
    }

    internal static ShellRect[] Build(ShellRect caption, ShellRect client, IReadOnlyList<ShellRect> excluded, int resizeEdge)
    {
        if (!Valid(caption) || !Valid(client) || resizeEdge < 0 || excluded.Count > 8) return [];
        long left = (long)client.X + resizeEdge, top = (long)client.Y + resizeEdge;
        long right = (long)client.X + client.Width - resizeEdge, bottom = (long)client.Y + client.Height - resizeEdge;
        if (right <= left || bottom <= top) return [];
        var safeClient = new ShellRect((int)left, (int)top, (int)(right - left), (int)(bottom - top));
        var first = Intersect(caption, safeClient);
        if (first is null) return [];
        var regions = new List<ShellRect> { first.Value };
        foreach (var control in excluded)
        {
            if (!Valid(control)) return []; // Unknown interactive geometry must never become a caption.
            var next = new List<ShellRect>();
            foreach (var region in regions)
            {
                if (Intersect(region, control) is not { } cut) { next.Add(region); continue; }
                Add(region.X, region.Y, region.Width, cut.Y - region.Y);
                Add(region.X, cut.Bottom, region.Width, region.Bottom - cut.Bottom);
                Add(region.X, cut.Y, cut.X - region.X, cut.Height);
                Add(cut.Right, cut.Y, region.Right - cut.Right, cut.Height);
            }
            if (next.Count > 32) return [];
            regions = next;
            void Add(int x, int y, int width, int height) { if (width > 0 && height > 0) next.Add(new(x, y, width, height)); }
        }
        return regions.ToArray();
    }

    private static bool Valid(ShellRect rect) => rect.Width > 0 && rect.Height > 0
        && (long)rect.X + rect.Width <= int.MaxValue && (long)rect.Y + rect.Height <= int.MaxValue;
    private static ShellRect? Intersect(ShellRect a, ShellRect b)
    {
        int left = Math.Max(a.X, b.X), top = Math.Max(a.Y, b.Y);
        int right = Math.Min(a.Right, b.Right), bottom = Math.Min(a.Bottom, b.Bottom);
        return right > left && bottom > top ? new(left, top, right - left, bottom - top) : null;
    }
}
