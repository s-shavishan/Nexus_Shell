namespace Nexus.Shell.Services;

public enum DesktopSessionMode { Preview, DesktopShell, NexusSession }
public enum DesktopExitCode { Stop = 0, Restart = 10, RestoreWindows = 20, SignOut = 30 }
public sealed record DesktopCapabilities(bool IsWindows, string Edition, int Build, int Revision = 0)
{
    public bool SupportsCustomInterface => IsWindows && (Build >= 19044 || (Build >= 19041 && Revision >= 1202)) &&
        (Edition.StartsWith("Professional", StringComparison.OrdinalIgnoreCase) || Edition.StartsWith("Enterprise", StringComparison.OrdinalIgnoreCase)
        || Edition.StartsWith("Education", StringComparison.OrdinalIgnoreCase) || Edition.StartsWith("IoTEnterprise", StringComparison.OrdinalIgnoreCase));
}
public sealed class ShellRestartBudget
{
    private int _failures;
    public bool MayRestart(TimeSpan uptime)
    { if (uptime >= TimeSpan.FromMinutes(5)) _failures = 0; return ++_failures <= 2; }
    public bool IsUnresponsive(TimeSpan uptime, TimeSpan sincePulse, bool receivedPulse) => receivedPulse
        ? sincePulse >= TimeSpan.FromSeconds(90) : uptime >= TimeSpan.FromSeconds(45);
}
public enum ShellKeyAction { None, Start, Files, ShowDesktop, Settings, Search, Overview, SwitchNext, SwitchPrevious, SwitchCommit, Controls }
public readonly record struct ShellKeyResult(bool Consume, ShellKeyAction Action = ShellKeyAction.None);

// Only shell shortcuts are interpreted; text input is never stored or logged.
public sealed class ShellKeyboardState
{
    private readonly HashSet<uint> _windows = [], _blocked = [];
    private readonly HashSet<uint> _alt = [];
    private bool _used, _owned, _switching;
    public ShellKeyResult Process(uint key, bool down, bool otherModifiers = false, bool shift = false, bool control = false)
    {
        if (key is 0x12 or 0xA4 or 0xA5)
        {
            if (down) { _alt.Add(key); if (_windows.Count > 0) _used = true; return new(false); }
            _alt.Remove(key);
            if (_alt.Count == 0 && _switching) { _switching = false; return new(true, ShellKeyAction.SwitchCommit); }
            return new(false);
        }
        if (key is 0x5B or 0x5C)
        {
            if (down) { if (_windows.Count == 0) { _used = otherModifiers; _owned = false; } _windows.Add(key); return new(false); }
            if (!_windows.Remove(key)) return new(false);
            if (_windows.Count > 0) return new(_owned);
            bool start = !_used && !otherModifiers;
            return new(start || _owned, start ? ShellKeyAction.Start : ShellKeyAction.None);
        }
        if (!down) return new(_blocked.Remove(key));
        if (key == 0x1B && control && !shift && _alt.Count == 0 && _windows.Count == 0)
            return new(true, _blocked.Add(key) ? ShellKeyAction.Start : ShellKeyAction.None);
        if (key == 0x09 && _alt.Count > 0 && _windows.Count == 0)
        {
            _switching = !control; bool firstTab = _blocked.Add(key);
            return new(true, firstTab ? control ? ShellKeyAction.Overview : shift ? ShellKeyAction.SwitchPrevious : ShellKeyAction.SwitchNext : ShellKeyAction.None);
        }
        if (_windows.Count == 0) return new(false);
        _used = true;
        var action = otherModifiers ? ShellKeyAction.None : key switch
        { 0x45 => ShellKeyAction.Files, 0x44 => ShellKeyAction.ShowDesktop, 0x49 => ShellKeyAction.Settings, 0x52 or 0x53 => ShellKeyAction.Search,
            0x09 => ShellKeyAction.Overview, 0x41 => ShellKeyAction.Controls, _ => ShellKeyAction.None };
        if (action == ShellKeyAction.None) return new(false); // Windows still owns lock/security shortcuts.
        _owned = true; bool first = _blocked.Add(key);
        return new(true, first ? action : ShellKeyAction.None);
    }
}
