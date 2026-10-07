using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

public static class CommandSearch
{
    public static CommandEntry[] Filter(IEnumerable<CommandEntry> entries, string query, int limit = 30)
    {
        if (limit <= 0) return [];
        query = query.Trim();
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return entries.Where(entry => terms.All(term =>
                entry.Title.Contains(term, StringComparison.CurrentCultureIgnoreCase) ||
                entry.Subtitle.Contains(term, StringComparison.CurrentCultureIgnoreCase)))
            .OrderBy(entry => query.Length == 0 ? 0 :
                entry.Title.StartsWith(query, StringComparison.CurrentCultureIgnoreCase) ? 0 :
                entry.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ? 1 : 2)
            .Take(limit).ToArray();
    }
}
