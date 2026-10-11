using Nexus.Shell.Models;
using Nexus.Shell.Services;

internal static class WindowPolishChecks
{
    internal static void Run()
    {
        int count = 0;
        void Check(bool value, string message) { count++; if (!value) throw new Exception(message); }
        var work = new ShellRect(0, 38, 1920, 1042);
        Check(MaximizedWindowLayout.Repair(new(-8, -8, 1936, 1056), new(0, 0, 1920, 1040), work, 1)
            == new ShellRect(-8, 30, 1936, 1058), "Correct the hidden caption and old bottom gap while retaining native resize borders.");
        Check(MaximizedWindowLayout.Repair(new(-8, 30, 1936, 1058), work, work, 1) is null, "An aligned app must not be repositioned.");
        Check(MaximizedWindowLayout.Repair(new(-8, 30, 1936, 1058), new(1, 39, 1918, 1040), work, 1) is null, "Two-pixel native rounding must not create a geometry loop.");
        Check(MaximizedWindowLayout.Repair(new(10, 10, 100, 100), new(0, 0, 100, 100), work, 1) is null, "Negative frame insets are not a native caption repair.");
        Check(MaximizedWindowLayout.Repair(new(-100, -100, 2120, 1280), new(0, 0, 1920, 1080), work, 1) is null, "Unusually large custom frame insets must be left alone.");
        Check(MaximizedWindowLayout.Repair(new(0, 0, 0, 20), work, work, 1) is null, "Damaged geometry must be rejected.");
        Check(MaximizedWindowLayout.Repair(work, work, new(int.MaxValue, 0, 100, 100), 1) is null, "Overflowing monitor geometry must be rejected.");
        foreach (var monitor in new[] { work, new(-1920, -300, 1920, 1080), new(0, 0, 321, 200) })
        foreach (double scale in new[] { .5, 1, 1.5, 4, double.NaN })
        {
            var left = WindowPlacementPlan.Bounds(monitor, "Left half", scale);
            var right = WindowPlacementPlan.Bounds(monitor, "Right half", scale);
            var center = WindowPlacementPlan.Bounds(monitor, "Centered", scale);
            Check(left.Right == right.X && left.Width + right.Width == monitor.Width && right.Right == monitor.Right, "Odd-width half placements must tile without a gap or overlap.");
            Check(center.Width > 0 && center.Height > 0 && center.X >= monitor.X && center.Y >= monitor.Y
                && center.Right <= monitor.Right && center.Bottom <= monitor.Bottom, "Centered placement must fit every supported monitor and scale.");
            var anchor = new ShellRect(monitor.Right - 100, monitor.Y + 4, 30, 28);
            var panel = DesktopLayout.AnchoredQuickControlBounds(monitor, scale, "Sound", anchor);
            Check(panel.X >= monitor.X && panel.Y >= monitor.Y && panel.Right <= monitor.Right && panel.Bottom <= monitor.Bottom, "Icon controls must remain on a scaled monitor, including tiny/negative-origin displays.");
        }
        var centeredPanel = DesktopLayout.AnchoredQuickControlBounds(work, 1, "Sound", new(600, 42, 30, 28));
        Check(centeredPanel.X == 615 - centeredPanel.Width / 2 && centeredPanel.Y >= 80, "A compact panel should open under its clicked icon.");
        var fallback = DesktopLayout.QuickControlBounds(work, 1, "Sound");
        Check(DesktopLayout.AnchoredQuickControlBounds(work, 1, "Sound", new(int.MaxValue, 0, int.MaxValue, 20)) == fallback, "An overflowing or stale anchor must use the safe panel placement.");
        var budget = new WindowRepairBudget();
        Check(budget.Admit(1, 100, work, 0), "First eligible correction should be immediate.");
        Check(!budget.Admit(1, 100, work, 999) && budget.Admit(1, 100, work, 1000), "A resistant app gets one delayed retry.");
        Check(!budget.Admit(1, 100, work, 10000), "A resistant app must exhaust its work-area retry budget.");
        Check(budget.Admit(1, 100, work with { Y = 40 }, 10001), "A changed work area permits a fresh correction.");
        Check(budget.Admit(1, 101, work, 10002), "A reused HWND with another process must get a separate budget.");
        budget.Forget(1); Check(budget.Admit(1, 101, work, 10003), "A restored/minimized window can be corrected on a later maximize.");
        budget.Reset();
        for (int handle = 1; handle <= 64; handle++) Check(budget.Admit(handle, 100, work, 0), "Budget should admit its bounded live set.");
        for (int round = 0; round < 4; round++)
        {
            Check(!budget.Admit(65 + round, 100, work, 1000 + round * 1000), "More than 64 resistant apps must not evict and renew existing budgets.");
            Check(budget.Admit(1, 100, work, 1000 + round * 1000) == (round == 0), "A full-budget scan must retain each app's retry count.");
        }
        budget.Prune([1]); Check(budget.Admit(65, 100, work, 5000), "Pruning closed apps must free budget capacity.");
        Check(!budget.Admit(0, 100, work, 5000) && !budget.Admit(2, 0, work, 5000), "Invalid native identities must not consume retry capacity.");
        foreach (string mood in AuraPalette.Moods)
        foreach (string style in GlassStyle.Choices)
        foreach (string role in new[] { "MenuBar", "Frame", "Dock" })
        {
            var recipe = GlassRecipe.For(AuraPalette.For(mood), role, style);
            Check(AuraColor.Parse(recipe.Fallback).A == 255 && AuraColor.Parse(recipe.Start).A < 128 && AuraColor.Parse(recipe.End).A < 128,
                "Every profile must retain translucent paint and an opaque native fallback.");
            Check(recipe.Frosted == (style == "Frosted") && recipe.TintOpacity is > 0 and < 1 && recipe.LuminosityOpacity is > 0 and <= 1,
                "All profiles must carry finite native composition parameters and the correct acrylic kind.");
        }
        Console.WriteLine($"PASS: {count} window/glass checks; caption-gap repair geometry, placement tiling, icon anchors, bounded retries across large desktops and all material profiles.");
    }
}
