using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Services;

namespace Nexus.Shell.UI;

// Keep one retained, non-interactive bevel per surface. Appearance polls must
// not reassign equal dependency properties or rebuild the XAML visual tree.
internal static class GlassDecorations
{
    private sealed class RimSurface(Border rim)
    {
        internal Border Rim { get; } = rim;
        internal Brush? Edge;
        internal CornerRadius? Corners;
        internal GlassRimAppearance? Appearance;
        internal int Writes;
    }

    private static readonly ConditionalWeakTable<Border, RimSurface> Rims = new();

    internal static void Apply(Border frame, ShellTheme theme, string? effectiveQuality = null)
    {
        if (!Rims.TryGetValue(frame, out var surface))
        {
            var body = frame.Child;
            var layers = new Grid();
            frame.Child = null;
            if (body is not null) layers.Children.Add(body);
            var rim = new Border { BorderThickness = new Thickness(1), IsHitTestVisible = false };
            layers.Children.Add(rim);
            frame.Child = layers;
            surface = new RimSurface(rim);
            Rims.Add(frame, surface);
            var retained = surface;
            frame.RegisterPropertyChangedCallback(Border.CornerRadiusProperty, (_, _) => ApplyCorners(frame, retained));
        }

        ApplyCorners(frame, surface);
        var edge = theme.Edge;
        if (!ReferenceEquals(surface.Edge, edge))
        {
            surface.Rim.BorderBrush = edge;
            surface.Edge = edge;
            surface.Writes++;
        }

        var appearance = GlassRimAppearance.For(effectiveQuality ?? theme.Quality.Effective, theme.HighContrast, theme.Simple);
        if (surface.Appearance is not { } previous || previous.Opacity != appearance.Opacity)
        {
            surface.Rim.Opacity = appearance.Opacity;
            surface.Writes++;
        }
        if (surface.Appearance is not { } old || old.Visible != appearance.Visible)
        {
            surface.Rim.Visibility = appearance.Visible ? Visibility.Visible : Visibility.Collapsed;
            surface.Writes++;
        }
        surface.Appearance = appearance;
    }

    internal static int PropertyWrites(Border frame) => Rims.TryGetValue(frame, out var surface) ? surface.Writes : 0;

    private static void ApplyCorners(Border frame, RimSurface surface)
    {
        var inset = Inset(frame.CornerRadius);
        if (surface.Corners is { } previous && previous.Equals(inset)) return;
        surface.Rim.CornerRadius = inset;
        surface.Corners = inset;
        surface.Writes++;
    }

    private static CornerRadius Inset(CornerRadius corners) => new(Math.Max(0, corners.TopLeft - 1), Math.Max(0, corners.TopRight - 1),
        Math.Max(0, corners.BottomRight - 1), Math.Max(0, corners.BottomLeft - 1));
}
