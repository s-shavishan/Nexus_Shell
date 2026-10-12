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

    internal bool ShouldSample(IntPtr foreground, bool maximized, long now)
    {
        if (foreground == IntPtr.Zero)
        {
            _initialized = false;
            return false;
        }
        if (_initialized && foreground == _lastWindow && maximized == _lastMaximized
            && now >= _lastSample && now - _lastSample < FallbackMilliseconds)
            return false;
        _lastWindow = foreground;
        _lastMaximized = maximized;
        _lastSample = now;
        _initialized = true;
        return true;
    }
}
