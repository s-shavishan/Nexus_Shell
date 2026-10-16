// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

public sealed record DesktopSearchCommand(string Id, string Name, string Detail, string Keywords);
public static class DesktopSearchCatalog
{
    private static readonly DesktopSearchCommand[] Commands =
    [
        new("launchpad", "Launchpad", "Find and launch your apps", "apps start programs"),
        new("files", "Nexus Files", "Browse folders and drives", "explorer documents downloads"),
        new("jobs", "File jobs", "Queue copies and moves with content verification", "transfer copy move queue progress cancel sha256"),
        new("monitor", "System Monitor", "Live CPU, memory, processes and drive space", "task manager performance storage disk free space end process close app ram"),
        new("places", "Places", "Favorite folders and a quick look inside", "favorites pinned folders"),
        new("windows", "Window board", "Switch windows and restore layouts", "arrange snap layout workspace"),
        new("control-center", "Control Center", "Sound, network, display and desktop preferences", "settings system pc"),
        new("sound", "Sound controls", "Volume and audio devices", "speaker microphone headphones mute audio"),
        new("network", "Network controls", "Connections and Wi-Fi", "internet wifi ethernet adapter"),
        new("display", "Display controls", "Brightness and displays", "monitor resolution screen"),
        new("bluetooth", "Bluetooth controls", "Bluetooth devices", "wireless pairing"),
        new("power", "Power controls", "Battery and power plans", "energy charging balanced performance"),
        new("controls", "Nexus Controls", "Core, personalization, desktop, privacy and startup", "settings preferences home"),
        new("appearance", "Desktop appearance", "Wallpaper, dock and glass preferences", "personalize theme effects animations"),
        new("notifications", "Windows notifications", "Open Windows notification center or Action Center", "inbox messages alerts system"),
        new("calendar", "Windows calendar", "Open Windows date and time", "date time clock timezone"),
        new("about", "About Nexus", "Version, system information and project links", "build diagnostics releases"),
        new("activity", "Nexus activity", "Nexus service alerts and local history", "inbox warnings errors"),
        new("windows-clipboard", "Windows clipboard history", "Open the Windows clipboard panel", "copy paste history win v"),
        new("task-view", "Windows Task View", "Windows desktops and open applications", "virtual desktops win tab"),
        new("clipboard", "Clipboard shelf", "Your captured text", "copy paste history"),
        new("status", "Session status", "Desktop health and recovery", "services diagnostics core stability"),
        new("guide", "Desktop shortcuts", "Nexus keyboard guide", "help keyboard hotkeys"),
        new("system", "Windows tools", "Windows utilities and temporary keep-awake controls", "system integration keep awake sleep timer")
    ];
    public static IReadOnlyList<DesktopSearchCommand> Find(string query) => Commands.Concat(WindowsToolCatalog.All.Select(tool =>
        new DesktopSearchCommand("system:" + tool.Id, tool.Name, tool.Detail, tool.Keywords)))
        .Concat(NexusControlsCatalog.Settings.Select(setting => new DesktopSearchCommand("nexus-controls:" + setting.Id, setting.Title, "Nexus Controls · " + setting.Detail, setting.Keywords)))
        .Where(command => Matches(query, command.Name + " " + command.Detail + " " + command.Keywords))
        .OrderByDescending(command => query.Trim().Length > 0 && command.Name.StartsWith(query.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
    public static bool Matches(string query, string text) => query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).All(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
}
