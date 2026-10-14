namespace Nexus.Shell.Services;

public enum WindowsShellSurface { Calendar, Notifications, QuickSettings, Clipboard, TaskView }
public readonly record struct ShellKey(ushort Key, bool Up);
public sealed record WindowsShellRoute(string Name, string Shortcut, string Fallback, ushort[] Keys);
public enum WindowsShellRequestState { Requested, ShellUnavailable, KeysHeld, InputRejected, PartialInput }
public interface IWindowsShortcutSender
{ bool ShellAvailable { get; } bool ModifiersUp { get; } bool KeyUp(ushort key); int Send(IReadOnlyList<ShellKey> keys); }

// Windows owns these panels. Sending a documented shortcut requests a panel;
// it cannot prove the panel rendered or override Windows policy/UIPI.
public static class WindowsShellPolicy
{
    public static WindowsShellRoute Route(WindowsShellSurface surface, int build) => surface switch
    {
        WindowsShellSurface.Calendar when build >= 22000 => new("Windows calendar & notifications", "Win+N", "ms-settings:dateandtime", [0x5B, 0x4E]),
        WindowsShellSurface.Calendar => new("Windows date & time", "Win+Alt+D", "ms-settings:dateandtime", [0x5B, 0x12, 0x44]),
        WindowsShellSurface.Notifications when build >= 22000 => new("Windows notifications & calendar", "Win+N", "ms-settings:notifications", [0x5B, 0x4E]),
        WindowsShellSurface.Notifications => new("Windows Action Center", "Win+A", "ms-settings:notifications", [0x5B, 0x41]),
        WindowsShellSurface.QuickSettings => new(build >= 22000 ? "Windows Quick Settings" : "Windows Action Center", "Win+A", "ms-settings:", [0x5B, 0x41]),
        WindowsShellSurface.Clipboard => new("Windows clipboard history", "Win+V", "ms-settings:clipboard", [0x5B, 0x56]),
        WindowsShellSurface.TaskView => new("Windows Task View", "Win+Tab", "ms-settings:multitasking", [0x5B, 0x09]),
        _ => throw new ArgumentOutOfRangeException(nameof(surface))
    };
    public static WindowsShellRequestState Request(WindowsShellRoute route, IWindowsShortcutSender sender)
    {
        if (!sender.ShellAvailable) return WindowsShellRequestState.ShellUnavailable;
        if (!sender.ModifiersUp || route.Keys.Any(key => !sender.KeyUp(key))) return WindowsShellRequestState.KeysHeld;
        ShellKey[] keys = route.Keys.Select(k => new ShellKey(k, false)).Concat(route.Keys.Reverse().Select(k => new ShellKey(k, true))).ToArray();
        int sent = sender.Send(keys);
        if (sent == keys.Length) return WindowsShellRequestState.Requested;
        if (sent <= 0) return WindowsShellRequestState.InputRejected;
        // Release only keys from the successfully injected prefix, never
        // arbitrary user modifiers. Never repeat a panel-toggle shortcut.
        var pressed = new List<ushort>();
        foreach (var key in keys.Take(Math.Min(sent, keys.Length))) { if (key.Up) pressed.Remove(key.Key); else pressed.Add(key.Key); }
        if (pressed.Count > 0) sender.Send(pressed.AsEnumerable().Reverse().Select(k => new ShellKey(k, true)).ToArray());
        return WindowsShellRequestState.PartialInput;
    }
}
