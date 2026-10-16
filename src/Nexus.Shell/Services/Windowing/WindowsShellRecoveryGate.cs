// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

public readonly record struct WindowsShellPresence(long DesktopWindow, long TaskbarWindow)
{
    public bool Ready => DesktopWindow != 0 && TaskbarWindow != 0;
    public bool Absent => DesktopWindow == 0 && TaskbarWindow == 0;
}

public enum WindowsShellStartDecision { Start, Ready, InProgress, SessionUnavailable, Incomplete, Waiting, Cooldown, Paused }

// Called only on the desktop dispatcher. The existing heartbeat supplies the
// monotonic clock; this policy owns no timer, process or Windows input.
public sealed class WindowsShellRecoveryGate
{
    public const int GraceMilliseconds = 5000, CooldownMilliseconds = 10000, StableMilliseconds = 300000;
    public const int AutomaticAttemptLimit = 3;
    private long? _absentSince, _readySince, _lastAttempt;
    public WindowsShellPresence Presence { get; private set; }
    public bool Observed { get; private set; }
    public bool Starting { get; private set; }
    public int Attempts { get; private set; }
    public bool AutomaticPaused => Attempts >= AutomaticAttemptLimit;

    public bool Observe(WindowsShellPresence presence, long now)
    {
        bool changed = !Observed || presence != Presence;
        Observed = true; Presence = presence;
        if (presence.Ready)
        {
            _absentSince = null;
            if (changed) _readySince = now;
            else _readySince ??= now;
            if (Elapsed(now, _readySince, StableMilliseconds)) Attempts = 0;
        }
        else
        {
            _readySince = null;
            if (presence.Absent) _absentSince ??= now;
            else _absentSince = null; // Incomplete startup is not evidence to spawn another Explorer.
        }
        return changed;
    }

    public WindowsShellStartDecision Begin(long now, bool sessionAvailable, bool automatic)
    {
        if (!sessionAvailable) return WindowsShellStartDecision.SessionUnavailable;
        if (Starting) return WindowsShellStartDecision.InProgress;
        if (Presence.Ready) return WindowsShellStartDecision.Ready;
        if (!Observed || !Presence.Absent) return WindowsShellStartDecision.Incomplete;
        if (automatic && AutomaticPaused) return WindowsShellStartDecision.Paused;
        if (_lastAttempt is not null && !Elapsed(now, _lastAttempt, CooldownMilliseconds)) return WindowsShellStartDecision.Cooldown;
        if (automatic && !Elapsed(now, _absentSince, GraceMilliseconds)) return WindowsShellStartDecision.Waiting;
        _lastAttempt = now; Starting = true;
        if (Attempts < int.MaxValue) Attempts++;
        return WindowsShellStartDecision.Start;
    }

    public void Complete() => Starting = false;
    private static bool Elapsed(long now, long? since, int milliseconds) => since is { } at && now >= at && now - at >= milliseconds;
}
