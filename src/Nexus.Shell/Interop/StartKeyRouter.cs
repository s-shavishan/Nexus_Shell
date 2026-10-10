using Nexus.Shell.Services;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Nexus.Shell.Interop;

// Managed desktop sessions own only a standalone Windows key. Native chords
// pass through unchanged. The hook thread never runs XAML, IPC or file work.
internal sealed class StartKeyRouter : IDisposable
{
    private readonly IntPtr _window;
    private readonly StartKeyGesture _gesture = new();
    private readonly HookProc _callback;
    private readonly Thread _thread;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static readonly UIntPtr Marker = new(0x4E585354);
    private IntPtr _hook;
    private uint _threadId;
    private int _stopping, _failureReported;
    internal static int InputSize => Marshal.SizeOf<Input>();
    internal static int KeyboardOffset => (int)Marshal.OffsetOf<Input>(nameof(Input.Keyboard));
    internal StartKeyRouter(IntPtr window)
    {
        _window = window; _callback = Handle;
        _thread = new Thread(Run) { Name = "Nexus Start key", IsBackground = true }; _thread.Start();
    }
    internal Task Ready => _ready.Task;
    private void Run()
    {
        try
        {
            _threadId = GetCurrentThreadId(); PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
            // Seed outside the hook: asynchronous state is not updated yet
            // inside LowLevelKeyboardProc for the event being processed.
            for (int key = 8; key < 256; key++) if ((GetAsyncKeyState(key) & 0x8000) != 0) _gesture.SeedHeld(key);
            if (Volatile.Read(ref _stopping) != 0) { _ready.TrySetCanceled(); return; }
            _hook = SetWindowsHookEx(13, _callback, GetModuleHandle(null), 0);
            if (_hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not connect the Nexus Start key.");
            if (SetTimer(IntPtr.Zero, new UIntPtr(1), 1000, IntPtr.Zero) == UIntPtr.Zero) throw new Win32Exception("Could not supervise Start-key state.");
            _ready.TrySetResult();
            int result;
            while ((result = GetMessage(out var message, IntPtr.Zero, 0, 0)) > 0)
            {
                if (message.Id == 0x113 && (GetAsyncKeyState(StartKeyGesture.LeftWindows) & 0x8000) == 0 && (GetAsyncKeyState(StartKeyGesture.RightWindows) & 0x8000) == 0)
                {
                    // Secure desktops can hide key-ups (Win+L/UAC). Resync
                    // outside callbacks, only when neither Windows key is held.
                    _gesture.Reset();
                    for (int key = 8; key < 256; key++) if ((GetAsyncKeyState(key) & 0x8000) != 0) _gesture.SeedHeld(key);
                }
                TranslateMessage(ref message); DispatchMessage(ref message);
            }
            if (result < 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch (Exception error)
        {
            if (!_ready.TrySetException(error)) ReportFailure();
        }
        finally { if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; } }
    }
    private IntPtr Handle(int code, UIntPtr message, IntPtr pointer)
    {
        try
        {
            if (code >= 0 && Volatile.Read(ref _stopping) == 0 && message.ToUInt64() is 0x100 or 0x101 or 0x104 or 0x105)
            {
                var key = Marshal.PtrToStructure<KeyboardEvent>(pointer);
                if (key.Extra != Marker && _gesture.Observe((int)key.Key, message.ToUInt64() is 0x100 or 0x104, (key.Flags & 0x10) != 0))
                {
                    // Windows saw the real key-down. Send a harmless mask pair
                    // followed by its matching key-up in one ordered batch.
                    // Only swallow the original key-up when all three succeed.
                    Input[] inputs = [Key(0x87, 0), Key(0x87, 2), Key((ushort)key.Key, 2 | (key.Flags & 1))]; // F24
                    uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
                    if (sent == inputs.Length)
                    { DesktopIntegration.PostMessage(_window, DesktopIntegration.LaunchpadMessage, UIntPtr.Zero, IntPtr.Zero); return new IntPtr(1); }
                    // A partial batch must not strand the mask key down.
                    if (sent == 1) SendInput(1, [Key(0x87, 2)], Marshal.SizeOf<Input>());
                    ReportFailure();
                }
            }
        }
        catch { ReportFailure(); }
        // Injection denial (including UIPI) preserves the physical key-up.
        return CallNextHookEx(_hook, code, message, pointer);
    }
    private static Input Key(ushort key, uint flags) => new() { Type = 1, Keyboard = new() { Key = key, Flags = flags, Extra = Marker } };
    private void ReportFailure()
    { if (Interlocked.Exchange(ref _failureReported, 1) == 0) DesktopIntegration.PostMessage(_window, DesktopIntegration.StartUnavailableMessage, UIntPtr.Zero, IntPtr.Zero); }
    public void Dispose()
    { if (Interlocked.Exchange(ref _stopping, 1) == 0 && _threadId != 0) PostThreadMessage(_threadId, 0x12, UIntPtr.Zero, IntPtr.Zero); }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr HookProc(int code, UIntPtr message, IntPtr pointer);
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardEvent { public uint Key, Scan, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
    // INPUT's union is 32 bytes on x64 (MOUSEINPUT is its largest member).
    [StructLayout(LayoutKind.Explicit, Size = 40)] private struct Input
    { [FieldOffset(0)] public uint Type; [FieldOffset(8)] public KeyboardInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct Message
    { public IntPtr Window; public uint Id; public UIntPtr WParam; public IntPtr LParam; public uint Time; public int X, Y; public uint Private; }
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int type, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, UIntPtr message, IntPtr pointer);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern UIntPtr SetTimer(IntPtr window, UIntPtr id, uint interval, IntPtr callback);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)] private static extern int GetMessage(out Message message, IntPtr window, uint first, uint last);
    [DllImport("user32.dll", EntryPoint = "PeekMessageW")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PeekMessage(out Message message, IntPtr window, uint first, uint last, uint remove);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")] private static extern IntPtr DispatchMessage(ref Message message);
    [DllImport("user32.dll", EntryPoint = "PostThreadMessageW")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostThreadMessage(uint thread, uint message, UIntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
}
