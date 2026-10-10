using Nexus.Runtime;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Nexus.Core.Settings;

internal static class BluetoothSettings
{
    internal static BluetoothState Execute(SettingsRequest request, CancellationToken cancellation)
    {
        var radios = new List<BluetoothRadioState>(); bool changed = false;
        var parameters = new RadioSearch { Size = (uint)Marshal.SizeOf<RadioSearch>() };
        IntPtr find = BluetoothFindFirstRadio(ref parameters, out IntPtr radio);
        if (find == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            if (error is not (0 or 259 or 1168)) throw new Win32Exception(error, "Could not enumerate Bluetooth radios.");
            if (request.Change is not null) throw new RuntimeFailure("settings-device", "No Bluetooth radio is available. Refresh after connecting one.");
            return new([], "No classic Bluetooth radio is available. A VM usually needs a passed-through Bluetooth adapter.");
        }
        try
        {
            do
            {
                try
                {
                    cancellation.ThrowIfCancellationRequested();
                    var info = new RadioInfo { Size = (uint)Marshal.SizeOf<RadioInfo>() };
                    uint error = BluetoothGetRadioInfo(radio, ref info); if (error != 0) throw new Win32Exception((int)error);
                    string id = info.Address.ToString("X12");
                    if (request.Change is { } change && change.DeviceId == id)
                    {
                        changed = true; cancellation.ThrowIfCancellationRequested();
                        if (!BluetoothEnableDiscovery(radio, change.Enabled == true)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not change Bluetooth visibility. The radio must allow incoming connections.");
                    }
                    radios.Add(new(id, info.Name, BluetoothIsDiscoverable(radio), Devices(radio, request.Scan, cancellation)));
                }
                finally { CloseHandle(radio); }
            } while (radios.Count < 16 && BluetoothFindNextRadio(find, out radio));
        }
        finally { BluetoothFindRadioClose(find); }
        if (request.Change is not null && !changed) throw new RuntimeFailure("settings-target", "The Bluetooth radio changed. Refresh first.");
        return new(radios.ToArray(), "Visibility lasts for this Nexus Core session. Device status covers classic Bluetooth; radio power, pairing and BLE management still use Windows device tools.");
    }
    private static BluetoothDeviceState[] Devices(IntPtr radio, bool scan, CancellationToken cancellation)
    {
        var result = new List<BluetoothDeviceState>();
        var search = new DeviceSearch { Size = (uint)Marshal.SizeOf<DeviceSearch>(), Authenticated = 1, Remembered = 1, Unknown = 1, Connected = 1, Inquiry = scan ? 1 : 0, Timeout = 2, Radio = radio };
        var info = new DeviceInfo { Size = (uint)Marshal.SizeOf<DeviceInfo>() };
        IntPtr find = BluetoothFindFirstDevice(ref search, ref info);
        if (find == IntPtr.Zero)
        { int error = Marshal.GetLastWin32Error(); if (error is not (0 or 259 or 1168)) throw new Win32Exception(error, "Could not enumerate Bluetooth devices."); return []; }
        try
        {
            do
            {
                cancellation.ThrowIfCancellationRequested();
                result.Add(new(info.Address.ToString("X12"), info.Name, info.Connected != 0, info.Authenticated != 0));
            } while (result.Count < 64 && BluetoothFindNextDevice(find, ref info));
        }
        finally { BluetoothFindDeviceClose(find); }
        return result.OrderByDescending(d => d.Connected).ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    [StructLayout(LayoutKind.Sequential)] internal struct RadioSearch { public uint Size; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct RadioInfo
    { public uint Size; public ulong Address; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)] public string Name; public uint Class; public ushort Subversion, Manufacturer; }
    [StructLayout(LayoutKind.Sequential)] internal struct Time { public ushort Year, Month, Weekday, Day, Hour, Minute, Second, Millisecond; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct DeviceInfo
    { public uint Size; public ulong Address; public uint Class; public int Connected, Remembered, Authenticated; public Time Seen, Used; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)] public string Name; }
    [StructLayout(LayoutKind.Sequential)] internal struct DeviceSearch
    { public uint Size; public int Authenticated, Remembered, Unknown, Connected, Inquiry; public byte Timeout; public IntPtr Radio; }
    [DllImport("bthprops.cpl", SetLastError = true)] private static extern IntPtr BluetoothFindFirstRadio(ref RadioSearch parameters, out IntPtr radio);
    [DllImport("bthprops.cpl", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool BluetoothFindNextRadio(IntPtr find, out IntPtr radio);
    [DllImport("bthprops.cpl")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool BluetoothFindRadioClose(IntPtr find);
    [DllImport("bthprops.cpl", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern uint BluetoothGetRadioInfo(IntPtr radio, ref RadioInfo info);
    [DllImport("bthprops.cpl")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool BluetoothIsDiscoverable(IntPtr radio);
    [DllImport("bthprops.cpl", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool BluetoothEnableDiscovery(IntPtr radio, [MarshalAs(UnmanagedType.Bool)] bool enabled);
    [DllImport("bthprops.cpl", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern IntPtr BluetoothFindFirstDevice(ref DeviceSearch search, ref DeviceInfo info);
    [DllImport("bthprops.cpl", CharSet = CharSet.Unicode, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool BluetoothFindNextDevice(IntPtr find, ref DeviceInfo info);
    [DllImport("bthprops.cpl")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool BluetoothFindDeviceClose(IntPtr find);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
}
