// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// Size the three-pane Files layout from its actual available content width,
// not the overall monitor dimensions. File pickers never show an inspector.
internal readonly record struct FilesPaneGeometry(double SidebarWidth, double InspectorWidth, bool InspectorVisible);

internal static class FilesPaneLayoutPolicy
{
    internal static FilesPaneGeometry Resolve(double width, bool browse)
    {
        if (!double.IsFinite(width) || width <= 0)
            return new(136, 0, false);
        double sidebar = width < 600 ? 136 : width < 1050 ? 190 : 206;
        bool inspector = browse && width >= 860;
        double inspectorWidth = inspector ? width >= 1170 ? 256 : 228 : 0;
        return new(sidebar, inspectorWidth, inspector);
    }
}
