using Nexus.Shell.Interop;
using System.ComponentModel;
using System.Runtime.InteropServices;

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
            window = CreateWindowEx(0, className, "Nexus desktop control check", 0, 0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, definition.Instance, IntPtr.Zero);
            if (window == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            var actions = new List<string>();
            using (var integration = new DesktopIntegration(window, actions.Add))
            {
                if (!DesktopIntegration.PostMessage(window, DesktopIntegration.SummonMessage, UIntPtr.Zero, IntPtr.Zero))
                    throw new Exception("The desktop invocation message was not posted.");
                Pump(window);
                if (!actions.SequenceEqual(new[] { "show" })) throw new Exception("The native subclass must deliver desktop invocation exactly once.");
                if (!integration.SetHotkey(false) || integration.HotkeyAvailable) throw new Exception("Disabling the desktop shortcut must release it.");
                if (!integration.SetResident(false) || integration.TrayAvailable) throw new Exception("Disabling resident mode must remove its tray registration.");
            }
            actions.Clear();
            DesktopIntegration.PostMessage(window, DesktopIntegration.SummonMessage, UIntPtr.Zero, IntPtr.Zero); Pump(window);
            if (actions.Count != 0) throw new Exception("Disposed desktop controls must no longer receive invocation.");
            Console.WriteLine("PASS: real Win32 desktop subclass, invocation, disabled controls, and cleanup (hidden test window).");
        }
        finally
        {
            if (window != IntPtr.Zero) DestroyWindow(window);
            UnregisterClass(className, definition.Instance);
            GC.KeepAlive(callback);
        }
    }
    private static void Pump(IntPtr window)
    {
        for (int i = 0; i < 100 && PeekMessage(out var message, window, 0, 0, 1); i++) DispatchMessage(ref message);
    }
}
