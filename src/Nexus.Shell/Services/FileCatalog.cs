namespace Nexus.Shell.Services;

public enum FileSelectionKind { Browse, OpenFile, OpenFiles, Folder, SaveFile }
public sealed record FileSelectionRequest(FileSelectionKind Kind, string Title = "My files", string[]? Extensions = null, string SuggestedName = "");
public sealed record FileEntry(string Name, string Path, bool IsFolder, string Detail)
{
    public string IconUri => "ms-appx:///Assets/Icons/" + (IsFolder ? "Files" : "Document") + ".svg";
}
public sealed record FolderSnapshot(string Path, IReadOnlyList<FileEntry> Entries, bool Limited);

public static class FileCatalog
{
    public const int MaximumEntries = 1000;
    public static FolderSnapshot Read(string path, string[]? extensions = null, CancellationToken cancellation = default)
    {
        path = System.IO.Path.GetFullPath(path);
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("This folder is no longer available.");
        var entries = new List<FileEntry>(); int scanned = 0;
        var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
        foreach (string item in Directory.EnumerateFileSystemEntries(path, "*", options))
        {
            cancellation.ThrowIfCancellationRequested();
            if (++scanned > MaximumEntries) break;
            try
            {
                bool folder = (File.GetAttributes(item) & FileAttributes.Directory) != 0;
                if (!folder && !Accepts(item, extensions)) continue;
                string detail = folder ? "Folder" : Size(new FileInfo(item).Length) + " · " + System.IO.Path.GetExtension(item).TrimStart('.').ToUpperInvariant();
                entries.Add(new(System.IO.Path.GetFileName(item), item, folder, detail));
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        return new(path, entries.OrderByDescending(e => e.IsFolder).ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(), scanned > MaximumEntries);
    }
    public static bool Accepts(string path, string[]? extensions) => extensions is null || extensions.Length == 0 || extensions.Contains("*")
        || extensions.Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    public static string Size(long bytes) => bytes >= 1073741824 ? $"{bytes / 1073741824d:0.#} GB" : bytes >= 1048576 ? $"{bytes / 1048576d:0.#} MB" : bytes >= 1024 ? $"{bytes / 1024d:0.#} KB" : $"{bytes} bytes";
    public static string ChildPath(string folder, string name)
    {
        name = name.Trim();
        // Reject Windows device names and separators even in the portable core checks.
        string stem = name.Split('.')[0];
        if (name.Length == 0 || name is "." or ".." || name.EndsWith('.') || name.EndsWith(' ') || name.IndexOfAny(['<','>',':','"','/','\\','|','?','*','\0']) >= 0
            || name.Any(char.IsControl) || new[] { "CON", "PRN", "AUX", "NUL" }.Contains(stem, StringComparer.OrdinalIgnoreCase)
            || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && stem[3] is >= '1' and <= '9'))
            throw new ArgumentException("Choose a file name without reserved characters or device names.");
        return System.IO.Path.Combine(System.IO.Path.GetFullPath(folder), name);
    }
    public static string SavePath(string folder, string name, string[]? extensions)
    {
        if (System.IO.Path.GetExtension(name).Length == 0 && extensions is { Length: > 0 } && extensions[0] != "*") name += extensions[0];
        string path = ChildPath(folder, name);
        if (!Accepts(path, extensions)) throw new ArgumentException("Choose a file with the requested extension.");
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Choose an available destination folder.");
        if (Directory.Exists(path)) throw new IOException("A folder already has that name.");
        return path;
    }
}
