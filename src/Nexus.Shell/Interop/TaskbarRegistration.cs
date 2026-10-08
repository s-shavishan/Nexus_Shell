using System.ComponentModel;
using System.Runtime.InteropServices;
using Nexus.Shell.Services;

namespace Nexus.Shell.Interop;

internal sealed class TaskbarRegistration : ITaskbarLayer
{
    private readonly IntPtr _handle;
    private readonly Action _reposition;
    private readonly Action<bool> _arrange;
    private readonly ShellLayerInterop.SubclassProc _callback;
    private readonly uint _message, _explorerRestart;
    private bool _registered, _disposed, _positioning, _fullscreen;
    public ShellRect Bounds { get; private set; }
    internal TaskbarRegistration(IntPtr handle, Action reposition, Action<bool> arrange)
    {
        _handle = handle; _reposition = reposition; _arrange = arrange; _callback = Message;
        _message = ShellLayerInterop.RegisterWindowMessage("WhiteDreams.Nexus.Taskbar.Position");
        _explorerRestart = ShellLayerInterop.RegisterWindowMessage("TaskbarCreated");
        if (_message == 0 || !ShellLayerInterop.SetWindowSubclass(handle, _callback, new UIntPtr(0x4E02), UIntPtr.Zero)) throw new Win32Exception("Could not attach the Nexus taskbar.");
        try { Register(); } catch { Dispose(); throw; }
    }
    private ShellLayerInterop.AppBarData Data() => new() { Size = (uint)Marshal.SizeOf<ShellLayerInterop.AppBarData>(), Window = _handle, Callback = _message, Edge = 3 };
    private void Register()
    { var data = Data(); _registered = ShellLayerInterop.SHAppBarMessage(0, ref data) != UIntPtr.Zero; if (!_registered) throw new Win32Exception("Windows could not reserve the Nexus taskbar area."); }
    public void Position(bool compact)
    {
        if (_disposed || !_registered || _positioning) return;
        _positioning = true;
        try
        {
            var data = Data(); data.Rect = ShellLayerInterop.Monitor(_handle).Monitor;
            int height = (int)Math.Round(DesktopLayout.TaskbarHeight(compact) * ShellLayerInterop.Scale(_handle));
            data.Rect.Top = data.Rect.Bottom - height;
            ShellLayerInterop.SHAppBarMessage(2, ref data);
            data.Rect.Top = data.Rect.Bottom - height;
            if (Bounds == data.Rect.Bounds) return;
            ShellLayerInterop.SHAppBarMessage(3, ref data);
            var b = data.Rect.Bounds;
            if (Bounds != b)
            { Bounds = b; ShellLayerInterop.SetWindowPos(_handle, _fullscreen ? ShellLayerInterop.Bottom : ShellLayerInterop.Topmost, b.X, b.Y, b.Width, b.Height, 0x0010); }
        }
        finally { _positioning = false; }
    }
    private IntPtr Message(IntPtr window, uint message, UIntPtr wp, IntPtr lp, UIntPtr id, UIntPtr extra)
    {
        try
        {
            if (!_disposed)
            {
                if (message == _message)
                {
                    switch (wp.ToUInt32())
                    {
                        case 1: if (!_positioning) _reposition(); break; // ABN_POSCHANGED
                        case 2:
                            _fullscreen = lp != IntPtr.Zero;
                            ShellLayerInterop.SetWindowPos(window, _fullscreen ? ShellLayerInterop.Bottom : ShellLayerInterop.Topmost, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
                            break;
                        case 3: _arrange(lp != IntPtr.Zero); break;
                    }
                    return IntPtr.Zero;
                }
                if (message == _explorerRestart) { _registered = false; Register(); Bounds = default; _reposition(); }
                if (message is 0x007E or 0x02E0) _reposition();
                if (_registered && message is 0x0006 or 0x0047)
                { var data = Data(); data.Parameter = new IntPtr(message == 0x0006 && (wp.ToUInt64() & 0xFFFF) != 0 ? 1 : 0); ShellLayerInterop.SHAppBarMessage(message == 0x0006 ? 6u : 9u, ref data); }
            }
        }
        catch (Exception ex) { Log.Write("Taskbar notification failed", ex); }
        return ShellLayerInterop.DefSubclassProc(window, message, wp, lp);
    }
    public void RefreshStacking() { }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        if (_registered) { var data = Data(); ShellLayerInterop.SHAppBarMessage(1, ref data); _registered = false; }
        ShellLayerInterop.RemoveWindowSubclass(_handle, _callback, new UIntPtr(0x4E02));
    }
}
