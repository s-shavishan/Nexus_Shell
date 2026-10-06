using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Nexus.Shell.UI;

public sealed class AppAccentConverter : IValueConverter
{
    private readonly SolidColorBrush _system = Make(59, 112, 133);
    private readonly SolidColorBrush _browser = Make(59, 86, 145);
    private readonly SolidColorBrush _development = Make(98, 80, 145);
    private readonly SolidColorBrush _game = Make(137, 70, 113);
    private readonly SolidColorBrush _default = Make(67, 77, 105);
    private static SolidColorBrush Make(byte r, byte g, byte b) => new(Windows.UI.Color.FromArgb(255, r, g, b));
    public object Convert(object value, Type targetType, object parameter, string language) => (value as string) switch
    {
        "System" => _system, "Browser" => _browser, "Development" => _development, "Game" => _game, _ => _default
    };
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
