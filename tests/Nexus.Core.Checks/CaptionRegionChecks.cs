using Nexus.Shell.Services;

internal static class CaptionRegionChecks
{
    internal static void Run()
    {
        int count = 0;
        void Check(bool condition, string message) { count++; if (!condition) throw new Exception(message); }
        static bool Contains(ShellRect r, int x, int y) => x >= r.X && x < r.Right && y >= r.Y && y < r.Bottom;
        static bool Overlaps(ShellRect a, ShellRect b) => a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;

        Check(LiveResizeRegionPolicy.ShouldSuspend(true, true, false, false, false),
            "User sizing of a normal resizable window must release its stale rounded clip.");
        Check(!LiveResizeRegionPolicy.ShouldSuspend(false, true, false, false, false),
            "Ordinary programmatic resizes must preserve the native clip.");
        Check(!LiveResizeRegionPolicy.ShouldSuspend(true, false, false, false, false),
            "Non-resizable windows must not lose their native decoration.");
        Check(!LiveResizeRegionPolicy.ShouldSuspend(true, true, true, false, false),
            "The custom-clipped dock must never clear its clipping region during sizing.");
        Check(!LiveResizeRegionPolicy.ShouldSuspend(true, true, false, true, false),
            "The Windows system-frame fallback must not be modified.");
        Check(!LiveResizeRegionPolicy.ShouldSuspend(true, true, false, false, true),
            "Repeated WM_SIZING events must not continuously reapply region updates.");

        foreach (double scale in new[] { 1d, 1.25, 1.5, 2 })
        {
            var client = CaptionRegionLayout.ToPixels(0, 0, 800, 600, scale)!.Value;
            var area = CaptionRegionLayout.ToPixels(80.3, .3, 699.5, 43.5, scale)!.Value;
            var search = CaptionRegionLayout.ToPixels(170.25, 6.25, 440.5, 31.5, scale, outward: true)!.Value;
            var regions = CaptionRegionLayout.Build(area, client, [search], (int)Math.Round(7 * scale));
            Check(regions.Length > 0, "A valid native title region must survive exclusion of its search control.");
            Check(regions.All(r => !Overlaps(r, search)), "Fractional-DPI caption rectangles must never steal search input.");
            Check(regions.All(r => r.X >= area.X && r.Y >= area.Y && r.Right <= area.Right && r.Bottom <= area.Bottom), "Drag regions must remain inside the actual XAML drag area.");
            Check(regions.All(r => r.Y >= Math.Round(7 * scale)), "Native caption input must leave the top resize edge available.");
            Check(!regions.Any(r => Contains(r, (int)(20 * scale), (int)(20 * scale))), "Traffic-light controls outside the title column must remain client input.");
            Check(!regions.Any(r => Contains(r, search.X, search.Y)), "The first interactive pixel must be excluded.");
            Check(CaptionRegionLayout.ToPixels(.2, .2, 10.4, 10.4, scale, true) is { } outer
                && outer.X <= .2 * scale && outer.Right >= 10.6 * scale, "Interactive exclusions round outward at every supported DPI.");
        }
        Check(CaptionRegionLayout.Build(new(0, 0, 100, 44), new(0, 0, 100, 200), [new(0, 0, 100, 44)], 7).Length == 0, "A fully interactive title area must have no caption input.");
        Check(CaptionRegionLayout.Build(new(0, 0, 100, 44), new(0, 0, 10, 10), [], 7).Length == 0, "A tiny window cannot register inverted edge insets.");
        Check(CaptionRegionLayout.Build(new(int.MaxValue, 0, 20, 44), new(0, 0, 100, 200), [], 7).Length == 0, "Overflowing native geometry must fail closed.");
        Check(CaptionRegionLayout.Build(new(0, 0, 100, 44), new(0, 0, 100, 200), [new(1, 1, 0, 10)], 7).Length == 0, "Unknown interactive dimensions must fail closed.");
        Check(CaptionRegionLayout.Build(new(0, 0, 100, 44), new(0, 0, 100, 200), Enumerable.Repeat(new ShellRect(1, 1, 2, 2), 9).ToArray(), 7).Length == 0, "Caption exclusion complexity must remain bounded.");
        foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Check(CaptionRegionLayout.ToPixels(bad, 0, 100, 44, 1) is null, "Non-finite origin must not reach native input APIs.");
            Check(CaptionRegionLayout.ToPixels(0, 0, bad, 44, 1) is null, "Non-finite size must not reach native input APIs.");
            Check(CaptionRegionLayout.ToPixels(0, 0, 100, 44, bad) is null, "Non-finite scale must not reach native input APIs.");
        }
        Check(CaptionRegionLayout.ToPixels(0, 0, 100, 44, 0) is null && CaptionRegionLayout.ToPixels(0, 0, -1, 44, 1) is null, "Zero scale and negative dimensions must fail closed.");
        Check(CaptionRegionLayout.ToPixels(int.MaxValue, 0, 100, 44, 2) is null, "Physical coordinates must not overflow Int32.");
        var original = CaptionRegionLayout.Build(new(80, 0, 700, 44), new(0, 0, 800, 600), [new(180, 8, 400, 28)], 7);
        for (int cycle = 0; cycle < 500; cycle++)
        {
            int width = 300 + cycle;
            var client = new ShellRect(0, 0, width, 200 + cycle);
            var regions = CaptionRegionLayout.Build(new(80, 0, width - 100, 44), client, [new(110, 8, Math.Max(1, width - 160), 28)], 7);
            Check(regions.All(r => r.X >= 7 && r.Y >= 7 && r.Right <= client.Right - 7 && r.Bottom <= client.Bottom - 7), "Rapid geometry changes must retain native edge input.");
            Check(CaptionRegionLayout.Build(new(80, 0, 700, 44), new(0, 0, 800, 600), [new(180, 8, 400, 28)], 7).SequenceEqual(original), "Region updates must not retain stale dimensions between windows.");
        }
        Console.WriteLine($"PASS: {count} native-caption geometry assertions, including 500 resize cycles. Native mouse release/snap/capture behavior requires Windows acceptance.");
    }
}
