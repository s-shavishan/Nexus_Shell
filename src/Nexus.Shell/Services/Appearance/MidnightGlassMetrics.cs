// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// P2.6 visual fidelity: one set of desktop-wide dimensions based on the
// user-approved Midnight Glass concept. These metrics do not take over the
// Windows caption/resize geometry and are disabled for accessibility contrast.
internal static class MidnightGlassMetrics
{
    internal const double FrameRadius = 18;
    internal const double DockRadius = 21;
    internal const double CardRadius = 14;
    internal const double ControlRadius = 10;
    internal const double SearchRadius = 18;
    internal const double ContentPadding = 20;
    internal const double DockButtonWidth = 64;
    internal const double DockButtonHeight = 68;
    internal const double DockCompactButtonWidth = 40;
    internal const double DockCompactButtonHeight = 44;

    internal static bool ReferenceEnabled(AuraPalette palette, bool highContrast)
        => !highContrast && palette.Name == "Midnight";
}
