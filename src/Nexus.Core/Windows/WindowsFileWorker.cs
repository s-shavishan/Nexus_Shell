// නමෝ බුද්ධාය 🤍
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using Nexus.Runtime;

namespace Nexus.Core.Windows;

// Shell extensions are untrusted in-process code. This helper has its own STA,
// owner HWND and message loop; Core supervises only this process, never Explorer.
internal sealed class WindowsFileWorker
{
    private readonly Process _core;
    private readonly WindowsFileAction _request;
    private readonly WindowProc _callback;
    private IntPtr _window;
    private IContextMenu2? _menu2;
    private IContextMenu3? _menu3;
    private bool _dispatching = true;
    private DateTime _lingerUntil;
    private WindowsFileWorker(Process core, WindowsFileAction request) { _core = core; _request = request; _callback = Message; }
    [SupportedOSPlatform("windows")]
    internal static int Run(string[] args, Action<string, Exception?> log)
    {
        try
        {
            if (args.Length != 3 || args[1] != "--core-pid" || !int.TryParse(args[2], out int id) || id <= 0 || id == Environment.ProcessId) throw new ArgumentException("Start this action through Nexus Core.");
            using var core = Process.GetProcessById(id); using var own = Process.GetCurrentProcess();
            if (core.HasExited || core.SessionId != own.SessionId || !string.Equals(core.MainModule?.FileName, Path.Combine(AppContext.BaseDirectory, "Nexus.Core.exe"), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The Windows file-action owner is invalid.");
            _ = core.Handle;
            string line = Console.ReadLine() ?? throw new InvalidDataException("The Windows file request is missing.");
            if (line.Length > 768 * 1024) throw new InvalidDataException("The Windows file request is too large.");
            var request = WindowsFileActionCoordinator.Validate(JsonSerializer.Deserialize<WindowsFileAction>(line)!);
            if (request.OwnerProcessId <= 0 || GetWindowThreadProcessId(new IntPtr(request.Owner), out uint caller) == 0 || caller != request.OwnerProcessId) throw new InvalidOperationException("The source window closed or changed owners.");
            if (!File.Exists(request.Path) && !Directory.Exists(request.Path)) throw new FileNotFoundException("This item is no longer available.");
            Exception? failure = null;
            var thread = new Thread(() => { try { new WindowsFileWorker(core, request).Execute(); } catch (Exception error) { failure = error; } });
            thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
            if (failure is not null) throw failure;
            return 0;
        }
        catch (Exception error) { log("Windows file action failed", error); return 1; }
    }
    [SupportedOSPlatform("windows")]
    private void Execute()
    {
        IntPtr previousDpi = SetThreadDpiAwarenessContext(new IntPtr(-4)); // Per-monitor V2; coordinates originate in WinUI's physical screen space.
        if (previousDpi == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        int initialized = CoInitializeEx(IntPtr.Zero, 2); Marshal.ThrowExceptionForHR(initialized);
        IntPtr pidl = IntPtr.Zero; object? folder = null, context = null; IntPtr popup = IntPtr.Zero;
        try
        {
            using var ready = EventWaitHandle.OpenExisting("Local\\WhiteDreams.Nexus.WindowsFile." + _request.Id.ToString("N"));
            using var admitted = EventWaitHandle.OpenExisting("Local\\WhiteDreams.Nexus.WindowsFile." + _request.Id.ToString("N") + ".admit");
            void Admit() { ready.Set(); if (!admitted.WaitOne(30000) || _core.HasExited) throw new TimeoutException("Core did not admit this Windows file command."); }
            string className = "Nexus.WindowsFile." + Environment.ProcessId;
            var kind = new WindowClass { Size = (uint)Marshal.SizeOf<WindowClass>(), Procedure = _callback, Instance = GetModuleHandle(null), Name = className };
            if (RegisterClassEx(ref kind) == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            _window = CreateWindowEx(0x00080080, className, "Windows file options", 0x80000000, _request.X, _request.Y, 1, 1, new IntPtr(_request.Owner), IntPtr.Zero, kind.Instance, IntPtr.Zero);
            if (_window == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            SetLayeredWindowAttributes(_window, 0, 1, 2); ShowWindow(_window, 5); SetForegroundWindow(_window); SetTimer(_window, new UIntPtr(1), 250, IntPtr.Zero);
            if (_request.Kind == "recycle")
            {
                WindowsRecycleOperation.Run([_request.Path, .. _request.AdditionalPaths ?? []], _window, Admit);
            }
            else if (_request.Kind == "reveal")
            {
                Marshal.ThrowExceptionForHR(SHParseDisplayName(_request.Path, IntPtr.Zero, out pidl, 0, out _));
                Admit(); Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(pidl, 0, [], 0)); // Absolute single-item form selects it in its parent.
            }
            else if (_request.Kind == "open-with")
            {
                if (Directory.Exists(_request.Path)) throw new InvalidOperationException("Choose a file for Open with.");
                var open = new OpenAs { File = _request.Path, Flags = 4 }; // OAIF_EXEC; no forced association registration.
                Admit();
                int result = SHOpenWithDialog(_window, ref open); if (result != unchecked((int)0x800704C7)) Marshal.ThrowExceptionForHR(result);
            }
            else
            {
                Marshal.ThrowExceptionForHR(SHParseDisplayName(_request.Path, IntPtr.Zero, out pidl, 0, out _));
                Guid folderId = typeof(IShellFolder).GUID; Marshal.ThrowExceptionForHR(SHBindToParent(pidl, ref folderId, out folder, out IntPtr child));
                Guid menuId = typeof(IContextMenu).GUID;
                Marshal.ThrowExceptionForHR(((IShellFolder)folder).GetUIObjectOf(_window, 1, [child], ref menuId, IntPtr.Zero, out context));
                var menu = (IContextMenu)context; _menu2 = context as IContextMenu2; _menu3 = context as IContextMenu3;
                popup = CreatePopupMenu(); if (popup == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                Marshal.ThrowExceptionForHR(menu.QueryContextMenu(popup, 0, 1, 0x7FFF, 0));
                Admit();
                uint command = _request.Kind == "menu" ? TrackPopupMenuEx(popup, 0x0100 | 0x0002, _request.X, _request.Y, _window, IntPtr.Zero) : 0;
                if (_request.Kind == "properties" || command != 0)
                {
                    IntPtr verb = _request.Kind == "properties" ? Marshal.StringToHGlobalAnsi("properties") : new IntPtr(command - 1);
                    try
                    {
                        var invoke = new InvokeInfo { Size = (uint)Marshal.SizeOf<InvokeInfo>(), Mask = 0x100, Owner = _window, Verb = verb, Show = 1 }; // CMIC_MASK_NOASYNC
                        IntPtr memory = Marshal.AllocHGlobal(Marshal.SizeOf<InvokeInfo>());
                        try { Marshal.StructureToPtr(invoke, memory, false); Marshal.ThrowExceptionForHR(menu.InvokeCommand(memory)); }
                        finally { Marshal.FreeHGlobal(memory); }
                    }
                    finally { if (_request.Kind == "properties") Marshal.FreeHGlobal(verb); }
                }
                PostMessage(_window, 0, UIntPtr.Zero, IntPtr.Zero);
            }
            // Some property/extension dialogs are modeless on this STA. Keep
            // COM and their message loop alive until those visible windows close.
            _dispatching = false;
            _lingerUntil = DateTime.UtcNow.AddMilliseconds(750);
            while ((DateTime.UtcNow < _lingerUntil || HasDialogs()) && !_core.HasExited)
            { int status = GetMessage(out var message, IntPtr.Zero, 0, 0); if (status < 0) throw new Win32Exception(Marshal.GetLastWin32Error()); if (status == 0) break; TranslateMessage(ref message); DispatchMessage(ref message); }
        }
        finally
        {
            _menu2 = null; _menu3 = null;
            if (popup != IntPtr.Zero) DestroyMenu(popup);
            if (context is not null) Marshal.FinalReleaseComObject(context);
            if (folder is not null && !ReferenceEquals(folder, context)) Marshal.FinalReleaseComObject(folder);
            if (pidl != IntPtr.Zero) Marshal.FreeCoTaskMem(pidl);
            if (_window != IntPtr.Zero) DestroyWindow(_window);
            CoUninitialize();
            SetThreadDpiAwarenessContext(previousDpi);
        }
    }
    private bool HasDialogs()
    { bool found = false; EnumWindows((window, _) => { if (window != _window && IsWindowVisible(window) && GetWindowThreadProcessId(window, out uint owner) != 0 && owner == Environment.ProcessId) found = true; return true; }, IntPtr.Zero); return found; }
    private IntPtr Message(IntPtr window, uint message, UIntPtr wp, IntPtr lp)
    {
        try
        {
            if (message == 0x0113 && (_core.HasExited || !_dispatching && DateTime.UtcNow >= _lingerUntil && !HasDialogs())) { PostQuitMessage(0); return IntPtr.Zero; }
            if (message is 0x0117 or 0x002B or 0x002C or 0x0120)
            {
                if (_menu3 is not null && _menu3.HandleMenuMsg2(message, wp, lp, out IntPtr result) == 0) return result;
                if (message != 0x0120 && _menu2 is not null && _menu2.HandleMenuMsg(message, wp, lp) == 0) return message is 0x002B or 0x002C ? new IntPtr(1) : IntPtr.Zero;
            }
        }
        catch { }
        return DefWindowProc(window, message, wp, lp);
    }
    internal static (int Invoke, int OpenAs, int WindowClass) Layouts => (Marshal.SizeOf<InvokeInfo>(), Marshal.SizeOf<OpenAs>(), Marshal.SizeOf<WindowClass>());
    [ComImport, Guid("000214E6-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IShellFolder
    {
        [PreserveSig] int ParseDisplayName(IntPtr owner, IntPtr bind, IntPtr name, IntPtr eaten, IntPtr item, IntPtr attrs);
        [PreserveSig] int EnumObjects(IntPtr owner, uint flags, IntPtr result);
        [PreserveSig] int BindToObject(IntPtr item, IntPtr bind, ref Guid id, IntPtr result);
        [PreserveSig] int BindToStorage(IntPtr item, IntPtr bind, ref Guid id, IntPtr result);
        [PreserveSig] int CompareIDs(IntPtr flags, IntPtr first, IntPtr second);
        [PreserveSig] int CreateViewObject(IntPtr owner, ref Guid id, IntPtr result);
        [PreserveSig] int GetAttributesOf(uint count, IntPtr items, IntPtr attrs);
        [PreserveSig] int GetUIObjectOf(IntPtr owner, uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] items, ref Guid id, IntPtr reserved, [MarshalAs(UnmanagedType.Interface)] out object result);
        [PreserveSig] int GetDisplayNameOf(IntPtr item, uint flags, IntPtr result);
        [PreserveSig] int SetNameOf(IntPtr owner, IntPtr item, IntPtr name, uint flags, IntPtr result);
    }
    [ComImport, Guid("000214E4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IContextMenu
    { [PreserveSig] int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags); [PreserveSig] int InvokeCommand(IntPtr info); [PreserveSig] int GetCommandString(UIntPtr command, uint flags, IntPtr reserved, IntPtr name, uint size); }
    [ComImport, Guid("000214F4-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IContextMenu2
    { [PreserveSig] int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags); [PreserveSig] int InvokeCommand(IntPtr info); [PreserveSig] int GetCommandString(UIntPtr command, uint flags, IntPtr reserved, IntPtr name, uint size); [PreserveSig] int HandleMenuMsg(uint message, UIntPtr wp, IntPtr lp); }
    [ComImport, Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IContextMenu3
    { [PreserveSig] int QueryContextMenu(IntPtr menu, uint index, uint first, uint last, uint flags); [PreserveSig] int InvokeCommand(IntPtr info); [PreserveSig] int GetCommandString(UIntPtr command, uint flags, IntPtr reserved, IntPtr name, uint size); [PreserveSig] int HandleMenuMsg(uint message, UIntPtr wp, IntPtr lp); [PreserveSig] int HandleMenuMsg2(uint message, UIntPtr wp, IntPtr lp, out IntPtr result); }
    [StructLayout(LayoutKind.Sequential)] private struct InvokeInfo { internal uint Size, Mask; internal IntPtr Owner, Verb, Parameters, Directory; internal int Show; internal uint HotKey; internal IntPtr Icon; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct OpenAs { [MarshalAs(UnmanagedType.LPWStr)] internal string File; [MarshalAs(UnmanagedType.LPWStr)] internal string? Class; internal uint Flags; }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr WindowProc(IntPtr window, uint message, UIntPtr wp, IntPtr lp);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate bool WindowVisitor(IntPtr window, IntPtr data);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass { internal uint Size, Style; internal WindowProc Procedure; internal int ClassExtra, WindowExtra; internal IntPtr Instance, Icon, Cursor, Background; [MarshalAs(UnmanagedType.LPWStr)] internal string? Menu; [MarshalAs(UnmanagedType.LPWStr)] internal string Name; internal IntPtr SmallIcon; }
    [StructLayout(LayoutKind.Sequential)] private struct MessageData { internal IntPtr Window; internal uint Id; internal UIntPtr WParam; internal IntPtr LParam; internal uint Time; internal int X, Y; internal uint Private; }
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern int SHParseDisplayName(string name, IntPtr bind, out IntPtr pidl, uint requested, out uint attrs);
    [DllImport("shell32.dll", ExactSpelling = true)] private static extern int SHBindToParent(IntPtr pidl, ref Guid id, [MarshalAs(UnmanagedType.Interface)] out object folder, out IntPtr child);
    [DllImport("shell32.dll", ExactSpelling = true)] private static extern int SHOpenWithDialog(IntPtr owner, ref OpenAs info);
    [DllImport("shell32.dll", ExactSpelling = true)] private static extern int SHOpenFolderAndSelectItems(IntPtr folder, uint count, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] items, uint flags);
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClass kind);
    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateWindowEx(uint ex, string kind, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr module, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr window, uint color, byte alpha, uint flags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern UIntPtr SetTimer(IntPtr window, UIntPtr id, uint time, IntPtr callback);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll")] private static extern uint TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr owner, IntPtr parameters);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")] private static extern IntPtr DefWindowProc(IntPtr window, uint message, UIntPtr wp, IntPtr lp);
    [DllImport("user32.dll", EntryPoint = "PostMessageW")] private static extern bool PostMessage(IntPtr window, uint message, UIntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowVisitor visitor, IntPtr data);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", EntryPoint = "GetMessageW")] private static extern int GetMessage(out MessageData message, IntPtr window, uint first, uint last);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MessageData message);
    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")] private static extern IntPtr DispatchMessage(ref MessageData message);
}
