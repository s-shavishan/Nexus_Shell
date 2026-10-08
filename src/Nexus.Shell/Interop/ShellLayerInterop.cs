using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Nexus.Shell.Services;

namespace Nexus.Shell.Interop;

// Standard Win32 window positioning and shell appbar registration. No Explorer
// injection, WorkerW creation message, keyboard hook, or shell replacement.
internal static class ShellLayerInterop
{
    internal static readonly IntPtr Topmost = new(-1), NotTopmost = new(-2), Bottom = new(1);
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; public ShellRect Bounds => new(Left, Top, Right - Left, Bottom - Top); }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct MonitorInfo { public uint Size; public Rect Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] internal struct WindowPos { public IntPtr Window, InsertAfter; public int X, Y, Width, Height; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] internal struct AppBarData { public uint Size; public IntPtr Window; public uint Callback, Edge; public Rect Rect; public IntPtr Parameter; }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate IntPtr SubclassProc(IntPtr window, uint message, UIntPtr wp, IntPtr lp, UIntPtr id, UIntPtr data);
    [DllImport("comctl32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowSubclass(IntPtr window, SubclassProc callback, UIntPtr id, UIntPtr data);
    [DllImport("comctl32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool RemoveWindowSubclass(IntPtr window, SubclassProc callback, UIntPtr id);
    [DllImport("comctl32.dll", ExactSpelling = true)] internal static extern IntPtr DefSubclassProc(IntPtr window, uint message, UIntPtr wp, IntPtr lp);
    [DllImport("shell32.dll")] internal static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern IntPtr GetTopWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string? title);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr window);
    internal static MonitorInfo Monitor(IntPtr window)
    {
        var result = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(window, 2), ref result)) throw new Win32Exception("Could not read the desktop monitor.");
        return result;
    }
    internal static double Scale(IntPtr window) => Math.Max(96, GetDpiForWindow(window)) / 96.0;
    internal static void ToolWindow(IntPtr window, bool noActivate = false)
    {
        long style = GetWindowLongPtr(window, -20).ToInt64();
        style = (style | 0x80L | (noActivate ? 0x08000000L : 0L)) & ~0x40000L;
        SetWindowLongPtr(window, -20, new IntPtr(style));
        SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020);
    }
    internal static IntPtr DesktopAnchor(IntPtr desktop)
    {
        // Keep the surface immediately above the existing Explorer desktop and
        // below application windows. Inspect existing windows; never reparent them.
        IntPtr previous = IntPtr.Zero, shell = GetShellWindow();
        for (var window = GetTopWindow(IntPtr.Zero); window != IntPtr.Zero; window = GetWindow(window, 2))
        {
            if (window == desktop) continue;
            var name = new StringBuilder(80); GetClassName(window, name, name.Capacity);
            bool desktopHost = name.ToString() is "Progman" or "WorkerW" && IsWindowVisible(window)
                && FindWindowEx(window, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero;
            if (window == shell || desktopHost) return previous == IntPtr.Zero ? NotTopmost : previous;
            if ((GetWindowLongPtr(window, -20).ToInt64() & 8) == 0) previous = window;
        }
        return Bottom;
    }
    internal static bool ExplorerDesktopPresent() => GetShellWindow() != IntPtr.Zero || FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null) != IntPtr.Zero;
    internal static void AnchorDesktop(IntPtr desktop, bool independent = false) => SetWindowPos(desktop, independent ? Bottom : DesktopAnchor(desktop), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
}

internal sealed class DesktopLayerHook : IDisposable
{
    private readonly IntPtr _handle;
    private readonly ShellLayerInterop.SubclassProc _callback;
    private readonly Action _reposition;
    private readonly bool _independent;
    private bool _disposed;
    internal DesktopLayerHook(IntPtr handle, Action reposition, bool independent = false)
    {
        _handle = handle; _reposition = reposition; _independent = independent; _callback = Message;
        if (!ShellLayerInterop.SetWindowSubclass(handle, _callback, new UIntPtr(0x4E01), UIntPtr.Zero)) throw new Win32Exception("Could not anchor the desktop layer.");
    }
    private IntPtr Message(IntPtr window, uint message, UIntPtr wp, IntPtr lp, UIntPtr id, UIntPtr data)
    {
        try
        {
            if (!_disposed && message == 0x0046 && lp != IntPtr.Zero)
            {
                var position = Marshal.PtrToStructure<ShellLayerInterop.WindowPos>(lp);
                if ((position.Flags & 0x0004) == 0)
                { position.InsertAfter = _independent ? ShellLayerInterop.Bottom : ShellLayerInterop.DesktopAnchor(window); Marshal.StructureToPtr(position, lp, false); }
            }
            if (!_disposed && message is 0x007E or 0x02E0) _reposition(); // display / DPI
        }
        catch (Exception ex) { Log.Write("Desktop layer message failed", ex); }
        return ShellLayerInterop.DefSubclassProc(window, message, wp, lp);
    }
    public void Dispose() { if (_disposed) return; _disposed = true; ShellLayerInterop.RemoveWindowSubclass(_handle, _callback, new UIntPtr(0x4E01)); }
}
