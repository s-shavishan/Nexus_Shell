using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Nexus.Shell.UI;

// One light, non-interactive bevel per surface. No per-card backdrop or shader.
internal static class GlassDecorations
{
    private static readonly ConditionalWeakTable<Border, Border> Rims = new();
    internal static void Apply(Border frame, ShellTheme theme, string? effectiveQuality = null)
    {
        if (!Rims.TryGetValue(frame, out var rim))
        {
            var body = frame.Child;
            var layers = new Grid(); frame.Child = null;
            if (body is not null) layers.Children.Add(body);
            rim = new Border { BorderThickness = new Thickness(1), IsHitTestVisible = false, Opacity = .65 };
            layers.Children.Add(rim); frame.Child = layers; Rims.Add(frame, rim);
            var edge = rim;
            frame.RegisterPropertyChangedCallback(Border.CornerRadiusProperty, (_, _) => edge.CornerRadius = Inset(frame.CornerRadius));
        }
        rim.CornerRadius = Inset(frame.CornerRadius);
        rim.BorderBrush = theme.Edge;
        string quality = effectiveQuality ?? theme.Quality.Effective;
        rim.Opacity = quality == "Ultra" ? .85 : .65;
        rim.Visibility = theme.HighContrast || theme.Simple || quality is "Essential" or "Solid" ? Visibility.Collapsed : Visibility.Visible;
    }
    private static CornerRadius Inset(CornerRadius corners) => new(Math.Max(0, corners.TopLeft - 1), Math.Max(0, corners.TopRight - 1),
        Math.Max(0, corners.BottomRight - 1), Math.Max(0, corners.BottomLeft - 1));
}
