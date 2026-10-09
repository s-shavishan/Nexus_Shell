using Nexus.Shell.Interop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Diagnostics;

internal static class DesktopNativeChecks
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WindowProc(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size, Style;
        public WindowProc Procedure;
        public int ClassExtra, WindowExtra;
        public IntPtr Instance, Icon, Cursor, Background;
        public string? MenuName;
        public string ClassName;
        public IntPtr SmallIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Message
    {
        public IntPtr Window;
        public uint Id;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Point Point;
        public uint Private;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClass value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClass(string name, IntPtr instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateWindowEx(uint extended, string className,
        string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DefWindowProc(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PeekMessage(out Message message, IntPtr window, uint minimum, uint maximum, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DispatchMessage(ref Message message);
    internal static void Run()
    {
        string className = "NexusCheck" + Guid.NewGuid().ToString("N");
        WindowProc callback = DefWindowProc;
        var definition = new WindowClass { Size = (uint)Marshal.SizeOf<WindowClass>(), Procedure = callback,
            Style = 0, ClassExtra = 0, WindowExtra = 0, Instance = GetModuleHandle(null),
            Icon = IntPtr.Zero, Cursor = IntPtr.Zero, Background = IntPtr.Zero, MenuName = null, ClassName = className, SmallIcon = IntPtr.Zero };
        if (RegisterClassEx(ref definition) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr window = IntPtr.Zero;
        try
        {
            window = CreateWindowEx(0, className, "Nexus desktop control check", 0x00CF0000, 100, 100, 240, 160, IntPtr.Zero, IntPtr.Zero, definition.Instance, IntPtr.Zero);
            if (window == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (NativeMethods.Activate(new RunningWindow(window, "Wrong owner", "test", Environment.ProcessId + 1)))
                throw new Exception("Taskbar/window activation must reject a stale process identity.");
            if (NativeMethods.ToggleDockWindow(new(window, "Wrong owner", "test", Environment.ProcessId + 1))
                || NativeMethods.Minimize(new(window, "Wrong owner", "test", Environment.ProcessId + 1))
                || NativeMethods.MaximizeOrRestore(new(window, "Wrong owner", "test", Environment.ProcessId + 1))
                || NativeMethods.RequestClose(new(window, "Wrong owner", "test", Environment.ProcessId + 1)))
                throw new Exception("Dock minimize, toggle, maximize and close must reject stale process identities.");
            var layouts = new WindowLayouts();
            if (!GetWindowRect(window, out var original)) throw new Exception("The native window bounds could not be read.");
            bool rejected = false;
            try { layouts.Arrange([new RunningWindow(window, "Wrong process", "test", Environment.ProcessId + 1)], "Columns"); }
            catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new Exception("The native layout must reject a handle whose process no longer matches.");
            layouts.Arrange([new RunningWindow(window, "Own test window", "test", Environment.ProcessId)], "Columns");
            if (!layouts.CanUndo || !GetWindowRect(window, out var arranged) || arranged.Equals(original))
                throw new Exception("The native layout must place its own test window in a usable monitor area.");
            if (layouts.Undo() != 1 || !GetWindowRect(window, out var restored) || !restored.Equals(original))
                throw new Exception("Undo must restore the test window's original geometry.");
            Console.WriteLine("PASS: actual Win32 window placement, process ownership rejection and undo (own test window only).");
            var actions = new List<string>();
            using (var integration = new DesktopIntegration(window, actions.Add))
            {
                if (!DesktopIntegration.PostMessage(window, DesktopIntegration.SummonMessage, UIntPtr.Zero, IntPtr.Zero))
                    throw new Exception("The desktop invocation message was not posted.");
                Pump(window);
                if (!actions.SequenceEqual(new[] { "show" })) throw new Exception("The native subclass must deliver desktop invocation exactly once.");
                if (!integration.SetResident(false) || integration.TrayAvailable) throw new Exception("Disabling resident mode must remove its tray registration.");
            }
            actions.Clear();
            DesktopIntegration.PostMessage(window, DesktopIntegration.SummonMessage, UIntPtr.Zero, IntPtr.Zero); Pump(window);
            if (actions.Count != 0) throw new Exception("Disposed desktop controls must no longer receive invocation.");
            Console.WriteLine("PASS: real Win32 desktop subclass, invocation, disabled controls, and cleanup (own test window).");
            var events = new List<uint>();
            var identity = new RunningWindow(window, "Own test window", "test", Environment.ProcessId);
            using (var observer = new WindowEventObserver(change => { if (change.Window == window) events.Add(change.Kind); }, includeOwnProcess: true))
            {
                GC.Collect(); GC.WaitForPendingFinalizers();
                ShowWindow(window, 5);
                if (!NativeMethods.Minimize(identity) || !PumpUntil(() => NativeMethods.IsMinimized(window) && events.Contains(0x0016)))
                    throw new Exception("Native minimize must deliver a window event and retain the same live HWND.");
                if (!NativeMethods.Activate(identity) || !PumpUntil(() => !NativeMethods.IsMinimized(window) && events.Contains(0x0017)))
                    throw new Exception("Native restore must deliver an event and restore the minimized window.");
                ShowWindow(window, 3);
                if (!PumpUntil(() => NativeMethods.IsZoomed(window))) throw new Exception("The test window must maximize.");
                NativeMethods.Minimize(identity);
                if (!PumpUntil(() => NativeMethods.IsMinimized(window))) throw new Exception("The maximized test window must minimize.");
                NativeMethods.Activate(identity);
                if (!PumpUntil(() => !NativeMethods.IsMinimized(window) && NativeMethods.IsZoomed(window)))
                    throw new Exception("Dock restore must retain a window's previous maximized placement.");
                ShowWindow(window, 1);
            }
            int observed = events.Count;
            NativeMethods.Minimize(identity);
            if (!PumpUntil(() => NativeMethods.IsMinimized(window)) || events.Count != observed)
                throw new Exception("Disposed event observers must not receive later minimize events.");
            NativeMethods.Activate(identity); PumpUntil(() => !NativeMethods.IsMinimized(window));
            Console.WriteLine("PASS: actual Win32 minimize/restore, maximized placement, WinEvent delivery after GC and observer cleanup (own test window only).");
            if (!NativeMethods.MaximizeOrRestore(identity) || !PumpUntil(() => NativeMethods.IsZoomed(window)))
                throw new Exception("Dock maximize must maximize its own test window.");
            if (!NativeMethods.MaximizeOrRestore(identity) || !PumpUntil(() => !NativeMethods.IsZoomed(window)))
                throw new Exception("Dock restore size must restore its own test window's normal bounds.");
            NativeMethods.Minimize(identity); PumpUntil(() => NativeMethods.IsMinimized(window));
            if (!NativeMethods.MaximizeOrRestore(identity) || !PumpUntil(() => !NativeMethods.IsMinimized(window) && NativeMethods.IsZoomed(window)))
                throw new Exception("Maximize from an iconic window must not be undone by a subsequent restore command.");
            ShowWindow(window, 9);
            CheckThumbnail(className, definition.Instance, identity);
            if (!NativeMethods.RequestClose(identity) || !PumpUntil(() => !NativeMethods.OwnsWindow(identity)))
                throw new Exception("WM_CLOSE must reach the test window's procedure and destroy that window normally.");
            Console.WriteLine("PASS: native dock maximize/restore-size/maximize-from-minimized and graceful WM_CLOSE (own test window only).");
            window = IntPtr.Zero;
        }
        finally
        {
            if (window != IntPtr.Zero) DestroyWindow(window);
            UnregisterClass(className, definition.Instance);
            GC.KeepAlive(callback);
        }
    }
    private static void CheckThumbnail(string className, IntPtr instance, RunningWindow source)
    {
        if (Marshal.SizeOf<WindowThumbnail.Properties>() != 48
            || Marshal.OffsetOf<WindowThumbnail.Properties>(nameof(WindowThumbnail.Properties.Visible)).ToInt32() != 40
            || Marshal.OffsetOf<WindowThumbnail.Properties>(nameof(WindowThumbnail.Properties.ClientOnly)).ToInt32() != 44)
            throw new Exception("DWM thumbnail properties must preserve DWORD/RECT/BYTE/Win32 BOOL layout.");
        if (!WindowThumbnail.CompositionAvailable())
        { Console.WriteLine("SKIP: native DWM thumbnail relationship; composition is unavailable in this Windows session. Static card fallback remains required."); return; }
        var destination = CreateWindowEx(0x08000080, className, "Nexus thumbnail destination check", 0x00CF0000,
            350, 100, 320, 240, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
        if (destination == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            ShowWindow(destination, 4);
            using var rejected = WindowThumbnail.TryCreate(destination, source with { ProcessId = Environment.ProcessId + 1 });
            if (rejected is not null || WindowThumbnail.TryCreate(source.Handle, source) is not null)
                throw new Exception("A thumbnail must reject a stale source and self-reference.");
            using var thumbnail = WindowThumbnail.TryCreate(destination, source);
            if (thumbnail is null || !thumbnail.IsAttached || !thumbnail.Update(new(12, 36, 240, 140)))
                throw new Exception("DWM must register and update a relationship between two own top-level test windows.");
            thumbnail.Dispose(); thumbnail.Dispose();
            if (thumbnail.IsAttached || thumbnail.Update(new(12, 36, 240, 140)))
                throw new Exception("Thumbnail release must be idempotent and prevent further native updates.");
            Console.WriteLine("PASS: native DWM thumbnail registration, source sizing, update, rejection and disposal (own test windows only; rendered WinUI preview remains a manual check).");
        }
        finally { DestroyWindow(destination); }
    }
    private static void Pump(IntPtr window)
    {
        for (int i = 0; i < 100 && PeekMessage(out var message, window, 0, 0, 1); i++) DispatchMessage(ref message);
    }
    private static bool PumpUntil(Func<bool> completed)
    {
        var timeout = Stopwatch.StartNew();
        do { Pump(IntPtr.Zero); if (completed()) return true; Thread.Sleep(10); } while (timeout.Elapsed < TimeSpan.FromSeconds(3));
        return false;
    }
}
