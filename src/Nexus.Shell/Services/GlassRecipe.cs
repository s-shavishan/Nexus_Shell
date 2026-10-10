namespace Nexus.Shell.Services;

// Paint parameters belong to Nexus; Windows decides whether live blur is
// available. Fallback is always a fully opaque color from the same palette.
public sealed record GlassRecipe(string Tint, string Fallback, float TintOpacity, float LuminosityOpacity,
    string Start, string End, string Highlight)
{
    public static GlassRecipe For(AuraPalette palette, string role = "Frame")
    {
        string color = palette.Panel[2..];
        string alpha = role switch { "MenuBar" => "32", "Dock" => "38", "Card" => "30", "Input" => "50", _ => "36" };
        return palette.IsLight
            ? new("FF" + color, "FF" + color, .76f, .78f, "50" + color, "32" + color, "38FFFFFF")
            : new("FF" + color, "FF" + color, .78f, .20f, alpha + color, "44" + color, "16FFFFFF");
    }
}
