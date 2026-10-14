using System.Runtime.InteropServices;

namespace Nexus.Shell.Services;

// A fast local capability probe for PRESENT classic Bluetooth radios. Unknown
// is deliberately different from absent: transient API failures must not hide
// the user's connection controls. No device inquiry or pairing is performed.
internal static class BluetoothRadioAvailability
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FindParameters { public uint Size; }

    [DllImport("bthprops.cpl", SetLastError = true)]
    private static extern IntPtr BluetoothFindFirstRadio(ref FindParameters parameters, out IntPtr radio);

    [DllImport("bthprops.cpl")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindRadioClose(IntPtr find);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    private static long _checkedAt;
    private static bool? _cached;

    internal static bool? Detect(bool refresh = false)
    {
        if (!OperatingSystem.IsWindows()) return null;
        long now = System.Environment.TickCount64;
        if (!refresh && _checkedAt != 0 && now >= _checkedAt && now - _checkedAt < 10000)
            return _cached;
        bool? status;
        try
        {
            var options = new FindParameters { Size = (uint)Marshal.SizeOf<FindParameters>() };
            IntPtr search = BluetoothFindFirstRadio(ref options, out var radio);
            if (search == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                status = NoRadioError(error) ? false : null;
            }
            else
            {
                try { status = radio != IntPtr.Zero; }
                finally
                {
                    if (radio != IntPtr.Zero) CloseHandle(radio);
                    BluetoothFindRadioClose(search);
                }
            }
        }
        catch (DllNotFoundException) { status = null; }
        catch (EntryPointNotFoundException) { status = null; }
        catch (Exception) { status = null; }
        _cached = status;
        _checkedAt = now;
        return status;
    }

    // ERROR_FILE_NOT_FOUND is also returned when there is no classic radio
    // installed on Win10 VirtualBox. Treat only known enumeration absence as
    // proof; service/driver failures remain unknown and are not suppressed.
    internal static bool NoRadioError(int code) => code is 0 or 2 or 55 or 259 or 433 or 1168;
    internal static bool ShowControls(bool? available) => available != false;
}
