// නමෝ බුද්ධාය 🤍
using System.Runtime.InteropServices;
using Nexus.Shell.Models;
using Nexus.Shell.Services;

namespace Nexus.Shell.Interop;

internal sealed class WindowPlacementCommands
{
    // Windows SDK WINDOWPLACEMENT is 44 bytes. Its additional rcDevice member
    // exists only under the SDK's _MAC conditional, not the Windows ABI.
    [StructLayout(LayoutKind.Sequential)] private struct Placement
    { public uint Length, Flags, Show; public ShellLayerInterop.Point Minimum, Maximum; public ShellLayerInterop.Rect Normal; }
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowPlacement(IntPtr window, ref Placement placement);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPlacement(IntPtr window, ref Placement placement);
    private (RunningWindow Window, Placement Before)? _undo;
    internal bool CanUndo => _undo is { } undo && NativeMethods.OwnsWindow(undo.Window);
    internal bool Place(RunningWindow window, string choice)
    {
        if (!NativeMethods.OwnsWindow(window) || (NativeMethods.WindowStyle(window.Handle) & 0x00040000) == 0) return false;
        var monitor = ShellLayerInterop.Monitor(window.Handle); var scale = ShellLayerInterop.Scale(window.Handle);
        var bounds = WindowPlacementPlan.Bounds(monitor.Work.Bounds, choice, scale);
        var before = new Placement { Length = (uint)Marshal.SizeOf<Placement>() };
        if (!GetWindowPlacement(window.Handle, ref before) || !NativeMethods.OwnsWindow(window)) return false;
        var restored = before; restored.Show = 4; restored.Flags = 4; // SHOWNOACTIVATE, ASYNCWINDOWPLACEMENT
        if (!SetWindowPlacement(window.Handle, ref restored)) return false;
        if (!NativeMethods.OwnsWindow(window) || !ShellLayerInterop.SetWindowPos(window.Handle, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x4214))
        { var rollback = before; rollback.Flags |= 4; if (NativeMethods.OwnsWindow(window)) SetWindowPlacement(window.Handle, ref rollback); return false; }
        _undo = (window, before); return true;
    }
    internal bool Undo()
    {
        if (_undo is not { } undo) return false;
        if (!NativeMethods.OwnsWindow(undo.Window)) { _undo = null; return false; }
        var placement = undo.Before; placement.Flags |= 4;
        if (!SetWindowPlacement(undo.Window.Handle, ref placement)) return false;
        _undo = null; return true;
    }
}
