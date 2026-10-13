namespace Nexus.Shell.Services;

// Native geometry repair is already event-driven. During dock edge polling,
// avoid issuing the same DWM/Win32 repair queries every 80 ms for one window.
// A bounded fallback still catches events missed by the shell observer.
internal sealed class ForegroundRepairCadence
{
    internal const int FallbackMilliseconds = 1000;
    private IntPtr _lastWindow;
    private bool _lastMaximized, _initialized;
    private long _lastSample;

    // Completion of an interactive move/resize is an actual geometry change,
    // not a polling tick. Admit its reconciliation despite the normal
    // one-second fallback cadence, then restart the fallback window.
    internal bool ShouldSample(IntPtr foreground, bool maximized, long now, bool force = false)
    {
        if (foreground == IntPtr.Zero)
        {
            _initialized = false;
            return false;
        }
        if (!force && _initialized && foreground == _lastWindow && maximized == _lastMaximized
            && now >= _lastSample && now - _lastSample < FallbackMilliseconds)
            return false;
        _lastWindow = foreground;
        _lastMaximized = maximized;
        _lastSample = now;
        _initialized = true;
        return true;
    }
}
