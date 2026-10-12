namespace Nexus.Shell.Services;

// A retained indicator needs compositor work only for a presence transition or
// to settle a previously animated state when motion is disabled/unloaded.
// Repeated identical states must not restart/stop the same animations.
internal enum DockIndicatorUpdate { None, Animate, Settle }

internal static class DockIndicatorUpdatePolicy
{
    internal static DockIndicatorUpdate Decide(
        bool changed, bool animationInFlight, bool animate, bool enabled, bool loaded)
    {
        if (changed)
            return animate && enabled && loaded ? DockIndicatorUpdate.Animate : DockIndicatorUpdate.Settle;
        return animationInFlight && (!animate || !enabled || !loaded)
            ? DockIndicatorUpdate.Settle : DockIndicatorUpdate.None;
    }
}
