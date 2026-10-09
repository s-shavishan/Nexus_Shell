using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

public enum DesktopVisualProfile { Fast, Balanced, Full }

public static class DesktopVisuals
{
    public static DesktopVisualProfile Read(ShellState state) => state.ReducedEffects ? DesktopVisualProfile.Fast
        : state.NativeGlass ? DesktopVisualProfile.Full : DesktopVisualProfile.Balanced;
    public static void Apply(ShellState state, DesktopVisualProfile profile)
    {
        if (!Enum.IsDefined(profile)) throw new ArgumentOutOfRangeException(nameof(profile));
        state.ReducedEffects = profile == DesktopVisualProfile.Fast;
        state.NativeGlass = profile == DesktopVisualProfile.Full;
    }
}
