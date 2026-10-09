using Nexus.Shell.Services;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

[SupportedOSPlatform("windows")]
internal sealed class WindowsDesktopSurfaces : IWindowsDesktopSurfaces, IDisposable
{
    private delegate bool WindowVisitor(IntPtr window, IntPtr state);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumWindows(WindowVisitor visitor, IntPtr state);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int size);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindowAsync(IntPtr window, int state);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? name, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? name, string? title);
    private readonly int _session = Process.GetCurrentProcess().SessionId;
    private readonly Dictionary<int, Process> _explorers = [];
    private static string ClassOf(IntPtr window) { var text = new StringBuilder(128); GetClassName(window, text, text.Capacity); return text.ToString(); }
    private bool Explorer(int id)
    {
        Process? candidate = null;
        try
        {
            if (_explorers.TryGetValue(id, out var known))
            {
                if (!known.HasExited) return true;
                known.Dispose(); _explorers.Remove(id);
            }
            candidate = Process.GetProcessById(id);
            if (candidate.SessionId != _session || !candidate.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(candidate.MainModule?.FileName, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), StringComparison.OrdinalIgnoreCase)) return false;
            // Validate the image once. Retaining the handle also detects PID reuse.
            _ = candidate.Handle;
            if (candidate.HasExited) return false;
            _explorers[id] = candidate; candidate = null; return true;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        finally { candidate?.Dispose(); }
    }
    public IReadOnlyList<WindowsDesktopSurface> Read()
    {
        List<WindowsDesktopSurface> result = []; Dictionary<int, bool> owners = []; int visited = 0;
        Exception? denied = null;
        bool enumerated = EnumWindows((window, _) =>
        {
            try
            {
            if (++visited > 5000) throw new InvalidOperationException("Windows desktop enumeration exceeded its session limit.");
            string name = ClassOf(window);
            if (name is not ("Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW")) return true;
            if (GetWindowThreadProcessId(window, out uint raw) == 0 || raw > int.MaxValue) return true;
            int id = (int)raw;
            bool owned;
            if (!owners.TryGetValue(id, out owned)) owners[id] = owned = Explorer(id);
            if (owned) result.Add(new(window.ToInt64(), id, name, IsWindowVisible(window)));
            return true;
            }
            catch (Exception ex) { denied = ex; return false; }
        }, IntPtr.Zero);
        if (denied is not null) throw new InvalidOperationException("The Windows desktop could not be inspected in this session.", denied);
        if (!enumerated) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows desktop enumeration failed.");
        return result;
    }
    public bool Matches(WindowsDesktopSurface surface)
    {
        var window = new IntPtr(surface.Handle);
        return GetWindowThreadProcessId(window, out uint id) != 0 && id == surface.ProcessId
            && ClassOf(window) == surface.ClassName && Explorer(surface.ProcessId);
    }
    public void SetVisible(WindowsDesktopSurface surface, bool visible)
    {
        if (!Matches(surface)) return;
        if (!ShowWindowAsync(new IntPtr(surface.Handle), visible ? 8 : 0))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows denied the desktop surface visibility change.");
    }
    internal void RestoreDefaultSurfaces()
    {
        List<Exception> errors = [];
        foreach (var surface in Read())
        {
            // A hidden wallpaper WorkerW is not necessarily the desktop icon holder.
            if (surface.ClassName == "WorkerW" && FindWindowEx(new IntPtr(surface.Handle), IntPtr.Zero, "SHELLDLL_DefView", null) == IntPtr.Zero) continue;
            try { SetVisible(surface, true); } catch (Exception ex) { errors.Add(ex); }
        }
        if (errors.Count > 0) throw new AggregateException("Some Windows desktop surfaces could not be shown.", errors);
    }
    internal static bool DesktopExists => GetShellWindow() != IntPtr.Zero;
    internal static bool NexusExists => FindWindow(null, "White Dreams Nexus Desktop") != IntPtr.Zero;
    public void Dispose() { foreach (var process in _explorers.Values) process.Dispose(); _explorers.Clear(); }
}
