namespace Nexus.Shell.Services;

// During a foreground switch only the previously active and the newly active
// button can change selected state. Structural minimize/restore/title events
// continue to refresh every button, as does the independent recovery scan.
internal static class DockStateRefreshPolicy
{
    internal static bool ShouldInspect(IntPtr handle, IntPtr previous, IntPtr foreground)
        => handle != IntPtr.Zero && (handle == previous || handle == foreground);
}
