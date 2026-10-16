// නමෝ බුද්ධාය 🤍
using System.Collections.Concurrent;

namespace Nexus.Shell.Services;

// State and admission belong to the desktop dispatcher. Workers only publish
// immutable completions; the existing dock heartbeat drains them even if an
// optional dispatcher wake-up is rejected. Never release a native worker's
// slot before it actually completes, including after unpin/rescan.
internal sealed class DockShortcutIdentityCache : IDisposable
{
    internal const int MaximumWorkers = 2;
    internal sealed class Request(string target)
    {
        internal string Target { get; } = target;
    }
    private sealed class Entry
    {
        internal string? Resolved;
        internal Request? Pending;
        internal DockShortcutRetryState Retry;
    }
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Request> _running = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<(Request Request, string? Resolved)> _completed = new();
    private readonly object _completionGate = new();
    private bool _disposed;

    internal void Reconcile(IEnumerable<string> targets, bool invalidate = false)
    {
        if (_disposed) return;
        var live = targets.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (invalidate) _entries.Clear();
        foreach (string removed in _entries.Keys.Where(key => !live.Contains(key)).ToArray())
            _entries.Remove(removed);
        foreach (string target in live)
            _entries.TryAdd(target, new Entry());
    }

    internal bool TryGetTarget(string target, out string resolved)
    {
        if (_entries.TryGetValue(target, out var entry) && entry.Resolved is { } value)
        { resolved = value; return true; }
        resolved = "";
        return false;
    }

    internal Request? TryBegin(string target, long now)
    {
        if (_disposed || _running.Count >= MaximumWorkers || _running.ContainsKey(target)
            || !_entries.TryGetValue(target, out var entry) || entry.Resolved is not null
            || entry.Pending is not null || !entry.Retry.CanAttempt(now)) return null;
        var request = new Request(target);
        entry.Pending = request;
        _running.Add(target, request);
        return request;
    }

    // Safe on a background STA. A rejected UI wake-up cannot lose this
    // completion, mutate dispatcher-owned dictionaries or strand admission.
    internal void Publish(Request request, string? resolved)
    {
        lock (_completionGate)
        {
            if (!_disposed) _completed.Enqueue((request, resolved));
        }
    }

    internal bool Drain(long now)
    {
        bool changed = false;
        while (_completed.TryDequeue(out var completion))
        {
            var request = completion.Request;
            // Duplicate or superseded completions cannot release another
            // worker's slot or write over a newer pin generation.
            if (!_running.TryGetValue(request.Target, out var running)
                || !ReferenceEquals(running, request)) continue;
            _running.Remove(request.Target);
            if (!_entries.TryGetValue(request.Target, out var entry)
                || !ReferenceEquals(entry.Pending, request)) continue;
            entry.Pending = null;
            if (!string.IsNullOrWhiteSpace(completion.Resolved)
                && completion.Resolved.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                entry.Resolved = completion.Resolved;
                entry.Retry = default;
                changed = true;
            }
            else entry.Retry = entry.Retry.Failed(now);
        }
        return changed;
    }

    public void Dispose()
    {
        lock (_completionGate)
        {
            if (_disposed) return;
            _disposed = true;
            _completed.Clear();
        }
        _entries.Clear();
        _running.Clear();
    }
}
