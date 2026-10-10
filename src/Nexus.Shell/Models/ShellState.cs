namespace Nexus.Shell.Models;

public sealed record AppEntry(string Id, string Name, string Target, string Glyph, string Category = "App");
public sealed record RunningWindow(IntPtr Handle, string Title, string ProcessName, int ProcessId = 0);
public sealed record ActivityEntry(DateTimeOffset Time, string Message);
public sealed record SavedItem(string Id, string Title, string Target, string Kind, string Collection = "Personal", bool Favorite = false,
    string SpaceId = "personal", string Note = "", string Tags = "");
public sealed record ExploreSpace(string Id, string Name, string Description, string Accent = "Iris");
public sealed record TaskEntry(string Id, string Title, bool Completed = false);
public sealed record RecentCommand(string Kind, string Target);
public sealed record CommandEntry(string Title, string Subtitle, string Glyph, string Kind, string Target);

public sealed record WorkspaceProfile(string Id, string Name, string Description, string Page, string Glyph,
    List<AppEntry> Apps, List<string> SavedItemIds);
public sealed record WorkspaceLaunchItem(string Title, string Target, string Kind);

public sealed class ShellState
{
    public long PersistenceRevision { get; set; }
    public string PersistenceCommitId { get; set; } = "";
    public string DisplayName { get; set; } = "Shan";
    public bool UsageTracking { get; set; }
    public bool FullScreen { get; set; }
    public bool FocusMode { get; set; }
    public bool ReducedEffects { get; set; }
    public bool CatalogInitialized { get; set; }
    public string Wallpaper { get; set; } = "Midnight";
    public bool NativeGlass { get; set; } = true;
    public List<ExploreSpace> ExploreSpaces { get; set; } = [];
    public string ActiveExploreSpaceId { get; set; } = "personal";
    public string ExploreSelectedItemId { get; set; } = "";
    public string ExploreView { get; set; } = "Board";
    public string ExploreQuery { get; set; } = "";
    public string ExploreCollection { get; set; } = "";
    public bool ExploreFavoritesOnly { get; set; }
    public bool Clock24Hour { get; set; } = true;
    public bool ShowClockWidget { get; set; } = true;
    public bool ShowSpaceWidget { get; set; } = true;
    public bool ShowHomeNotes { get; set; } = true;
    public bool ShowHomeEssentials { get; set; } = true;
    public bool CompactDock { get; set; }
    public bool FloatingTaskbar { get; set; } = true;
    public bool DockPreviews { get; set; } = true;
    public bool RememberRecentItems { get; set; } = true;
    public List<RecentCommand> RecentCommands { get; set; } = [];
    public bool DesktopLayout { get; set; } = true;
    public bool KeepAvailable { get; set; }
    public bool ResumeWorkspace { get; set; } = true;
    public string LastPage { get; set; } = "Home";
    public string ActiveProfileId { get; set; } = "personal";
    public List<WorkspaceProfile> Profiles { get; set; } = [];
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
        PersistenceRevision = PersistenceRevision, PersistenceCommitId = PersistenceCommitId,
        DisplayName = DisplayName, UsageTracking = UsageTracking, FullScreen = FullScreen,
        FocusMode = FocusMode, ReducedEffects = ReducedEffects, CatalogInitialized = CatalogInitialized,
        Clock24Hour = Clock24Hour, ShowClockWidget = ShowClockWidget, ShowSpaceWidget = ShowSpaceWidget,
        ShowHomeNotes = ShowHomeNotes, ShowHomeEssentials = ShowHomeEssentials, CompactDock = CompactDock, FloatingTaskbar = FloatingTaskbar, DockPreviews = DockPreviews,
        RememberRecentItems = RememberRecentItems, RecentCommands = [.. RecentCommands],
        ExploreSpaces = [.. ExploreSpaces], ActiveExploreSpaceId = ActiveExploreSpaceId,
        ExploreSelectedItemId = ExploreSelectedItemId, ExploreView = ExploreView,
        ExploreQuery = ExploreQuery, ExploreCollection = ExploreCollection, ExploreFavoritesOnly = ExploreFavoritesOnly,
        NativeGlass = NativeGlass, DesktopLayout = DesktopLayout, KeepAvailable = KeepAvailable,
        ResumeWorkspace = ResumeWorkspace, LastPage = LastPage, ActiveProfileId = ActiveProfileId,
        Profiles = Profiles.Select(p => p with { Apps = [.. p.Apps], SavedItemIds = [.. p.SavedItemIds] }).ToList(),
        Wallpaper = Wallpaper, QuickNote = QuickNote, FocusMinutes = FocusMinutes,
        FocusDay = FocusDay, FocusCompleted = FocusCompleted, SavedItems = [.. SavedItems],
        FocusRemainingSeconds = FocusRemainingSeconds, FocusTaskId = FocusTaskId, Tasks = [.. Tasks],
        PinnedApps = [.. PinnedApps], Activity = [.. Activity],
        UsageSeconds = new(UsageSeconds, StringComparer.OrdinalIgnoreCase)
    };
}
