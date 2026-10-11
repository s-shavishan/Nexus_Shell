namespace Nexus.Runtime;

// The slot belongs to the actual engine task, even when its caller disconnects.
// Rapid typing can never accumulate unbounded native queries in Core.
public sealed class SearchCoordinator(IFileSearchBackend backend, string profile)
{
    private readonly SemaphoreSlim _slot = new(1, 1);
    public async Task<SearchReply> QueryAsync(SearchRequest request, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var plan = SearchPolicy.Create(request, profile);
        if (plan.Request.Query.Length == 0) return SearchReply.Empty(SearchStatus.Ready);
        if (!await _slot.WaitAsync(0, cancellation).ConfigureAwait(false)) return SearchReply.Empty(SearchStatus.Busy);
        try
        {
            var reply = await backend.QueryAsync(plan, cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            // A malformed engine reply cannot become an executable URI, device
            // path or a result outside the user's selected lexical scope.
            if (!Enum.IsDefined(reply.Status) || reply.Items is null || reply.Items.Length > SearchPolicy.PageSize || reply.EngineVersion is null || reply.EngineVersion.Length > 64
                || (reply.Status == SearchStatus.Ready && (reply.Offset != request.Offset || reply.Total < (uint)reply.Items.Length))
                || reply.Items.Any(item => item is null || !SearchPolicy.InScope(item.Path, plan.Root) || item.Name != SearchPolicy.Name(item.Path)))
                return SearchReply.Empty(SearchStatus.Failed);
            return reply.Status == SearchStatus.Ready ? reply : reply with { Items = [], Total = 0, Offset = 0 };
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
        catch (TimeoutException) { return SearchReply.Empty(SearchStatus.TimedOut); }
        catch (Exception) { return SearchReply.Empty(SearchStatus.Failed); }
        finally { _slot.Release(); }
    }
}
