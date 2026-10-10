using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Nexus.Shell.Models;

namespace Nexus.Shell.Interop;

internal static class NativeMethods
{
    internal const uint NexusMinimizeMessage = 0x805A, NexusRestoreMessage = 0x805B;
    [DllImport("dwmapi.dll", ExactSpelling = true)] private static extern int DwmGetWindowAttribute(IntPtr window, uint attribute, out uint value, uint size);
    private delegate bool EnumWindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximum);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern IntPtr GetLastActivePopup(IntPtr window);
    [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out ShellLayerInterop.Rect rect);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out ShellLayerInterop.Point point);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string? className, string? name);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInputInfo input);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MessageBox(IntPtr owner, string text, string caption, uint type);
    [StructLayout(LayoutKind.Sequential)] private struct LastInputInfo { public uint Size; public uint Tick; }
    [StructLayout(LayoutKind.Sequential)] private struct HighContrastInfo
    {
        public uint Size;
        public uint Flags;
        public IntPtr DefaultScheme;
    }
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadHighContrast(uint action, uint size, ref HighContrastInfo value, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadBooleanPreference(uint action, uint parameter, out int value, uint flags);

    internal static bool TryGetHighContrast(out bool enabled)
    {
        var value = new HighContrastInfo { Size = (uint)Marshal.SizeOf<HighContrastInfo>() };
        bool success = ReadHighContrast(0x0042, value.Size, ref value, 0); // SPI_GETHIGHCONTRAST
        enabled = success && (value.Flags & 0x00000001) != 0; // HCF_HIGHCONTRASTON
        return success;
    }
    internal static bool TryGetAnimationsEnabled(out bool enabled)
    {
        // Win32 BOOL is a four-byte integer, including on x64.
        bool success = ReadBooleanPreference(0x1042, 0, out int value, 0); // SPI_GETCLIENTAREAANIMATION
        enabled = success && value != 0;
        return success;
    }

    internal static void ShowStartupError(string message) =>
        MessageBox(IntPtr.Zero, message, "Nexus startup error", 0x00000010); // MB_OK | MB_ICONERROR

    internal static bool Activate(IntPtr window)
    {
        if (window == IntPtr.Zero || !IsWindow(window)) return false;
        // Async commands avoid blocking Nexus on another application's UI thread.
        bool restoring = IsIconic(window) && ShowWindowAsync(window, 9); // SW_RESTORE retains maximize placement
        var popup = GetLastActivePopup(window);
        var target = popup != IntPtr.Zero && IsWindowVisible(popup) ? popup : window;
        return SetForegroundWindow(target) || restoring;
    }
    internal static bool Activate(RunningWindow window)
    {
        if (!OwnsWindow(window)) return false;
        if (IsCurrentNexus(window)) PostMessage(window.Handle, NexusRestoreMessage, IntPtr.Zero, IntPtr.Zero);
        return Activate(window.Handle);
    }
    internal static bool OwnsWindow(RunningWindow window) => window.Handle != IntPtr.Zero && window.ProcessId > 0
        && IsWindow(window.Handle) && GetWindowThreadProcessId(window.Handle, out uint id) != 0 && id == window.ProcessId;
    internal static bool IsMinimized(IntPtr window) => IsIconic(window);
    internal static int WindowProcessId(IntPtr window) { GetWindowThreadProcessId(window, out uint id); return (int)id; }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool LockWorkStation();
    internal static bool LockScreen() => LockWorkStation();
    internal static IntPtr ForegroundTaskWindow()
    {
        var foreground = GetForegroundWindow();
        var owner = GetAncestor(foreground, 3); // GA_ROOTOWNER: owned modal dialogs share their app's dock entry
        return owner == IntPtr.Zero ? foreground : owner;
    }
    internal static bool Visible(IntPtr window) => IsWindowVisible(window);
    internal static bool IsCloaked(IntPtr window) => DwmGetWindowAttribute(window, 14, out uint cloaked, 4) == 0 && cloaked != 0;
    internal static bool Minimize(RunningWindow window) => OwnsWindow(window)
        && (IsCurrentNexus(window) ? PostMessage(window.Handle, NexusMinimizeMessage, IntPtr.Zero, IntPtr.Zero) : ShowWindowAsync(window.Handle, 6));
    internal static bool MaximizeOrRestore(RunningWindow window)
    {
        if (!OwnsWindow(window)) return false;
        if (IsCurrentNexus(window)) PostMessage(window.Handle, NexusRestoreMessage, IntPtr.Zero, IntPtr.Zero);
        bool queued = ShowWindowAsync(window.Handle, !IsIconic(window.Handle) && IsZoomed(window.Handle) ? 9 : 3);
        // Do not queue SW_RESTORE after SW_MAXIMIZE for an iconic window.
        // That would undo the requested maximize on a slow application's thread.
        if (queued) SetForegroundWindow(window.Handle);
        return queued;
    }
    private static bool IsCurrentNexus(RunningWindow window)
    {
        if (!window.ProcessName.Equals("Nexus.Shell", StringComparison.OrdinalIgnoreCase)) return false;
        try { using var process = Process.GetProcessById(window.ProcessId); return string.Equals(process.MainModule?.FileName, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }
    internal static bool RequestClose(RunningWindow window) => OwnsWindow(window)
        && PostMessage(window.Handle, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE; the app owns save prompts
    [DllImport("user32.dll", EntryPoint = "PostMessageW", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    internal static bool ToggleDockWindow(RunningWindow window)
    {
        if (!OwnsWindow(window)) return false;
        return ForegroundTaskWindow() == window.Handle && !IsIconic(window.Handle)
            ? Minimize(window) : Activate(window);
    }
    internal static void BeginDrag(IntPtr window)
    {
        ReleaseCapture();
        SendMessage(window, 0x00A1, new IntPtr(2), IntPtr.Zero); // WM_NCLBUTTONDOWN / HTCAPTION
    }

    internal static string? ForegroundProcessName()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint id);
        if (id == 0 || id == (uint)Environment.ProcessId) return null;
        try { using var process = Process.GetProcessById((int)id); return process.ProcessName; }
        catch { return null; }
    }
    internal static bool IsOwnToolWindow(IntPtr window)
    {
        if (window == IntPtr.Zero) return false;
        GetWindowThreadProcessId(window, out uint id);
        return id == (uint)Environment.ProcessId && (GetWindowLongPtr(window, -20).ToInt64() & 0x80) != 0;
    }

    internal static double IdleSeconds()
    {
        var input = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref input)) return double.PositiveInfinity;
        return unchecked((uint)Environment.TickCount - input.Tick) / 1000.0;
    }

    internal static IReadOnlyList<RunningWindow> RunningWindows(IntPtr ownHandle)
    {
        var result = new List<RunningWindow>();
        using var ownProcess = Process.GetCurrentProcess();
        int session = ownProcess.SessionId;
        EnumWindows((window, _) =>
        {
            if (window == ownHandle || !IsWindowVisible(window)) return true;
            if (DwmGetWindowAttribute(window, 14, out uint cloaked, 4) == 0 && cloaked != 0) return true; // DWMWA_CLOAKED
            long style = GetWindowLongPtr(window, -20).ToInt64();
            if ((style & 0x80) != 0) return true; // WS_EX_TOOLWINDOW
            if ((style & 0x40000) == 0 && GetWindow(window, 4) != IntPtr.Zero) return true; // owned dialog unless WS_EX_APPWINDOW
            var title = new StringBuilder(512);
            GetWindowText(window, title, title.Capacity);
            if (title.Length == 0 || ShellLayerInterop.IsExplorerDesktopWindow(window)) return true;
            GetWindowThreadProcessId(window, out uint id);
            if (id == (uint)Environment.ProcessId) return true;
            try
            {
                using var process = Process.GetProcessById((int)id);
                if (process.SessionId == session) result.Add(new(window, title.ToString(), process.ProcessName, (int)id));
            }
            catch { /* A window can disappear while it is being enumerated. */ }
            return true; // Overflow scrolls; never silently lose the 21st/81st app.
        }, IntPtr.Zero);
        return result;
    }
}
