using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

public static class LaunchpadCatalog
{
    public const int PageSize = 24;
    public static IReadOnlyList<AppEntry> Build(IEnumerable<AppEntry> builtins, IEnumerable<AppEntry> pins, IEnumerable<AppEntry> discovered)
        => builtins.Concat(pins).Concat(discovered).Where(app => app is not null && !string.IsNullOrWhiteSpace(app.Name) && !string.IsNullOrWhiteSpace(app.Target))
            .DistinctBy(app => app.Target, StringComparer.OrdinalIgnoreCase).Take(600).ToArray();
    public static IReadOnlyList<AppEntry> Filter(IEnumerable<AppEntry> apps, string query, string category)
        => apps.Where(app => (category == "All" || Category(app) == category)
            && (query.Length == 0 || app.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) || (app.Category ?? "").Contains(query, StringComparison.CurrentCultureIgnoreCase))).ToArray();
    public static string Category(AppEntry app) => app.Category switch
    { "System" => "System", "Development" => "Development", "Game" => "Games", "Utility" => "Tools", _ => "Apps" };
    public static IReadOnlyList<AppEntry> Page(IReadOnlyList<AppEntry> items, int page)
        => items.Skip(Math.Clamp(page, 0, Math.Max(0, (items.Count - 1) / PageSize)) * PageSize).Take(PageSize).ToArray();
}
