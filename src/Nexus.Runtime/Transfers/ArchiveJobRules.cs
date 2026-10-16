// නමෝ බුද්ධාය 🤍
namespace Nexus.Runtime.Transfers;

public static class ArchiveJobRules
{
    public const long MaximumBytes = 64L * 1024 * 1024 * 1024;
    public const int MaximumMetadataBytes = 16 * 1024 * 1024;
    // Windows-compatible names are enforced on every platform, so portable
    // checks catch aliases that would collide or become devices on Windows.
    public static string Leaf(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 240 || name is "." or ".."
            || name.EndsWith('.') || name.EndsWith(' ') || name.Any(char.IsControl)
            || name.IndexOfAny(['/', '\\', ':', '<', '>', '|', '?', '*', '"']) >= 0)
            throw new RuntimeFailure("file-archive", "Choose a normal file or folder name without a path or trailing dots/spaces.");
        string device = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        if (device is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$"
            || device.Length == 4 && (device.StartsWith("COM", StringComparison.Ordinal) || device.StartsWith("LPT", StringComparison.Ordinal))
                && "0123456789¹²³".Contains(device[3]))
            throw new RuntimeFailure("file-archive", "This name is reserved by Windows. Choose another name.");
        return name;
    }
    internal static string EntryPath(string name)
    {
        if (name.Length is < 1 or > 1024) throw new InvalidDataException("A ZIP entry name is missing or too long.");
        string normalized = name.Replace('\\', '/');
        string[] parts = (normalized.EndsWith('/') ? normalized[..^1] : normalized).Split('/');
        if (parts.Length > FileJobRules.MaximumDepth) throw new InvalidDataException("A ZIP path exceeds the 64-level limit.");
        foreach (string part in parts) _ = Leaf(part);
        return string.Join('/', parts);
    }
    public static string SuggestedName(IReadOnlyList<string> sources, FileJobKind kind)
    {
        string stem = sources.Count == 1 ? Path.GetFileNameWithoutExtension(sources[0]) : "Archive";
        try { _ = Leaf(stem); if (stem.Length > 180) stem = "Archive"; }
        catch (RuntimeFailure) { stem = "Archive"; }
        return kind == FileJobKind.CreateZip ? stem + ".zip" : stem;
    }
}
