namespace Nexus.Shell.Services;

// Track the geometry the preview is actually anchored to. An ABN_POSCHANGED
// callback or repeated appearance application may leave this geometry
// unchanged; closing a live preview in that case creates avoidable flicker.
internal readonly record struct DockPlacementSnapshot(
    ShellRect Bounds,
    ShellRect Reservation,
    ShellRect WorkArea,
    ShellRect Monitor,
    bool Floating,
    double Scale = 1);

internal static class DockPlacementPolicy
{
    internal static bool RequiresPreviewDismissal(DockPlacementSnapshot? previous, DockPlacementSnapshot current)
        => previous is null || previous.Value != current;

    // Width-only changes affect the dock anchor, not system work areas.
    // Avoid expensive maximized-window repair and panel placement on no-ops.
    internal static bool RequiresDesktopRelayout(DockPlacementSnapshot? previous, DockPlacementSnapshot current)
        => previous is null
            || previous.Value.Monitor != current.Monitor
            || previous.Value.WorkArea != current.WorkArea
            || previous.Value.Reservation != current.Reservation
            || previous.Value.Floating != current.Floating
            || previous.Value.Scale != current.Scale;
}
