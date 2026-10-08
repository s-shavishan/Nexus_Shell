using System.ComponentModel;
using System.Runtime.InteropServices;
using Nexus.Shell.Services;

namespace Nexus.Shell.Interop;

internal sealed class ShellKeyboardHook : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct KeyInfo { public uint Key, ScanCode, Flags, Time; public UIntPtr Extra; }
    private delegate IntPtr HookProc(int code, UIntPtr message, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int hook, HookProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, UIntPtr message, IntPtr data);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    private readonly HookProc _callback;
    private readonly Action<ShellKeyAction> _dispatch;
    private readonly ShellKeyboardState _keys = new();
    private IntPtr _hook;
    internal ShellKeyboardHook(Action<ShellKeyAction> dispatch)
    {
        _dispatch = dispatch; _callback = Message;
        _hook = SetWindowsHookEx(13, _callback, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Nexus could not attach its desktop shortcuts.");
    }
    private IntPtr Message(int code, UIntPtr message, IntPtr pointer)
    {
        try
        {
        if (code >= 0 && pointer != IntPtr.Zero)
        {
            uint id = message.ToUInt32();
            if (id is 0x0100 or 0x0101 or 0x0104 or 0x0105)
            {
                var key = Marshal.PtrToStructure<KeyInfo>(pointer);
                // Injected events pass through. The callback performs no file/network work.
                if ((key.Flags & 0x10) == 0)
                {
                    bool shift = GetAsyncKeyState(0x10) < 0, control = GetAsyncKeyState(0x11) < 0;
                    bool modifiers = shift || control || GetAsyncKeyState(0x12) < 0;
                    var result = _keys.Process(key.Key, id is 0x0100 or 0x0104, modifiers, shift, control);
                    if (result.Action != ShellKeyAction.None) _dispatch(result.Action);
                    if (result.Consume) return new IntPtr(1);
                }
            }
        }
        }
        catch { /* Never propagate an exception across the unmanaged hook callback. */ }
        return CallNextHookEx(_hook, code, message, pointer);
    }
    public void Dispose() { if (_hook == IntPtr.Zero) return; UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; GC.KeepAlive(_callback); }
}
