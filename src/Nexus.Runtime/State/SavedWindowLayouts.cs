// නමෝ බුද්ධාය 🤍
using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

public readonly record struct LayoutRect(int X, int Y, int Width, int Height);
public sealed record LayoutCandidate(string Key, string ProcessPath, string Title);
public sealed record LayoutMatch(int EntryIndex, string CandidateKey);

// Persistent layouts contain app identity and proportions, never native handles.
public static class SavedWindowLayouts
{
    public const int MaximumLayouts = 8, MaximumWindows = 12;
    public static void Normalize(ShellState state)
    {
        state.WindowLayouts = (state.WindowLayouts ?? []).Where(layout => layout is not null && Guid.TryParseExact(layout.Id, "N", out _))
            .DistinctBy(layout => layout.Id, StringComparer.OrdinalIgnoreCase).Take(MaximumLayouts).Select(layout => layout with
            {
                Id = layout.Id.ToLowerInvariant(),
                Name = Clean(layout.Name, 40, "My layout"),
                Windows = (layout.Windows ?? []).Where(Valid).Take(MaximumWindows).Select(entry => entry with
                {
                    ProcessPath = entry.ProcessPath.Trim(), Title = Clean(entry.Title, 512, ""), MonitorId = Clean(entry.MonitorId, 64, ""),
                    Left = Math.Clamp(entry.Left, 0, 1), Top = Math.Clamp(entry.Top, 0, 1),
                    Width = Math.Clamp(entry.Width, .05, 1), Height = Math.Clamp(entry.Height, .05, 1)
                }).ToList()
            }).Where(layout => layout.Windows.Count > 0).ToList();
    }
    private static bool Valid(SavedWindowPosition? entry) => entry is not null && !string.IsNullOrWhiteSpace(entry.ProcessPath)
        && entry.ProcessPath.Length <= 4096 && !entry.ProcessPath.Contains('\0')
        && double.IsFinite(entry.Left) && double.IsFinite(entry.Top) && double.IsFinite(entry.Width) && double.IsFinite(entry.Height)
        && entry.Width > 0 && entry.Height > 0;
    private static string Clean(string? text, int maximum, string fallback)
    { text = (text ?? "").Replace('\0', ' ').Trim(); return text.Length == 0 ? fallback : text[..Math.Min(maximum, text.Length)]; }
    public static SavedWindowPosition Capture(string path, string title, string monitor, LayoutRect window, LayoutRect work, bool maximized)
    {
        if (work.Width < 1 || work.Height < 1 || window.Width < 1 || window.Height < 1) throw new ArgumentException("The window or display has no usable area.");
        var entry = new SavedWindowPosition(path, title, monitor,
            (window.X - (double)work.X) / work.Width, (window.Y - (double)work.Y) / work.Height,
            window.Width / (double)work.Width, window.Height / (double)work.Height, maximized);
        if (!Valid(entry)) throw new ArgumentException("The window identity is unavailable.");
        return entry with { Title = Clean(title, 512, ""), Left = Math.Clamp(entry.Left, 0, 1), Top = Math.Clamp(entry.Top, 0, 1),
            Width = Math.Clamp(entry.Width, .05, 1), Height = Math.Clamp(entry.Height, .05, 1) };
    }
    public static LayoutRect Project(SavedWindowPosition entry, LayoutRect work)
    {
        if (!Valid(entry) || work.Width < 1 || work.Height < 1 || (long)work.X + work.Width > int.MaxValue || (long)work.Y + work.Height > int.MaxValue)
            throw new ArgumentException("The saved window or display is invalid.");
        int width = Math.Clamp((int)Math.Round(Math.Clamp(entry.Width, .05, 1) * work.Width), Math.Min(240, work.Width), work.Width);
        int height = Math.Clamp((int)Math.Round(Math.Clamp(entry.Height, .05, 1) * work.Height), Math.Min(160, work.Height), work.Height);
        // Left/top are proportions of the work area, not of remaining free space.
        int x = Math.Clamp(work.X + (int)Math.Round(Math.Clamp(entry.Left, 0, 1) * work.Width), work.X, work.X + work.Width - width);
        int y = Math.Clamp(work.Y + (int)Math.Round(Math.Clamp(entry.Top, 0, 1) * work.Height), work.Y, work.Y + work.Height - height);
        return new(x, y, width, height);
    }
    public static IReadOnlyList<LayoutMatch> Match(IReadOnlyList<SavedWindowPosition> saved, IReadOnlyList<LayoutCandidate> current)
    {
        var result = new List<LayoutMatch>(); var used = new HashSet<string>(StringComparer.Ordinal);
        var candidates = current.Where(c => !string.IsNullOrWhiteSpace(c.Key) && !string.IsNullOrWhiteSpace(c.ProcessPath))
            .DistinctBy(c => c.Key).ToArray();
        for (int i = 0; i < Math.Min(saved.Count, MaximumWindows); i++)
        {
            var entry = saved[i]; if (!Valid(entry)) continue;
            bool SameApp(string path) => string.Equals(path, entry.ProcessPath, StringComparison.OrdinalIgnoreCase);
            var app = candidates.Where(c => SameApp(c.ProcessPath)).ToArray();
            var exact = app.Where(c => c.Title == entry.Title).ToArray();
            var sameSaved = saved.Count(s => Valid(s) && SameApp(s.ProcessPath));
            bool duplicateTitle = saved.Count(s => Valid(s) && SameApp(s.ProcessPath) && s.Title == entry.Title) != 1;
            LayoutCandidate? match = exact.Length == 1 && !duplicateTitle ? exact[0]
                : exact.Length == 0 && app.Length == 1 && sameSaved == 1 ? app[0] : null;
            if (match is not null && used.Add(match.Key)) result.Add(new(i, match.Key));
        }
        return result;
    }
}
