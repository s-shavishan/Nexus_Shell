namespace Nexus.Shell.Services;

// Compatibility adapter for the existing Files/Desktop native preflight.
// Extension classification now comes entirely from the validated JSON catalog.
public sealed record FileContextCapabilities(string OpenLabel, string Icon, bool OpenWith, bool Folder);
public static class FileContextPolicy
{
    public static FileContextCapabilities For(string path, bool folder)
    {
        var profile = FileTypeContextCatalog.Resolve(path, folder);
        string label = folder ? "Open folder" : profile.Primary switch
        {
            "play" => "Play", "run" => "Run application",
            _ => profile.Id == "archive.generic" ? "Open archive" : "Open"
        };
        string icon = profile.Primary switch
        {
            "play" or "run" => "\uE768",
            _ => folder ? "\uED25" : "\uE8A5"
        };
        return new(label, icon, profile.Actions.Contains("open_with"), folder);
    }
}
