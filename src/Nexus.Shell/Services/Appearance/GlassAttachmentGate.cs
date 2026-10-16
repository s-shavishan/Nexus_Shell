// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// UI-thread-owned attachment identity for per-HWND system backdrops.
// A delayed native callback may only mutate its own active generation.
public sealed class GlassAttachmentGate<T> where T : class
{
    private T? _active;
    private bool _closed;

    public bool IsCurrent(T source) => !_closed && ReferenceEquals(_active, source);

    public void Attach(T? source)
    {
        if (_closed) return;
        _active = source;
    }

    public bool DetachFailed(T source)
    {
        if (!IsCurrent(source)) return false;
        _active = null;
        return true;
    }

    public void Close()
    {
        _closed = true;
        _active = null;
    }
}
