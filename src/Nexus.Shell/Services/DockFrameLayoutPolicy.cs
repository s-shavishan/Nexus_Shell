namespace Nexus.Shell.Services;

// Frame geometry is independent from desktop-wide work-area reconciliation.
// Repeated taskbar/appbar callbacks should not rewrite the same WinUI layout
// properties or request the same rounded HWND clipping region.
internal readonly record struct DockFrameGeometry(
    ShellRect Bounds,
    ShellRect Reservation,
    double Scale,
    bool Floating,
    bool HighContrast);

internal static class DockFrameLayoutPolicy
{
    internal static bool RequiresLayout(DockFrameGeometry? previous, DockFrameGeometry current)
        => previous is null || previous.Value != current;

    internal static ShellRect Clip(DockFrameGeometry frame)
        => new(frame.Bounds.X - frame.Reservation.X, frame.Bounds.Y - frame.Reservation.Y,
            frame.Bounds.Width, frame.Bounds.Height);

    internal static double CornerRadius(DockFrameGeometry frame)
        => frame.Floating && !frame.HighContrast ? MidnightGlassMetrics.DockRadius : 0;
}
