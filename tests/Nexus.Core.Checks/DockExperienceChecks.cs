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

        // Idle ABN_POSCHANGED callbacks must not close a hover card when its
        // real anchor/work-area geometry hasn't changed.
        var initial = new DockPlacementSnapshot(
            new(-950, 900, 500, 70), new(-1200, 890, 1920, 90),
            new(-1200, -100, 1920, 1080), new(-1200, -100, 1920, 1080), true);
        Check(DockPlacementPolicy.RequiresPreviewDismissal(null, initial),
            "Initial placement should invalidate a potentially stale preview.");
        Check(!DockPlacementPolicy.RequiresPreviewDismissal(initial, initial),
            "Repeated appbar or appearance notifications with unchanged geometry must preserve hover previews.");
        Check(DockPlacementPolicy.RequiresPreviewDismissal(initial, initial with { Bounds = initial.Bounds with { X = -940 } }),
            "An actual floating dock move must invalidate its preview anchor.");
        Check(DockPlacementPolicy.RequiresPreviewDismissal(initial, initial with { Reservation = initial.Reservation with { Height = 80 } }),
            "Reservation changes must invalidate the preview's interaction corridor.");
        Check(DockPlacementPolicy.RequiresPreviewDismissal(initial, initial with { WorkArea = initial.WorkArea with { Height = 1000 } }),
            "Work-area changes must refresh the preview's placement.");
        Check(DockPlacementPolicy.RequiresPreviewDismissal(initial, initial with { Monitor = new(0, 0, 2560, 1440) }),
            "Moving the dock between monitors must dismiss stale preview geometry.");
        Check(DockPlacementPolicy.RequiresPreviewDismissal(initial, initial with { Floating = false }),
            "Changing from floating to attached dock must dismiss the old hover preview.");

        var anchor = new ShellRect(-950, 850, 48, 52); var preview = new ShellRect(-1100, 570, 336, 268);
        Check(DockPreviewPolicy.InInteractionArea(anchor, preview, -930, 870)
            && DockPreviewPolicy.InInteractionArea(anchor, preview, -1000, 700)
            && DockPreviewPolicy.InInteractionArea(anchor, preview, -930, 844), "The icon, card and intervening gap must keep the preview reachable.");
        Check(!DockPreviewPolicy.InInteractionArea(anchor, preview, -700, 844)
            && !DockPreviewPolicy.InInteractionArea(anchor, preview, -1000, 540), "Unrelated desktop areas must not keep a preview open.");
        Check(!DockPreviewPolicy.Contains(anchor, anchor.Right, anchor.Y) && !DockPreviewPolicy.Contains(default, 0, 0),
            "The hover delay must require a real, non-empty icon hit area.");
        // A live preview can remain open while its target is minimized, cloaked,
        // restored or moved between virtual desktops. Reattach DWM once only
        // when the app becomes drawable, with accessibility fallbacks intact.
        Check(DockPreviewPolicy.ShouldAttachThumbnail(true, true, false, false, false, false),
            "An open, drawable restored window should regain a live DWM thumbnail.");
        Check(!DockPreviewPolicy.ShouldAttachThumbnail(true, true, false, false, false, true),
            "A still-attached thumbnail must not be registered twice.");
        Check(!DockPreviewPolicy.ShouldAttachThumbnail(true, true, true, false, false, false),
            "Minimized apps must retain the readable static preview card.");
        Check(!DockPreviewPolicy.ShouldAttachThumbnail(true, true, false, true, false, false),
            "Cloaked virtual-desktop windows must not have a live preview.");
        Check(!DockPreviewPolicy.ShouldAttachThumbnail(true, false, false, false, false, false),
            "Closed/reused HWND identities must never acquire a DWM relationship.");
        Check(!DockPreviewPolicy.ShouldAttachThumbnail(false, true, false, false, false, false),
            "Hidden preview surfaces must not hold a live thumbnail.");
        Check(!DockPreviewPolicy.ShouldAttachThumbnail(true, true, false, false, true, false),
            "Reduced-effects or high-contrast display must stay on the static preview fallback.");

        // Transient DWM registration failure must retry without a tight 80 ms
        // native loop. Delays cap at 4s and reset when the card is unavailable.
        var retry = new DockThumbnailRetry();
        Check(retry.ShouldAttempt(true, false, 0), "First DWM thumbnail registration should be immediate.");
        Check(!retry.ShouldAttempt(true, false, 80) && !retry.ShouldAttempt(true, false, 249),
            "Repeated foreground ticks should not hot-loop a failed DWM registration.");
        Check(retry.ShouldAttempt(true, false, 250) && !retry.ShouldAttempt(true, false, 749),
            "First retry must wait for the 250 ms backoff.");
        Check(retry.ShouldAttempt(true, false, 750) && !retry.ShouldAttempt(true, false, 1749),
            "Second retry should back off for 500 ms.");
        Check(retry.ShouldAttempt(true, false, 1750) && !retry.ShouldAttempt(true, false, 3749),
            "Subsequent DWM failures should back off for 1 second.");
        Check(retry.ShouldAttempt(true, false, 3750) && !retry.ShouldAttempt(true, false, 7749),
            "Backoff should grow to the bounded 4-second maximum.");
        Check(retry.ShouldAttempt(true, false, 7750) && !retry.ShouldAttempt(true, false, 11749),
            "Repeated failures should retain the 4-second cap.");
        Check(retry.ShouldAttempt(true, true, 7751) == false && retry.ShouldAttempt(true, false, 7752),
            "Successful attachment must reset retries for a later DWM disconnect.");
        Check(!retry.ShouldAttempt(false, false, 7753) && retry.ShouldAttempt(true, false, 7754),
            "Minimize, cloaking, High Contrast and closing must reset the retry gate.");
        Check(retry.ShouldAttempt(true, false, 1),
            "A monotonic-clock rollback must allow recovery rather than permanently stalling.");
        retry.Reset();
        Check(retry.ShouldAttempt(true, false, long.MaxValue - 10)
            && !retry.ShouldAttempt(true, false, long.MaxValue - 5),
            "Deadline calculation must never overflow long ticks.");

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
