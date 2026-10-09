namespace Nexus.Shell.Services;

public static class BrightnessScale
{
    public static double Percent(uint minimum, uint current, uint maximum)
    {
        if (maximum <= minimum || current < minimum || current > maximum)
            throw new ArgumentOutOfRangeException(nameof(current), "The display returned an invalid brightness range.");
        return (current - minimum) * 100d / (maximum - minimum);
    }
    public static uint Native(double percent, uint minimum, uint maximum)
    {
        if (!double.IsFinite(percent) || percent < 0 || percent > 100 || maximum <= minimum)
            throw new ArgumentOutOfRangeException(nameof(percent));
        return (uint)Math.Round(minimum + (maximum - minimum) * (percent / 100));
    }
}
