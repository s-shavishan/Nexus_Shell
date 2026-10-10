namespace Nexus.Shell.Services;

public sealed record MinimizedDesktopMetrics(int Width, int HorizontalGap, int VerticalGap, int Arrangement);

// ARW_HIDE changes the position of minimized captions, not application lifetime,
// visibility styles, restore placement or Alt+Tab membership.
public static class MinimizedWindowPolicy
{
    public static MinimizedDesktopMetrics Hidden(MinimizedDesktopMetrics original) => original with { Arrangement = original.Arrangement | 8 };
    public static bool Owns(MinimizedDesktopMetrics current, MinimizedDesktopMetrics original) => current.Arrangement == Hidden(original).Arrangement;
    public static MinimizedDesktopMetrics Restore(MinimizedDesktopMetrics current, MinimizedDesktopMetrics original)
        => Owns(current, original) ? current with { Arrangement = original.Arrangement } : current;
    public static bool IsValid(MinimizedDesktopMetrics value) => value.Width is >= 0 and <= 32768
        && value.HorizontalGap is >= 0 and <= 32768 && value.VerticalGap is >= 0 and <= 32768 && value.Arrangement is >= 0 and <= 15;
}
