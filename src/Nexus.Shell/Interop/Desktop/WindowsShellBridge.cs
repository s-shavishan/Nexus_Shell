// නමෝ බුද්ධාය 🤍
using System.Runtime.InteropServices;
using Nexus.Shell.Services;

namespace Nexus.Shell.Interop;

internal sealed class WindowsShellBridge : IWindowsShortcutSender
{
    private static readonly UIntPtr Marker = new(0x4E585346);
    internal WindowsShellPresence Observe() => new(GetShellWindow().ToInt64(), FindWindow("Shell_TrayWnd", null).ToInt64());
    public bool ShellAvailable => Observe().Ready;
    public bool ModifiersUp => new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.All(key => (GetAsyncKeyState(key) & 0x8000) == 0);
    public bool KeyUp(ushort key) => (GetAsyncKeyState(key) & 0x8000) == 0;
    public int Send(IReadOnlyList<ShellKey> keys)
    {
        if (!Environment.Is64BitProcess) throw new PlatformNotSupportedException("The Windows shell bridge requires the Nexus x64 build.");
        var inputs = keys.Select(key => new Input { Type = 1, Keyboard = new() { Key = key.Key, Flags = key.Up ? 2u : 0u, Extra = Marker } }).ToArray();
        return (int)SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }
    internal static (int X, int Y) Cursor() => GetCursorPos(out var point) ? (point.X, point.Y) : (0, 0);
    internal static bool AllowForeground(int processId) => AllowSetForegroundWindow((uint)processId);
    internal static int InputSize => Marshal.SizeOf<Input>();
    [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { internal ushort Key, Scan; internal uint Flags, Time; internal UIntPtr Extra; }
    [StructLayout(LayoutKind.Explicit, Size = 40)] private struct Input { [FieldOffset(0)] internal uint Type; [FieldOffset(8)] internal KeyboardInput Keyboard; }
    [DllImport("user32.dll", ExactSpelling = true)] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern IntPtr FindWindow(string name, string? title);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true, ExactSpelling = true)] private static extern uint SendInput(uint count, Input[] input, int size);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AllowSetForegroundWindow(uint process);
}
