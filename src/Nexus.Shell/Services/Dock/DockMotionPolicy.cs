// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

public enum DockPresence { Running, Active, Minimized }
public enum DockMotionCue { None, Arrive, Launch, Activate, Minimize, Restore }

// Only a real lifecycle transition produces a cue. Theme repaint, title changes
// and the first reconciliation cannot look like an app has minimized/restored.
public static class DockMotionPolicy
{
    public static int Duration(DockMotionCue cue) => cue switch { DockMotionCue.Launch => 360, DockMotionCue.Arrive => 220, DockMotionCue.Restore => 190, DockMotionCue.Minimize => 150, _ => 130 };
    public static DockMotionCue Transition(DockPresence? previous, DockPresence next, bool newlyOpened = false)
    {
        if (previous is null) return newlyOpened ? DockMotionCue.Arrive : DockMotionCue.None;
        if (previous == next) return DockMotionCue.None;
        if (next == DockPresence.Minimized) return DockMotionCue.Minimize;
        if (previous == DockPresence.Minimized) return DockMotionCue.Restore;
        return next == DockPresence.Active ? DockMotionCue.Activate : DockMotionCue.None;
    }
    public static float IndicatorScale(DockPresence presence) => presence switch
    { DockPresence.Active => 1, DockPresence.Minimized => 5f / 22, _ => 10f / 22 };
    public static float IndicatorOpacity(DockPresence presence) => presence == DockPresence.Minimized ? .45f : 1;
}


// Animation cues are transient feedback, not durable window state.
// Only the initial arrival can wait for a button's first XAML load.
internal enum DockCueDelivery { None, Play, BufferArrival, Discard }

internal static class DockCueDeliveryPolicy
{
    internal static DockCueDelivery Decide(DockMotionCue cue,
        bool loaded, bool everLoaded, bool compositorReady)
    {
        if (cue == DockMotionCue.None) return DockCueDelivery.None;
        if (loaded) return compositorReady ? DockCueDelivery.Play : DockCueDelivery.Discard;
        return !everLoaded && cue == DockMotionCue.Arrive
            ? DockCueDelivery.BufferArrival : DockCueDelivery.Discard;
    }
}
