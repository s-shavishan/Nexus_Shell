// නමෝ බුද්ධාය 🤍
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Nexus.Shell.UI;

// WinUI dependency-property writes can enqueue layout/render invalidations,
// even when a theme/material refresh selects the same retained brush. Keep the
// visible base opaque until the backdrop reports Active.
internal static class GlassPaintRetention
{
    internal static void Background(Border frame, Brush paint)
    {
        if (!ReferenceEquals(frame.Background, paint)) frame.Background = paint;
    }

    internal static void Edge(Border frame, Brush paint)
    {
        if (!ReferenceEquals(frame.BorderBrush, paint)) frame.BorderBrush = paint;
    }

    internal static void Theme(Border frame, ElementTheme appearance)
    {
        if (frame.RequestedTheme != appearance) frame.RequestedTheme = appearance;
    }
}
