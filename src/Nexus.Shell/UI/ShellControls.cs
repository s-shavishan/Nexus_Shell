using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Nexus.Shell.UI;

internal static class ShellControls
{
    internal static FontIcon Icon(string glyph, double size = 16) => new() { Glyph = glyph, FontSize = size, FontFamily = new FontFamily("Segoe MDL2 Assets") };
    internal static Button IconButton(string glyph, string name, Action action)
    {
        var button = new Button { Content = Icon(glyph, 14), Width = 32, Height = 32, MinHeight = 32, Padding = new Thickness(0), CornerRadius = new CornerRadius(9), Style = (Style)Application.Current.Resources["QuietButton"] };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, name);
        ToolTipService.SetToolTip(button, name); button.Click += (_, _) => action(); return button;
    }
    internal static Border Badge(string glyph, ShellTheme theme, double size = 34)
        => new() { Child = new FontIcon { Glyph = glyph, FontSize = size * .45, FontFamily = new FontFamily("Segoe MDL2 Assets"), Foreground = theme.Brush("NexusAccent") }, Width = size, Height = size, CornerRadius = new CornerRadius(theme.HighContrast ? 0 : size * .3), Background = theme.Brush("NexusSelection") };
}
