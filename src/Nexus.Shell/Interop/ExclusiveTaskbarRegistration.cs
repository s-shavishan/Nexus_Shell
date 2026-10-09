using System.ComponentModel;
using System.Runtime.InteropServices;
using Nexus.Shell.Services;

namespace Nexus.Shell.Interop;

internal interface ITaskbarLayer : IDisposable
{
    ShellRect Bounds { get; }
    ShellRect Reservation { get; }
    void Position(bool compact, bool floating, double preferredWidthDip);
    void RefreshStacking();
}

// A desktop shell cannot ask Explorer's appbar manager for its work area.
internal sealed class ExclusiveTaskbarRegistration : ITaskbarLayer
{
    private readonly IntPtr _handle, _desktop;
    private readonly Action _reposition;
    private readonly ShellLayerInterop.SubclassProc _callback;
    private readonly DesktopWorkAreaReservation _workArea = new();
    private bool _disposed, _fullscreen;
    public ShellRect Bounds { get; private set; }
    public ShellRect Reservation { get; private set; }
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWorkArea(uint action, uint parameter, ref ShellLayerInterop.Rect rect, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out ShellLayerInterop.Rect rect);
    internal ExclusiveTaskbarRegistration(IntPtr handle, IntPtr desktop, Action reposition)
    {
        _handle = handle; _desktop = desktop; _reposition = reposition; _callback = Message;
        if (!ShellLayerInterop.SetWindowSubclass(handle, _callback, new UIntPtr(0x4E03), UIntPtr.Zero)) throw new Win32Exception("Could not attach the Nexus desktop taskbar.");
    }
    public void Position(bool compact, bool floating, double preferredWidthDip)
    {
        if (_disposed) return;
        var info = ShellLayerInterop.Monitor(_handle); var monitor = info.Monitor;
        double scale = ShellLayerInterop.Scale(_handle);
        int height = Math.Min(monitor.Bottom - monitor.Top, DesktopLayout.TaskbarReservationHeight(compact, floating, scale));
        var reservation = new ShellRect(monitor.Left, monitor.Bottom - height, monitor.Right - monitor.Left, height);
        var work = monitor; work.Bottom = reservation.Y;
        bool moved = Reservation != reservation;
        _workArea.Apply(work.Bounds, DesktopWorkArea.Apply);
        Reservation = reservation; Bounds = DesktopLayout.TaskbarBounds(reservation, floating, preferredWidthDip, scale);
        if (moved) ShellLayerInterop.SetWindowPos(_handle, _fullscreen ? ShellLayerInterop.Bottom : ShellLayerInterop.Topmost, reservation.X, reservation.Y, reservation.Width, reservation.Height, 0x0010);
    }
    public void RefreshStacking()
    {
        if (_disposed) return;
        var foreground = NativeMethods.GetForegroundWindow();
        var info = ShellLayerInterop.Monitor(_handle); var monitor = info.Monitor.Bounds;
        bool full = foreground != IntPtr.Zero && foreground != _handle && foreground != _desktop
            && GetWindowRect(foreground, out var rect) && rect.Left <= monitor.X && rect.Top <= monitor.Y && rect.Right >= monitor.Right && rect.Bottom >= monitor.Bottom;
        if (_fullscreen == full) return; _fullscreen = full;
        ShellLayerInterop.SetWindowPos(_handle, full ? ShellLayerInterop.Bottom : ShellLayerInterop.Topmost, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
    }
    private IntPtr Message(IntPtr window, uint message, UIntPtr wp, IntPtr lp, UIntPtr id, UIntPtr data)
    {
        try { if (!_disposed && message is 0x007E or 0x02E0) { _workArea.Invalidate(); _reposition(); } }
        catch (Exception ex) { Log.Write("Desktop taskbar message failed", ex); }
        return ShellLayerInterop.DefSubclassProc(window, message, wp, lp);
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        try
        {
            var monitor = ShellLayerInterop.Monitor(_handle);
            // Do not overwrite a work-area change made by another component.
            if (monitor.Work.Bounds == new ShellRect(Reservation.X, monitor.Monitor.Top, Reservation.Width, Reservation.Y - monitor.Monitor.Top))
            { var full = monitor.Monitor; SetWorkArea(0x002F, 0, ref full, 2); }
        }
        finally { ShellLayerInterop.RemoveWindowSubclass(_handle, _callback, new UIntPtr(0x4E03)); }
    }
}
