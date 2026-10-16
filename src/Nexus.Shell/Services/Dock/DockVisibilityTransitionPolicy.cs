// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// Keep desired dock state, actual HWND visibility and in-flight animations
// distinct. Windows can show/hide a tool HWND independent of Nexus state.
internal enum DockVisibilityAction { None, Show, AnimateHide, HideImmediately }

internal static class DockVisibilityTransitionPolicy
{
    internal static DockVisibilityAction Decide(
        bool shouldShow, bool shown, bool nativeVisible, bool hidePending, bool forceHide)
    {
        if (shouldShow)
            return !shown || !nativeVisible || hidePending ? DockVisibilityAction.Show : DockVisibilityAction.None;
        if (forceHide)
            return shown || nativeVisible || hidePending ? DockVisibilityAction.HideImmediately : DockVisibilityAction.None;
        if (shown)
            return nativeVisible ? DockVisibilityAction.AnimateHide : DockVisibilityAction.HideImmediately;
        // Do not interrupt an in-flight hide. If the animation has completed,
        // any externally exposed window must be hidden immediately.
        return nativeVisible && !hidePending ? DockVisibilityAction.HideImmediately : DockVisibilityAction.None;
    }
}
