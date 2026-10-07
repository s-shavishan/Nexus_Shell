using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

public static class DesktopWorkspace
{
    public const int MaximumProfiles = 8;
    public const int MaximumApps = 8;
    public const int MaximumSavedItems = 8;
    public static List<WorkspaceProfile> Defaults() =>
    [
        new("personal", "Personal", "Your everyday essentials", "Home", "\uE80F", [], []),
        new("study", "Study", "Make room for your A/L studies", "Study", "\uE916", [], []),
        new("coding", "Coding", "Build your next idea", "Apps", "\uE943", [], []),
        new("entertainment", "Entertainment", "Games, music and a little time off", "Gaming", "\uE7FC", [], [])
    ];

    public static bool IsAppTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target) || target.Length > 4096) return false;
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && !uri.IsFile)
            return uri.Scheme is "https" or "http" || target.Equals("ms-settings:", StringComparison.OrdinalIgnoreCase);
        return Path.IsPathFullyQualified(target) && Path.GetExtension(target).ToLowerInvariant() is ".exe" or ".lnk";
    }

    public static void Normalize(ShellState state)
    {
        var savedIds = state.SavedItems.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        state.Profiles = (state.Profiles ?? []).Where(p => p is not null && !string.IsNullOrWhiteSpace(p.Id) &&
                !string.IsNullOrWhiteSpace(p.Name)).DistinctBy(p => p.Id, StringComparer.Ordinal).Take(MaximumProfiles)
            .Select(p => p with
            {
                Id = p.Id[..Math.Min(64, p.Id.Length)], Name = p.Name.Trim()[..Math.Min(32, p.Name.Trim().Length)],
                Description = (p.Description ?? "")[..Math.Min(160, (p.Description ?? "").Length)],
                Page = p.Page is "Home" or "Study" or "Explore" or "Apps" or "Gaming" ? p.Page : "Home",
                Glyph = string.IsNullOrEmpty(p.Glyph) ? "\uE80F" : p.Glyph[..Math.Min(2, p.Glyph.Length)],
                Apps = (p.Apps ?? []).Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Name) && IsAppTarget(a.Target))
                    .DistinctBy(a => a.Target, StringComparer.OrdinalIgnoreCase).Take(MaximumApps)
                    .Select(a => a with { Name = a.Name[..Math.Min(100, a.Name.Length)],
                        Glyph = string.IsNullOrEmpty(a.Glyph) ? "\uE8A5" : a.Glyph[..Math.Min(2, a.Glyph.Length)] }).ToList(),
                SavedItemIds = (p.SavedItemIds ?? []).Where(savedIds.Contains).Distinct().Take(MaximumSavedItems).ToList()
            }).DistinctBy(p => p.Id, StringComparer.Ordinal).ToList();
        if (state.Profiles.Count == 0) state.Profiles = Defaults();
        if (!state.Profiles.Any(p => p.Id == state.ActiveProfileId)) state.ActiveProfileId = state.Profiles[0].Id;
        state.LastPage = state.LastPage is "Home" or "Study" or "Explore" or "Apps" or "Gaming" or "Activity" or "Running apps" or "Workspaces" or "Personalize"
            ? state.LastPage : "Home";
    }

    // Producing a plan never launches anything. The UI asks before opening it.
    public static WorkspaceLaunchItem[] Plan(WorkspaceProfile profile, IEnumerable<SavedItem> saved)
    {
        var items = profile.Apps.Where(a => IsAppTarget(a.Target)).Take(MaximumApps)
            .Select(a => new WorkspaceLaunchItem(a.Name, a.Target, "App"));
        var selected = profile.SavedItemIds.ToHashSet(StringComparer.Ordinal);
        return items.Concat(saved.Where(s => selected.Contains(s.Id)).Take(MaximumSavedItems)
                .Select(s => new WorkspaceLaunchItem(s.Title, s.Target, s.Kind)))
            .DistinctBy(i => i.Target, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
