namespace Nexus.Shell.Services;

// Track the geometry the preview is actually anchored to. An ABN_POSCHANGED
// callback or repeated appearance application may leave this geometry
// unchanged; closing a live preview in that case creates avoidable flicker.
internal readonly record struct DockPlacementSnapshot(
    ShellRect Bounds,
    ShellRect Reservation,
    ShellRect WorkArea,
    ShellRect Monitor,
    bool Floating);

internal static class DockPlacementPolicy
{
    internal static bool RequiresPreviewDismissal(DockPlacementSnapshot? previous, DockPlacementSnapshot current)
        => previous is null || previous.Value != current;
}
