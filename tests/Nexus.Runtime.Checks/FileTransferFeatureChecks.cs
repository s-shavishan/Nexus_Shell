using System.Text.Json;
using Nexus.Runtime;
using Nexus.Runtime.Transfers;

internal static class FileTransferFeatureChecks
{
    private static int _checks;
    private static void Check(bool pass, string message)
    { _checks++; if (!pass) throw new InvalidOperationException(message); }
    private static async Task Eventually(Func<bool> ready)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!ready()) await Task.Delay(5, timeout.Token);
    }
    private static void Reject(Action action)
    { bool rejected = false; try { action(); } catch (RuntimeFailure) { rejected = true; } Check(rejected, "An invalid/stale active transfer control must be rejected."); }
    private sealed class Journal : IFileJobsJournal
    {
        internal FileJobRecord[] Records = [];
        public string Notice => "";
        public FileJobRecord[] Read() => Records;
        public void Write(IReadOnlyList<FileJobRecord> jobs) => Records = jobs.ToArray();
    }
    private sealed class Source : ITransferSource
    {
        internal readonly Reader Reader;
        internal bool Disposed;
        internal Source(string path, bool block) => Reader = new(path, block);
        public Stream Content => Reader;
        public void CommitMove() => throw new InvalidOperationException("Only copies are used by this fixture.");
        public void Dispose() { Disposed = true; Reader.Dispose(); }
    }
    private sealed class Reader(string path, bool block) : Stream
    {
        private readonly FileStream _file = File.OpenRead(path);
        internal readonly TaskCompletionSource ReadingSecond = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource ReleaseSecond = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Reads;
        internal long BytesRead;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellation = default)
        {
            if (Interlocked.Increment(ref Reads) == 2 && block)
            { ReadingSecond.TrySetResult(); await ReleaseSecond.Task.WaitAsync(cancellation); }
            int size = await _file.ReadAsync(buffer, cancellation);
            Interlocked.Add(ref BytesRead, size); return size;
        }
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _file.Length;
        public override long Position { get => _file.Position; set => _file.Position = value; }
        public override void Flush() => _file.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _file.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _file.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _file.Dispose(); base.Dispose(disposing); }
    }
    internal static async Task RunAsync()
    {
        Meter();
        for (int cycle = 0; cycle < 100; cycle++)
        {
            var events = new List<bool>(); var flow = new FileTransferFlow(events.Add);
            Check(flow.Pause() && !flow.Pause() && flow.PauseRequested, "Pause must be idempotent.");
            var waiting = flow.WaitAsync(CancellationToken.None).AsTask();
            Check(!waiting.IsCompleted && events.SequenceEqual([true]), "Pause acknowledgement must come from a reached checkpoint.");
            Check(flow.Resume() && !flow.Resume(), "Resume must release precisely the current pause generation.");
            await waiting.WaitAsync(TimeSpan.FromSeconds(2));
            Check(!flow.PauseRequested && events.SequenceEqual([true, false]), "Resume must leave no pending checkpoint or stale pause state.");
        }
        {
            FileTransferFlow flow = null!; int acknowledged = 0;
            flow = new FileTransferFlow(paused =>
            {
                if (paused && ++acknowledged == 1) { flow.Resume(); flow.Pause(); }
            });
            flow.Pause(); var waiting = flow.WaitAsync(CancellationToken.None).AsTask();
            Check(acknowledged == 2 && !waiting.IsCompleted,
                "Immediate resume/re-pause must acknowledge the new generation without allowing transfer work through.");
            flow.Resume(); await waiting.WaitAsync(TimeSpan.FromSeconds(2));
        }
        string root = Path.Combine(Path.GetTempPath(), "Nexus-flow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string file = Path.Combine(root, "source.bin");
            byte[] bytes = Enumerable.Range(0, 1024 * 1024).Select(i => (byte)(i % 239)).ToArray();
            await File.WriteAllBytesAsync(file, bytes);
            await QueueAsync(root, file, bytes);
            foreach (string phase in new[] { "Copying", "Verifying" })
            foreach (bool cancel in new[] { false, true })
            {
                string destination = Path.Combine(root, phase + cancel); Directory.CreateDirectory(destination);
                var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var flow = new FileTransferFlow(paused => { if (paused) reached.TrySetResult(); });
                using var cancellation = new CancellationTokenSource();
                bool requested = false;
                var run = new FileTransferEngine().RunAsync(new(Guid.NewGuid(), FileJobKind.Copy, [file], destination), progress =>
                {
                    if (!requested && progress.Phase == phase) { requested = true; flow.Pause(); }
                }, flow, cancellation.Token);
                await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Check(!run.IsCompleted && !File.Exists(Path.Combine(destination, "source.bin")) && File.Exists(file),
                    "Paused copying/verification must retain the source without publishing uncommitted output.");
                if (cancel)
                {
                    cancellation.Cancel(); bool cancelled = false;
                    try { await run.WaitAsync(TimeSpan.FromSeconds(5)); } catch (OperationCanceledException) { cancelled = true; }
                    Check(cancelled && Directory.GetFileSystemEntries(destination).Length == 0 && File.Exists(file),
                        "Cancelling a paused copy or verification must wake the worker and clean staging.");
                }
                else
                {
                    flow.Resume(); await run.WaitAsync(TimeSpan.FromSeconds(5));
                    Check((await File.ReadAllBytesAsync(Path.Combine(destination, "source.bin"))).SequenceEqual(bytes),
                        "Resume must continue the original copy/verification and commit identical verified content.");
                }
            }
            foreach (bool cancel in new[] { false, true })
            {
                string source = Path.Combine(root, "folder-" + cancel), destination = Path.Combine(root, "folder-destination-" + cancel);
                Directory.CreateDirectory(source); Directory.CreateDirectory(destination);
                await File.WriteAllTextAsync(Path.Combine(source, "note.txt"), "retained folder");
                var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var flow = new FileTransferFlow(state => { if (state) paused.TrySetResult(); });
                using var cancellation = new CancellationTokenSource();
                var run = new FileTransferEngine().RunAsync(new(Guid.NewGuid(), FileJobKind.Move, [source], destination),
                    progress => { if (progress.Phase == "Moving folder") flow.Pause(); }, flow, cancellation.Token);
                await paused.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Check(Directory.Exists(source) && Directory.GetFileSystemEntries(destination).Length == 0,
                    "Pause before an atomic folder rename must retain the complete source tree.");
                if (cancel)
                {
                    cancellation.Cancel(); bool cancelled = false;
                    try { await run.WaitAsync(TimeSpan.FromSeconds(5)); } catch (OperationCanceledException) { cancelled = true; }
                    Check(cancelled && File.Exists(Path.Combine(source, "note.txt")), "Cancel at the folder boundary must retain the source.");
                }
                else
                {
                    flow.Resume(); await run.WaitAsync(TimeSpan.FromSeconds(5));
                    Check(!Directory.Exists(source) && File.Exists(Path.Combine(destination, Path.GetFileName(source), "note.txt")),
                        "Resuming at the folder boundary must commit the intact same-volume move once.");
                }
            }
        }
        finally { Directory.Delete(root, true); }
        Console.WriteLine($"PASS: {_checks} File Jobs feature checks; active pause/resume, real copy/verification checkpoints, cancellation cleanup, one-worker queue ownership, restart safety, phase speed/ETA and protocol compatibility.");
    }
    private static async Task QueueAsync(string root, string file, byte[] bytes)
    {
        string destination = Path.Combine(root, "queue"); Directory.CreateDirectory(destination);
        var sources = new List<Source>();
        var engine = new FileTransferEngine((path, _) =>
        { var source = new Source(path, sources.Count == 0); lock (sources) sources.Add(source); return source; });
        var journal = new Journal();
        var queue = new FileJobCoordinator(engine, journal);
        try
        {
            var spec = new FileJobSpec(Guid.NewGuid(), FileJobKind.Copy, [file], destination);
            queue.Submit(new(queue.Epoch, spec));
            await Eventually(() => { lock (sources) return sources.Count == 1; });
            Source source; lock (sources) source = sources[0];
            await source.Reader.ReadingSecond.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var second = spec with { Id = Guid.NewGuid() }; queue.Submit(new(queue.Epoch, second));
            queue.Control(new(queue.Epoch, Guid.Empty, "pause"));
            Reject(() => queue.Control(new(Guid.NewGuid(), spec.Id, "pause-active")));
            Reject(() => queue.Control(new(queue.Epoch, second.Id, "pause-active")));
            Reject(() => queue.Control(new(queue.Epoch, Guid.Empty, "pause-active")));
            var pausing = queue.Control(new(queue.Epoch, spec.Id, "pause-active"));
            Check(pausing.Jobs.Single(j => j.Job.Id == spec.Id).State == FileJobState.Pausing,
                "An in-flight read must report Pausing until the next actual checkpoint.");
            Check(queue.Control(new(queue.Epoch, spec.Id, "resume-active")).Jobs.Single(j => j.Job.Id == spec.Id).State == FileJobState.Running,
                "Resume before checkpoint acknowledgement must clear Pausing immediately.");
            queue.Control(new(queue.Epoch, spec.Id, "pause-active"));
            source.Reader.ReleaseSecond.TrySetResult();
            await Eventually(() => queue.Snapshot().Jobs.Single(j => j.Job.Id == spec.Id).State == FileJobState.Paused);
            var paused = queue.Snapshot().Jobs.Single(j => j.Job.Id == spec.Id);
            Check(source.Reader.Reads == 2 && !source.Disposed && paused.Progress.BytesCopied == source.Reader.BytesRead,
                "Acknowledged pause must retain the stream and publish exact latest bytes, including throttled progress.");
            Check(paused.Progress.BytesPerSecond == 0 && paused.Progress.SecondsRemaining is null,
                "Paused transfers cannot show a running rate or ETA.");
            Check(queue.Control(new(queue.Epoch, spec.Id, "pause-active")).Jobs.Single(j => j.Job.Id == spec.Id).State == FileJobState.Paused,
                "Repeated pause cannot regress an acknowledged pause into a request.");
            await Task.Delay(20);
            Check(source.Reader.Reads == 2 && sources.Count == 1 && !File.Exists(Path.Combine(destination, "source.bin")),
                "A paused transfer keeps its worker slot without reading, committing or starting the next job.");
            queue.Control(new(queue.Epoch, spec.Id, "resume-active"));
            await Eventually(() => queue.Snapshot().Jobs.Single(j => j.Job.Id == spec.Id).State == FileJobState.Complete);
            Check(queue.Snapshot().Paused && queue.Snapshot().Jobs.Single(j => j.Job.Id == second.Id).State == FileJobState.Queued,
                "Resuming the current job must preserve the independent hold-next-jobs setting.");
            Check((await File.ReadAllBytesAsync(Path.Combine(destination, "source.bin"))).SequenceEqual(bytes)
                && sources.Count == 1 && source.Disposed, "Resume cannot reopen or duplicate the original transfer.");
            queue.Control(new(queue.Epoch, Guid.Empty, "resume"));
            await Eventually(() => queue.Snapshot().Jobs.All(j => j.State == FileJobState.Complete));
            Check(sources.Count == 2, "The next job must use its own unpaused flow only after queue hold is released.");
            Reject(() => queue.Control(new(queue.Epoch, spec.Id, "resume-active")));

            // A paused journal entry requires review after Core restart;
            // it must never be replayed as an automatic resume.
            var pausedRecord = paused with { State = FileJobState.Paused };
            await using var restarted = new FileJobCoordinator(engine, new Journal { Records = [pausedRecord] });
            Check(restarted.Snapshot().Jobs.Single().State == FileJobState.Interrupted && sources.Count == 2,
                "A restored paused journal entry is Interrupted, never auto-resumed after restart.");
        }
        finally { await queue.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)); }

        foreach (bool stop in new[] { false, true })
        {
            var blocked = new Source(file, true);
            await using var stopping = new FileJobCoordinator(new FileTransferEngine((_, _) => blocked), new Journal());
            var stopSpec = new FileJobSpec(Guid.NewGuid(), FileJobKind.Copy, [file], destination);
            stopping.Submit(new(stopping.Epoch, stopSpec));
            await blocked.Reader.ReadingSecond.Task.WaitAsync(TimeSpan.FromSeconds(5));
            stopping.Control(new(stopping.Epoch, stopSpec.Id, "pause-active")); blocked.Reader.ReleaseSecond.TrySetResult();
            await Eventually(() => stopping.Snapshot().Jobs.Single().State == FileJobState.Paused);
            if (stop) await stopping.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            else
            {
                stopping.Control(new(stopping.Epoch, stopSpec.Id, "cancel"));
                await Eventually(() => stopping.Snapshot().Jobs.Single().State == FileJobState.Cancelled);
            }
            Check(blocked.Disposed && !Directory.GetFileSystemEntries(destination).Any(p => p.EndsWith(".partial")),
                "Explicit cancellation and Core shutdown must release a paused stream/staging without Resume.");
        }
    }
    private static void Meter()
    {
        var meter = new FileTransferMeter(); var progress = new FileJobProgress("Copying", "file", 0, 10000, 0, 0, 1);
        Check(meter.Sample(progress, 0).BytesPerSecond == 0, "Rate needs a real interval before estimating.");
        var measured = meter.Sample(progress with { BytesCopied = 1000 }, 1000);
        Check(measured.BytesPerSecond == 1000 && measured.SecondsRemaining == 9,
            "Copy ETA must use remaining bytes and a measured monotonic phase rate.");
        Check(meter.Sample(progress with { BytesCopied = 1000 }, 2000).BytesPerSecond == 500,
            "Rate must decay when bytes stop advancing rather than retaining a stale peak.");
        meter.Reset();
        Check(meter.Sample(progress with { BytesCopied = 1000 }, 100000).BytesPerSecond == 0,
            "Pause must invalidate the old sample.");
        Check(meter.Sample(progress with { BytesCopied = 2000 }, 101000).BytesPerSecond == 1000,
            "Resume rate must exclude paused time.");
        Check(meter.Sample(progress with { Phase = "Verifying" }, 102000).BytesPerSecond == 0,
            "Verification and atomic renames must not pretend to be a copy rate.");
        meter.Sample(progress with { BytesCopied = 5000 }, 103000);
        Check(meter.Sample(progress with { BytesCopied = 4000 }, 104000).BytesPerSecond == 0,
            "A reset byte counter cannot produce a negative rate.");
        Check(meter.Sample(progress with { BytesCopied = 5000 }, 100).BytesPerSecond == 0,
            "A reset monotonic clock must restart measurement.");
        var encoded = JsonSerializer.Serialize(measured);
        Check(JsonSerializer.Deserialize<FileJobProgress>(encoded) == measured, "Rate/ETA must round-trip through the existing JSON protocol.");
        var old = JsonSerializer.Deserialize<FileJobProgress>("{\"Phase\":\"Copying\",\"Current\":\"file\",\"BytesCopied\":1,\"TotalBytes\":2,\"Completed\":0,\"Skipped\":0,\"TotalFiles\":1}")!;
        Check(old.BytesPerSecond == 0 && old.SecondsRemaining is null, "Older job snapshots must default to unknown estimates.");
        Check((int)FileJobState.Interrupted == 6 && !FileJobRules.Terminal(FileJobState.Paused)
            && !FileJobRules.Terminal(FileJobState.Pausing), "New live states cannot renumber durable history or become terminal.");
    }
}
