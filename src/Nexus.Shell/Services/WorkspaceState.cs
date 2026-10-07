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
        state.SavedItems = (state.SavedItems ?? []).Where(a => a is not null &&
                !string.IsNullOrWhiteSpace(a.Id) && a.Id.Length <= 64 && !string.IsNullOrWhiteSpace(a.Title) &&
                !string.IsNullOrWhiteSpace(a.Target) && a.Target.Length <= 4096 &&
                (a.Kind is "Link" or "File" or "Folder") &&
                (a.Kind != "Link" || (Uri.TryCreate(a.Target, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")))
            .DistinctBy(a => a.Target, StringComparer.OrdinalIgnoreCase)
            .DistinctBy(a => a.Id, StringComparer.Ordinal).Take(MaximumItems)
            .Select(a => a with { Title = a.Title.Trim()[..Math.Min(a.Title.Trim().Length, 100)], Collection = CollectionName(a.Collection) }).ToList();
    }
}
