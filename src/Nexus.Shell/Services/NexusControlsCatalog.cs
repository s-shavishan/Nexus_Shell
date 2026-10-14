using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

public sealed record NexusControlPage(string Id, string Title, string Description, string Glyph);
public sealed record NexusControlSetting(string Id, string Page, string Title, string Detail, string Keywords);

public static class NexusControlsCatalog
{
    public static IReadOnlyList<NexusControlPage> Pages { get; } = Array.AsReadOnly<NexusControlPage>([
        new("home", "Home", "Your desktop, connected.", "\uE80F"),
        new("core", "Core & recovery", "Understand the session. Keep Nexus running.", "\uE9D9"),
        new("personalize", "Personalize", "A desktop that feels like yours.", "\uE790"),
        new("desktop", "Desktop & dock", "Choose what stays within reach.", "\uE7F4"),
        new("privacy", "Privacy", "Control what Nexus remembers.", "\uE72E"),
        new("startup", "Startup & session", "Choose how your Nexus desktop begins.", "\uE7E8")
    ]);
    public static IReadOnlyList<NexusControlSetting> Settings { get; } = Array.AsReadOnly<NexusControlSetting>([
        new("services", "core", "Service health", "Core, file workers, search and desktop status", "diagnostics reconnect repair"),
        new("save", "core", "Save settings now", "Write a settings snapshot through Nexus Core", "persistence backup disk"),
        new("repair", "core", "Repair desktop layout", "Recalculate the work area, menu bar and dock", "display monitor screen geometry"),
        new("presets", "personalize", "Desktop presets", "Midnight Glass, Quiet desktop and Compact desktop, with undo", "appearance look theme reset"),
        new("palette", "personalize", "Wallpaper & palette", "Choose the colors used across your desktop", "mood background color personalize"),
        new("glass", "personalize", "Glass & motion", "Material style, quality, animations and reduced effects", "transparency performance acrylic"),
        new("name", "personalize", "Display name", "Your name on the Nexus desktop", "identity profile"),
        new("dock", "desktop", "Dock behavior", "Floating placement, compact icons and hover previews", "taskbar density size"),
        new("icons", "desktop", "Desktop icons", "Visibility and ordering for desktop items", "files sort newest kind"),
        new("clock", "desktop", "Clock", "Choose 24-hour or 12-hour time", "time format"),
        new("notifications", "desktop", "Quiet notifications", "Keep alerts in the inbox without desktop banners", "do not disturb focus"),
        new("recent", "privacy", "Recent apps & searches", "Remember or clear recent references", "history forget retention"),
        new("usage", "privacy", "Usage tracking", "Local app usage totals and activity history", "statistics clear data"),
        new("clipboard", "privacy", "Clipboard shelf", "Optional text capture held in session memory", "copy paste clear"),
        new("signin", "startup", "Startup & sign-in", "Preview, supervised desktop and Windows recovery", "boot login shell session restore")
    ]);
    public static string PageId(string? value) => value?.ToLowerInvariant() switch
    {
        "personalize" or "appearance" => "personalize", "pc controls" or "core" => "core",
        "desktop" => "desktop", "privacy" => "privacy", "startup" => "startup", _ => "home"
    };
    public static IReadOnlyList<NexusControlSetting> Find(string? query)
    {
        string bounded = (query ?? "").Trim();
        if (bounded.Length > 160) bounded = bounded[..160];
        return Settings.Where(item => DesktopSearchCatalog.Matches(bounded, item.Title + " " + item.Detail + " " + item.Keywords)).Take(20).ToArray();
    }
    public static string Target(NexusControlSetting setting) => "nexus:controls/" + setting.Page + "/" + setting.Id;
    public static bool TryRoute(string target, out string page, out string? setting)
    {
        page = "home"; setting = null;
        if (target.Equals("nexus:sections", StringComparison.OrdinalIgnoreCase) || target.Equals("nexus:settings", StringComparison.OrdinalIgnoreCase)
            || target.Equals("nexus:controls", StringComparison.OrdinalIgnoreCase)) return true;
        const string prefix = "nexus:controls/";
        if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var parts = target[prefix.Length..].Split('/');
        var found = Pages.FirstOrDefault(p => p.Id.Equals(parts[0], StringComparison.OrdinalIgnoreCase));
        if (found is null || parts.Length > 2) return false;
        page = found.Id;
        if (parts.Length == 2)
        {
            var item = Settings.FirstOrDefault(s => s.Page == found.Id && s.Id.Equals(parts[1], StringComparison.OrdinalIgnoreCase));
            if (item is null) return false;
            setting = item.Id;
        }
        return true;
    }
    public static bool MigrateShortcuts(ShellState state)
    {
        bool changed = false;
        string Target(string value)
        { if (!value.Equals("nexus:sections", StringComparison.OrdinalIgnoreCase)) return value; changed = true; return "nexus:controls"; }
        AppEntry App(AppEntry app) => app.Target.Equals("nexus:sections", StringComparison.OrdinalIgnoreCase)
            ? app with { Id = "controls", Name = "Nexus Controls", Target = Target(app.Target), Glyph = "\uE713", Category = "System" } : app;
        state.PinnedApps = state.PinnedApps.Select(App).DistinctBy(a => a.Target, StringComparer.OrdinalIgnoreCase).ToList();
        state.RecentApps = state.RecentApps.Select(App).DistinctBy(a => a.Target, StringComparer.OrdinalIgnoreCase).ToList();
        state.HiddenAppTargets = state.HiddenAppTargets.Select(Target).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        state.AppCollections = state.AppCollections.Select(g => g with { Targets = g.Targets.Select(Target).Distinct(StringComparer.OrdinalIgnoreCase).ToList() }).ToList();
        state.RecentCommands = state.RecentCommands.Select(r => r.Kind == "App" ? r with { Target = Target(r.Target) } : r).Distinct().ToList();
        return changed;
    }
    public static bool CompactNavigation(double width) => double.IsFinite(width) && width < 780;
}

// Appearance-only snapshots. Undo cannot overwrite privacy, content or startup.
public sealed record NexusAppearance(string Wallpaper, bool NativeGlass, string GlassStyle, string GlassQuality,
    bool ReducedEffects, bool SurfaceAnimations, bool FloatingTaskbar, bool CompactDock, bool DockPreviews)
{
    public static NexusAppearance Capture(ShellState s) => new(s.Wallpaper, s.NativeGlass, s.GlassStyle, s.GlassQuality,
        s.ReducedEffects, s.SurfaceAnimations, s.FloatingTaskbar, s.CompactDock, s.DockPreviews);
    public void Apply(ShellState s)
    {
        s.Wallpaper = Wallpaper; s.NativeGlass = NativeGlass; s.GlassStyle = GlassStyle; s.GlassQuality = GlassQuality;
        s.ReducedEffects = ReducedEffects; s.SurfaceAnimations = SurfaceAnimations;
        s.FloatingTaskbar = FloatingTaskbar; s.CompactDock = CompactDock; s.DockPreviews = DockPreviews;
    }
    public static NexusAppearance Preset(string id) => id switch
    {
        "midnight" => new("Midnight", true, "Balanced", "Balanced", false, true, true, false, true),
        "quiet" => new("Slate", false, "Balanced", "Solid", true, false, true, false, false),
        "compact" => new("Midnight", true, "Balanced", "Balanced", false, true, true, true, true),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
}
