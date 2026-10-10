using Nexus.Runtime;
using System.Runtime.InteropServices;

namespace Nexus.Core.Settings;

internal static class DisplaySettings
{
    internal static DisplayState[] Read()
    {
        var result = new List<DisplayState>();
        for (uint index = 0; index < 32; index++)
        {
            var device = new Device { Size = (uint)Marshal.SizeOf<Device>() };
            if (!EnumDisplayDevices(null, index, ref device, 0)) break;
            if ((device.Flags & 1) == 0 || (device.Flags & 8) != 0) continue;
            var mode = new Mode { Size = (ushort)Marshal.SizeOf<Mode>() };
            if (EnumDisplaySettings(device.Name, -1, ref mode)) result.Add(new(device.Description, (int)mode.Width, (int)mode.Height, (int)mode.Frequency, (device.Flags & 4) != 0));
        }
        return result.Take(16).ToArray();
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct Device
    {
        public uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }
    [StructLayout(LayoutKind.Explicit, Size = 220)] internal struct Mode
    { [FieldOffset(68)] public ushort Size; [FieldOffset(172)] public uint Width; [FieldOffset(176)] public uint Height; [FieldOffset(184)] public uint Frequency; }
    [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(string? device, uint index, ref Device info, uint flags);
    [DllImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(string name, int mode, ref Mode info);
}
