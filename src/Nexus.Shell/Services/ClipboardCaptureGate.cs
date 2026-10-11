namespace Nexus.Shell.Services;

// A blocked delayed-rendering source may consume one worker, never a series of
// replacement workers. Invalidation prevents late results after clear/lock/off.
public sealed class ClipboardCaptureGate
{
    public readonly record struct Lease(long Id, long Epoch, uint Sequence);
    private readonly object _gate = new();
    private long _epoch, _next;
    private Lease? _busy;
    public bool Busy { get { lock (_gate) return _busy.HasValue; } }
    public Lease? Begin(uint sequence)
    { lock (_gate) { if (_busy.HasValue) return null; var lease = new Lease(++_next, _epoch, sequence); _busy = lease; return lease; } }
    public bool Current(Lease lease) { lock (_gate) return lease.Epoch == _epoch; }
    public void End(Lease lease) { lock (_gate) { if (_busy == lease) _busy = null; } }
    public void Invalidate() { lock (_gate) _epoch++; }
}
