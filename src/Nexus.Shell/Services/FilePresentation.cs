namespace Nexus.Shell.Services;

public enum FileSort { Name, Newest, Size, Kind }

public static class FilePresentation
{
    public static bool CanPreview(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp";
    public static string Icon(string path, bool folder) => folder ? "Files" : System.IO.Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" => "Picture",
        ".zip" or ".7z" or ".rar" or ".tar" or ".gz" => "Archive",
        ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" => "Video",
        ".mp3" or ".flac" or ".wav" or ".ogg" => "Music",
        ".exe" or ".msi" or ".lnk" => "Apps", _ => "Document"
    };
    public static FileEntry[] Filter(IEnumerable<FileEntry> entries, string query, FileSort sort, bool descending)
    {
        var filtered = entries.Where(e => e.Name.Contains(query.Trim(), StringComparison.CurrentCultureIgnoreCase));
        IOrderedEnumerable<FileEntry> ordered = filtered.OrderByDescending(e => e.IsFolder);
        ordered = sort switch
        {
            FileSort.Newest => descending ? ordered.ThenByDescending(e => e.ModifiedUtc) : ordered.ThenBy(e => e.ModifiedUtc),
            FileSort.Size => descending ? ordered.ThenByDescending(e => e.Bytes) : ordered.ThenBy(e => e.Bytes),
            FileSort.Kind => descending ? ordered.ThenByDescending(e => System.IO.Path.GetExtension(e.Path), StringComparer.OrdinalIgnoreCase) : ordered.ThenBy(e => System.IO.Path.GetExtension(e.Path), StringComparer.OrdinalIgnoreCase),
            _ => descending ? ordered.ThenByDescending(e => e.Name, StringComparer.CurrentCultureIgnoreCase) : ordered.ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
        };
        return ordered.ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
}
