using Nexus.Runtime;
using Nexus.Shell.Services;

namespace Nexus.Shell.Desktop;

internal sealed partial class DesktopEnvironment
{
    private SearchWindow? _search;
    private SearchReply? _lastSearch;
    private DateTimeOffset _lastSearchAt;
    internal string SearchServiceStatus => (_lastSearch is { } reply
        ? "Everything: " + reply.Status + (reply.EngineVersion.Length > 0 ? " · " + reply.EngineVersion : "") + " · observed " + Math.Max(0, (int)(DateTimeOffset.Now - _lastSearchAt).TotalSeconds) + "s ago. "
        : "Everything is checked when you search for a file. ") + (_integration?.SearchHotkeyAvailable == true ? "Ctrl+Alt+Space is ready." : "Use the menu-bar Search button; the global shortcut is unavailable.");
    internal void ShowSearch()
    {
        if (IsStopping) return;
        bool wasOpen = _search?.IsOpen == true; HideMenu();
        try { _search ??= new(this); if (!wasOpen) _search.Show(); }
        catch (Exception error) { Report("Nexus Search could not open", error); }
    }
    internal void SearchClosed(SearchWindow window) { if (ReferenceEquals(_search, window)) _search = null; }
    internal async Task<SearchReply> SearchFilesAsync(SearchRequest request, CancellationToken cancellation)
    {
        var reply = await _core.SearchAsync(request, cancellation);
        if (!IsStopping && !cancellation.IsCancellationRequested) { _lastSearch = reply with { Items = [], Total = 0, Offset = 0 }; _lastSearchAt = DateTimeOffset.Now; }
        return reply;
    }
    internal void RunSearchCommand(string id)
    {
        if (id.StartsWith("system:", StringComparison.Ordinal)) { RunWindowsTool(id[7..]); return; }
        if (id.StartsWith("nexus-controls:", StringComparison.Ordinal))
        { var setting = NexusControlsCatalog.Settings.FirstOrDefault(item => item.Id == id["nexus-controls:".Length..]); if (setting is not null) ShowControls(setting.Page, setting.Id); return; }
        switch (id)
        {
            case "launchpad": ShowMenu(); break;
            case "files": ShowFiles(); break;
            case "jobs": ShowUtility("File jobs"); break;
            case "places": ShowPlaces(); break;
            case "windows": ShowWindowOverview(); break;
            case "control-center": ShowQuickSettings(); break;
            case "controls": ShowControls(); break;
            case "sound": ShowControlCenter("Sound"); break;
            case "network": ShowControlCenter("Network"); break;
            case "display": ShowControlCenter("Display"); break;
            case "bluetooth": ShowControlCenter("Bluetooth"); break;
            case "power": ShowControlCenter("Power"); break;
            case "appearance": ShowControls("Personalize"); break;
            case "notifications": OpenWindowsSurface(WindowsShellSurface.Notifications); break;
            case "calendar": OpenWindowsSurface(WindowsShellSurface.Calendar); break;
            case "about": ShowAbout(); break;
            case "activity": ShowNotifications(); break;
            case "windows-clipboard": OpenWindowsSurface(WindowsShellSurface.Clipboard); break;
            case "task-view": OpenWindowsSurface(WindowsShellSurface.TaskView); break;
            case "clipboard": ShowClipboardShelf(); break;
            case "status": ShowSessionStatus(); break;
            case "guide": ShowDesktopGuide(); break;
            case "system": ShowWindowsTools(); break;
        }
    }
}
