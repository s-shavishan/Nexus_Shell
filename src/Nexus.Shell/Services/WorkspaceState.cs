using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

// Pure, bounded migration rules shared by persistence and the CI checks.
public static class WorkspaceState
{
    public const int MaximumItems = 100;
    public static string CollectionName(string? value)
    {
        string name = value?.Trim() ?? "";
        return name.Length == 0 ? "Personal" : name[..Math.Min(24, name.Length)];
    }
    public static void Normalize(ShellState state)
    {
        state.FocusMinutes = state.FocusMinutes is 5 or 25 or 50 ? state.FocusMinutes : 25;
        state.FocusRemainingSeconds = double.IsFinite(state.FocusRemainingSeconds) && state.FocusRemainingSeconds >= 0
            ? Math.Min(state.FocusRemainingSeconds, state.FocusMinutes * 60) : -1;
        state.Tasks = (state.Tasks ?? []).Where(t => t is not null && !string.IsNullOrWhiteSpace(t.Id) &&
                t.Id.Length <= 64 && !string.IsNullOrWhiteSpace(t.Title))
            .DistinctBy(t => t.Id, StringComparer.Ordinal).Take(MaximumItems)
            .Select(t => t with { Title = t.Title.Trim()[..Math.Min(t.Title.Trim().Length, 160)] }).ToList();
        if (!state.Tasks.Any(t => !t.Completed && t.Id == state.FocusTaskId)) state.FocusTaskId = "";
        state.SavedItems = ExploreWorkspace.NormalizeItems(state.SavedItems);
    }
}
