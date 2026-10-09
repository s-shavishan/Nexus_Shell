using System.ComponentModel;
using System.Runtime.InteropServices;
using Nexus.Shell.Services;

namespace Nexus.Shell.Interop;

internal static class DesktopWorkArea
{
    private delegate bool Visitor(IntPtr window, IntPtr state);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetArea(uint action, uint parameter, ref ShellLayerInterop.Rect area, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumWindows(Visitor visitor, IntPtr state);
    [DllImport("user32.dll", EntryPoint = "SendNotifyMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Notify(IntPtr window, uint message, UIntPtr wp, IntPtr lp);

    internal static void Apply(ShellRect area)
    {
        var rect = new ShellLayerInterop.Rect { Left = area.X, Top = area.Y, Right = area.Right, Bottom = area.Bottom };
        // A synchronous broadcast can stall dragging and make Explorer's hidden
        // appbar manager repeatedly undo the session's reservation.
        if (!SetArea(0x002F, 0, ref rect, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not set the Nexus desktop work area.");
        Log.Write($"Desktop work area reserved: {area.X},{area.Y} {area.Width}×{area.Height}");
        GetWindowThreadProcessId(GetShellWindow(), out uint explorer);
        EnumWindows((window, _) =>
        {
            try
            {
                GetWindowThreadProcessId(window, out uint process);
                if (process != 0 && process != explorer && process != Environment.ProcessId)
                    Notify(window, 0x001A, new UIntPtr(0x002F), IntPtr.Zero);
            }
            catch (Exception ex) { Log.Write("Could not notify an app of the desktop work area", ex); }
            return true;
        }, IntPtr.Zero);
    }
}
