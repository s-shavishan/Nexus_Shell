// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// The desktop's widget rail adapts to the *actual* work area. In a small
// session only the existing Today button remains; the shortcut tier is
// reserved for spacious desktops. Focus Mode always removes both.
internal readonly record struct DesktopWidgetLayout(bool ShowToday, bool ShowActions, bool AtTop);

internal static class DesktopWidgetLayoutPolicy
{
    internal static DesktopWidgetLayout Resolve(
        double width, double height, bool enabled, bool focusMode)
    {
        bool visible = enabled && !focusMode
            && double.IsFinite(width) && double.IsFinite(height)
            && width >= 400 && height >= 480;
        bool roomy = visible && width >= 1120 && height >= 740;
        return new(visible, roomy, roomy);
    }
}
