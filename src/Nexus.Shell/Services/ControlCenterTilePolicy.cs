namespace Nexus.Shell.Services;

// The Midnight reference emphasizes connectivity and media in the first
// module group. Power and Desktop remain real actions, but are intentionally
// quieter secondary modules underneath the four prominent controls.
internal static class ControlCenterTilePolicy
{
    internal static bool IsPrimary(string section)
        => section is "Sound" or "Network" or "Bluetooth" or "Display";

    internal static double Height(string section)
        => IsPrimary(section) ? 68 : 52;

    internal static double Radius(string section)
        => IsPrimary(section) ? MidnightGlassMetrics.CardRadius : MidnightGlassMetrics.ControlRadius;
}
