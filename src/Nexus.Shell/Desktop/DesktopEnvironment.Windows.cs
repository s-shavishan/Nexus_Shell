using Nexus.Shell.Interop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;

namespace Nexus.Shell.Desktop;

internal sealed partial class DesktopEnvironment
{
    private readonly MaximizedWindowReconciler _maximizedLayout = new();
    private readonly WindowPlacementCommands _placements = new();
    internal bool CanUndoWindowPlacement => _placements.CanUndo;
    private readonly ForegroundLookupCache<RunningWindow> _foregroundLookup = new();
    internal RunningWindow? ActiveWindow { get; private set; }
    internal bool ActiveWindowMaximized { get; private set; }
    internal event Action? ActiveWindowChanged;
    internal void ObserveForeground(IntPtr foreground)
    {
        if (IsStopping || NativeMethods.IsOwnToolWindow(foreground) || IsControlCenterWindow(foreground)) return;
        // Keep a stable native identity while the HWND and enumerated task
        // snapshot are unchanged. Internal Nexus utility windows are resolved
        // afresh because they can change without the external window snapshot.
        var window = _foregroundLookup.Resolve(
            foreground, _windows,
            () => DockWindows().FirstOrDefault(item => item.Handle == foreground && NativeMethods.OwnsWindow(item)),
            previous => previous.ProcessId != Environment.ProcessId && NativeMethods.OwnsWindow(previous),
            safeEmpty: foreground == IntPtr.Zero || foreground == Desktop.Handle);
        bool maximized = window is not null && NativeMethods.IsZoomed(window.Handle);
        if (window == ActiveWindow && maximized == ActiveWindowMaximized) return;
        ActiveWindow = window; ActiveWindowMaximized = maximized; ActiveWindowChanged?.Invoke();
    }
    internal void ReconcileMaximizedWindows(ShellRect monitor, ShellRect work, double scale)
    {
        if (!IsManagedDesktop || IsStopping) return;
        _maximizedLayout.Prune(_windows);
        foreach (var window in _windows) _maximizedLayout.Refresh(window, monitor, work, scale);
    }
    internal void ReconcileForeground(ShellRect monitor, ShellRect work, double scale)
    {
        var window = ActiveWindow;
        if (IsManagedDesktop && !IsStopping && window is not null) _maximizedLayout.Refresh(window, monitor, work, scale);
    }
    internal void RepairDesktopLayout()
    {
        if (IsStopping) return;
        _maximizedLayout.Reset(); Taskbar.Position();
        var monitor = ShellLayerInterop.Monitor(Taskbar.Handle);
        ReconcileMaximizedWindows(monitor.Monitor.Bounds, monitor.Work.Bounds, ShellLayerInterop.Scale(Taskbar.Handle));
        UpdateTaskbar();
    }
    internal void PlaceWindow(RunningWindow window, string placement)
    {
        if (IsStopping) return;
        try { if (!_placements.Place(window, placement)) Report("This window could not be placed. It may not support resizing or may have closed."); }
        catch (Exception error) { Report("Window placement could not finish", error); }
        UpdateTaskbar();
    }
    internal void UndoWindowPlacement()
    { if (!IsStopping && !_placements.Undo()) Report("The previous window placement is no longer available."); UpdateTaskbar(); }
}
