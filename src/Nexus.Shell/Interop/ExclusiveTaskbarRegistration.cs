using System.ComponentModel;
using System.Runtime.InteropServices;
using Nexus.Shell.Services;

namespace Nexus.Shell.Interop;

internal interface ITaskbarLayer : IDisposable
{
    ShellRect Bounds { get; }
    ShellRect Reservation { get; }
    ShellRect WorkArea { get; }
    void Position(bool compact, bool floating, double preferredWidthDip);
}

// A desktop shell cannot ask Explorer's appbar manager for its work area.
internal sealed class ExclusiveTaskbarRegistration : ITaskbarLayer
{
    private readonly IntPtr _handle;
    private readonly Action _reposition;
    private readonly ShellLayerInterop.SubclassProc _callback;
    private readonly DesktopWorkAreaReservation _workArea = new();
    private bool _disposed;
    private readonly uint _explorerRestart = ShellLayerInterop.RegisterWindowMessage("TaskbarCreated");
    public ShellRect Bounds { get; private set; }
    public ShellRect Reservation { get; private set; }
    public ShellRect WorkArea { get; private set; }
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWorkArea(uint action, uint parameter, ref ShellLayerInterop.Rect rect, uint flags);
    internal ExclusiveTaskbarRegistration(IntPtr handle, Action reposition)
    {
        _handle = handle; _reposition = reposition; _callback = Message;
        if (!ShellLayerInterop.SetWindowSubclass(handle, _callback, new UIntPtr(0x4E03), UIntPtr.Zero)) throw new Win32Exception("Could not attach the Nexus desktop taskbar.");
    }
    public void Position(bool compact, bool floating, double preferredWidthDip)
    {
        if (_disposed) return;
        var info = ShellLayerInterop.Monitor(_handle); var monitor = info.Monitor;
        double scale = ShellLayerInterop.Scale(_handle);
        int height = Math.Min(monitor.Bottom - monitor.Top, DesktopLayout.TaskbarReservationHeight(compact, floating, scale));
        var reservation = new ShellRect(monitor.Left, monitor.Bottom - height, monitor.Right - monitor.Left, height);
        var work = DesktopLayout.ManagedWorkArea(monitor.Bounds, compact, floating, scale);
        bool moved = Reservation != reservation;
        _workArea.Apply(work, DesktopWorkArea.Apply); WorkArea = work;
        Reservation = reservation; Bounds = DesktopLayout.TaskbarBounds(reservation, floating, preferredWidthDip, scale);
        if (moved) ShellLayerInterop.SetWindowPos(_handle, ShellLayerInterop.Topmost, reservation.X, reservation.Y, reservation.Width, reservation.Height, 0x0010);
    }
    private IntPtr Message(IntPtr window, uint message, UIntPtr wp, IntPtr lp, UIntPtr id, UIntPtr data)
    {
        try
        {
            if (!_disposed && (message is 0x007E or 0x02E0 || (_explorerRestart != 0 && message == _explorerRestart)))
            { _workArea.Invalidate(); _reposition(); }
        }
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
            if (monitor.Work.Bounds == WorkArea)
            { var full = monitor.Monitor; SetWorkArea(0x002F, 0, ref full, 2); }
        }
        finally { ShellLayerInterop.RemoveWindowSubclass(_handle, _callback, new UIntPtr(0x4E03)); }
    }
}
