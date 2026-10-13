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
        // Deferred motion is safe only for the first arrival. Previously
        // unloaded icons must not replay obsolete minimize/restore cues.
        Check(DockCueDeliveryPolicy.Decide(DockMotionCue.Arrive, false, false, false)
                == DockCueDelivery.BufferArrival,
            "First arrival may wait until the initial XAML load.");
        Check(DockCueDeliveryPolicy.Decide(DockMotionCue.Arrive, true, false, true)
                == DockCueDelivery.Play,
            "A loaded arrival should animate when the compositor is available.");
        Check(DockCueDeliveryPolicy.Decide(DockMotionCue.Minimize, true, true, true)
                == DockCueDelivery.Play
            && DockCueDeliveryPolicy.Decide(DockMotionCue.Restore, true, true, true)
                == DockCueDelivery.Play,
            "Live minimize/restore transitions should animate only while loaded.");
        Check(DockCueDeliveryPolicy.Decide(DockMotionCue.Minimize, false, true, true)
                == DockCueDelivery.Discard
            && DockCueDeliveryPolicy.Decide(DockMotionCue.Restore, false, true, true)
                == DockCueDelivery.Discard,
            "Old minimize/restore cues may not survive icon unloading.");
        Check(DockCueDeliveryPolicy.Decide(DockMotionCue.Arrive, false, true, true)
                == DockCueDelivery.Discard,
            "Previously loaded icons must not replay first-arrival animation.");
        Check(DockCueDeliveryPolicy.Decide(DockMotionCue.Activate, true, true, false)
                == DockCueDelivery.Discard,
            "A missing compositor must not retain old activation animations.");
        Check(DockCueDeliveryPolicy.Decide(DockMotionCue.None, true, true, true)
                == DockCueDelivery.None,
            "Stable dock state must never start compositor work.");

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
        Check(DockPlacementPolicy.RequiresPreviewDismissal(initial, initial with { Scale = 1.25 }),
            "DPI changes must invalidate preview anchor measurements even with unchanged pixel bounds.");
        Check(DockPlacementPolicy.RequiresDesktopRelayout(null, initial),
            "Initial dock positioning must reconcile the native monitor and desktop work area.");
        Check(!DockPlacementPolicy.RequiresDesktopRelayout(initial, initial),
            "No-op position notifications must not repeat global maximized-window and panel layout.");
        Check(!DockPlacementPolicy.RequiresDesktopRelayout(initial, initial with { Bounds = initial.Bounds with { Width = 450 } }),
            "Dock width animation or pin count changes must not reposition unrelated desktop windows.");
        Check(DockPlacementPolicy.RequiresDesktopRelayout(initial, initial with { Reservation = initial.Reservation with { Height = 80 } }),
            "Reservation height changes require desktop-wide reconciliation.");
        Check(DockPlacementPolicy.RequiresDesktopRelayout(initial, initial with { WorkArea = initial.WorkArea with { Height = 1000 } }),
            "Work-area adjustments must trigger native maximized-window repair.");
        Check(DockPlacementPolicy.RequiresDesktopRelayout(initial, initial with { Monitor = new(0, 0, 2560, 1440) }),
            "Changed monitor topology must reflow desktop panels.");
        Check(DockPlacementPolicy.RequiresDesktopRelayout(initial, initial with { Floating = false }),
            "Attached/floating transitions must recompute all work-area-dependent geometry.");
        Check(DockPlacementPolicy.RequiresDesktopRelayout(initial, initial with { Scale = 1.25 }),
            "DPI changes must recompute native work-area geometry even with identical pixel rectangles.");

        var screen = new ShellRect(0, 0, 1920, 1080);
        var work = new ShellRect(0, 38, 1920, 1042);
        var ordinary = DockForegroundGeometryPolicy.Classify(
            true, new ShellRect(400, 220, 800, 600), screen, work, false, false, 0x00C00000, 1);
        Check(!ordinary.Fullscreen && !ordinary.Attached,
            "Normal resizable windows should not change the floating dock or attached menu bar.");
        var captioned = DockForegroundGeometryPolicy.Classify(
            true, new ShellRect(-8, -8, 1936, 1096), screen, work, true, false, 0x00C00000, 1);
        Check(!captioned.Fullscreen && captioned.Attached,
            "Maximized captioned windows must retain native title actions and dock edge reveal.");
        var borderless = DockForegroundGeometryPolicy.Classify(
            true, screen, screen, work, true, false, 0, 1);
        Check(borderless.Fullscreen && borderless.Attached,
            "A borderless maximized game should be suppressed as fullscreen.");
        var unavailable = DockForegroundGeometryPolicy.Classify(
            false, screen, screen, work, true, false, 0, 1);
        Check(!unavailable.Fullscreen && !unavailable.Attached,
            "An unavailable or cross-monitor foreground rect must not trigger fullscreen mode.");
        var minimized = DockForegroundGeometryPolicy.Classify(
            true, screen, screen, work, true, true, 0, 1);
        Check(minimized.Fullscreen && !minimized.Attached,
            "Native minimized status must prevent attached-menu placement from a stale rect.");
        var attached = DockForegroundGeometryPolicy.Classify(
            true, work, screen, work, false, false, 0x00C00000, 1.25);
        Check(!attached.Fullscreen && attached.Attached,
            "Edge-aligned application geometry should attach the menu bar without hiding the dock.");

        foreach (var presence in Enum.GetValues<DockPresence>())
        {
            Check(!DockIndicatorUpdatePolicy.ShouldTransition(false, null, presence)
                && !DockIndicatorUpdatePolicy.ShouldTransition(false, DockPresence.Active, presence),
                "An invalid or expired dock HWND must not replay native indicator transitions.");
            Check(DockIndicatorUpdatePolicy.ShouldTransition(true, null, presence),
                "A newly valid dock HWND must reconcile initial indicator appearance.");
            Check(!DockIndicatorUpdatePolicy.ShouldTransition(true, presence, presence),
                "An unchanged valid dock presence must not restart its compositor animation.");
        }
        Check(DockIndicatorUpdatePolicy.Decide(false, false, true, true, true) == DockIndicatorUpdate.None,
            "Stable dock presence must not restart compositor animation or rewrite indicator state.");
        Check(DockIndicatorUpdatePolicy.Decide(true, false, true, true, true) == DockIndicatorUpdate.Animate,
            "A live presence transition should use the retained compositor animation.");
        Check(DockIndicatorUpdatePolicy.Decide(true, false, true, false, true) == DockIndicatorUpdate.Settle
            && DockIndicatorUpdatePolicy.Decide(true, false, true, true, false) == DockIndicatorUpdate.Settle,
            "Reduced motion and unloaded indicators must use safe static state.");
        Check(DockIndicatorUpdatePolicy.Decide(false, true, true, true, true) == DockIndicatorUpdate.None,
            "Repeated presence updates must let an active animation finish uninterrupted.");
        Check(DockIndicatorUpdatePolicy.Decide(false, true, false, true, true) == DockIndicatorUpdate.Settle
            && DockIndicatorUpdatePolicy.Decide(false, true, true, false, true) == DockIndicatorUpdate.Settle
            && DockIndicatorUpdatePolicy.Decide(false, true, true, true, false) == DockIndicatorUpdate.Settle,
            "A pending animation must settle when animations disable, accessibility changes or an icon unloads.");
        Check(DockIndicatorUpdatePolicy.Decide(false, false, false, false, false) == DockIndicatorUpdate.None,
            "Already-settled indicators must not repeatedly stop/set compositor animations.");

        var frame = new DockFrameGeometry(
            new(-1020, 922, 480, 60), new(-1200, 910, 1920, 78), 1.25, true, false);
        Check(DockFrameLayoutPolicy.RequiresLayout(null, frame),
            "The initial dock frame must always be measured and clipped.");
        Check(!DockFrameLayoutPolicy.RequiresLayout(frame, frame),
            "No-op ABN_POSCHANGED notifications must not rewrite dock frame properties.");
        Check(DockFrameLayoutPolicy.Clip(frame) == new ShellRect(180, 12, 480, 60)
            && DockFrameLayoutPolicy.CornerRadius(frame) == 22,
            "Negative monitor origins must produce a local native clip with floating corners.");
        Check(DockFrameLayoutPolicy.RequiresLayout(frame, frame with { Bounds = frame.Bounds with { Width = 520 } }),
            "Changed pin count and dock width must resize and clip the frame.");
        Check(DockFrameLayoutPolicy.RequiresLayout(frame, frame with { Reservation = frame.Reservation with { Y = 900 } }),
            "Changes in the HWND reservation must recalculate the frame offset.");
        Check(DockFrameLayoutPolicy.RequiresLayout(frame, frame with { Scale = 1.5 }),
            "DPI changes must reapply logical dimensions even at identical pixel bounds.");
        Check(DockFrameLayoutPolicy.RequiresLayout(frame, frame with { Floating = false })
            && DockFrameLayoutPolicy.CornerRadius(frame with { Floating = false }) == 0,
            "Attached mode must remove floating native corners.");
        Check(DockFrameLayoutPolicy.RequiresLayout(frame, frame with { HighContrast = true })
            && DockFrameLayoutPolicy.CornerRadius(frame with { HighContrast = true }) == 0,
            "High Contrast must restore square system-safe corners without requiring a geometry change.");

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
