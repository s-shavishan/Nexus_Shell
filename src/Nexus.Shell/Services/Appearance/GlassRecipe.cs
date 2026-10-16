// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// Paint parameters belong to Nexus; Windows decides whether live blur is
// available. Fallback is always a fully opaque color from the same palette.
public sealed record GlassRecipe(string Tint, string Fallback, float TintOpacity, float LuminosityOpacity,
    string Start, string End, string Highlight, bool Frosted = false)
{
    public static GlassRecipe For(AuraPalette palette, string role = "Frame", string style = GlassStyle.Default)
    {
        string color = palette.Panel[2..];
        style = GlassStyle.Normalize(style);
        bool clear = style == "Clear", frost = style == "Frosted";
        float tint = palette.IsLight ? clear ? .42f : frost ? .80f : .62f : clear ? .40f : frost ? .82f : .64f;
        float luminosity = palette.IsLight ? clear ? .72f : .88f : clear ? .68f : frost ? .90f : .82f;
        // The Midnight reference uses a clearer, blue-black glass material.
        // Keep its contrast anchored by the existing opaque fallback instead
        // of using an overbright/gray acrylic wash.
        if (palette.Name == "Midnight")
        {
            tint = clear ? .32f : frost ? .69f : .50f;
            luminosity = clear ? .54f : frost ? .78f : .68f;
        }
        // Palette luminosity and a quiet translucent overlay aim to reduce the
        // gray wash. Native appearance still depends on the Windows compositor.
        string alpha = role switch { "MenuBar" => clear ? "16" : "28", "Dock" => clear ? "20" : "32", "Card" => "38", "Input" => "58", _ => clear ? "22" : "38" };
        if (frost && role is not "Input") alpha = "4A";
        return new("FF" + color, "FF" + color, tint, luminosity, alpha + color,
            (palette.IsLight ? "46" : clear ? "32" : "50") + color, palette.IsLight ? "70FFFFFF" : "38FFFFFF", frost);
    }
}
