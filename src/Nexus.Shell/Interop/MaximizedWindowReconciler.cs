using System.Runtime.InteropServices;
using Nexus.Shell.Models;
using Nexus.Shell.Services;

namespace Nexus.Shell.Interop;

internal sealed class MaximizedWindowReconciler
{
    private readonly WindowRepairBudget _budget = new();
    [DllImport("dwmapi.dll", ExactSpelling = true)] private static extern int DwmGetWindowAttribute(IntPtr window, uint attribute, out ShellLayerInterop.Rect rect, uint size);
    internal void Reset() => _budget.Reset();
    internal void Prune(IEnumerable<RunningWindow> windows) => _budget.Prune(windows.Select(window => window.Handle.ToInt64()));
    internal void Refresh(RunningWindow window, ShellRect monitor, ShellRect work, double scale)
    {
        if (!NativeMethods.OwnsWindow(window) || !NativeMethods.Visible(window.Handle) || NativeMethods.IsMinimized(window.Handle)
            || !NativeMethods.IsZoomed(window.Handle)) { _budget.Forget(window.Handle.ToInt64()); return; }
        // Borderless fullscreen, our custom content windows, and another display
        // do not belong to this correction. Native captions/styles are retained.
        if (window.ProcessId == Environment.ProcessId || NativeMethods.IsCloaked(window.Handle)
            || (NativeMethods.WindowStyle(window.Handle) & 0x00C00000) != 0x00C00000
            || ShellLayerInterop.Monitor(window.Handle).Monitor.Bounds != monitor
            || !NativeMethods.GetWindowRect(window.Handle, out var outer)
            || DwmGetWindowAttribute(window.Handle, 9, out var visible, (uint)Marshal.SizeOf<ShellLayerInterop.Rect>()) != 0) return;
        var target = MaximizedWindowLayout.Repair(outer.Bounds, visible.Bounds, work, scale);
        if (target is not { } next || !_budget.Admit(window.Handle.ToInt64(), window.ProcessId, work, Environment.TickCount64)) return;
        if (!NativeMethods.OwnsWindow(window) || !NativeMethods.IsZoomed(window.Handle)) return;
        // ASYNCWINDOWPOS | NOZORDER | NOACTIVATE | NOOWNERZORDER. No minimize /
        // restore cycle, style patch, focus transfer or synchronous foreign call.
        bool accepted = ShellLayerInterop.SetWindowPos(window.Handle, IntPtr.Zero, next.X, next.Y, next.Width, next.Height, 0x4214);
        // Normal accepted repairs are frequent enough during native work-area
        // transitions that filesystem logging adds avoidable UI-thread stalls.
        // ETW is disabled by default and emits no user-facing window metadata.
        UiTrace.Surface(accepted ? "maximized-repair-queued" : "maximized-repair-refused",
            "WorkArea", window.Handle, next.Width, next.Height);
        if (!accepted)
            Log.Write($"Maximized layout request refused: hwnd={window.Handle.ToInt64()} pid={window.ProcessId} work={work.X},{work.Y},{work.Width},{work.Height}");
    }
}
