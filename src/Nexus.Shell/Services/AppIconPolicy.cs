namespace Nexus.Shell.Services;

public static class AppIconPolicy
{
    public const int CacheLimit = 128, PixelSize = 96, MaximumBytes = 256 * 1024;
    public static bool IsLocalFile(string? target)
        => !string.IsNullOrWhiteSpace(target) && target.Length <= 32767 && target.Length > 3
            && char.IsAsciiLetter(target[0]) && target[1] == ':' && target[2] == '\\'
            && target.IndexOfAny(['\r', '\n', '\0']) < 0
            && new[] { ".lnk", ".exe", ".appref-ms" }.Contains(Path.GetExtension(target), StringComparer.OrdinalIgnoreCase);
}
