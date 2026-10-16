// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// The native fallback scan must recover from transient EnumWindows or WinUI
// application failures without busy-looping. The heartbeat remains a separate
// recovery mechanism. A successful scan immediately restores normal cadence.
internal sealed class WindowScanRetryBackoff
{
    internal const int NormalMilliseconds = 50;
    internal const int MaxDelayMilliseconds = 5000;
    private int _consecutiveFailures;

    internal int ConsecutiveFailures => _consecutiveFailures;

    internal int Complete(bool applied)
    {
        if (applied)
        {
            _consecutiveFailures = 0;
            return NormalMilliseconds;
        }

        _consecutiveFailures = Math.Min(_consecutiveFailures + 1, 4);
        return _consecutiveFailures switch
        {
            1 => 500,
            2 => 1000,
            3 => 2000,
            _ => MaxDelayMilliseconds
        };
    }
}
