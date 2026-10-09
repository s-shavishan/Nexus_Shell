namespace Nexus.Shell.Services;

public readonly record struct WindowsDesktopSurface(long Handle, int ProcessId, string ClassName, bool Visible);
public interface IWindowsDesktopSurfaces
{
    IReadOnlyList<WindowsDesktopSurface> Read();
    bool Matches(WindowsDesktopSurface surface);
    void SetVisible(WindowsDesktopSurface surface, bool visible);
}

// The host owns this lease, so closing/crashing the UI cannot strand the taskbar.
// Keep Explorer's desktop windows alive behind Nexus. Hiding Progman/WorkerW
// also changes the desktop infrastructure; only the taskbars need hiding.
public sealed class DesktopSurfaceLease(IWindowsDesktopSurfaces surfaces, Action<IReadOnlyList<WindowsDesktopSurface>>? beforeHide = null)
{
    private readonly Dictionary<long, WindowsDesktopSurface> _original = [];
    private bool _pending;
    public bool Active { get; private set; }
    public void TakeOver() { Active = true; Maintain(); }
    public void Maintain()
    {
        if (!Active) return;
        var currentSurfaces = surfaces.Read()
            .Where(s => s.ClassName is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd").ToList();
        foreach (var current in currentSurfaces)
        {
            if (!_original.TryGetValue(current.Handle, out var saved)
                || saved.ProcessId != current.ProcessId || saved.ClassName != current.ClassName)
            { _original[current.Handle] = current; _pending = true; }
        }
        // Persist the original visibility before making any new surface invisible.
        if (_pending) { beforeHide?.Invoke(_original.Values.ToList()); _pending = false; }
        foreach (var current in currentSurfaces)
            if (current.Visible) surfaces.SetVisible(current, false);
    }
    public void Restore()
    {
        Active = false;
        RestoreSaved(surfaces, _original.Values);
        _original.Clear(); _pending = false;
    }
    public static void RestoreSaved(IWindowsDesktopSurfaces surfaces, IEnumerable<WindowsDesktopSurface> savedSurfaces)
    {
        List<Exception> errors = [];
        foreach (var saved in savedSurfaces)
        {
            try { if (surfaces.Matches(saved)) surfaces.SetVisible(saved, saved.Visible); }
            catch (Exception ex) { errors.Add(ex); }
        }
        if (errors.Count > 0) throw new AggregateException("Some Windows desktop surfaces could not be restored.", errors);
    }
}

public sealed record DesktopRecoveryResult(bool WindowsDesktopRequested, bool SignInRestored, IReadOnlyList<Exception> Errors);
public static class DesktopSessionRecovery
{
    // Every desktop recovery step must run even when a policy write is denied.
    public static DesktopRecoveryResult Restore(Action? restoreSignIn, Action restoreSurfaces, Action resetWorkArea, Action startExplorer)
    {
        List<Exception> errors = [];
        bool Run(Action action) { try { action(); return true; } catch (Exception ex) { errors.Add(ex); return false; } }
        bool policy = restoreSignIn is null || Run(restoreSignIn);
        Run(restoreSurfaces); Run(resetWorkArea);
        bool desktop = Run(startExplorer);
        return new(desktop, policy, errors);
    }
}
