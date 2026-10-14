namespace Nexus.Shell.Services;

public sealed record FileContextCapabilities(string OpenLabel, string Icon, bool OpenWith, bool Folder);
public static class FileContextPolicy
{
    public static FileContextCapabilities For(string path, bool folder)
    {
        if (folder) return new("Open folder", "\uED25", false, true);
        string extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".exe" or ".com" or ".bat" or ".cmd" => new("Run application", "\uE768", false, false),
            ".msi" or ".msix" or ".appx" => new("Open installer", "\uE7B8", false, false),
            ".lnk" or ".url" => new("Open shortcut", "\uE71B", false, false),
            ".mp3" or ".flac" or ".wav" or ".ogg" or ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" => new("Play", "\uE768", true, false),
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" or ".svg" => new("Open image", "\uEB9F", true, false),
            ".zip" or ".7z" or ".rar" or ".tar" or ".gz" => new("Open archive", "\uE7B8", true, false),
            _ => new("Open", "\uE8A5", true, false)
        };
    }
}
