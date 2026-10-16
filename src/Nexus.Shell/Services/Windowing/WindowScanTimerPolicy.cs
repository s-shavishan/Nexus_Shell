// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// A failed native scan owns the next retry even when a 50 ms WinEvent timer
// was already scheduled. Successful reconciliation never interrupts a
// coalesced event timer that is still waiting to run.
internal enum WindowScanTimerAction { None, Start, Restart }

internal static class WindowScanTimerPolicy
{
    internal static WindowScanTimerAction Decide(
        bool stopping, bool applied, bool pending, bool timerRunning)
    {
        if (stopping) return WindowScanTimerAction.None;
        if (!applied) return timerRunning ? WindowScanTimerAction.Restart : WindowScanTimerAction.Start;
        return pending && !timerRunning ? WindowScanTimerAction.Start : WindowScanTimerAction.None;
    }
}
