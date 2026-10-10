using Nexus.Shell.Models;
using Nexus.Shell.Services;
using System.Text.Json;

internal static class ResponsiveUiChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { ++checks; if (!value) throw new Exception(message); }
        var state = new ShellState { NativeGlass = true, SurfaceAnimations = false };
        for (int index = 0; index < 30; index++) RecentApplications.Record(state, new("app" + index, "App " + index, @"C:\Apps\" + index + ".exe", ""));
        Check(state.RecentApps.Count == 12 && state.RecentApps[0].Id == "app29", "Recent apps must be bounded and newest first.");
        RecentApplications.Record(state, new("changed-id", "Changed name", @"c:\apps\25.EXE", ""));
        Check(state.RecentApps[0].Id == "changed-id" && state.RecentApps.Count == 12
            && state.RecentApps.Count(app => app.Target.Equals(@"C:\Apps\25.exe", StringComparison.OrdinalIgnoreCase)) == 1, "Reopening must update one target without case-sensitive duplicates.");
        Check(LaunchpadCatalog.Filter(state.RecentApps, "", "All")[0].Id == "changed-id", "Launchpad must preserve recent ordering.");
        var snapshot = state.Snapshot(); snapshot.RecentApps.Clear();
        Check(state.RecentApps.Count == 12 && !snapshot.SurfaceAnimations && snapshot.NativeGlass, "Snapshot must isolate history and preserve independent effects.");
        string directory = Path.Combine(Path.GetTempPath(), "Nexus-responsive-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new StateStore(directory); store.Save(state); var loaded = store.Load();
            Check(loaded.RecentApps.Count == 12 && loaded.RecentApps[0].Name == "Changed name" && !loaded.SurfaceAnimations && loaded.NativeGlass, "History and effects must survive actual atomic persistence.");
            loaded.RememberRecentItems = false; store.Save(loaded); loaded = store.Load();
            Check(loaded.RecentApps.Count == 0, "Opting out must remove app history on load.");
            RecentApplications.Record(loaded, state.RecentApps[0]); Check(loaded.RecentApps.Count == 0, "Opted-out launches must not be recorded.");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        Check(JsonSerializer.Deserialize<ShellState>("{}")!.SurfaceAnimations, "Old settings must retain animations by default.");
        Check(RecentApplications.Normalize([new("invalid", "", "target", ""), new("invalid2", "Title", "", "")]).Count == 0, "Damaged recent entries must not enter the grid.");
        foreach (string path in new[] { @"C:\Apps\Code.exe", @"D:\Start Menu\Editor.LNK", @"E:\Apps\Tool.appref-ms" })
            Check(AppIconPolicy.IsLocalFile(path), "Local app icon candidates must be accepted.");
        foreach (string path in new[] { @"\\server\share\tool.lnk", "file://server/tool.exe", "https://example.test/app.exe", "nexus:files", "ms-settings:", @"C:tool.exe", "/tmp/tool.exe", "C:\\app\0.exe", @"C:\Document.txt" })
            Check(!AppIconPolicy.IsLocalFile(path), "Direct network, URI, relative, damaged or non-app icon paths must be rejected.");
        foreach (string mood in AuraPalette.Moods)
        foreach (string role in new[] { "Frame", "MenuBar", "Dock", "Card", "Input" })
        {
            var recipe = GlassRecipe.For(AuraPalette.For(mood), role);
            Check(AuraColor.Parse(recipe.Fallback).A == 255 && AuraColor.Parse(recipe.Tint).A == 255, "Unavailable blur must use an opaque palette fallback.");
            Check(float.IsFinite(recipe.TintOpacity) && recipe.TintOpacity is > 0 and < 1
                && float.IsFinite(recipe.LuminosityOpacity) && recipe.LuminosityOpacity is >= 0 and <= 1
                && AuraColor.Parse(recipe.Start).A < 128 && AuraColor.Parse(recipe.End).A < 128, "Live glass paint must remain translucent with finite parameters.");
        }
        foreach (var monitor in new[] { new ShellRect(0, 0, 1920, 1080), new(-1280, -300, 1280, 720), new(0, 0, 320, 240) })
        foreach (double scale in new[] { .5, 1, 1.5, 4, double.NaN })
        foreach (string section in new[] { "Sound", "Network", "Bluetooth", "Display", "Power" })
        {
            var panel = DesktopLayout.QuickControlBounds(monitor, scale, section);
            Check(panel.Width > 0 && panel.Height > 0 && panel.X >= monitor.X && panel.Y >= monitor.Y
                && panel.Right <= monitor.Right && panel.Bottom <= monitor.Bottom, "Direct controls must fit scaled and negative-origin monitors.");
        }
        Console.WriteLine($"PASS: {checks} responsive UI checks; recent ordering/privacy/persistence, icon path policy, finite glass fallbacks and direct-control layout.");
    }
}
