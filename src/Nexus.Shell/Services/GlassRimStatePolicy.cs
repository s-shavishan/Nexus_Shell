namespace Nexus.Shell.Services;

// Never present a live-acrylic highlight until Windows confirms its controller
// is Active. Keep the saved/requested quality unchanged; this affects only
// decorative paint during Pending, Detached and fallback states.
internal static class GlassRimStatePolicy
{
    internal static string AppearanceQuality(string effectiveQuality, string? controllerState)
        => controllerState == "Active" ? effectiveQuality : "Solid";
}
