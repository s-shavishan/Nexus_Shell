namespace Nexus.Shell.Services;

// Only the observed Desktop Window Manager helper is excluded. This does
// not blacklist an entire Windows process or legitimate user windows.
public static class DockPinnedWindowPolicy
{
    public static bool IsShellArtifact(string? title, string? process)
        => string.Equals(process, "dwm", StringComparison.OrdinalIgnoreCase)
        && string.Equals(title?.Trim(), "DWM Notification Window", StringComparison.OrdinalIgnoreCase);

    public static bool Matches(string? target, string? name, string? title, string? process)
    {
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(process)) return false;
        if (target.StartsWith("nexus:", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(process, "nexus", StringComparison.OrdinalIgnoreCase)) return false;
            string? expected = target.ToLowerInvariant() switch
            {
                "nexus:calculator" => "Calculator", "nexus:notes" => "Notes",
                "nexus:files" => "Files", "nexus:controls" => "Nexus Controls",
                "nexus:jobs" => "File jobs", _ => null
            };
            return expected is not null && string.Equals(expected, title, StringComparison.OrdinalIgnoreCase);
        }
        if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
        string last = target.Replace('\\', '/').Split('/').Last();
        return last.Length > 4 && string.Equals(last[..^4], process, StringComparison.OrdinalIgnoreCase);
    }
}
