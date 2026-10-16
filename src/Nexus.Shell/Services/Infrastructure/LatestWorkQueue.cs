// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// One in-flight operation, with only the latest waiting request allowed to run.
// Cancellation cannot interrupt every filesystem/WinRT call. Keep its slot until
// it actually finishes, then discard its result before admitting newer work.
public sealed class LatestWorkQueue : IDisposable
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _slot = new(1, 1);
    private CancellationTokenSource? _current;
    private int _requests;
    private bool _disposed;

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work)
    {
        CancellationTokenSource request;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _current?.Cancel(); _current = request = new(); _requests++;
        }
        var token = request.Token;
        bool entered = false;
        try
        {
            await _slot.WaitAsync(token); entered = true;
            token.ThrowIfCancellationRequested();
            var result = await work(token);
            token.ThrowIfCancellationRequested();
            return result;
        }
        catch (Exception) when (token.IsCancellationRequested)
        { throw new OperationCanceledException(token); }
        finally
        {
            if (entered) _slot.Release();
            lock (_gate)
            {
                if (ReferenceEquals(_current, request)) _current = null;
                request.Dispose();
                if (--_requests == 0 && _disposed) _slot.Dispose();
            }
        }
    }
    public void Cancel() { lock (_gate) _current?.Cancel(); }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true; _current?.Cancel();
            if (_requests == 0) _slot.Dispose();
        }
    }
}
