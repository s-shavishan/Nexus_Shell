// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// Some borderless games report IsZoomed=true while their HWND occupies the
// entire monitor. A maximized captioned app, by contrast, should retain the
// dock's ordinary auto-hide and edge-reveal behavior.
internal static class DockFullscreenPolicy
{
    private const long WindowCaptionStyle = 0x00C00000; // WS_CAPTION

    internal static bool IsFullscreen(ShellRect window, ShellRect monitor, bool maximized, long windowStyle)
    {
        if (window.Width <= 0 || window.Height <= 0 || monitor.Width <= 0 || monitor.Height <= 0) return false;
        bool coversMonitor = (long)window.X <= monitor.X && (long)window.Y <= monitor.Y
            && (long)window.X + window.Width >= (long)monitor.X + monitor.Width
            && (long)window.Y + window.Height >= (long)monitor.Y + monitor.Height;
        if (!coversMonitor) return false;
        // Preserve the old behavior for ordinary maximized windows with a
        // native caption. Never let IsZoomed hide a borderless fullscreen game.
        return !maximized || (windowStyle & WindowCaptionStyle) != WindowCaptionStyle;
    }
}
