using Nexus.Runtime;
using Nexus.Shell.Services;
using System.Threading.Channels;

internal static class ConnectionChecks
{
    // Only the OS transport is replaced. The production connection handler,
    // framing, role checks, Core router, state writer, and cancellation run.
    private sealed class Duplex(Channel<byte[]> incoming, Channel<byte[]> outgoing) : Stream
    {
        private byte[]? _buffer;
        private int _position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellation = default)
        {
            if (_buffer is null || _position == _buffer.Length)
            {
                try { _buffer = await incoming.Reader.ReadAsync(cancellation); _position = 0; }
                catch (ChannelClosedException) { return 0; }
            }
            int count = Math.Min(buffer.Length, _buffer.Length - _position);
            _buffer.AsMemory(_position, count).CopyTo(buffer); _position += count; return count;
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!outgoing.Writer.TryWrite(buffer.ToArray())) throw new IOException("The peer disconnected.");
            return ValueTask.CompletedTask;
        }
        protected override void Dispose(bool disposing) { if (disposing) outgoing.Writer.TryComplete(); base.Dispose(disposing); }
        internal static (Duplex Client, Duplex Server) Pair()
        { var a = Channel.CreateUnbounded<byte[]>(); var b = Channel.CreateUnbounded<byte[]>(); return (new(a, b), new(b, a)); }
    }
    internal static async Task RunAsync()
    {
        string folder = Path.Combine(Path.GetTempPath(), "Nexus-connection-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var state = new CoreStateRepository(new StateStore(folder));
            bool stopped = false;
            using var files = new FilesCoordinator(_ => new FakeProcess(() => stopped = true), (_, _) => true);
            var settingsBackend = new SettingsChecks.Backend((request, _) => Task.FromResult(new SettingsSnapshot(default, request.Section,
                Sound: new(true, "output", "Test speakers", (float)((request.Change?.Value ?? 60) / 100), request.Change?.Enabled ?? false, [], ""))));
            var router = new CoreRouter(state, files, new SystemSettingsCoordinator(settingsBackend));
            async Task<RuntimeResponse> Request(string role, string operation, object payload)
            {
                var pair = Duplex.Pair(); using var client = pair.Client; using var server = pair.Server;
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var serving = RuntimeConnection.ServeAsync(server, _ => true, router.DispatchAsync, deadline.Token);
                Guid id = Guid.NewGuid();
                await RuntimeProtocol.WriteAsync(client, new RuntimeRequest(RuntimeProtocol.Version, id, role, 100, operation, RuntimeProtocol.Body(payload)), deadline.Token);
                var response = await RuntimeProtocol.ReadAsync<RuntimeResponse>(client, deadline.Token);
                await serving; if (response.Id != id) throw new Exception("Connection reply correlation failed."); return response;
            }
            var loaded = RuntimeProtocol.Payload<StateReadResult>((await Request("desktop", RuntimeOperations.ReadState, new { })).Payload);
            loaded.State.QuickNote = "Production connection state";
            var saved = await Request("desktop", RuntimeOperations.CommitState, new StateCommit(loaded.State, 0, Guid.NewGuid()));
            if (!saved.Ok || new StateStore(folder).Load().QuickNote != loaded.State.QuickNote) throw new Exception("Connection commit did not persist.");
            var rejected = await Request("files", RuntimeOperations.CommitState, new StateCommit(loaded.State, 1, Guid.NewGuid()));
            if (rejected.Ok || rejected.Code != "forbidden" || state.Revision != 1) throw new Exception("Connection role authorization failed.");
            var settingsRead = await Request("desktop", RuntimeOperations.Settings, new SettingsRequest("Sound"));
            var snapshot = RuntimeProtocol.Payload<SettingsSnapshot>(settingsRead.Payload);
            if (!settingsRead.Ok || snapshot.Epoch == Guid.Empty || snapshot.Sound!.Volume != .6f) throw new Exception("Production connection did not return the settings service state.");
            var settingsWrite = await Request("desktop", RuntimeOperations.Settings, new SettingsRequest("Sound", Change: new("volume", "output", Value: 20, Epoch: snapshot.Epoch)));
            if (!settingsWrite.Ok || RuntimeProtocol.Payload<SettingsSnapshot>(settingsWrite.Payload).Sound!.Volume != .2f) throw new Exception("Production settings mutation failed.");
            var settingsDenied = await Request("files", RuntimeOperations.Settings, new SettingsRequest("Sound"));
            if (settingsDenied.Ok || settingsDenied.Code != "forbidden" || settingsBackend.Calls != 2) throw new Exception("Files must not access desktop hardware controls.");
            var settingsStale = await Request("desktop", RuntimeOperations.Settings, new SettingsRequest("Sound", Change: new("volume", "output", Value: 99, Epoch: Guid.NewGuid())));
            if (settingsStale.Ok || settingsStale.Code != "settings-stale" || settingsBackend.Calls != 2) throw new Exception("The production connection accepted a stale device command.");
            var pair = Duplex.Pair(); using var serverSide = pair.Server; using var clientSide = pair.Client;
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var handling = RuntimeConnection.ServeAsync(serverSide, _ => true, router.DispatchAsync, stop.Token);
            await RuntimeProtocol.WriteAsync(clientSide, new RuntimeRequest(1, Guid.NewGuid(), "desktop", 100, RuntimeOperations.OpenFiles,
                RuntimeProtocol.Body(new FilesLaunch(new(FileSelectionKind.OpenFile)))), stop.Token);
            for (int n = 0; n < 100 && files.Health().Count == 0; n++) await Task.Delay(10);
            if (files.Health().Count != 1) throw new Exception("Connection picker did not start.");
            clientSide.Dispose(); await handling;
            if (!stopped) throw new Exception("Production connection did not cancel the disconnected picker.");
            foreach (int length in new[] { -1, 0, RuntimeProtocol.MaximumFrameBytes + 1 })
            {
                bool failed = false;
                try { await RuntimeProtocol.ReadAsync<RuntimeRequest>(new MemoryStream(BitConverter.GetBytes(length)), CancellationToken.None); }
                catch (InvalidDataException) { failed = true; }
                if (!failed) throw new Exception("Invalid frame length was accepted.");
            }
            bool truncated = false;
            try { await RuntimeProtocol.ReadAsync<RuntimeRequest>(new MemoryStream([10, 0, 0, 0, 1]), CancellationToken.None); }
            catch (EndOfStreamException) { truncated = true; }
            if (!truncated) throw new Exception("Truncated frame was accepted.");
            int reports = 0, dispatches = 0;
            using (var empty = new MemoryStream())
                await RuntimeConnection.ServeAsync(empty,_=>true,(_,_)=>{dispatches++;throw new Exception("Empty request dispatched.");},CancellationToken.None,_=>reports++);
            if (reports != 0 || dispatches != 0) throw new Exception("An empty probe must close quietly without dispatch.");
            using (var partial = new MemoryStream())
            {
                partial.Write([10,0]); partial.Position = 0;
                await RuntimeConnection.ServeAsync(partial,_=>true,(_,_)=>{dispatches++;throw new Exception("Partial request dispatched.");},CancellationToken.None,_=>reports++);
            }
            if (reports != 1 || dispatches != 0) throw new Exception("A partial frame must be reported without dispatch.");
            Console.WriteLine("PASS: production stream connection, framed state commit, role rejection, disconnect cancellation, empty-probe handling, and invalid/truncated frame rejection (OS transport replaced).");
        }
        finally { Directory.Delete(folder, true); }
    }
    private sealed class FakeProcess(Action stopped) : IFilesProcess
    {
        public int Id => 100;
        public bool HasExited { get; private set; }
        public int ExitCode => 0;
        public void Stop() { HasExited = true; stopped(); }
        public void Dispose() { }
    }
}
