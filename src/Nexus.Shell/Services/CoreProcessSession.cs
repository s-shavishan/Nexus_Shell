using Nexus.Runtime;
using Nexus.Shell.Models;
using System.Diagnostics;

namespace Nexus.Shell.Services;

// The UI never performs synchronous Core I/O. Failed/uncertain commits are
// reconciled with their original identifier before newer snapshots are sent.
internal sealed class CoreProcessSession : IAsyncDisposable
{
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private readonly CancellationTokenSource _operations = new();
    private readonly RuntimeRestartBudget _budget = new();
    private Process? _process;
    private RuntimeClient? _client;
    private RevisionedStateWriter? _writer;
    private int _healthFailures, _saveFailures;
    private bool _disposed;
    internal async Task<StateReadResult> InitializeAsync()
    {
        var client = await ClientAsync().ConfigureAwait(false);
        var loaded = await client.CallAsync<StateReadResult>(RuntimeOperations.ReadState, new { }, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        _writer = new(loaded.State.PersistenceRevision, ReadStateAsync, CommitAsync); return loaded;
    }
    private async Task<RuntimeClient> ClientAsync()
    {
        await _startGate.WaitAsync(_operations.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_process is { HasExited: false } && _client is not null) return _client;
            _process?.Dispose(); _process = null; _client = null;
            if (!_budget.TryStart()) throw new RuntimeFailure("restart", "Core has stopped repeatedly. Nexus paused automatic restarts for one minute; your current edits remain in the desktop.");
            string executable = Path.Combine(AppContext.BaseDirectory, "Nexus.Core.exe");
            if (!File.Exists(executable)) throw new FileNotFoundException("Nexus.Core.exe is missing. Keep the complete build folder together.", executable);
            string endpoint = Guid.NewGuid().ToString("N");
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory };
            start.ArgumentList.Add("--parent-pid"); start.ArgumentList.Add(Environment.ProcessId.ToString());
            start.ArgumentList.Add("--endpoint"); start.ArgumentList.Add(endpoint);
            _process = Process.Start(start) ?? throw new InvalidOperationException("Nexus Core did not start.");
            _ = _process.Handle;
            using var own = Process.GetCurrentProcess();
            var client = new RuntimeClient("WhiteDreams.Nexus.Core." + own.SessionId + "." + endpoint, "desktop", _process.Id);
            var elapsed = Stopwatch.StartNew();
            while (elapsed.Elapsed < TimeSpan.FromSeconds(10))
            {
                if (_process.HasExited) throw new InvalidOperationException("Nexus Core could not start. See core.log in the Nexus data folder.");
                try
                {
                    await client.CallAsync<RuntimeHealth>(RuntimeOperations.Health, new { }, TimeSpan.FromMilliseconds(500), _operations.Token).ConfigureAwait(false);
                    _client = client; Interlocked.Exchange(ref _healthFailures, 0); Interlocked.Exchange(ref _saveFailures, 0);
                    Log.Write("Connected to Nexus Core process " + _process.Id); return client;
                }
                catch (Exception ex) when (ex is IOException or TimeoutException)
                { await Task.Delay(100, _operations.Token).ConfigureAwait(false); }
            }
            throw new TimeoutException("Nexus Core did not become ready. See core.log in the Nexus data folder.");
        }
        catch
        {
            StopProcess(); _client = null; throw;
        }
        finally { _startGate.Release(); }
    }
    private void StopProcess()
    {
        if (_process is null) return;
        try { if (!_process.HasExited) _process.Kill(entireProcessTree: false); }
        catch (Exception ex) { Log.Write("Could not stop Nexus Core", ex); }
        _process.Dispose(); _process = null;
    }
    private async Task TransportFailedAsync(RuntimeClient client, Exception error, bool saving = false)
    {
        if (error is not IOException and not TimeoutException) return;
        if ((saving ? Interlocked.Increment(ref _saveFailures) : Interlocked.Increment(ref _healthFailures)) < 3) return;
        await _startGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_disposed && ReferenceEquals(_client, client))
            { Log.Write("Core requests timed out repeatedly; stopping its process for bounded recovery."); StopProcess(); _client = null; }
        }
        finally { _startGate.Release(); }
    }
    internal Task SaveAsync(ShellState snapshot) => (_writer ?? throw new InvalidOperationException("Core state has not been loaded.")).SaveAsync(snapshot);
    private async Task<StateReadResult> ReadStateAsync()
    {
        var client = await ClientAsync().ConfigureAwait(false);
        try { return await client.CallAsync<StateReadResult>(RuntimeOperations.ReadState, new { }, TimeSpan.FromSeconds(5), _operations.Token).ConfigureAwait(false); }
        catch (Exception ex) { await TransportFailedAsync(client, ex, saving: true).ConfigureAwait(false); throw; }
    }
    private async Task<StateCommitResult> CommitAsync(StateCommit commit)
    {
        var client = await ClientAsync().ConfigureAwait(false);
        try
        {
            var result = await client.CallAsync<StateCommitResult>(RuntimeOperations.CommitState, commit, TimeSpan.FromSeconds(5), _operations.Token).ConfigureAwait(false);
            Interlocked.Exchange(ref _saveFailures, 0); return result;
        }
        catch (Exception ex) { await TransportFailedAsync(client, ex, saving: true).ConfigureAwait(false); throw; }
    }
    internal async Task<FilesResult> OpenFilesAsync(FilesLaunch launch, CancellationToken cancellation)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _operations.Token);
        var client = await ClientAsync().ConfigureAwait(false);
        return await client.CallAsync<FilesResult>(RuntimeOperations.OpenFiles, launch,
            launch.Request.Kind == FileSelectionKind.Browse ? TimeSpan.FromSeconds(18) : null, linked.Token).ConfigureAwait(false);
    }
    internal async Task<RuntimeHealth> HealthAsync()
    {
        var client = await ClientAsync().ConfigureAwait(false);
        try
        {
            var health = await client.CallAsync<RuntimeHealth>(RuntimeOperations.Health, new { }, TimeSpan.FromSeconds(3), _operations.Token).ConfigureAwait(false);
            Interlocked.Exchange(ref _healthFailures, 0); return health;
        }
        catch (Exception ex) { await TransportFailedAsync(client, ex).ConfigureAwait(false); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        _disposed = true; _operations.Cancel();
        await _startGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_process is { HasExited: false } && _client is not null)
            {
                try
                {
                    await _client.CallAsync<object>(RuntimeOperations.Stop, new { }, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    await _process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
                }
                catch (Exception ex) { Log.Write("Core graceful shutdown did not finish", ex); }
            }
            StopProcess(); _client = null;
        }
        finally { _startGate.Release(); }
        // Concurrent cancelled requests may still be unwinding; they own no
        // process or settings writer after this point.
    }
}
