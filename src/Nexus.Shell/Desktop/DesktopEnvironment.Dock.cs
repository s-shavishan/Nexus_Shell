using Nexus.Shell.Interop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;

namespace Nexus.Shell.Desktop;

internal sealed partial class DesktopEnvironment
{
    private DockPreviewWindow? _dockPreview;
    internal bool CanShowDockPreview => !IsStopping && Session.State.DockPreviews && !_sessionDialog
        && _menu?.IsOpen != true && _quickSettings?.IsOpen != true && _notifications?.IsOpen != true && _switcher?.IsOpen != true;
    internal void ShowDockPreview(RunningWindow window, ShellRect anchor, bool keyboard = false)
    {
        if (!CanShowDockPreview) return;
        try { _dockPreview ??= new(this); _dockPreview.Show(window, anchor, keyboard); Taskbar.RefreshVisibility(); }
        catch (Exception ex) { _dockPreview?.Hide(); Log.Write("Dock preview unavailable; window actions remain available from the dock menu", ex); }
    }
    internal void HideDockPreview()
    { Taskbar?.View.CancelPreviewRequest(); _dockPreview?.Hide(); }
    internal void DockPreviewClosed(DockPreviewWindow window) { if (ReferenceEquals(_dockPreview, window)) _dockPreview = null; }
    internal void UpdateDockPreviewPointer(bool fullscreen) => _dockPreview?.UpdatePointer(fullscreen);
    internal void RefreshDockPreviewWindows(IReadOnlyList<RunningWindow> windows) => _dockPreview?.RefreshWindows(windows);
    internal void MaximizeDockWindow(RunningWindow window)
    {
        HideDockPreview();
        if (IsStopping || !NativeMethods.OwnsWindow(window)) { UpdateTaskbar(); return; }
        // A Nexus window may be tucked instead of iconic. Reveal it through its
        // owner before the native maximize command so its hidden state agrees.
        if (window.ProcessId == Environment.ProcessId && !NativeMethods.Visible(window.Handle)) RestoreDockWindow(window);
        if (!NativeMethods.MaximizeOrRestore(window)) Report("Windows could not resize this window.");
        UpdateTaskbar();
    }
    internal void CloseDockWindow(RunningWindow window)
    {
        HideDockPreview();
        if (IsStopping || !NativeMethods.OwnsWindow(window)) { UpdateTaskbar(); return; }
        // Reveal/activate the target so an app's unsaved-work dialog can be seen,
        // including when a Nexus window is tucked rather than iconic.
        RestoreDockWindow(window);
        if (!NativeMethods.RequestClose(window)) Report("Windows could not request that this window close.");
        UpdateTaskbar();
    }
}
