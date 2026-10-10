using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Nexus.Shell.Models;

namespace Nexus.Shell.UI;

// Original static vectors. Sources are shared on the UI thread and never
// resolve paths from a saved card or invoke a shell extension during layout.
public static class NexusIcons
{
    private static readonly Dictionary<string, SvgImageSource> Sources = new(StringComparer.Ordinal);
    public static ImageSource Source(string name)
    {
        if (!Sources.TryGetValue(name, out var source))
        {
            source = new SvgImageSource(new Uri("ms-appx:///Assets/Icons/" + name + ".svg"))
            { RasterizePixelWidth = 128, RasterizePixelHeight = 128 };
            Sources.Add(name, source);
        }
        return source;
    }
    public static Image Image(string name, double size = 48) => new()
    { Source = Source(name), Width = size, Height = size, Stretch = Stretch.Uniform };
    public static string ForKind(string kind) => kind switch
    { "Note" => "Note", "Folder" => "Files", "Link" => "Browser", _ => "Document" };
    // Category artwork avoids loading arbitrary executable icons on the UI
    // thread. Window titles remain available through the native tooltip.
    public static string ForProcess(string process)
    {
        string name = process.ToLowerInvariant();
        if (name.Contains("firefox") || name.Contains("chrome") || name.Contains("edge") || name.Contains("browser")) return "Browser";
        if (name.Contains("explorer")) return "Files";
        if (name.Contains("terminal") || name.Contains("powershell") || name.Contains("code") || name == "cmd") return "Terminal";
        if (name.Contains("settings")) return "Settings";
        if (name.Contains("notepad") || name.Contains("word")) return "Document";
        return "Windows";
    }
    public static string ForApp(AppEntry app) => app.Id switch
    {
        "sections" => "Apps", "files" => "Files", "settings" => "Settings", "terminal" => "Terminal", "calculator" => "Calculator", "notes" => "Note",
        _ => app.Category switch
        { "Browser" => "Browser", "Development" => "Terminal", "Game" => "Game", "System" => "Settings", _ => "Apps" }
    };
}
public sealed class AppIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        NexusIcons.Source(value is AppEntry app ? NexusIcons.ForApp(app) : "Apps");
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
public sealed class SavedIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        NexusIcons.Source(NexusIcons.ForKind(value as string ?? "File"));
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
public sealed class SavedSubtitleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is SavedItem item
        ? (item.Kind == "Note" ? item.Note : item.Target).Replace('\r', ' ').Replace('\n', ' ')
        : "";
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
