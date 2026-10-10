using Nexus.Shell.Services;
using System.ComponentModel;
using System.Runtime.InteropServices;

internal static class WindowsMinimizedMetrics
{
    [StructLayout(LayoutKind.Sequential)] private struct Metrics { public uint Size; public int Width, HorizontalGap, VerticalGap, Arrangement; }
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Parameters(uint action, uint size, ref Metrics metrics, uint flags);
    internal static MinimizedDesktopMetrics Read()
    {
        var metrics = new Metrics { Size = (uint)Marshal.SizeOf<Metrics>() };
        if (!Parameters(0x002B, metrics.Size, ref metrics, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not read minimized window arrangement.");
        return new(metrics.Width, metrics.HorizontalGap, metrics.VerticalGap, metrics.Arrangement);
    }
    internal static void Apply(MinimizedDesktopMetrics metrics)
    {
        if (!MinimizedWindowPolicy.IsValid(metrics)) throw new InvalidDataException("The minimized window arrangement is invalid.");
        var value = new Metrics { Size = (uint)Marshal.SizeOf<Metrics>(), Width = metrics.Width, HorizontalGap = metrics.HorizontalGap, VerticalGap = metrics.VerticalGap, Arrangement = metrics.Arrangement };
        // Session-only: do not persist a system preference to the user's profile.
        if (!Parameters(0x002C, value.Size, ref value, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not change minimized window arrangement.");
    }
    internal static void Restore(MinimizedDesktopMetrics? original)
    {
        if (original is null) return;
        var current = Read(); var restored = MinimizedWindowPolicy.Restore(current, original);
        if (restored != current) Apply(restored);
    }
}
