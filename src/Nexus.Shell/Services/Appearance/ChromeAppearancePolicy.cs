// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// Native GDI corner diameters must be finite and bounded before converting
// floating-point DIPs into physical pixels. No arbitrary negative/NaN radius
// may reach CreateRoundRectRgn during a theme or DPI transition.
internal static class ChromeAppearancePolicy
{
    internal const double MaxRadiusDip = 48;

    internal static double Radius(double requested, bool highContrast = false)
        => highContrast || !double.IsFinite(requested) ? 0 : Math.Clamp(requested, 0, MaxRadiusDip);

    internal static bool CornersChanged(double previous, double requested)
        => previous != requested;

    internal static bool ClipChanged(ShellRect? previous, ShellRect requested, double radiusBefore, double radiusAfter)
        => previous != requested || CornersChanged(radiusBefore, radiusAfter);
}
