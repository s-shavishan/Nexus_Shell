using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Services;
using Nexus.Shell.UI;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    // The wallpaper is drawn by WinUI itself. Its palette and paths are also
    // consumed by the layout-reference exporter; there is no animated backdrop.
    private void ApplyWallpaperPalette(AuraPalette palette)
    {
        string[] colors = palette.Name switch
        {
            "Solstice" => ["FFFFD3AD", "FFD68267", "FF9E5B71", "FF493251"],
            "Ember" => ["FFCD683C", "FFAF3546", "FF682637", "FF271F3E"],
            "Opal" => ["FFECD9F8", "FFB0B7EA", "FF9BBACF", "FF576CAB"],
            "Lagoon" => ["FF547C82", "FF315F66", "FF28425D", "FF102D37"],
            "Graphite" => ["FF6A7690", "FF414C6A", "FF293B51", "FF171E36"],
            _ => ["FF7D668D", "FF515A84", "FF37526D", "FF1F2B48"]
        };
        WallpaperBase.Fill = AuraGradient(colors[0], colors[3]);
        WallpaperRibbon.Fill = AuraGradient(colors[0], colors[1]);
        WallpaperFold.Fill = AuraGradient(colors[1], colors[2]);
        WallpaperHorizon.Fill = AuraGradient(colors[2], colors[3]);
        WallpaperEdge.Stroke = Resource("NexusHighlight");
        WallpaperAccents.Opacity = 1;
    }

    private void SetSegment(Button button, bool selected)
    {
        button.Background = selected ? Resource(_highContrast ? "NexusAccent" : "NexusSegment") : _transparent;
        button.Foreground = Resource(_highContrast && selected ? "NexusAccentText" : "NexusText");
        button.BorderBrush = selected ? Resource("NexusBorder") : _transparent;
        button.BorderThickness = new Thickness(selected ? 1 : 0);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(button, selected ? "Selected" : "");
    }

    private void UpdateDockSelection()
    {
        var choices = new[] { (DockHomeButton, "Home"), (DockExploreButton, "Explore"),
            (DockStudyButton, "Study"), (DockPcButton, "PC controls"), (DockRunningButton, "Running apps") };
        foreach (var (button, page) in choices)
        {
            if (button.Content is not Grid face) continue;
            var indicator = face.Children.OfType<Border>().FirstOrDefault();
            if (indicator is null) continue;
            indicator.Background = Resource("NexusAccent");
            indicator.Visibility = _page == page ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private Grid RunningDockFace(string icon)
    {
        var face = new Grid();
        face.Children.Add(NexusIcons.Image(icon, 52));
        face.Children.Add(new Border { Width = 5, Height = 3, CornerRadius = new CornerRadius(2),
            Background = Resource("NexusAccent"), VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Center });
        return face;
    }
}
