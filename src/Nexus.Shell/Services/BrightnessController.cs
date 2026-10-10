using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Nexus.Shell.Services;

public sealed record BrightnessSnapshot(bool Available, string DeviceId, string Device, double Percent, string Message)
{
    public static BrightnessSnapshot Unavailable(string message) => new(false, "", "Display", 0, message);
}

[SupportedOSPlatform("windows")]
internal sealed class BrightnessController(IntPtr window)
{
    private readonly object _gate = new();
    internal Task<BrightnessSnapshot> ReadAsync() => Task.Run(() => { lock (_gate) return Access(null, null); });
    internal Task<BrightnessSnapshot> ChangeAsync(string expectedDevice, double percent, CancellationToken cancellation = default) => Task.Run(() =>
    { lock (_gate) return Access(expectedDevice, percent, cancellation); });

    private BrightnessSnapshot Access(string? expectedDevice, double? percent, CancellationToken cancellation = default)
    {
        PhysicalMonitor[] physical = [];
        bool acquired = false;
        try
        {
            var monitor = MonitorFromWindow(window, 2);
            var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
                return BrightnessSnapshot.Unavailable("The display is unavailable. Choose Refresh.");
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out uint count) || count != 1)
                return BrightnessSnapshot.Unavailable("This display or VM does not expose hardware brightness controls.");
            physical = new PhysicalMonitor[count];
            if (!GetPhysicalMonitorsFromHMONITOR(monitor, count, physical))
                return BrightnessSnapshot.Unavailable("This display does not expose hardware brightness controls.");
            acquired = true;
            var display = physical[0];
            string id = info.Device + "|" + display.Description;
            if (expectedDevice is not null && expectedDevice != id)
                throw new InvalidOperationException("The display changed. Refresh before changing brightness.");
            if (!GetMonitorCapabilities(display.Handle, out uint capabilities, out _) || (capabilities & 2) == 0
                || !GetMonitorBrightness(display.Handle, out uint minimum, out uint current, out uint maximum))
                return BrightnessSnapshot.Unavailable("This display does not support hardware brightness control.");
            double level = BrightnessScale.Percent(minimum, current, maximum);
            if (percent is double requested)
            {
                cancellation.ThrowIfCancellationRequested();
                if (!SetMonitorBrightness(display.Handle, BrightnessScale.Native(requested, minimum, maximum)))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "The display rejected the brightness change.");
                if (!GetMonitorBrightness(display.Handle, out minimum, out current, out maximum))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "The display could not confirm its brightness.");
                level = BrightnessScale.Percent(minimum, current, maximum);
            }
            return new(true, id, display.Description, level, "");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Log.Write("Hardware brightness unavailable", ex);
            return BrightnessSnapshot.Unavailable(ex.Message);
        }
        finally { if (acquired) DestroyPhysicalMonitors((uint)physical.Length, physical); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MonitorInfo
    { public uint Size; public Rect Monitor, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct PhysicalMonitor
    { public IntPtr Handle; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("dxva2.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);
    [DllImport("dxva2.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] PhysicalMonitor[] physical);
    [DllImport("dxva2.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyPhysicalMonitors(uint count, [In] PhysicalMonitor[] physical);
    [DllImport("dxva2.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorCapabilities(IntPtr monitor, out uint capabilities, out uint temperatures);
    [DllImport("dxva2.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorBrightness(IntPtr monitor, out uint minimum, out uint current, out uint maximum);
    [DllImport("dxva2.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetMonitorBrightness(IntPtr monitor, uint value);
}
