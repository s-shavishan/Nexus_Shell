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
            if (EnumDisplaySettings(device.Name, -1, ref mode))
            {
                var choices = new Dictionary<string, DisplayModeOption>(StringComparer.Ordinal);
                for (int m = 0; m < 256; m++)
                {
                    var candidate = new Mode { Size = (ushort)Marshal.SizeOf<Mode>() };
                    if (!EnumDisplaySettings(device.Name, m, ref candidate)) break;
                    if (!Accept(candidate)) continue;
                    string id = ModeId(candidate);
                    if (!choices.ContainsKey(id))
                        choices.Add(id, new(id, (int)candidate.Width, (int)candidate.Height, (int)candidate.Frequency));
                }
                // Preserve the actual current mode even if the driver does not
                // advertise it among the alternatives.
                string current = ModeId(mode);
                if (Accept(mode) && !choices.ContainsKey(current))
                    choices[current] = new(current, (int)mode.Width, (int)mode.Height, (int)mode.Frequency);
                var modes = choices.Values.OrderByDescending(v => v.Width == mode.Width && v.Height == mode.Height && v.RefreshHz == mode.Frequency)
                    .ThenByDescending(v => (long)v.Width * v.Height).ThenByDescending(v => v.RefreshHz).Take(60).ToArray();
                result.Add(new(device.Description, (int)mode.Width, (int)mode.Height, (int)mode.Frequency,
                    (device.Flags & 4) != 0, device.Name, modes));
            }
        }
        return result.Take(16).ToArray();
    }
    internal static string ModeId(Mode mode) => mode.Width + "x" + mode.Height + "@" + mode.Frequency;
    private static bool Accept(Mode mode)
        => mode.Width is >= 640 and <= 16384 && mode.Height is >= 480 and <= 16384
        && mode.Frequency is >= 23 and <= 360
        && mode.BitsPerPixel >= 32;

    internal static Mode CurrentMode(string id)
    {
        if (!Read().Any(d => string.Equals(d.DeviceId, id, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("The selected monitor is no longer attached.");
        var mode = new Mode { Size = (ushort)Marshal.SizeOf<Mode>() };
        if (!EnumDisplaySettings(id, -1, ref mode))
            throw new InvalidOperationException("Windows cannot read this monitor's current display mode.");
        return mode;
    }

    internal static Mode Choice(string deviceId, string modeId)
    {
        if (!Read().Any(d => string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("The monitor changed. Refresh first.");
        for (int index = 0; index < 256; index++)
        {
            var candidate = new Mode { Size = (ushort)Marshal.SizeOf<Mode>() };
            if (!EnumDisplaySettings(deviceId, index, ref candidate)) break;
            if (Accept(candidate) && ModeId(candidate) == modeId) return candidate;
        }
        throw new InvalidOperationException("This display mode is no longer supported by the driver.");
    }

    internal static void Apply(string deviceId, ref Mode mode, bool persist)
    {
        // Never transplant driver-enumerated monitor position/orientation flags
        // into a new mode. Change only geometry and frequency on this adapter.
        mode.Fields = 0x00080000u | 0x00100000u | 0x00400000u;
        int test = ChangeDisplaySettingsEx(deviceId, ref mode, IntPtr.Zero, 0x00000002, IntPtr.Zero);
        if (test != 0) throw new InvalidOperationException("Windows rejected the selected display mode (test " + test + ").");
        int result = ChangeDisplaySettingsEx(deviceId, ref mode, IntPtr.Zero, persist ? 0x00000001u : 0u, IntPtr.Zero);
        if (result != 0)
            throw new InvalidOperationException("Windows could not apply the display mode (result " + result + ").");
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
    { [FieldOffset(68)] public ushort Size; [FieldOffset(72)] public uint Fields;
      [FieldOffset(168)] public uint BitsPerPixel; [FieldOffset(172)] public uint Width; [FieldOffset(176)] public uint Height; [FieldOffset(184)] public uint Frequency; }
    [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(string? device, uint index, ref Device info, uint flags);
    [DllImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(string name, int mode, ref Mode info);
    [DllImport("user32.dll", EntryPoint = "ChangeDisplaySettingsExW", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsEx(string name, ref Mode mode, IntPtr window, uint flags, IntPtr parameters);
}
