using Nexus.Shell.Models;
using Nexus.Shell.Services;
using System.Text.Json;

internal static class DesktopPerformanceChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var reservation = new DesktopWorkAreaReservation();
        var area = new ShellRect(0, 0, 1920, 1012); int writes = 0;
        for (int i = 0; i < 1000; i++) reservation.Apply(area, _ => writes++);
        check(writes == 1, "Repeated taskbar refreshes must reserve one unchanged work area only once.");
        reservation.Apply(area with { Height = 1024 }, _ => writes++);
        reservation.Invalidate(); reservation.Apply(area with { Height = 1024 }, _ => writes++);
        check(writes == 3, "Density changes and explicit display invalidation must each update the reservation once.");
        bool failed = false;
        try { reservation.Apply(area, _ => throw new IOException("Native reservation denied")); }
        catch (IOException) { failed = true; }
        reservation.Apply(area, _ => writes++);
        check(failed && writes == 4, "A failed native work-area update must stay retryable.");

        var state = new ShellState { Wallpaper = "Solstice", QuickNote = "Keep my note", CompactDock = true,
            PinnedApps = [new("files", "Files", "nexus:files", "")] };
        foreach (var profile in Enum.GetValues<DesktopVisualProfile>())
        {
            DesktopVisuals.Apply(state, profile);
            var saved = JsonSerializer.Deserialize<ShellState>(JsonSerializer.Serialize(state))!;
            check(DesktopVisuals.Read(saved) == profile && saved.ReducedEffects == (profile == DesktopVisualProfile.Fast)
                && saved.NativeGlass == (profile == DesktopVisualProfile.Full), "Visual quality must persist through existing effect preferences.");
            check(saved.Wallpaper == "Solstice" && saved.QuickNote == "Keep my note" && saved.CompactDock
                && saved.PinnedApps.Single().Target == "nexus:files", "Performance controls must preserve the user's desktop and content.");
        }
        state.ReducedEffects = true; state.NativeGlass = true;
        check(DesktopVisuals.Read(state) == DesktopVisualProfile.Fast, "The existing reduced-effects preference takes precedence over glass.");

        foreach (var setup in new[] {
            (new ShellRect(0, 0, 1920, 1080), new ShellRect(0, 1012, 1920, 68), 1d),
            (new ShellRect(-2560, -120, 2560, 1440), new ShellRect(-2560, 1218, 2560, 102), 1.5),
            (new ShellRect(0, 0, 640, 480), new ShellRect(0, 344, 640, 136), 2d),
            (new ShellRect(100, 200, 320, 240), new ShellRect(100, 380, 320, 60), double.NaN) })
        {
            var panel = DesktopLayout.QuickSettingsBounds(setup.Item1, setup.Item2, setup.Item3);
            check(panel.Width > 0 && panel.Height > 0 && panel.X >= setup.Item1.X && panel.Right <= setup.Item1.Right
                && panel.Y >= setup.Item1.Y && panel.Bottom < setup.Item2.Y, "Quick Settings must fit above the taskbar across DPI and monitor origins.");
        }
        check(BrightnessScale.Percent(20, 60, 100) == 50 && BrightnessScale.Native(50, 20, 100) == 60
            && BrightnessScale.Native(0, 20, 100) == 20 && BrightnessScale.Native(100, 20, 100) == 100,
            "Brightness percentages must use the display's native minimum and maximum, not assume a 0–100 hardware range.");
        check(BrightnessScale.Native(100, 0, uint.MaxValue) == uint.MaxValue,
            "Brightness conversion must not overflow large driver ranges.");
        int refused = 0;
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, -1, 101 })
            try { BrightnessScale.Native(invalid, 0, 100); } catch (ArgumentOutOfRangeException) { refused++; }
        try { BrightnessScale.Percent(10, 9, 100); } catch (ArgumentOutOfRangeException) { refused++; }
        try { BrightnessScale.Percent(10, 10, 10); } catch (ArgumentOutOfRangeException) { refused++; }
        check(refused == 6, "Invalid brightness values and driver ranges must be rejected before a hardware write.");
        Console.WriteLine("PASS: unchanged work-area writes, failed-update retry, persistent visual profiles, scaled Quick Settings and brightness range validation.");
    }
}
