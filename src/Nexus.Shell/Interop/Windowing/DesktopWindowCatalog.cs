// නමෝ බුද්ධාය 🤍
using System.Runtime.InteropServices;
using System.Text;
using Nexus.Shell.Models;
using Nexus.Shell.Services;

namespace Nexus.Shell.Interop;

internal sealed record DesktopMonitor(string Id, string Name, IntPtr Handle, LayoutRect Bounds, LayoutRect Work);
internal sealed record DesktopWindowEntry(RunningWindow Window, string ProcessPath, string MonitorId, bool Minimized, bool Maximized, bool CanArrange)
{
    internal string Key => Window.Handle.ToInt64() + ":" + Window.ProcessId;
    internal LayoutCandidate Candidate => new(Key, ProcessPath, Window.Title);
}
internal static class DesktopWindowCatalog
{
    internal static int MonitorInfoSize => Marshal.SizeOf<MonitorInfo>();
    internal static int PlacementSize => Marshal.SizeOf<Placement>();
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MonitorInfo
    { internal uint Size; internal ShellLayerInterop.Rect Bounds, Work; internal uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string Device; }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint id);
    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref uint length);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
    [StructLayout(LayoutKind.Sequential)] private struct Placement
    { internal uint Length, Flags, Show; internal ShellLayerInterop.Point Minimum, Maximum; internal ShellLayerInterop.Rect Normal; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowPlacement(IntPtr window, ref Placement placement);

    internal static IReadOnlyList<DesktopMonitor> Monitors()
    {
        var monitors = new List<DesktopMonitor>();
        MonitorCallback callback = (handle, _, _, _) =>
        {
            var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>(), Device = "" };
            if (GetMonitorInfo(handle, ref info) && info.Work.Right > info.Work.Left && info.Work.Bottom > info.Work.Top)
            {
                string label = "Display " + (monitors.Count + 1) + ((info.Flags & 1) != 0 ? " · primary" : "");
                monitors.Add(new(info.Device, label, handle, Convert(info.Bounds.Bounds), Convert(info.Work.Bounds)));
            }
            return monitors.Count < 16;
        };
        _ = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback); return monitors;
    }
    internal static LayoutRect Convert(ShellRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
    internal static IReadOnlyList<DesktopWindowEntry> Read(IReadOnlyList<RunningWindow> windows, IReadOnlyList<DesktopMonitor> monitors)
    {
        var result = new List<DesktopWindowEntry>(); var paths = new Dictionary<int, string>();
        foreach (var window in windows.Take(200))
        {
            if (!NativeMethods.OwnsWindow(window) || NativeMethods.IsCloaked(window.Handle)) continue;
            if (!paths.TryGetValue(window.ProcessId, out string? path)) paths[window.ProcessId] = path = ProcessPath(window.ProcessId);
            var monitor = monitors.FirstOrDefault(m => m.Handle == MonitorFromWindow(window.Handle, 2));
            bool resize = (NativeMethods.WindowStyle(window.Handle) & 0x00040000) != 0;
            if (NativeMethods.OwnsWindow(window)) result.Add(new(window, path, monitor?.Id ?? "", NativeMethods.IsMinimized(window.Handle), NativeMethods.IsZoomed(window.Handle), resize));
        }
        return result;
    }
    private static string ProcessPath(int pid)
    {
        IntPtr handle = OpenProcess(0x1000, false, (uint)pid); if (handle == IntPtr.Zero) return "";
        try { uint size = 4096; var path = new StringBuilder((int)size); return QueryFullProcessImageName(handle, 0, path, ref size) ? path.ToString() : ""; }
        finally { CloseHandle(handle); }
    }
    // Moving an already-open HWND must never depend on whether Windows
    // permits querying its executable path. Process path is required ONLY
    // for persistent saved-layout matching, not live window arrangement.
    internal static LayoutRect? CurrentBounds(DesktopWindowEntry entry, IReadOnlyList<DesktopMonitor> monitors)
    {
        if (!entry.CanArrange || !NativeMethods.OwnsWindow(entry.Window)) return null;
        var monitor = monitors.FirstOrDefault(m => m.Id == entry.MonitorId);
        if (monitor is null) return null;
        LayoutRect bounds;
        if (NativeMethods.IsMinimized(entry.Window.Handle) || NativeMethods.IsZoomed(entry.Window.Handle))
        {
            var placement = new Placement { Length = (uint)Marshal.SizeOf<Placement>() };
            if (!GetWindowPlacement(entry.Window.Handle, ref placement)) return null;
            // WINDOWPLACEMENT normal rectangles use workspace coordinates.
            bool tool = (GetWindowLongPtr(entry.Window.Handle, -20).ToInt64() & 0x80) != 0;
            bounds = new(placement.Normal.Left + (tool ? 0 : monitor.Work.X - monitor.Bounds.X),
                placement.Normal.Top + (tool ? 0 : monitor.Work.Y - monitor.Bounds.Y),
                placement.Normal.Right - placement.Normal.Left, placement.Normal.Bottom - placement.Normal.Top);
        }
        else
        {
            if (!NativeMethods.GetWindowRect(entry.Window.Handle, out var rectangle)) return null;
            bounds = Convert(rectangle.Bounds);
        }
        return bounds.Width > 0 && bounds.Height > 0 && NativeMethods.OwnsWindow(entry.Window) ? bounds : null;
    }

    internal static SavedWindowPosition? Capture(DesktopWindowEntry entry, IReadOnlyList<DesktopMonitor> monitors)
    {
        // Saved layouts intentionally require a stable executable identity.
        // Live movement does not; see CurrentBounds above.
        if (string.IsNullOrWhiteSpace(entry.ProcessPath)) return null;
        var bounds = CurrentBounds(entry, monitors);
        var monitor = monitors.FirstOrDefault(m => m.Id == entry.MonitorId);
        return bounds is { } actual && monitor is not null
            ? SavedWindowLayouts.Capture(entry.ProcessPath, entry.Window.Title,
                monitor.Id, actual, monitor.Work, NativeMethods.IsZoomed(entry.Window.Handle))
            : null;
    }
}
