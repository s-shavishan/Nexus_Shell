using Nexus.Shell.Models;
using System.Runtime.InteropServices;

namespace Nexus.Shell.Interop;

internal sealed class NexusDesktopToggle
{
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Placement { public uint Length, Flags, Show; public Point Minimum, Maximum; public Rect Normal; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint id);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowPlacement(IntPtr window, ref Placement placement);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPlacement(IntPtr window, ref Placement placement);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindowAsync(IntPtr window, int command);
    private readonly List<(RunningWindow Window, Placement Before)> _hidden = [];
    private static bool Matches(RunningWindow window) => window.Handle != IntPtr.Zero && window.ProcessId > 0 && GetWindowThreadProcessId(window.Handle, out uint id) != 0 && id == window.ProcessId;
    internal void Toggle(IEnumerable<RunningWindow> windows)
    {
        if (_hidden.Count > 0)
        {
            foreach (var entry in _hidden)
            {
                if (!Matches(entry.Window) || !IsIconic(entry.Window.Handle)) continue;
                var before = entry.Before; SetWindowPlacement(entry.Window.Handle, ref before);
            }
            _hidden.Clear(); return;
        }
        foreach (var window in windows.DistinctBy(w => w.Handle).Take(100))
        {
            if (!Matches(window) || !IsWindowVisible(window.Handle) || IsIconic(window.Handle)) continue;
            var before = new Placement { Length = (uint)Marshal.SizeOf<Placement>() };
            if (GetWindowPlacement(window.Handle, ref before)) { _hidden.Add((window, before)); ShowWindowAsync(window.Handle, 6); }
        }
    }
}
