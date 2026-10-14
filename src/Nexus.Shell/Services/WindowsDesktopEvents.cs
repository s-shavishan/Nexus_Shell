namespace Nexus.Shell.Services;

[Flags]
public enum WindowsDesktopChange
{
    None = 0, Suspend = 1, Resume = 2, Displays = 4, Devices = 8,
    Clock = 16, Appearance = 32, Power = 64, InputLanguage = 128
}

public static class WindowsDesktopEvents
{
    // No lParam pointer dereferencing; all work is queued outside WndProc.
    public static WindowsDesktopChange Classify(uint message, ulong parameter) => message switch
    {
        0x0218 => parameter switch // WM_POWERBROADCAST
        {
            4 => WindowsDesktopChange.Suspend,
            7 or 18 => WindowsDesktopChange.Resume,
            10 => WindowsDesktopChange.Power,
            _ => WindowsDesktopChange.None
        },
        0x007E => WindowsDesktopChange.Displays, // WM_DISPLAYCHANGE
        0x0219 when parameter is 0x0007 or 0x8000 or 0x8004 => WindowsDesktopChange.Devices,
        0x001E => WindowsDesktopChange.Clock, // WM_TIMECHANGE
        0x0015 or 0x031A => WindowsDesktopChange.Appearance,
        // SPI_SETWORKAREA is emitted by our own reservation; do not loop.
        0x001A when parameter != 0x002F => WindowsDesktopChange.Appearance | WindowsDesktopChange.Clock,
        0x0051 => WindowsDesktopChange.InputLanguage,
        _ => WindowsDesktopChange.None
    };
}
