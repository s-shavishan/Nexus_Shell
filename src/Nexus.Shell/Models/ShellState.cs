namespace Nexus.Shell.Models;

public sealed record AppEntry(string Id, string Name, string Target, string Glyph, string Category = "App");
public sealed record RunningWindow(IntPtr Handle, string Title, string ProcessName);
public sealed record ActivityEntry(DateTimeOffset Time, string Message);
public sealed record SavedItem(string Id, string Title, string Target, string Kind, string Collection = "Personal", bool Favorite = false);
public sealed record TaskEntry(string Id, string Title, bool Completed = false);
public sealed record CommandEntry(string Title, string Subtitle, string Glyph, string Kind, string Target);

public sealed class ShellState
{
    public string DisplayName { get; set; } = "Shan";
    public bool UsageTracking { get; set; }
    public bool FullScreen { get; set; }
    public bool FocusMode { get; set; }
    public bool ReducedEffects { get; set; }
    public bool CatalogInitialized { get; set; }
    public string Wallpaper { get; set; } = "Orbit";
    public string QuickNote { get; set; } = "";
    public int FocusMinutes { get; set; } = 25;
    public string FocusDay { get; set; } = "";
    public int FocusCompleted { get; set; }
    public double FocusRemainingSeconds { get; set; } = -1;
    public string FocusTaskId { get; set; } = "";
    public List<TaskEntry> Tasks { get; set; } = [];
    public List<SavedItem> SavedItems { get; set; } = [];
    public List<AppEntry> PinnedApps { get; set; } = [];
    public Dictionary<string, double> UsageSeconds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ActivityEntry> Activity { get; set; } = [];

    // Snapshot on the UI thread before handing persistence to a worker thread.
    public ShellState Snapshot() => new()
    {
        DisplayName = DisplayName, UsageTracking = UsageTracking, FullScreen = FullScreen,
        FocusMode = FocusMode, ReducedEffects = ReducedEffects, CatalogInitialized = CatalogInitialized,
        Wallpaper = Wallpaper, QuickNote = QuickNote, FocusMinutes = FocusMinutes,
        FocusDay = FocusDay, FocusCompleted = FocusCompleted, SavedItems = [.. SavedItems],
        FocusRemainingSeconds = FocusRemainingSeconds, FocusTaskId = FocusTaskId, Tasks = [.. Tasks],
        PinnedApps = [.. PinnedApps], Activity = [.. Activity],
        UsageSeconds = new(UsageSeconds, StringComparer.OrdinalIgnoreCase)
    };
}
