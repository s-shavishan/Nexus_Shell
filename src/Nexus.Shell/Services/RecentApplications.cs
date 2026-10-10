using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

public static class RecentApplications
{
    public const int Limit = 12;
    public static List<AppEntry> Normalize(IEnumerable<AppEntry>? apps) => (apps ?? [])
        .Where(app => app is not null && !string.IsNullOrWhiteSpace(app.Name) && app.Name.Length <= 200
            && !string.IsNullOrWhiteSpace(app.Target) && app.Target.Length <= 32767)
        .DistinctBy(app => app.Target, StringComparer.OrdinalIgnoreCase).Take(Limit)
        .Select(app => app with { Category = app.Category ?? "App", Glyph = string.IsNullOrEmpty(app.Glyph) ? "\uE8A5" : app.Glyph }).ToList();
    public static void Record(ShellState state, AppEntry app)
    {
        if (!state.RememberRecentItems) { state.RecentApps.Clear(); return; }
        state.RecentApps = Normalize(new[] { app }.Concat(state.RecentApps));
    }
}
