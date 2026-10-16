// නමෝ බුද්ධාය 🤍
using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

// Only observed Window Manager helpers are excluded; ordinary windows must
// remain discoverable regardless of their display name.
public static class DockPinnedWindowPolicy
{
    public static bool IsShellArtifact(string? title, string? process)
        => string.Equals(process, "dwm", StringComparison.OrdinalIgnoreCase)
        && string.Equals(title?.Trim(), "DWM Notification Window", StringComparison.OrdinalIgnoreCase);

    public static string ActionLabel(string name, bool running, bool foreground, bool minimized)
        => !running ? "Open " + name
            : minimized ? "Restore " + name
            : foreground ? "Minimize " + name
            : "Switch to " + name;

    // The pin represents one app, but each additional document HWND must
    // remain reachable from its context menu. Deduplicate by native identity
    // and cap the flyout without making a broad process-name-only claim.
    public static IReadOnlyList<RunningWindow> MatchingWindows(
        string? target, string? name, IEnumerable<RunningWindow> candidates,
        int maximum = 12)
    {
        if (maximum is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(maximum));
        return candidates.Where(window => window.Handle != IntPtr.Zero && window.ProcessId > 0
            && Matches(target, name, window.Title, window.ProcessName))
            .DistinctBy(window => (window.Handle, window.ProcessId))
            .Take(maximum).ToArray();
    }

    public static bool Matches(string? target, string? name, string? title, string? process)
    {
        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(process)) return false;
        if (target.StartsWith("nexus:", StringComparison.OrdinalIgnoreCase))
        {
            // Nexus Files is a separate supervised Nexus.Shell.exe worker,
            // not an in-process utility. Its native caption changes to
            // "<folder> · Nexus Files" during navigation; the old policy
            // expected a process named "nexus" and a fixed title "Files",
            // so the pinned Files icon could NEVER own its running entry.
            if (target.Equals("nexus:files", StringComparison.OrdinalIgnoreCase))
                return string.Equals(process, "Nexus.Shell", StringComparison.OrdinalIgnoreCase)
                    && (string.Equals(title, "My files · Nexus", StringComparison.OrdinalIgnoreCase)
                        || (title?.EndsWith(" · Nexus Files", StringComparison.OrdinalIgnoreCase) ?? false));
            if (!string.Equals(process, "nexus", StringComparison.OrdinalIgnoreCase)) return false;
            string? expected = target.ToLowerInvariant() switch
            {
                "nexus:calculator" => "Calculator", "nexus:notes" => "Notes",
                "nexus:controls" => "Nexus Controls",
                "nexus:monitor" => "System Monitor",
                "nexus:jobs" => "File jobs", _ => null
            };
            return expected is not null && string.Equals(expected, title, StringComparison.OrdinalIgnoreCase);
        }
        if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
        string last = target.Replace('\\', '/').Split('/').Last();
        if (last.Length > 4 && string.Equals(last[..^4], process, StringComparison.OrdinalIgnoreCase))
            return true;

        // Windows 10/11 System32 calc.exe is a launcher, whereas its
        // packaged window belongs to CalculatorApp.exe. Match this one known
        // Windows identity translation; never use fuzzy title-only matching
        // or a user-provided generic "calc.exe" from another directory.
        string normalized = target.Replace('\\', '/');
        return string.Equals(name, "Calculator", StringComparison.OrdinalIgnoreCase)
            && normalized.EndsWith("/Windows/System32/calc.exe", StringComparison.OrdinalIgnoreCase)
            && string.Equals(process, "CalculatorApp", StringComparison.OrdinalIgnoreCase);
    }
}
