using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

// Persist identifiers and user choices, never a second installed-app catalog.
public static class DesktopOrganization
{
    public const int MaximumCollections = 12, MaximumMembers = 100, MaximumHidden = 600, MaximumPlaces = 24;
    private static readonly StringComparer Targets = StringComparer.OrdinalIgnoreCase;
    private static bool Target(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 4096 && !value.Any(char.IsControl);
    public static string Name(string? value)
    {
        string text = (value ?? "").Trim();
        if (text.Length is < 1 or > 40 || text.Any(char.IsControl)) throw new ArgumentException("Use a name between 1 and 40 characters.");
        return text;
    }
    public static void Normalize(ShellState state)
    {
        state.LaunchpadOrder = state.LaunchpadOrder is "Name" or "Catalog" ? state.LaunchpadOrder : "Name";
        state.DesktopIconOrder = state.DesktopIconOrder is "Name" or "Kind" or "Newest" ? state.DesktopIconOrder : "Name";
        state.AppCollections = (state.AppCollections ?? []).Where(g => g is not null && Guid.TryParseExact(g.Id, "N", out _)
                && !string.IsNullOrWhiteSpace(g.Name) && !g.Name.Any(char.IsControl))
            .Select(g => g with { Id = Guid.ParseExact(g.Id, "N").ToString("N"), Name = g.Name.Trim()[..Math.Min(40, g.Name.Trim().Length)],
                Targets = (g.Targets ?? []).Where(Target).Distinct(Targets).Take(MaximumMembers).ToList() })
            .DistinctBy(g => g.Id).DistinctBy(g => g.Name, Targets).Take(MaximumCollections).ToList();
        state.HiddenAppTargets = (state.HiddenAppTargets ?? []).Where(Target).Distinct(Targets).Take(MaximumHidden).ToList();
        state.Places = (state.Places ?? []).Where(p => p is not null && Guid.TryParseExact(p.Id, "N", out _)
                && !string.IsNullOrWhiteSpace(p.Name) && !p.Name.Any(char.IsControl))
            .Select(p => TryPlacePath(p.Path, out var path) ? p with { Id = Guid.ParseExact(p.Id, "N").ToString("N"), Name = p.Name.Trim()[..Math.Min(40, p.Name.Trim().Length)], Path = path } : null)
            .OfType<PinnedPlace>()
            .DistinctBy(p => p.Id).DistinctBy(p => p.Path, Targets).Take(MaximumPlaces).ToList();
    }
    public static AppCollection CreateCollection(ShellState state, string name)
    {
        name = Name(name);
        if (state.AppCollections.Count >= MaximumCollections) throw new InvalidOperationException("You can keep up to 12 collections.");
        if (state.AppCollections.Any(g => Targets.Equals(g.Name, name))) throw new ArgumentException("A collection already has that name.");
        var group = new AppCollection(Guid.NewGuid().ToString("N"), name, []); state.AppCollections.Add(group); return group;
    }
    public static void RenameCollection(ShellState state, string id, string name)
    {
        name = Name(name); int index = state.AppCollections.FindIndex(g => g.Id == id);
        if (index < 0) throw new InvalidOperationException("This collection no longer exists.");
        if (state.AppCollections.Any(g => g.Id != id && Targets.Equals(g.Name, name))) throw new ArgumentException("A collection already has that name.");
        state.AppCollections[index] = state.AppCollections[index] with { Name = name };
    }
    public static void ToggleMember(ShellState state, string id, string target)
    {
        if (!Target(target)) throw new ArgumentException("Choose an available app.");
        var group = state.AppCollections.FirstOrDefault(g => g.Id == id) ?? throw new InvalidOperationException("This collection no longer exists.");
        if (group.Targets.RemoveAll(t => Targets.Equals(t, target)) != 0) return;
        if (group.Targets.Count >= MaximumMembers) throw new InvalidOperationException("Each collection holds up to 100 apps.");
        group.Targets.Add(target);
    }
    public static void SetHidden(ShellState state, string target, bool hidden)
    {
        if (!Target(target)) throw new ArgumentException("Choose an available app.");
        if (!hidden) { state.HiddenAppTargets.RemoveAll(t => Targets.Equals(t, target)); return; }
        if (state.HiddenAppTargets.Contains(target, Targets)) return;
        if (state.HiddenAppTargets.Count >= MaximumHidden) throw new InvalidOperationException("The hidden-app list is full. Restore some apps first.");
        state.HiddenAppTargets.Add(target);
    }
    public static IReadOnlyList<AppEntry> Apps(IEnumerable<AppEntry> catalog, ShellState state, string section, string query)
    {
        IEnumerable<AppEntry> input = section == "Recent" ? state.RememberRecentItems ? state.RecentApps : [] : catalog;
        var hidden = state.HiddenAppTargets.ToHashSet(Targets);
        input = input.Where(a => section == "Hidden" ? hidden.Contains(a.Target) : !hidden.Contains(a.Target));
        var group = state.AppCollections.FirstOrDefault(g => section == "collection:" + g.Id);
        if (group is not null) { var members = group.Targets.ToHashSet(Targets); input = input.Where(a => members.Contains(a.Target)); }
        else if (section.StartsWith("collection:", StringComparison.Ordinal)) return [];
        else if (section == "Pinned") input = state.PinnedApps.Where(a => !hidden.Contains(a.Target));
        else if (section is not ("All" or "Recent" or "Hidden")) input = input.Where(a => Category(a) == section);
        query = query.Trim(); input = input.Where(a => query.Length == 0 || a.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || (a.Category ?? "").Contains(query, StringComparison.CurrentCultureIgnoreCase));
        if (state.LaunchpadOrder == "Name" && section is not ("Recent" or "Pinned")) input = input.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(a => a.Target, Targets);
        return input.DistinctBy(a => a.Target, Targets).Take(600).ToArray();
    }
    private static string Category(AppEntry app) => app.Category switch { "System" => "System", "Development" => "Development", "Game" => "Games", "Utility" => "Tools", _ => "Apps" };
    public static bool MovePin(List<AppEntry> pins, string target, int direction)
    {
        int from = pins.FindIndex(p => Targets.Equals(p.Target, target)), to = from + Math.Sign(direction);
        if (from < 0 || to < 0 || to >= pins.Count || from == to) return false;
        (pins[from], pins[to]) = (pins[to], pins[from]); return true;
    }
    public static bool ValidPlacePath(string? path) => TryPlacePath(path, out _);
    private static bool TryPlacePath(string? path, out string normalized)
    {
        normalized = "";
        if (!Target(path) || !Path.IsPathFullyQualified(path!)) return false;
        // Windows accepts both separators. Check device prefixes before and
        // after canonicalization, including paths from older settings files.
        static bool Device(string value) => value.Replace('/', '\\') is var windows
            && (windows.StartsWith("\\\\?\\", StringComparison.Ordinal) || windows.StartsWith("\\\\.\\", StringComparison.Ordinal));
        if (Device(path!)) return false;
        try
        {
            string full = Path.GetFullPath(path!);
            if (Device(full)) return false;
            normalized = Path.TrimEndingDirectorySeparator(full); return true;
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }
    // Existence checks belong on a worker at the moment of adding/opening, not settings load.
    public static PinnedPlace AddPlace(ShellState state, string name, string path)
    {
        name = Name(name);
        if (!TryPlacePath(path, out var normalized)) throw new ArgumentException("Choose a full folder path.");
        path = normalized;
        var existing = state.Places.FirstOrDefault(p => Targets.Equals(p.Path, path)); if (existing is not null) return existing;
        if (state.Places.Count >= MaximumPlaces) throw new InvalidOperationException("Places holds up to 24 favorite folders.");
        var place = new PinnedPlace(Guid.NewGuid().ToString("N"), name, path); state.Places.Add(place); return place;
    }
    public static void RenamePlace(ShellState state, string id, string name)
    { name = Name(name); int index = state.Places.FindIndex(p => p.Id == id); if (index >= 0) state.Places[index] = state.Places[index] with { Name = name }; }
    public static bool MovePlace(ShellState state, string id, int direction)
    {
        int from = state.Places.FindIndex(p => p.Id == id), to = from + Math.Sign(direction);
        if (from < 0 || to < 0 || to >= state.Places.Count || from == to) return false;
        (state.Places[from], state.Places[to]) = (state.Places[to], state.Places[from]); return true;
    }
}
