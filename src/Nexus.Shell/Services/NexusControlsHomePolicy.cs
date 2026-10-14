namespace Nexus.Shell.Services;

// Keep a card comfortably usable on a narrow Controls window. No native
// monitors, DPI queries or UI thread dependencies in this policy.
internal static class NexusControlsHomePolicy
{
    internal static int Columns(double width)
        => !double.IsFinite(width) || width < 500 ? 1 : width < 840 ? 2 : 3;
}
