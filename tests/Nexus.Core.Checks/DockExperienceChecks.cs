using Nexus.Shell.Models;
using Nexus.Shell.Services;
using System.Text.Json;

internal static class DockExperienceChecks
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    internal static void Run()
    {
        foreach (var presence in Enum.GetValues<DockPresence>())
        {
            Check(DockMotionPolicy.Transition(null, presence) == DockMotionCue.None,
                "Initial discovery, including an already minimized app, must not replay an operation.");
            Check(DockMotionPolicy.Transition(null, presence, newlyOpened: true) == DockMotionCue.Arrive,
                "Only windows first observed after baseline reconciliation should get an arrival cue.");
            Check(DockMotionPolicy.Transition(presence, presence) == DockMotionCue.None,
                "Repeated polls, title changes and theme repaint must not replay a state cue.");
        }
        var expected = new Dictionary<(DockPresence, DockPresence), DockMotionCue>
        {
            [(DockPresence.Running, DockPresence.Active)] = DockMotionCue.Activate,
            [(DockPresence.Active, DockPresence.Running)] = DockMotionCue.None,
            [(DockPresence.Running, DockPresence.Minimized)] = DockMotionCue.Minimize,
            [(DockPresence.Active, DockPresence.Minimized)] = DockMotionCue.Minimize,
            [(DockPresence.Minimized, DockPresence.Active)] = DockMotionCue.Restore,
            [(DockPresence.Minimized, DockPresence.Running)] = DockMotionCue.Restore
        };
        foreach (var pair in expected) Check(DockMotionPolicy.Transition(pair.Key.Item1, pair.Key.Item2) == pair.Value,
            "Restore/minimize feedback must take priority over activation and ordinary deactivation.");
        var sequence = new[] { DockPresence.Active, DockPresence.Minimized, DockPresence.Minimized, DockPresence.Running, DockPresence.Active };
        var cues = sequence.Zip(sequence.Skip(1), (old, next) => DockMotionPolicy.Transition(old, next)).ToArray();
        Check(cues.SequenceEqual(new[] { DockMotionCue.Minimize, DockMotionCue.None, DockMotionCue.Restore, DockMotionCue.Activate }),
            "A rapid state sequence must settle to the current state without duplicate minimize cues.");

        var anchor = new ShellRect(-950, 850, 48, 52); var preview = new ShellRect(-1100, 570, 336, 268);
        Check(DockPreviewPolicy.InInteractionArea(anchor, preview, -930, 870)
            && DockPreviewPolicy.InInteractionArea(anchor, preview, -1000, 700)
            && DockPreviewPolicy.InInteractionArea(anchor, preview, -930, 844), "The icon, card and intervening gap must keep the preview reachable.");
        Check(!DockPreviewPolicy.InInteractionArea(anchor, preview, -700, 844)
            && !DockPreviewPolicy.InInteractionArea(anchor, preview, -1000, 540), "Unrelated desktop areas must not keep a preview open.");
        Check(!DockPreviewPolicy.Contains(anchor, anchor.Right, anchor.Y) && !DockPreviewPolicy.Contains(default, 0, 0),
            "The hover delay must require a real, non-empty icon hit area.");
        var viewport = new ShellRect(14, 58, 308, 130);
        foreach (var (width, height) in new[] { (1920, 1080), (1080, 1920), (1, 10000), (10000, 1), (1, 1) })
        {
            var fit = DockPreviewPolicy.FitSource(viewport, width, height);
            Check(fit.Width > 0 && fit.Height > 0 && fit.X >= viewport.X && fit.Y >= viewport.Y
                && fit.Right <= viewport.Right && fit.Bottom <= viewport.Bottom, "A thumbnail must fit its viewport without stretching or cropping outside it.");
            Check(Math.Abs((fit.X - viewport.X) - (viewport.Right - fit.Right)) <= 1
                && Math.Abs((fit.Y - viewport.Y) - (viewport.Bottom - fit.Bottom)) <= 1, "Portrait and landscape previews must stay centered.");
        }
        Check(DockPreviewPolicy.FitSource(viewport, 0, 100) == default && DockPreviewPolicy.FitSource(default, 100, 100) == default,
            "Unavailable source sizes cannot produce a native thumbnail destination.");
        var oldState = JsonSerializer.Deserialize<ShellState>("{\"QuickNote\":\"keep\"}")!;
        Check(oldState.DockPreviews && oldState.QuickNote == "keep", "Older settings must receive hover previews without losing user content.");
        oldState.DockPreviews = false;
        var snapshot = oldState.Snapshot();
        Check(!snapshot.DockPreviews && !JsonSerializer.Deserialize<ShellState>(JsonSerializer.Serialize(snapshot))!.DockPreviews,
            "The hover preview preference must survive both snapshots and persistence.");
        foreach (var profile in Enum.GetValues<DesktopVisualProfile>())
        { DesktopVisuals.Apply(oldState, profile); Check(!oldState.DockPreviews, "Performance profiles must not overwrite an explicit preview preference."); }
        Console.WriteLine("PASS: dock motion transitions/initial discovery, rapid state sequence, preview pointer corridor/aspect fit and preference persistence.");
    }
}
