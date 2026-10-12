namespace Nexus.Shell.Services;

// A Liquid Glass bevel is decoration, not an interactive surface. Separate its
// state from WinUI controls so repeated appearance refreshes are idempotent.
internal readonly record struct GlassRimAppearance(bool Visible, double Opacity)
{
    internal static GlassRimAppearance For(string? effectiveQuality, bool highContrast, bool simple)
    {
        bool visible = !highContrast && !simple && (effectiveQuality is "Ultra" or "Balanced");
        return new(visible, effectiveQuality == "Ultra" ? .85 : .65);
    }
}
