namespace Nexus.Shell.Services;

public sealed record DesktopShortcut(string Id, string Name, string IconUri, string Target);

public static class DesktopCatalog
{
    public static IReadOnlyList<DesktopShortcut> Read()
    {
        var items = new List<DesktopShortcut>
        {
            new("sections", "Sections", "ms-appx:///Assets/Icons/Apps.svg", "nexus:sections"),
            new("files", "My files", "ms-appx:///Assets/Icons/Files.svg", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            new("recycle", "Recycle Bin", "ms-appx:///Assets/Icons/Windows.svg", "nexus:recycle")
        };
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in new[] { Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.CommonDesktopDirectory })
        {
            string folder = Environment.GetFolderPath(directory);
            if (folder.Length == 0 || !Directory.Exists(folder)) continue;
            try
            {
                foreach (string path in Directory.EnumerateFileSystemEntries(folder).Take(512).OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase))
                {
                    if (items.Count >= 67) break;
                    try
                    {
                        var attributes = File.GetAttributes(path);
                        if ((attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0 || !paths.Add(path)) continue;
                        bool folderItem = (attributes & FileAttributes.Directory) != 0;
                        string extension = Path.GetExtension(path).ToLowerInvariant();
                        string icon = folderItem ? "Files" : extension is ".lnk" or ".exe" ? "Apps" : extension == ".url" ? "Browser" : "Document";
                        string name = extension is ".lnk" or ".url" ? Path.GetFileNameWithoutExtension(path) : Path.GetFileName(path);
                        items.Add(new(path, name, "ms-appx:///Assets/Icons/" + icon + ".svg", path));
                    }
                    catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        return items;
    }
}
