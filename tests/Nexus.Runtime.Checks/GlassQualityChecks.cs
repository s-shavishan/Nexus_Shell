using System.Text.Json;
using Nexus.Runtime;
using Nexus.Shell.Models;
using Nexus.Shell.Services;

internal static class GlassQualityChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        Check(JsonSerializer.Deserialize<ShellState>("{}")!.GlassQuality == GlassQuality.Default,
            "Old settings must keep the existing Balanced quality without a destructive migration.");
        Check(JsonSerializer.Deserialize<AppearanceSettings>("{\"Wallpaper\":\"Midnight\",\"NativeGlass\":true}")!.GlassQuality == GlassQuality.Default,
            "Older worker appearance snapshots must default quality safely.");
        var state = new ShellState { QuickNote = "Keep my note", GlassStyle = "Clear", PinnedApps = [new("a", "App", "a.exe", "")] };
        foreach (string quality in GlassQuality.Choices)
        {
            ControlCenterPreferences.Set(state, new(Guid.NewGuid(), "preference", DesktopPreference.GlassQuality, Value: quality));
            var worker = new ShellState(); ControlCenterPreferences.From(state).Apply(worker);
            Check(worker.GlassQuality == quality && state.Snapshot().GlassQuality == quality,
                "Quality must reach the actual desktop/worker snapshot contract.");
            Check(state.GlassStyle == "Clear" && state.QuickNote == "Keep my note" && state.PinnedApps.Count == 1,
                "Selecting quality changed the user's material look or unrelated data.");
            var restored = JsonSerializer.Deserialize<ControlCenterPreferences>(JsonSerializer.Serialize(ControlCenterPreferences.From(state)))!;
            Check(restored.GlassQuality == quality, "Quality disappeared from the worker protocol JSON.");
            for (int flags = 0; flags < 512; flags++)
            {
                bool Bit(int index) => (flags & (1 << index)) != 0;
                var context = new GlassQualityContext(Bit(0), Bit(1), Bit(2), Bit(3), Bit(4), Bit(5), Bit(6), Bit(7), Bit(8));
                var decision = GlassQualityPolicy.Resolve(quality, context);
                Check(decision.Requested == quality && GlassQuality.Choices.Contains(decision.Effective),
                    "A temporary policy override rewrote the request or returned an unsupported tier.");
                if (decision.NativeBackdrop)
                    Check(context.NativeGlass && !context.ReducedEffects && !context.HighContrast && context.Transparency && context.BackdropSupported
                        && !context.RemoteSession && !context.EnergySaver && !context.ControllerFailed && !context.WindowsFallback,
                        "The quality policy allowed native glass under a required downgrade.");
                if (context.HighContrast || context.ReducedEffects || !context.NativeGlass || !context.Transparency)
                    Check(decision.Effective == "Solid" && !decision.EdgeLighting && !decision.Motion,
                        "Accessibility or explicit glass-off policies must provide a solid surface.");
            }
        }
        var healthy = new GlassQualityContext();
        Check(GlassQualityPolicy.Resolve("Ultra", healthy).Effective == "Ultra", "A healthy system must respect the requested Ultra tier.");
        Check(GlassQualityPolicy.Resolve("Ultra", healthy with { EnergySaver = true }).Effective == "Essential", "Energy saver must lower expensive quality temporarily.");
        Check(GlassQualityPolicy.Resolve("Ultra", healthy with { RemoteSession = true }).Effective == "Essential", "Remote sessions must avoid native backdrop cost.");
        Check(GlassQualityPolicy.Resolve("Essential", healthy with { BackdropSupported = false }).Effective == "Essential", "Essential must work without a native backdrop.");
        Check(GlassQualityPolicy.Resolve("invalid", healthy).Requested == GlassQuality.Default, "Unknown stored quality must normalize to the existing default.");
        Check(GlassQualityPolicy.Resolve("Ultra", healthy with { WindowsFallback = true }).Effective == "Solid", "Windows' actual fallback must be disclosed as solid.");
        foreach (var action in new[] {
            new PanelAction(Guid.NewGuid(), "preference", DesktopPreference.GlassQuality, Value: "ultra"),
            new PanelAction(Guid.NewGuid(), "preference", DesktopPreference.GlassQuality, true, "Ultra"),
            new PanelAction(Guid.NewGuid(), "preference", DesktopPreference.GlassQuality, true),
            new PanelAction(Guid.NewGuid(), "preference", DesktopPreference.GlassQuality) })
        {
            bool rejected = false;
            try { ControlCenterPreferences.Validate(action); } catch (RuntimeFailure) { rejected = true; }
            Check(rejected, "The typed preference boundary accepted a malformed quality request.");
        }
        string folder = Path.Combine(Path.GetTempPath(), "Nexus-quality-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var repository = new CoreStateRepository(new StateStore(folder)))
            {
                foreach (string quality in GlassQuality.Choices)
                {
                    var candidate = repository.Read().State; candidate.GlassQuality = quality; candidate.GlassStyle = "Frosted";
                    repository.Commit(new(candidate, repository.Revision, Guid.NewGuid()));
                    Check(repository.Appearance().GlassQuality == quality && new StateStore(folder).Load().GlassQuality == quality,
                        "Acknowledged quality must persist and reach isolated Files appearance reads.");
                }
                var invalid = repository.Read().State; invalid.GlassQuality = "unknown"; invalid.QuickNote = "Retain data";
                repository.Commit(new(invalid, repository.Revision, Guid.NewGuid()));
                Check(repository.Appearance().GlassQuality == GlassQuality.Default && repository.Read().State.QuickNote == "Retain data",
                    "Normalization lost data while recovering an invalid quality value.");
            }
            using var restarted = new CoreStateRepository(new StateStore(folder));
            Check(restarted.Appearance().GlassQuality == GlassQuality.Default && restarted.Appearance().GlassStyle == "Frosted",
                "A Core restart lost quality normalization or changed the independent material look.");
        }
        finally { Directory.Delete(folder, true); }
        Console.WriteLine($"PASS: {checks} glass quality checks; policy matrix, requested/effective separation, accessibility, power/RDP fallback, legacy contracts, strict edits and durable state.");
    }
}
