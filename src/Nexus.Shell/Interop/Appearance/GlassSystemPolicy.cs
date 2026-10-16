// නමෝ බුද්ධාය 🤍
using System.Runtime.InteropServices;

namespace Nexus.Shell.Interop;

// Read-only, sampled by existing appearance refreshes. No timer, capture,
// hardware command, privilege change or persisted system-policy state.
internal static class GlassSystemPolicy
{
    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus
    {
        public byte AcLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public uint BatteryLifeTime, BatteryFullLifeTime;
    }
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out PowerStatus status);

    internal static (bool BackdropSupported, bool RemoteSession, bool EnergySaver) Read()
    {
        bool supported = false, remote = false, saver = false;
        if (!OperatingSystem.IsWindows()) return (false, false, false);
        try { supported = Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported(); } catch { }
        try { remote = GetSystemMetrics(0x1000) != 0; } catch { } // SM_REMOTESESSION
        try { saver = GetSystemPowerStatus(out var status) && status.SystemStatusFlag == 1; } catch { }
        return (supported, remote, saver);
    }
}
