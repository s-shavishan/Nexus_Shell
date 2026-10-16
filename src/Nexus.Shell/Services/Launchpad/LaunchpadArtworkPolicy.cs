// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// Installed app targets may lack a usable WinRT file thumbnail (especially
// legacy .lnk shortcuts). Use a legible, app-specific visual identity instead
// of repeating the same Nexus application glyph across Launchpad pages.
public static class LaunchpadArtworkPolicy
{
    private static readonly string[] Palette =
    [
        "FF285C87", "FF63448A", "FF286A70", "FF82583A",
        "FF35589B", "FF7F4D76", "FF406A4B", "FF685C9B"
    ];

    public static bool UsesNexusArtwork(string? id)
        => id is "jobs" or "controls" or "sections" or "files" or "settings" or "terminal" or "calculator" or "notes";

    public static string Monogram(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "APP";
        var words = name.Split([' ', '-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return "APP";
        if (words.Length > 1) return string.Concat(words.Take(2).Select(x => char.ToUpperInvariant(x[0])));
        return words[0].Length == 1 ? words[0].ToUpperInvariant()
            : words[0][..2].ToUpperInvariant();
    }

    // A High Contrast theme switch must also affect already realized
    // Launchpad tiles; never leave a bright custom background behind.
    public static string BackgroundForContrast(string? name, bool highContrast)
        => highContrast ? "00000000" : Background(name);

    public static string Background(string? name)
    {
        uint hash = 2166136261;
        foreach (char ch in (name ?? string.Empty).ToUpperInvariant())
            hash = unchecked((hash ^ ch) * 16777619);
        return Palette[(int)(hash % (uint)Palette.Length)];
    }
}
