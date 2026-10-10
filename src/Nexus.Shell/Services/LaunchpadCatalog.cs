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
    public static bool IsPinned(IEnumerable<AppEntry> pins, AppEntry app)
        => pins.Any(pin => string.Equals(pin.Target, app.Target, StringComparison.OrdinalIgnoreCase));
    public static bool TogglePin(List<AppEntry> pins, AppEntry app)
    {
        if (string.IsNullOrWhiteSpace(app.Name) || string.IsNullOrWhiteSpace(app.Target)) throw new ArgumentException("Choose an application first.");
        if (IsPinned(pins, app)) { pins.RemoveAll(pin => string.Equals(pin.Target, app.Target, StringComparison.OrdinalIgnoreCase)); return false; }
        if (pins.Count >= 100) throw new InvalidOperationException("The dock has 100 pins. Remove a pin before adding another.");
        pins.Add(app); return true;
    }
}
