using System.Globalization;

namespace Nexus.Shell.Services;

// The input experiment is separate from startup and the passive UI probe.
internal sealed record InputProbeOptions(int Cycles)
{
    internal const int DefaultCycles = 20, MaximumCycles = 500;
    internal static InputProbeOptions Parse(string[] args)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int cycles = DefaultCycles;
        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (index == 0 && !option.StartsWith("--", StringComparison.Ordinal)) continue; // Executable path.
            if (option is not ("--verify-input" or "--allow-test-input" or "--cycles" or "--report") || !seen.Add(option))
                throw new ArgumentException("The input experiment refuses unknown/duplicate options and mixed launch modes.");
            if (option is "--cycles" or "--report")
            {
                if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]) || args[index].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException("The input experiment requires a value after " + option + ".");
                if (option == "--cycles" && (!int.TryParse(args[index], NumberStyles.None, CultureInfo.InvariantCulture, out cycles) || cycles is < 1 or > MaximumCycles))
                    throw new ArgumentException("--cycles must be one integer between 1 and 500.");
            }
        }
        if (!seen.Contains("--verify-input") || !seen.Contains("--allow-test-input"))
            throw new ArgumentException("The interactive probe requires --verify-input and explicit --allow-test-input.");
        return new(cycles);
    }
}

internal static class InputProbeCoordinates
{
    // SendInput ABSOLUTE + VIRTUALDESK uses physical virtual-screen coordinates.
    internal static (int X, int Y) Normalize(int x, int y, ShellRect desktop)
    {
        long right = (long)desktop.X + desktop.Width, bottom = (long)desktop.Y + desktop.Height;
        if (desktop.Width < 2 || desktop.Height < 2 || x < desktop.X || y < desktop.Y || x >= right || y >= bottom)
            throw new ArgumentOutOfRangeException(nameof(desktop), "The test point must be inside a usable virtual desktop.");
        return ((int)Math.Round(((long)x - desktop.X) * 65535d / (desktop.Width - 1)),
            (int)Math.Round(((long)y - desktop.Y) * 65535d / (desktop.Height - 1)));
    }
}
