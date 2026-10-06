using Nexus.Shell.Interop;
using Nexus.Shell.Models;
using System.Diagnostics;

namespace Nexus.Shell.Services;

public sealed class UsageTracker(ShellState state)
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private string? _previous;
    public bool Tick()
    {
        double seconds = _clock.Elapsed.TotalSeconds;
        _clock.Restart();
        // The sampling method reports approximate foreground time, not process uptime.
        // A long gap represents sleep, debugging, or an interrupted UI thread.
        var current = state.UsageTracking && NativeMethods.IdleSeconds() < 90 ? NativeMethods.ForegroundProcessName() : null;
        bool changed = current is not null && current == _previous && seconds <= 10 &&
            (state.UsageSeconds.ContainsKey(current) || state.UsageSeconds.Count < 300);
        if (changed)
            state.UsageSeconds[current!] = state.UsageSeconds.GetValueOrDefault(current!) + seconds;
        _previous = current;
        return changed;
    }
    public void ResetSample() { _clock.Restart(); _previous = null; }
}
