using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Nexus.Shell.Services;

namespace Nexus.Shell.Interop;

// Test-only real input driver. Constructed exclusively by --verify-input after
// explicit consent. Run on a worker so a native modal move loop cannot prevent
// button release. No hooks, third-party HWNDs or foreground forcing.
internal sealed class InputProbeDriver
{
    [StructLayout(LayoutKind.Sequential)] internal struct MouseInput
    { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] internal struct KeyInput
    { public ushort Key, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Explicit)] internal struct InputData
    { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyInput Key; }
    [StructLayout(LayoutKind.Sequential)] internal struct Input
    { public uint Type; public InputData Data; }
    [StructLayout(LayoutKind.Sequential)] internal struct GuiInfo
    {
        public uint Size, Flags;
        public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        public ShellLayerInterop.Rect CaretRect;
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetGUIThreadInfo(uint thread, ref GuiInfo info);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(ShellLayerInterop.Point point);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out ShellLayerInterop.Rect rectangle);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern uint GetDoubleClickTime();
    private readonly IntPtr _owner;
    private readonly uint _thread;
    private bool _mouseDown, _escapeDown;
    internal sealed record Result(string Scenario, ShellRect Before, ShellRect Held, ShellRect Released,
        ShellRect AfterCursorMove, double ReleaseMilliseconds);

    internal InputProbeDriver(IntPtr owner)
    {
        if (!OperatingSystem.IsWindows() || !Environment.UserInteractive) throw new InvalidOperationException("An interactive Windows desktop is required.");
        _thread = GetWindowThreadProcessId(owner, out uint process);
        if (_thread == 0 || process != Environment.ProcessId) throw new InvalidOperationException("The input experiment requires its own live HWND.");
        _owner = owner;
        if (GetSystemMetrics(23) != 0) throw new InvalidOperationException("This fixture currently requires an unswapped primary mouse button.");
    }

    internal static async Task WaitBetweenPressesAsync(CancellationToken token)
        => await Task.Delay(TimeSpan.FromMilliseconds(GetDoubleClickTime() + 30), token).ConfigureAwait(false);

    internal async Task<Result> RunAsync(string scenario, int startX, int startY, int dx, int dy,
        int bodyX, int bodyY, bool expectMovement, bool cancelWithEscape, CancellationToken token)
    {
        try
        {
            EnsureIdle(); token.ThrowIfCancellationRequested();
            Move(startX, startY, requirePointOwner: true); await Task.Delay(40, token).ConfigureAwait(false);
            ShellRect before = Bounds(); EnsurePoint(startX, startY); EnsureForeground();
            Mouse(2); _mouseDown = true; await Task.Delay(40, token).ConfigureAwait(false);
            if (GetAsyncKeyState(1) >= 0) throw new InvalidOperationException("Windows did not report the injected primary button held.");
            for (int step = 1; step <= 4 && expectMovement; step++)
            {
                Move(startX + dx * step / 4, startY + dy * step / 4, requirePointOwner: false);
                await Task.Delay(40, token).ConfigureAwait(false);
            }
            ShellRect held = Bounds();
            if (expectMovement && held == before) throw new InvalidOperationException("Caption input did not move the production native-frame fixture.");
            if (!expectMovement && held != before) throw new InvalidOperationException("A click unexpectedly moved the fixture.");
            if (cancelWithEscape) Escape();
            var release = Stopwatch.StartNew(); Mouse(4); _mouseDown = false;
            // Bound recovery on the worker, even if the UI is stuck in a native loop.
            while (!Released())
            {
                EnsureForeground(); token.ThrowIfCancellationRequested();
                if (release.Elapsed > TimeSpan.FromMilliseconds(250)) throw new TimeoutException("Native capture/move state survived the 250 ms primary-release observation budget.");
                await Task.Delay(10, token).ConfigureAwait(false);
            }
            double elapsed = release.Elapsed.TotalMilliseconds;
            await Task.Delay(80, token).ConfigureAwait(false);
            ShellRect released = Bounds();
            // Body point follows the fixture's final position, including Escape rollback.
            int x = released.X + bodyX, y = released.Y + bodyY;
            Move(x, y, requirePointOwner: true); await Task.Delay(40, token).ConfigureAwait(false);
            Move(x + 12, y + 8, requirePointOwner: true); await Task.Delay(40, token).ConfigureAwait(false);
            var after = Bounds();
            if (after != released) throw new InvalidOperationException("The window followed cursor movement after primary-button release.");
            if (!expectMovement && released != before) throw new InvalidOperationException("Client/caption click changed window placement.");
            EnsureIdle();
            return new(scenario, before, held, released, after, elapsed);
        }
        finally
        {
            // Release only buttons/keys this fixture inserted. Escape targets only
            // this HWND's move loop; never send cancellation to another application.
            try { if (_mouseDown) { Mouse(4); _mouseDown = false; } } catch { }
            try { if (_escapeDown) { Key(2); _escapeDown = false; } } catch { }
            try { if (GetForegroundWindow() == _owner && (Gui().Flags & 2) != 0) Escape(); } catch { }
        }
    }
    private void EnsureForeground()
    {
        if (GetForegroundWindow() != _owner || GetWindowThreadProcessId(_owner, out uint process) != _thread || process != Environment.ProcessId)
            throw new InvalidOperationException("The probe lost its foreground window; input was stopped.");
        if (new[] { 2, 4, 5, 6, 16, 17, 18, 27, 91, 92 }.Any(k => GetAsyncKeyState(k) < 0))
            throw new InvalidOperationException("External mouse/modifier input interrupted the probe.");
    }
    private void EnsureIdle()
    { EnsureForeground(); if (GetAsyncKeyState(1) < 0 || !Released()) throw new InvalidOperationException("Mouse or native capture/move state is not idle."); }
    private bool Released()
    { var info = Gui(); return GetAsyncKeyState(1) >= 0 && info.Capture == IntPtr.Zero && (info.Flags & 2) == 0; }
    private GuiInfo Gui()
    {
        var info = new GuiInfo { Size = (uint)Marshal.SizeOf<GuiInfo>() };
        if (!GetGUIThreadInfo(_thread, ref info)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Probe GUI-thread state unavailable.");
        return info;
    }
    private ShellRect Bounds()
    { EnsureForeground(); if (!GetWindowRect(_owner, out var rect)) throw new Win32Exception("Probe window bounds unavailable."); return rect.Bounds; }
    private void EnsurePoint(int x, int y)
    {
        if (NativeMethods.GetAncestor(WindowFromPoint(new() { X = x, Y = y }), 2) != _owner)
            throw new InvalidOperationException("The input point is obscured or outside the probe window.");
    }
    private void Move(int x, int y, bool requirePointOwner)
    {
        EnsureForeground(); if (requirePointOwner) EnsurePoint(x, y);
        var desktop = new ShellRect(GetSystemMetrics(76), GetSystemMetrics(77), GetSystemMetrics(78), GetSystemMetrics(79));
        var point = InputProbeCoordinates.Normalize(x, y, desktop);
        Mouse(0x0001 | 0x4000 | 0x8000, point.X, point.Y); // MOVE | VIRTUALDESK | ABSOLUTE
    }
    private void Mouse(uint flags, int x = 0, int y = 0)
        => Send([new() { Data = new() { Mouse = new() { X = x, Y = y, Flags = flags, Extra = new UIntPtr(0x4E455855) } } }]);
    private void Escape()
    { EnsureForeground(); Key(0); _escapeDown = true; Key(2); _escapeDown = false; }
    private void Key(uint flags)
        => Send([new() { Type = 1, Data = new() { Key = new() { Key = 0x1B, Flags = flags, Extra = new UIntPtr(0x4E455855) } } }]);
    private static void Send(Input[] inputs)
    {
        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows did not accept all probe input; no privilege change is attempted.");
    }
}
