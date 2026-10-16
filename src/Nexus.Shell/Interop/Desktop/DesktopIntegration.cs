// නමෝ බුද්ධාය 🤍
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Nexus.Shell.Interop;

// Window messages provide invocation and notification-area controls without a
// keyboard hook, helper process, or polling loop. All methods run on the UI thread.
internal sealed class DesktopIntegration : IDisposable
{
    internal const uint SummonMessage = 0x8000 + 41;
    internal const uint LaunchpadMessage = 0x8000 + 43;
    internal const uint StartUnavailableMessage = 0x8000 + 44;
    private const uint TrayMessage = 0x8000 + 42;
    private const uint SubclassId = 0x4E58;
    private readonly IntPtr _window;
    private readonly SubclassProc _callback;
    private readonly Action<string> _dispatch;
    private readonly Action<Services.WindowsDesktopChange>? _systemChanged;
    private readonly uint _taskbarCreated;
    private IntPtr _icon;
    private IntPtr _powerNotifications;
    private bool _hooked, _disposed, _trayRequested;
    private const int SearchHotkey = 0x4E53;
    internal bool SearchHotkeyAvailable { get; private set; }
    internal bool SleepNotificationsAvailable => _powerNotifications != IntPtr.Zero;
    internal static int NotificationDataSize => Marshal.SizeOf<NotifyData>();
    internal static int NotificationIconOffset => (int)Marshal.OffsetOf<NotifyData>(nameof(NotifyData.Icon));
    internal bool TrayAvailable { get; private set; }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr SubclassProc(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data);
    [DllImport("comctl32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(IntPtr window, SubclassProc callback, UIntPtr id, UIntPtr data);
    [DllImport("comctl32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(IntPtr window, SubclassProc callback, UIntPtr id);
    [DllImport("comctl32.dll", ExactSpelling = true)]
    private static extern IntPtr DefSubclassProc(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr instance, string path, uint type, int width, int height, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool NotifyIcon(uint message, ref NotifyData data);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr id, string text);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr owner, IntPtr parameters);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr RegisterSuspendResumeNotification(IntPtr recipient, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterSuspendResumeNotification(IntPtr registration);
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyData
    {
        public uint Size;
        public IntPtr Window;
        public uint Id, Flags, Callback;
        public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid GuidItem;
        public IntPtr BalloonIcon;
    }

    internal DesktopIntegration(IntPtr window, Action<string> dispatch, Action<Services.WindowsDesktopChange>? systemChanged = null)
    {
        _window = window; _dispatch = dispatch; _systemChanged = systemChanged; _callback = HandleMessage;
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        _hooked = SetWindowSubclass(window, _callback, new UIntPtr(SubclassId), UIntPtr.Zero);
        if (!_hooked) throw new Win32Exception("Nexus could not attach its desktop controls.");
        if (systemChanged is not null)
        {
            _powerNotifications = RegisterSuspendResumeNotification(window, 0); // DEVICE_NOTIFY_WINDOW_HANDLE
            if (_powerNotifications == IntPtr.Zero) Services.Log.Write("Sleep notification registration unavailable; standard desktop broadcasts remain active.");
        }
        SearchHotkeyAvailable = RegisterHotKey(window, SearchHotkey, 0x4003, 0x20); // no repeat, Ctrl+Alt+Space
        if (!SearchHotkeyAvailable) Services.Log.Write("Nexus Search shortcut is in use; menu-bar and desktop search remain available.");
    }
    private NotifyData Data() => new()
    {
        Size = (uint)Marshal.SizeOf<NotifyData>(), Window = _window, Id = 1,
        Flags = 1 | 2 | 4, Callback = TrayMessage, Icon = _icon,
        Tip = "Nexus", Info = "", InfoTitle = ""
    };
    internal bool SetResident(bool enabled)
    {
        if (_disposed) return false;
        _trayRequested = enabled;
        if (TrayAvailable) { var previous = Data(); NotifyIcon(2, ref previous); TrayAvailable = false; }
        if (!enabled) return true;
        if (_icon == IntPtr.Zero)
            _icon = LoadImage(IntPtr.Zero, Path.Combine(AppContext.BaseDirectory, "Assets", "Nexus.ico"), 1, 32, 32, 0x0010);
        if (_icon == IntPtr.Zero) return false;
        var data = Data();
        TrayAvailable = NotifyIcon(0, ref data);
        return TrayAvailable;
    }
    private IntPtr HandleMessage(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data)
    {
        try
        {
            if (!_disposed)
            {
                var change = Services.WindowsDesktopEvents.Classify(message, wParam.ToUInt64());
                if (change != Services.WindowsDesktopChange.None) _systemChanged?.Invoke(change);
                if (message == 0x0312 && wParam.ToUInt64() == SearchHotkey) { _dispatch("search"); return IntPtr.Zero; }
                if (message == SummonMessage) { _dispatch("show"); return IntPtr.Zero; }
                if (message == LaunchpadMessage) { _dispatch("start"); return IntPtr.Zero; }
                if (message == StartUnavailableMessage) { _dispatch("start-unavailable"); return IntPtr.Zero; }
                if (message == TrayMessage)
                {
                    uint action = unchecked((uint)lParam.ToInt64()); // legacy icon callback, deliberately no NIM_SETVERSION
                    if (action == 0x0202) _dispatch("show"); // left button up
                    else if (action is 0x0205 or 0x007B) ShowTrayMenu(); // right button up / context menu
                    return IntPtr.Zero;
                }
                if (_taskbarCreated != 0 && message == _taskbarCreated)
                {
                    // Managed sessions do not request a tray icon, but still
                    // need desktop anchoring and work-area recovery after Explorer.
                    _systemChanged?.Invoke(Services.WindowsDesktopChange.Shell);
                    if (_trayRequested)
                    {
                        TrayAvailable = false;
                        if (!SetResident(true)) _dispatch("tray-lost");
                    }
                }
            }
        }
        catch (Exception ex) { Services.Log.Write("Desktop control message failed", ex); }
        var result = DefSubclassProc(window, message, wParam, lParam);
        return !_disposed && _systemChanged is not null && message == 0x0218
            && Services.WindowsDesktopEvents.Classify(message, wParam.ToUInt64()) != Services.WindowsDesktopChange.None
            ? new IntPtr(1) : result;
    }
    private void ShowTrayMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;
        try
        {
            AppendMenu(menu, 0, new UIntPtr(1), "Nexus Controls");
            AppendMenu(menu, 0, new UIntPtr(2), "Launchpad");
            AppendMenu(menu, 0, new UIntPtr(3), "Windows calendar");
            AppendMenu(menu, 0, new UIntPtr(4), "Windows notifications");
            AppendMenu(menu, 0x0800, UIntPtr.Zero, "");
            AppendMenu(menu, 0, new UIntPtr(5), "About Nexus");
            if (!GetCursorPos(out var point)) return;
            SetForegroundWindow(_window);
            uint chosen = TrackPopupMenuEx(menu, 0x0100 | 0x0002, point.X, point.Y, _window, IntPtr.Zero);
            PostMessage(_window, 0, UIntPtr.Zero, IntPtr.Zero);
            string? command = chosen switch { 1 => "show", 2 => "start", 3 => "windows-calendar", 4 => "windows-notifications", 5 => "about", _ => null };
            if (command is not null) _dispatch(command);
        }
        finally { DestroyMenu(menu); }
    }
    public void Dispose()
    {
        if (_disposed) return;
        SetResident(false); _disposed = true;
        if (_powerNotifications != IntPtr.Zero) { UnregisterSuspendResumeNotification(_powerNotifications); _powerNotifications = IntPtr.Zero; }
        if (SearchHotkeyAvailable) { UnregisterHotKey(_window, SearchHotkey); SearchHotkeyAvailable = false; }
        if (_hooked) { RemoveWindowSubclass(_window, _callback, new UIntPtr(SubclassId)); _hooked = false; }
        if (_icon != IntPtr.Zero) { DestroyIcon(_icon); _icon = IntPtr.Zero; }
    }
}
