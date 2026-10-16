// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// Quality is independent of the existing Clear/Balanced/Frosted material look.
// Store the user's request; temporary system overrides must never rewrite it.
public static class GlassQuality
{
    public const string Default = "Balanced";
    public static readonly string[] Choices = ["Ultra", "Balanced", "Essential", "Solid"];
    public static string Normalize(string? value) => Choices.Contains(value) ? value! : Default;
}

public readonly record struct GlassQualityContext(
    bool NativeGlass = true, bool ReducedEffects = false, bool HighContrast = false,
    bool Transparency = true, bool BackdropSupported = true, bool RemoteSession = false,
    bool EnergySaver = false, bool ControllerFailed = false, bool WindowsFallback = false)
{
    public GlassQualityContext() : this(true, false, false, true, true, false, false, false, false) { }
}

public readonly record struct GlassQualityDecision(string Requested, string Effective, string Reason)
{
    public bool NativeBackdrop => Effective is "Balanced" or "Ultra";
    public bool EdgeLighting => NativeBackdrop;
    public bool Motion => NativeBackdrop;
    public bool IsOverride => Requested != Effective;
}

public static class GlassQualityPolicy
{
    public static GlassQualityDecision Resolve(string? requested, GlassQualityContext context)
    {
        string choice = GlassQuality.Normalize(requested);
        if (context.HighContrast) return new(choice, "Solid", "High contrast");
        if (context.ReducedEffects) return new(choice, "Solid", "Reduced effects");
        if (!context.NativeGlass) return new(choice, "Solid", "Glass disabled");
        if (!context.Transparency) return new(choice, "Solid", "Windows transparency off");
        if (choice == "Solid") return new(choice, choice, "Selected");
        if (context.ControllerFailed) return new(choice, "Solid", "Native material unavailable");
        if (context.WindowsFallback) return new(choice, "Solid", "Windows backdrop fallback");
        if (choice == "Essential") return new(choice, choice, "Selected");
        if (!context.BackdropSupported) return new(choice, "Solid", "Native backdrop unsupported");
        if (context.RemoteSession) return new(choice, "Essential", "Remote session");
        if (context.EnergySaver) return new(choice, "Essential", "Energy saver");
        return new(choice, choice, "Selected");
    }
}
