using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

public static class ShellExperience
{
    public const int RecentLimit = 8;
    public static readonly string[] Categories = ["All", "Apps", "Saved", "Workspaces", "Actions", "Tasks", "Windows"];
    public static bool MatchesCategory(CommandEntry entry, string category) => category switch
    {
        "Apps" => entry.Kind == "App", "Saved" => entry.Kind is "Saved" or "Space",
        "Workspaces" => entry.Kind is "Workspace" or "Profile", "Actions" => entry.Kind == "Action",
        "Tasks" => entry.Kind == "Task", "Windows" => entry.Kind == "Window", _ => true
    };
    private static bool CanRemember(RecentCommand? entry) => entry is not null &&
        entry.Kind is "App" or "Saved" or "Space" or "Workspace" or "Profile" &&
        !string.IsNullOrWhiteSpace(entry.Target) && entry.Target.Length <= 4096;
    public static List<RecentCommand> NormalizeRecent(IEnumerable<RecentCommand>? entries) =>
        (entries ?? []).Where(CanRemember).Distinct().Take(RecentLimit).ToList();
    public static void Normalize(ShellState state)
    {
        state.RecentCommands = state.RememberRecentItems ? NormalizeRecent(state.RecentCommands) : [];
    }
    public static void Remember(ShellState state, CommandEntry entry)
    {
        var reference = new RecentCommand(entry.Kind, entry.Target);
        if (!state.RememberRecentItems || !CanRemember(reference)) return;
        state.RecentCommands = NormalizeRecent(new[] { reference }.Concat(state.RecentCommands));
    }
    public static CommandEntry[] Search(IEnumerable<CommandEntry> source, string query, string category,
        IEnumerable<RecentCommand>? recent, bool remember, int limit = 30)
    {
        if (limit <= 0) return [];
        var entries = source.DistinctBy(e => new RecentCommand(e.Kind, e.Target)).ToArray();
        var filtered = entries.Where(e => MatchesCategory(e, category));
        if (!remember || !string.IsNullOrWhiteSpace(query) || category != "All")
            return CommandSearch.Filter(filtered, query, limit);

        // Resolve references against today's catalog; stale entries never become launch targets.
        var byReference = entries.ToDictionary(e => new RecentCommand(e.Kind, e.Target));
        var resolved = NormalizeRecent(recent).Where(byReference.ContainsKey)
            .Select(r => byReference[r] with { Subtitle = "Recent · " + byReference[r].Subtitle }).ToArray();
        var used = resolved.Select(e => new RecentCommand(e.Kind, e.Target)).ToHashSet();
        return resolved.Concat(filtered.Where(e => !used.Contains(new(e.Kind, e.Target)))).Take(limit).ToArray();
    }
}
