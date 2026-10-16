// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// A recovery heartbeat is a safety net, not a structural invalidation.
// If an enumeration is already running it covers the periodic check; native
// changes or explicit user commands still require a follow-up pass.
internal enum WindowScanAdmission { Start, QueueFollowUp, CoveredByActiveScan }

internal static class WindowScanAdmissionPolicy
{
    internal static WindowScanAdmission Decide(bool scanActive, bool recoveryHeartbeat)
    {
        if (!scanActive) return WindowScanAdmission.Start;
        return recoveryHeartbeat
            ? WindowScanAdmission.CoveredByActiveScan
            : WindowScanAdmission.QueueFollowUp;
    }
}
