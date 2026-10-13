namespace Nexus.Shell.Services;

// Each EnumWindows pass owns only the destructions that happened BEFORE it.
// Later native events belong to the following scan, even if they arrive while
// the worker is enumerating or while WinUI is applying the previous result.
// A failed scan restores its batch; an event storm forces a bounded full reset.
internal readonly record struct DockLifecycleBatch(
    long Generation, IReadOnlySet<IntPtr> Destroyed, bool ResetAll);

internal sealed class DockLifecycleJournal
{
    internal const int MaxTrackedHandles = 2048;
    private readonly object _gate = new();
    private readonly HashSet<IntPtr> _pending = [];
    private bool _resetPending;
    private long _nextGeneration, _activeGeneration;

    internal bool HasPending
    {
        get { lock (_gate) return _resetPending || _pending.Count != 0; }
    }

    internal void RecordDestroy(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        lock (_gate)
        {
            if (_resetPending) return;
            _pending.Add(handle);
            if (_pending.Count > MaxTrackedHandles)
            {
                _pending.Clear();
                _resetPending = true;
            }
        }
    }

    internal DockLifecycleBatch BeginScan()
    {
        lock (_gate)
        {
            if (_activeGeneration != 0)
                throw new InvalidOperationException("A window enumeration is already using this lifecycle journal.");
            long generation = unchecked(++_nextGeneration);
            if (generation == 0) generation = unchecked(++_nextGeneration);
            _activeGeneration = generation;
            var batch = new DockLifecycleBatch(generation,
                new HashSet<IntPtr>(_pending), _resetPending);
            _pending.Clear();
            _resetPending = false;
            return batch;
        }
    }

    // Return false for an obsolete/double completion; its caller must not
    // release or requeue another scan's batch.
    internal bool CompleteScan(DockLifecycleBatch batch, bool applied)
    {
        lock (_gate)
        {
            if (_activeGeneration == 0 || batch.Generation != _activeGeneration) return false;
            _activeGeneration = 0;
            if (!applied && !_resetPending)
            {
                if (batch.ResetAll) { _pending.Clear(); _resetPending = true; }
                else
                {
                    _pending.UnionWith(batch.Destroyed);
                    if (_pending.Count > MaxTrackedHandles)
                    {
                        _pending.Clear();
                        _resetPending = true;
                    }
                }
            }
            return true;
        }
    }
}
