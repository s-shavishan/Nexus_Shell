using System.ComponentModel;
using System.Runtime.InteropServices;
using Nexus.Shell.Services;

namespace Nexus.Shell.Interop;

// Own Nexus HWNDs only. Non-client cleanup and resize hit testing retain native
// dragging. Window regions change only when size, DPI or the visible dock changes.
internal sealed class WindowChrome : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo { public Point Reserved, MaximumSize, MaximumPosition, MinimumTrackSize, MaximumTrackSize; }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out ShellLayerInterop.Rect rectangle);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] private static extern int SetWindowRgn(IntPtr window, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(IntPtr value);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, uint attribute, ref int value, uint size);
    private readonly IntPtr _handle;
    private readonly bool _resizable, _customClip;
    private readonly int _minimumWidth, _minimumHeight;
    private readonly Action? _tuck;
    private readonly ShellLayerInterop.SubclassProc _callback;
    private readonly UIntPtr _id = new(0x4E10);
    private ShellRect? _clip;
    private (ShellRect Rectangle, int Diameter, bool Clear)? _applied;
    private double _radius = 18;
    private bool _fullscreen, _disposed, _applying, _reported, _regionsEnabled = true;
    internal WindowChrome(IntPtr handle, bool resizable = false, bool customClip = false, Action? tuck = null, int minimumWidth = 480, int minimumHeight = 360)
    {
        _minimumWidth = Math.Max(160, minimumWidth); _minimumHeight = Math.Max(160, minimumHeight);
        _handle = handle; _resizable = resizable; _customClip = customClip; _tuck = tuck; _callback = Message;
        if (!ShellLayerInterop.SetWindowSubclass(handle, _callback, _id, UIntPtr.Zero)) throw new Win32Exception("Could not attach the Nexus window frame.");
        // DWMNCRP_DISABLED removes Windows 10's bright non-client outline.
        int policy = 1; _ = DwmSetWindowAttribute(handle, 2, ref policy, 4);
        int none = unchecked((int)0xFFFFFFFE); _ = DwmSetWindowAttribute(handle, 34, ref none, 4);
        int ownCorners = 1; _ = DwmSetWindowAttribute(handle, 33, ref ownCorners, 4);
        ShellLayerInterop.SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020);
        Refresh();
    }
    internal void SetCorners(bool highContrast)
    { _radius = highContrast ? 0 : 18; Refresh(); }
    internal void SetFullscreen(bool value) { _fullscreen = value; Refresh(); }
    internal void SetClip(ShellRect rectangle, double radiusDip)
    { _clip = rectangle; _radius = Math.Max(0, radiusDip); Refresh(); }
    private void Refresh()
    {
        try { RefreshCore(); }
        catch (Exception ex)
        { _regionsEnabled = false; if (!_reported) { _reported = true; Log.Write("Rounded window decoration unavailable", ex); } }
    }
    private void RefreshCore()
    {
        if (_disposed || _applying || !_regionsEnabled || IsIconic(_handle) || !GetWindowRect(_handle, out var window)) return;
        int width = window.Right - window.Left, height = window.Bottom - window.Top;
        if (width <= 0 || height <= 0 || (_customClip && _clip is null)) return;
        bool clear = !_customClip && (_fullscreen || IsZoomed(_handle));
        var rectangle = _clip ?? new ShellRect(0, 0, width, height);
        int diameter = (int)Math.Round(Math.Min(_radius * ShellLayerInterop.Scale(_handle) * 2, Math.Min(rectangle.Width, rectangle.Height)));
        var shape = (rectangle, diameter, clear);
        if (_applied is { } previous && previous.Rectangle == rectangle && previous.Diameter == diameter && previous.Clear == clear) return;
        _applying = true;
        try
        {
            IntPtr region = clear ? IntPtr.Zero : CreateRoundRectRgn(rectangle.X, rectangle.Y, rectangle.Right + 1, rectangle.Bottom + 1, diameter, diameter);
            if (!clear && region == IntPtr.Zero) throw new Win32Exception("Could not create the rounded Nexus window.");
            // SetWindowRgn transfers ownership to Windows only after success.
            if (SetWindowRgn(_handle, region, true) == 0)
            { int error = Marshal.GetLastWin32Error(); if (region != IntPtr.Zero) DeleteObject(region); throw new Win32Exception(error, "Could not round the Nexus window."); }
            _applied = shape;
        }
        finally { _applying = false; }
    }
    private IntPtr ResizeHit(IntPtr coordinates)
    {
        if (!_resizable || _fullscreen || IsZoomed(_handle) || !GetWindowRect(_handle, out var rect)) return IntPtr.Zero;
        long value = coordinates.ToInt64();
        int x = unchecked((short)(value & 0xFFFF)), y = unchecked((short)((value >> 16) & 0xFFFF));
        int edge = Math.Max(4, (int)Math.Round(7 * ShellLayerInterop.Scale(_handle)));
        bool left = x < rect.Left + edge, right = x >= rect.Right - edge, top = y < rect.Top + edge, bottom = y >= rect.Bottom - edge;
        return new IntPtr(top ? left ? 13 : right ? 14 : 12 : bottom ? left ? 16 : right ? 17 : 15 : left ? 10 : right ? 11 : 0);
    }
    private IntPtr Message(IntPtr window, uint message, UIntPtr wp, IntPtr lp, UIntPtr id, UIntPtr data)
    {
        try
        {
            if (!_disposed)
            {
                if (_tuck is not null && message == 0x0112 && (wp.ToUInt64() & 0xFFF0) == 0xF020)
                { _tuck(); return IntPtr.Zero; }
                if (message == 0x0083) return IntPtr.Zero; // whole HWND is client area
                if (message == 0x0085) return IntPtr.Zero;
                if (message == 0x0086) return new IntPtr(1);
                if (message == 0x0084) { var hit = ResizeHit(lp); if (hit != IntPtr.Zero) return hit; }
                if (_resizable && !_fullscreen && message == 0x0024 && lp != IntPtr.Zero)
                {
                    var monitor = ShellLayerInterop.Monitor(window); var info = Marshal.PtrToStructure<MinMaxInfo>(lp);
                    info.MaximumPosition = new() { X = monitor.Work.Left - monitor.Monitor.Left, Y = monitor.Work.Top - monitor.Monitor.Top };
                    info.MaximumSize = new() { X = monitor.Work.Right - monitor.Work.Left, Y = monitor.Work.Bottom - monitor.Work.Top };
                    info.MinimumTrackSize = new() { X = Math.Min(info.MaximumSize.X, (int)(_minimumWidth * ShellLayerInterop.Scale(window))), Y = Math.Min(info.MaximumSize.Y, (int)(_minimumHeight * ShellLayerInterop.Scale(window))) };
                    Marshal.StructureToPtr(info, lp, false); return IntPtr.Zero;
                }
            }
        }
        catch (Exception ex)
        { if (!_reported) { _reported = true; Log.Write("Nexus frame decoration unavailable", ex); } }
        var result = ShellLayerInterop.DefSubclassProc(window, message, wp, lp);
        if (!_disposed && message is 0x0005 or 0x0047 or 0x02E0) Refresh();
        return result;
    }
    public void Dispose()
    { if (_disposed) return; _disposed = true; ShellLayerInterop.RemoveWindowSubclass(_handle, _callback, _id); }
}
