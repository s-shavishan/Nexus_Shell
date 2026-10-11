using System.Text.Json;
using Nexus.Runtime;
using Nexus.Shell.Models;
using Nexus.Shell.Services;

internal static class GlassPreferenceChecks
{
    internal static void Run()
    {
        int count = 0;
        void Check(bool value, string message) { count++; if (!value) throw new Exception(message); }
        void Reject(PanelAction action)
        { try { ControlCenterPreferences.Validate(action); } catch (RuntimeFailure) { count++; return; } throw new Exception("Malformed material preference was accepted."); }
        Check(JsonSerializer.Deserialize<ShellState>("{}")!.GlassStyle == GlassStyle.Default, "Old settings must default to Balanced glass.");
        Check(new ControlCenterPreferences("Midnight", true, false, true, false, true, false, true, true, false).GlassStyle == GlassStyle.Default, "Prior preference snapshots must preserve a useful material default.");
        var state = new ShellState { QuickNote = "Retain my note", NativeGlass = true, SurfaceAnimations = false };
        foreach (string style in GlassStyle.Choices)
        {
            ControlCenterPreferences.Set(state, new(Guid.NewGuid(), "preference", DesktopPreference.GlassStyle, Value: style));
            Check(state.GlassStyle == style && state.QuickNote == "Retain my note" && !state.SurfaceAnimations && state.NativeGlass, "Live material edits must preserve unrelated preferences.");
            var copy = new ShellState(); ControlCenterPreferences.From(state).Apply(copy);
            Check(copy.GlassStyle == style && copy.Snapshot().GlassStyle == style, "Worker and desktop snapshots must carry the selected profile.");
        }
        foreach (var invalid in new[] {
            new PanelAction(Guid.NewGuid(), "preference", DesktopPreference.GlassStyle, true, "Clear"),
            new PanelAction(Guid.NewGuid(), "preference", DesktopPreference.GlassStyle, true),
            new PanelAction(Guid.NewGuid(), "preference", DesktopPreference.GlassStyle, Value: "clear"),
            new PanelAction(Guid.NewGuid(), "preference", DesktopPreference.GlassStyle, Value: "unknown"),
            new PanelAction(Guid.NewGuid(), "preference", DesktopPreference.GlassStyle),
            new PanelAction(Guid.NewGuid(), "preference", DesktopPreference.NativeGlass, true, "Clear") }) Reject(invalid);
        string folder = Path.Combine(Path.GetTempPath(), "Nexus-glass-check-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new StateStore(folder);
            using (var repository = new CoreStateRepository(store))
            {
                foreach (string style in GlassStyle.Choices)
                {
                    var draft = repository.Read().State; draft.GlassStyle = style;
                    repository.Commit(new(draft, repository.Revision, Guid.NewGuid()));
                    Check(repository.Appearance().GlassStyle == style && new StateStore(folder).Load().GlassStyle == style,
                        "Acknowledged profiles must reach isolated appearance reads and durable settings.");
                }
                var damaged = repository.Read().State; damaged.GlassStyle = "invalid";
                repository.Commit(new(damaged, repository.Revision, Guid.NewGuid()));
                Check(repository.Appearance().GlassStyle == GlassStyle.Default, "Invalid saved profiles must recover to Balanced without losing settings.");
            }
            using var restarted = new CoreStateRepository(new StateStore(folder));
            Check(restarted.Appearance().GlassStyle == GlassStyle.Default, "A Core restart must recover the normalized material profile.");
        }
        finally { Directory.Delete(folder, true); }
        var desired = new PanelDesired(5, true, "Sound", new(0, 0, 1920, 1080, 1, 123, true), true, new(700, 4, 30, 28));
        Check(JsonSerializer.Deserialize<PanelDesired>(JsonSerializer.Serialize(desired)) == desired, "An icon anchor must survive the actual worker protocol JSON round trip.");
        Check(JsonSerializer.Deserialize<PanelDesired>("{\"Sequence\":1,\"Visible\":true,\"Section\":\"Sound\",\"Monitor\":{\"Width\":1920,\"Height\":1080,\"Scale\":1}}")!.Anchor is null,
            "Prior panel requests must retain safe placement without an anchor.");
        Console.WriteLine($"PASS: {count} material/protocol checks; strict typed edits, legacy defaults, isolated appearance reads, durable profiles, normalization and icon-anchor round trips.");
    }
}
