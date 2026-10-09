using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Nexus.Shell.Interop;

internal readonly record struct WindowEvent(uint Kind, IntPtr Window)
{
    internal bool IsForeground => Kind == 0x0003;
}

// Receive only task-list events. No keyboard hooks, DLL injection or stream of
// location-change callbacks while dragging. Construct/dispose on the UI thread.
internal sealed class WindowEventObserver : IDisposable
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void WinEventProc(IntPtr hook, uint kind, IntPtr window, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWinEventHook(uint first, uint last, IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    private readonly List<IntPtr> _hooks = [];
    private readonly WinEventProc _callback;
    private readonly Action<WindowEvent> _changed;
    private GCHandle _callbackRoot;
    private bool _disposed;
    internal WindowEventObserver(Action<WindowEvent> changed, bool includeOwnProcess = false)
    {
        _changed = changed; _callback = Changed; _callbackRoot = GCHandle.Alloc(_callback);
        uint flags = includeOwnProcess ? 0u : 2u; // OUTOFCONTEXT | SKIPOWNPROCESS
        try
        {
            Add(0x0003, 0x0003, flags); // foreground
            Add(0x000B, 0x000B, flags); // move/resize finished, not every pixel
            Add(0x0016, 0x0017, flags); // minimize start/end
            Add(0x8000, 0x8003, flags); // create/destroy/show/hide
            Add(0x800C, 0x800C, flags); // window title
            Add(0x8017, 0x8018, flags); // cloaked/uncloaked (virtual desktops)
        }
        catch { Dispose(); throw; }
    }
    private void Add(uint first, uint last, uint flags)
    {
        var hook = SetWinEventHook(first, last, IntPtr.Zero, _callback, 0, 0, flags);
        if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not subscribe to window changes.");
        _hooks.Add(hook);
    }
    private void Changed(IntPtr hook, uint kind, IntPtr window, int objectId, int childId, uint thread, uint time)
    {
        if (_disposed || window == IntPtr.Zero) return;
        // OBJID_WINDOW / CHILDID_SELF. Ignore text, controls and cursor events.
        if (kind >= 0x8000 && (objectId != 0 || childId != 0)) return;
        // A destroyed HWND cannot be queried; the consumer reconciles by identity.
        if (kind != 0x8001 && GetAncestor(window, 2) != window) return;
        try { _changed(new(kind, window)); }
        catch (Exception ex) { Services.Log.Write("Window event delivery failed", ex); }
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        for (int i = _hooks.Count - 1; i >= 0; i--)
            if (UnhookWinEvent(_hooks[i])) _hooks.RemoveAt(i);
        // If Windows refuses an unhook, retain the GC root. The disposed
        // callback does nothing, and can never become a freed function pointer.
        if (_hooks.Count == 0 && _callbackRoot.IsAllocated) _callbackRoot.Free();
        else if (_hooks.Count > 0) Services.Log.Write("A window event subscription could not be released on its owner thread.");
        GC.KeepAlive(_callback);
    }
}
