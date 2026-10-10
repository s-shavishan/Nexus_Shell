using System.IO.Pipes;

namespace Nexus.Runtime;

// One framed request per connection. Picker waits observe disconnects;
// admission limits leave capacity for state and UI heartbeat requests.
public sealed class RuntimeServer(string pipeName, Func<NamedPipeServerStream, RuntimeRequest, bool> authorize,
    Func<RuntimeRequest, CancellationToken, Task<RuntimeResponse>> dispatch, Action<Exception>? report = null)
{
    public const int MaximumConnections = 24;
    public async Task RunAsync(CancellationToken cancellation)
    {
        using var slots = new SemaphoreSlim(MaximumConnections);
        var handlers = new HashSet<Task>();
        async Task Handle(NamedPipeServerStream connected)
        {
            try
            {
                using (connected) await RuntimeConnection.ServeAsync(connected, request => authorize(connected, request), dispatch, cancellation, report).ConfigureAwait(false);
            }
            finally { slots.Release(); }
        }
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                await slots.WaitAsync(cancellation).ConfigureAwait(false);
                NamedPipeServerStream? pipe = null;
                try
                {
                    pipe = new(pipeName, PipeDirection.InOut, MaximumConnections, PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, 4096, 4096);
                    await pipe.WaitForConnectionAsync(cancellation).ConfigureAwait(false);
                    var connected = pipe; pipe = null;
                    handlers.RemoveWhere(task => task.IsCompletedSuccessfully);
                    handlers.Add(Handle(connected));
                }
                catch { pipe?.Dispose(); slots.Release(); throw; }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally { await Task.WhenAll(handlers).ConfigureAwait(false); }
    }
}

// Framing, role checks, deadlines, and disconnect handling are transport
// independent. Windows supplies peer verification in the named-pipe wrapper.
public static class RuntimeConnection
{
    public static async Task ServeAsync(Stream stream, Func<RuntimeRequest, bool> authorize,
        Func<RuntimeRequest, CancellationToken, Task<RuntimeResponse>> dispatch, CancellationToken serverCancellation, Action<Exception>? report = null)
    {
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(serverCancellation);
        Guid id = Guid.Empty;
        try
        {
            RuntimeRequest request;
            using (var headerDeadline = CancellationTokenSource.CreateLinkedTokenSource(serverCancellation))
            {
                headerDeadline.CancelAfter(TimeSpan.FromSeconds(3));
                request = await RuntimeProtocol.ReadAsync<RuntimeRequest>(stream, headerDeadline.Token).ConfigureAwait(false);
            }
            id = request.Id; RuntimeProtocol.Validate(request);
            if (!authorize(request)) throw new RuntimeFailure("forbidden", "This client does not belong to this Nexus session.");
            async Task ObserveDisconnect()
            {
                try { await stream.ReadAsync(new byte[1], requestCancellation.Token).ConfigureAwait(false); requestCancellation.Cancel(); }
                catch (OperationCanceledException) { }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException) { requestCancellation.Cancel(); }
            }
            var disconnect = ObserveDisconnect();
            try
            {
                var response = await dispatch(request, requestCancellation.Token).ConfigureAwait(false);
                using var responseDeadline = CancellationTokenSource.CreateLinkedTokenSource(requestCancellation.Token);
                responseDeadline.CancelAfter(TimeSpan.FromSeconds(3));
                await RuntimeProtocol.WriteAsync(stream, response, responseDeadline.Token).ConfigureAwait(false);
            }
            finally { requestCancellation.Cancel(); await disconnect.ConfigureAwait(false); }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (serverCancellation.IsCancellationRequested) return;
            if (ex is not RuntimeFailure and not OperationCanceledException) report?.Invoke(ex);
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                string code = ex is RuntimeFailure failure ? failure.Code : "request";
                string message = ex is RuntimeFailure or InvalidDataException ? ex.Message : "Nexus could not complete this request. Details are in core.log.";
                await RuntimeProtocol.WriteAsync(stream, RuntimeProtocol.Failure(id, code, message), deadline.Token).ConfigureAwait(false);
            }
            catch (Exception write) when (write is IOException or OperationCanceledException or ObjectDisposedException) { }
        }
    }
}
