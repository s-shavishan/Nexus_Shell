namespace Nexus.Shell.Services;

public enum DockPresence { Running, Active, Minimized }
public enum DockMotionCue { None, Arrive, Launch, Activate, Minimize, Restore }

// Only a real lifecycle transition produces a cue. Theme repaint, title changes
// and the first reconciliation cannot look like an app has minimized/restored.
public static class DockMotionPolicy
{
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
