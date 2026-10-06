namespace Nexus.Shell.Models;

public sealed record AppEntry(string Id, string Name, string Target, string Glyph, string Category = "App");
public sealed record RunningWindow(IntPtr Handle, string Title, string ProcessName);
public sealed record ActivityEntry(DateTimeOffset Time, string Message);

public sealed class ShellState
{
    public string DisplayName { get; set; } = "Shan";
    public bool UsageTracking { get; set; }
    public bool FullScreen { get; set; }
    public bool FocusMode { get; set; }
    public bool ReducedEffects { get; set; }
    public bool CatalogInitialized { get; set; }
    public List<AppEntry> PinnedApps { get; set; } = [];
    public Dictionary<string, double> UsageSeconds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ActivityEntry> Activity { get; set; } = [];

    // Snapshot on the UI thread before handing persistence to a worker thread.
    public ShellState Snapshot() => new()
    {
        DisplayName = DisplayName, UsageTracking = UsageTracking, FullScreen = FullScreen,
        FocusMode = FocusMode, ReducedEffects = ReducedEffects, CatalogInitialized = CatalogInitialized,
        PinnedApps = [.. PinnedApps], Activity = [.. Activity],
        UsageSeconds = new(UsageSeconds, StringComparer.OrdinalIgnoreCase)
    };
}
