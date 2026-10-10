namespace Nexus.Shell.Services;

public static class WindowSwitcherPolicy
{
    private static int _reportedUnavailable;
    // Shell-less Windows 10 sessions can return E_NOTIMPL. Each surface also
    // applies the native TOOLWINDOW style after this optional SDK request.
    public static void Request(Action hide, Action<Exception> report)
    {
        try { hide(); }
        catch (Exception error) when (error.HResult == unchecked((int)0x80004001))
        { if (Interlocked.Exchange(ref _reportedUnavailable, 1) == 0) report(error); }
    }
}
