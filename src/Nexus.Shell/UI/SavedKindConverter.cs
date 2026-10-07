using Microsoft.UI.Xaml.Data;

namespace Nexus.Shell.UI;

public sealed class SavedKindConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool favorite) return favorite ? "★" : "";
        return (value as string) switch
        {
        "Link" => "\uE774", "Folder" => "\uE8B7", "Note" => "\uE70B", _ => "\uE8A5"
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
