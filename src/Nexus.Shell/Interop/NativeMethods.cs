using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Nexus.Shell.Models;

namespace Nexus.Shell.Interop;

internal static class NativeMethods
{
    [DllImport("dwmapi.dll", ExactSpelling = true)] private static extern int DwmGetWindowAttribute(IntPtr window, uint attribute, out uint value, uint size);
    private delegate bool EnumWindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximum);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
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
        if (window == IntPtr.Zero) return false;
        if (IsIconic(window)) ShowWindowAsync(window, 9);
        return SetForegroundWindow(window);
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
            var title = new StringBuilder(512);
            GetWindowText(window, title, title.Capacity);
            if (title.Length == 0 || title.ToString() == "Program Manager") return true;
            GetWindowThreadProcessId(window, out uint id);
            if (id == (uint)Environment.ProcessId) return true;
            try
            {
                using var process = Process.GetProcessById((int)id);
                if (process.SessionId == session) result.Add(new(window, title.ToString(), process.ProcessName, (int)id));
            }
            catch { /* A window can disappear while it is being enumerated. */ }
            return result.Count < 80;
        }, IntPtr.Zero);
        return result;
    }
}
