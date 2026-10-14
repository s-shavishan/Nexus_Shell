namespace Nexus.Shell.Services;

public enum WindowsToolKind { Settings, SystemExecutable, ScreenSnip, ControlPanelApplet }

public sealed record WindowsTool(string Id, string Name, string Detail, string Group,
    string Keywords, WindowsToolKind Kind, string Target);

// One allowlist serves the desktop panel and Spotlight. Search text never
// becomes an executable name, argument, URI or command line.
public static class WindowsToolCatalog
{
    public static IReadOnlyList<WindowsTool> All { get; } = Array.AsReadOnly(new WindowsTool[]
    {
        new("screen-snip", "Screen snip", "Choose an area with Windows Snipping Tool", "Everyday", "screenshot capture image", WindowsToolKind.ScreenSnip, "ms-screenclip:"),
        new("task-manager", "Task Manager", "Inspect running apps and resource use", "Everyday", "process cpu memory performance", WindowsToolKind.SystemExecutable, "Taskmgr.exe"),
        new("project-display", "Project display", "Choose a Windows display projection mode", "Everyday", "monitor duplicate extend second screen", WindowsToolKind.SystemExecutable, "DisplaySwitch.exe"),
        new("night-light", "Night light", "Configure color temperature and scheduled night light", "Display", "warm color screen blue light", WindowsToolKind.Settings, "ms-settings:nightlight"),
        new("network-settings", "Network settings", "Manage Ethernet, Wi-Fi and connection details", "Connections", "network connectivity internet", WindowsToolKind.Settings, "ms-settings:network-status"),
        new("network-adapters", "Network adapters", "Open classic Windows network connections", "Connections", "network ethernet control panel connections", WindowsToolKind.ControlPanelApplet, "ncpa.cpl"),
        new("bluetooth-settings", "Bluetooth & devices", "Pair devices and manage Bluetooth hardware in Windows", "Connections", "wireless radio pairing devices", WindowsToolKind.Settings, "ms-settings:bluetooth"),
        new("volume-mixer", "Volume mixer", "Open the classic Windows app volume mixer", "Everyday", "sound audio speaker", WindowsToolKind.SystemExecutable, "SndVol.exe"),
        new("installed-apps", "Installed apps", "Manage apps in Windows Settings", "Windows settings", "uninstall programs software", WindowsToolKind.Settings, "ms-settings:appsfeatures"),
        new("startup-apps", "Startup apps", "Choose which apps start when you sign in", "Windows settings", "boot login background", WindowsToolKind.Settings, "ms-settings:startupapps"),
        new("storage", "Storage settings", "Review drive space and storage options", "Windows settings", "disk cleanup space", WindowsToolKind.Settings, "ms-settings:storagesense"),
        new("default-apps", "Default apps", "Choose apps for links and file types", "Windows settings", "browser associations", WindowsToolKind.Settings, "ms-settings:defaultapps"),
        new("language", "Language & keyboard", "Manage Windows languages and input methods", "Windows settings", "typing sinhala tamil english layout", WindowsToolKind.Settings, "ms-settings:regionlanguage"),
        new("date-time", "Date & time", "Windows clock, time zone and synchronization", "Windows settings", "calendar clock timezone daylight", WindowsToolKind.Settings, "ms-settings:dateandtime"),
        new("notifications", "Notification settings", "Windows app notifications and alerts", "Windows settings", "action center do not disturb focus", WindowsToolKind.Settings, "ms-settings:notifications"),
        new("accessibility", "Accessibility", "Windows vision, hearing and interaction options", "Windows settings", "ease access contrast magnifier narrator", WindowsToolKind.Settings, "ms-settings:easeofaccess"),
        new("windows-update", "Windows Update", "Review Windows updates", "Windows settings", "security patches upgrade", WindowsToolKind.Settings, "ms-settings:windowsupdate"),
        new("resource-monitor", "Resource Monitor", "Inspect CPU, disk, network and memory activity", "System tools", "performance processes", WindowsToolKind.SystemExecutable, "resmon.exe"),
        new("system-information", "System Information", "Inspect hardware and Windows configuration", "System tools", "specs bios drivers version", WindowsToolKind.SystemExecutable, "msinfo32.exe"),
        new("control-panel", "Control Panel", "Open classic Windows configuration", "System tools", "advanced settings devices", WindowsToolKind.SystemExecutable, "control.exe")
    });

    // Keep native launch targets entirely closed: a plain System32 executable
    // or the one explicitly registered Windows Control Panel applet. User input
    // is never interpreted as an executable, argument or arbitrary CPL path.
    public static bool SafeNativeTarget(WindowsTool tool)
    {
        string target = tool.Target;
        if (string.IsNullOrWhiteSpace(target)
            || !target.All(c => char.IsAsciiLetterOrDigit(c) || c == '.')) return false;
        return tool.Kind switch
        {
            WindowsToolKind.SystemExecutable
                => target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase),
            WindowsToolKind.ControlPanelApplet
                => tool.Id == "network-adapters"
                    && string.Equals(target, "ncpa.cpl", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    public static WindowsTool? Find(string id) => All.FirstOrDefault(tool => tool.Id == id);
    public static IEnumerable<WindowsTool> Search(string query) => All.Where(tool =>
        DesktopSearchCatalog.Matches(query, $"{tool.Name} {tool.Detail} {tool.Group} {tool.Keywords}"));
}
