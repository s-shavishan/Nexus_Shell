namespace Nexus.Shell.Services;

public static class WindowPlacementPlan
{
    public static readonly string[] Choices = ["Left half", "Right half", "Centered"];
    public static ShellRect Bounds(ShellRect work, string placement, double scale)
    {
        if (work.Width < 2 || work.Height < 1 || !Choices.Contains(placement)) throw new ArgumentException("Choose a supported window placement and usable work area.");
        if (placement == "Left half") return work with { Width = work.Width / 2 };
        if (placement == "Right half") return work with { X = work.X + work.Width / 2, Width = work.Width - work.Width / 2 };
        scale = double.IsFinite(scale) ? Math.Clamp(scale, .5, 4) : 1;
        int width = Math.Min(work.Width, (int)Math.Round(1080 * scale)), height = Math.Min(work.Height, (int)Math.Round(740 * scale));
        return new(work.X + (work.Width - width) / 2, work.Y + (work.Height - height) / 2, width, height);
    }
}
