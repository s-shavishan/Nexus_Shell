namespace Nexus.Shell.Services;

// Preserve topmost ordering and no-activation on every shell notification,
// while skipping native move/size work for an already correctly placed HWND.
internal static class MenuBarNativePlacement
{
    internal const uint NoActivate = 0x0010; // SWP_NOACTIVATE
    internal const uint NoSize = 0x0001;     // SWP_NOSIZE
    internal const uint NoMove = 0x0002;     // SWP_NOMOVE

    internal static uint Flags(bool geometryKnown, ShellRect actual, ShellRect desired)
        => NoActivate | (geometryKnown && actual == desired ? NoMove | NoSize : 0u);
}
