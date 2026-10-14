using Nexus.Runtime;
using Nexus.Runtime.Transfers;
using Nexus.Core.Transfers;
using System.Text;

internal static class FileJobsChecks
{
    private static int _checks;
    private static void Check(bool value, string message) { _checks++; if (!value) throw new InvalidOperationException(message); }
    private static void Reject<T>(Action action) where T : Exception
    { try { action(); } catch (T) { _checks++; return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
    private static async Task RejectAsync<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T) { _checks++; return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
    private static async Task Eventually(Func<bool> ready)
    { for (int i = 0; i < 300; i++) { if (ready()) return; await Task.Delay(10); } throw new TimeoutException("File Jobs condition did not finish."); }
    private sealed class MemoryJournal : IFileJobsJournal
    {
        internal FileJobRecord[] Records = [];
        internal int Writes, FailAt;
        public string Notice => "";
        public FileJobRecord[] Read() => Records;
        public void Write(IReadOnlyList<FileJobRecord> jobs)
        { if (++Writes == FailAt) throw new IOException("Injected history failure"); Records = jobs.ToArray(); }
    }
    private sealed class ControlledBackend : IFileTransferBackend
    {
        internal int Calls, Active, MaximumActive;
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task RunAsync(FileJobSpec job, Action<FileJobProgress> report, CancellationToken cancellation)
        {
            Interlocked.Increment(ref Calls); int active = Interlocked.Increment(ref Active); MaximumActive = Math.Max(MaximumActive, active);
            try { report(new("Copying", job.Sources[0], 1, 2, 0, 0, 1)); await Release.Task.WaitAsync(cancellation); report(new("Finished", "", 2, 2, 1, 0, 1)); }
            finally { Interlocked.Decrement(ref Active); }
        }
    }
    internal static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "Nexus-jobs-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "source"), destination = Path.Combine(root, "destination"); Directory.CreateDirectory(source); Directory.CreateDirectory(destination);
            byte[] data = Enumerable.Range(0, 400000).Select(i => (byte)(i % 251)).ToArray(); string file = Path.Combine(source, "資料.bin"); await File.WriteAllBytesAsync(file, data);
            DateTime timestamp = new(2020, 1, 2, 3, 4, 6, DateTimeKind.Utc); File.SetLastWriteTimeUtc(file, timestamp);
            FileJobSpec Spec(params string[] sources) => new(Guid.NewGuid(), FileJobKind.Copy, sources, destination);
            var engine = new FileTransferEngine(); var progress = new List<FileJobProgress>();
            await engine.RunAsync(Spec(file), progress.Add, CancellationToken.None); string copied = Path.Combine(destination, Path.GetFileName(file));
            Check((await File.ReadAllBytesAsync(copied)).SequenceEqual(data) && File.Exists(file), "A verified copy must preserve the source and commit identical bytes.");
            Check(File.GetLastWriteTimeUtc(copied) == timestamp && progress.Last().Completed == 1 && progress.Any(p => p.Phase == "Verifying"), "Copies must retain modification time and report verification/commit accurately.");
            await engine.RunAsync(Spec(file), _ => { }, CancellationToken.None);
            Check(File.Exists(Path.Combine(destination, "資料 (2).bin")) && (await File.ReadAllBytesAsync(copied)).SequenceEqual(data), "Keep-both collisions must preserve the previous destination and allocate another name.");
            progress.Clear(); await engine.RunAsync(Spec(file) with { Collision = FileCollision.Skip }, progress.Add, CancellationToken.None);
            Check(progress.Last().Skipped == 1 && progress.Last().Completed == 0, "Skip must preserve existing items and disclose that no new copy committed.");
            string folder = Path.Combine(source, "Folder"); Directory.CreateDirectory(Path.Combine(folder, "empty")); Directory.CreateDirectory(Path.Combine(folder, "nested")); await File.WriteAllTextAsync(Path.Combine(folder, "nested", "note.txt"), "Keep my note 🌿");
            await engine.RunAsync(Spec(folder), _ => { }, CancellationToken.None);
            Check(Directory.Exists(Path.Combine(destination, "Folder", "empty")) && await File.ReadAllTextAsync(Path.Combine(destination, "Folder", "nested", "note.txt")) == "Keep my note 🌿", "Folder copying must retain empty directories and Unicode contents.");
            string cancelDestination = Path.Combine(root, "cancel"); Directory.CreateDirectory(cancelDestination);
            using (var cancel = new CancellationTokenSource())
                await RejectAsync<OperationCanceledException>(() => engine.RunAsync(Spec(file) with { Destination = cancelDestination }, p => { if (p.BytesCopied > 0) cancel.Cancel(); }, cancel.Token));
            Check(Directory.GetFileSystemEntries(cancelDestination).Length == 0 && (await File.ReadAllBytesAsync(file)).SequenceEqual(data), "Mid-buffer cancellation must remove uncommitted staging and retain the original.");
            await File.WriteAllBytesAsync(Path.Combine(folder, "big.bin"), data);
            using (var cancel = new CancellationTokenSource())
                await RejectAsync<OperationCanceledException>(() => engine.RunAsync(Spec(folder) with { Destination = cancelDestination }, p => { if (p.BytesCopied > 0) cancel.Cancel(); }, cancel.Token));
            Check(Directory.GetFileSystemEntries(cancelDestination).Length == 0 && File.Exists(Path.Combine(folder, "big.bin")), "Cancelled folders must never publish a partial final folder or remove sources.");
            Reject<RuntimeFailure>(() => FileJobRules.Validate(Spec(folder) with { Destination = Path.Combine(folder, "inside") }));
            Reject<RuntimeFailure>(() => FileJobRules.Validate(Spec(folder, Path.Combine(folder, "big.bin"))));
            Reject<RuntimeFailure>(() => FileJobRules.Validate(Spec(file) with { Kind = (FileJobKind)42 }));
            Reject<RuntimeFailure>(() => FileJobRules.Validate(Spec(file) with { Collision = (FileCollision)42 }));
            Reject<RuntimeFailure>(() => FileJobRules.Validate(null!));
            Reject<RuntimeFailure>(() => FileJobRules.Validate(Spec(Enumerable.Repeat(file, 25).ToArray())));
            Reject<RuntimeFailure>(() => FileJobRules.PathName("\\\\?\\C:\\temp\\file"));
            Reject<RuntimeFailure>(() => FileJobRules.PathName(Path.Combine(root, "file:stream")));
            Check(FileJobRules.Validate(Spec(file, file)).Sources.Length == 1, "Duplicate sources must not create duplicate transfers.");
            await RejectAsync<FileNotFoundException>(() => engine.RunAsync(Spec(Path.Combine(source, "missing.bin")), _ => { }, CancellationToken.None));
            Check(Directory.GetFileSystemEntries(cancelDestination).Length == 0, "Preflight failures must not leave new output staging.");
            string link = Path.Combine(source, "linked");
            try
            {
                Directory.CreateSymbolicLink(link, folder);
                await RejectAsync<IOException>(() => engine.RunAsync(Spec(link), _ => { }, CancellationToken.None));
                Check(!Directory.Exists(Path.Combine(destination, "linked")), "Linked folders must not be traversed or committed.");
            }
            catch (Exception error) when (error is UnauthorizedAccessException or PlatformNotSupportedException) { Console.WriteLine("SKIP: symbolic-link creation is unavailable on this test host."); }
            await QueueAsync(Spec(file), root);
            string moveFolder = Path.Combine(source, "Move folder"); Directory.CreateDirectory(moveFolder); await File.WriteAllTextAsync(Path.Combine(moveFolder, "note.txt"), "keep this");
            await engine.RunAsync(Spec(moveFolder) with { Kind = FileJobKind.Move }, _ => { }, CancellationToken.None);
            Check(!Directory.Exists(moveFolder) && await File.ReadAllTextAsync(Path.Combine(destination, "Move folder", "note.txt")) == "keep this", "Same-volume folder moves must publish the intact tree through a rename.");
            string refusedMove = Path.Combine(source, "refused-delete.bin"); await File.WriteAllBytesAsync(refusedMove, data);
            await RejectAsync<IOException>(() => engine.RunAsync(Spec(refusedMove) with { Kind = FileJobKind.Move }, _ => { }, CancellationToken.None));
            Check((await File.ReadAllBytesAsync(refusedMove)).SequenceEqual(data) && (await File.ReadAllBytesAsync(Path.Combine(destination, "refused-delete.bin"))).SequenceEqual(data), "A source deletion failure must retain both the original and the verified destination.");
            if (OperatingSystem.IsWindows())
            {
                string moveSource = Path.Combine(source, "move.bin"); await File.WriteAllBytesAsync(moveSource, data);
                using (var held = WindowsTransferSource.Open(moveSource, true))
                { Reject<IOException>(() => File.Move(moveSource, moveSource + ".changed")); Reject<IOException>(() => File.WriteAllText(moveSource, "changed")); }
                await new FileTransferEngine(WindowsTransferSource.Open).RunAsync(Spec(moveSource) with { Kind = FileJobKind.Move }, _ => { }, CancellationToken.None);
                Check(!File.Exists(moveSource) && (await File.ReadAllBytesAsync(Path.Combine(destination, "move.bin"))).SequenceEqual(data), "Windows moves must commit verified bytes before deleting the exact held source.");
                Check(WindowsTransferSource.InfoSize == 52, "The native file-information layout must retain its Windows ABI.");
            }
            else Console.WriteLine("SKIP: native Windows move-handle and share-lock checks require Windows.");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        Console.WriteLine($"PASS: {_checks} File Jobs checks; real copies, verification, collisions, folder/cancellation safety, bounded queue, idempotency, journal recovery and permissions.");
    }
    private static async Task QueueAsync(FileJobSpec spec, string root)
    {
        var backend = new ControlledBackend(); var journal = new MemoryJournal();
        await using (var queue = new FileJobCoordinator(backend, journal))
        {
            queue.Submit(new(queue.Epoch, spec)); await Eventually(() => backend.Calls == 1);
            var duplicate = queue.Submit(new(queue.Epoch, spec)); Check(duplicate.Jobs.Length == 1 && backend.Calls == 1, "Repeated uncertain submissions must not start another transfer.");
            Reject<RuntimeFailure>(() => queue.Submit(new(queue.Epoch, spec with { Verify = false })));
            var second = spec with { Id = Guid.NewGuid() }; queue.Submit(new(queue.Epoch, second)); queue.Control(new(queue.Epoch, Guid.Empty, "pause"));
            queue.Control(new(queue.Epoch, spec.Id, "cancel")); await Eventually(() => queue.Snapshot().Jobs.Any(j => j.Job.Id == spec.Id && j.State == FileJobState.Cancelled));
            Check(backend.Calls == 1 && queue.Snapshot().Jobs.Single(j => j.Job.Id == second.Id).State == FileJobState.Queued, "Pause must hold the next job while explicit cancellation stops the active job.");
            queue.Control(new(queue.Epoch, Guid.Empty, "resume")); backend.Release.TrySetResult(); await Eventually(() => queue.Snapshot().Jobs.All(j => FileJobRules.Terminal(j.State)));
            Check(backend.MaximumActive == 1, "Only one backend operation may run at a time.");
            var exposed = queue.Snapshot(); exposed.Jobs[0].Job.Sources[0] = "corrupt";
            Check(queue.Snapshot().Jobs[0].Job.Sources[0] != "corrupt", "UI snapshots must not mutate accepted job sources.");
            queue.Control(new(queue.Epoch, Guid.Empty, "clear")); Check(queue.Snapshot().Jobs.Length == 0, "Clear must remove only terminal history.");
            queue.Submit(new(queue.Epoch, spec)); Check(backend.Calls == 2 && queue.Snapshot().Jobs.Length == 0, "Clearing history must not remove idempotency tombstones for accepted job IDs.");
            Reject<RuntimeFailure>(() => queue.Control(new(Guid.NewGuid(), Guid.Empty, "pause")));
        }
        var denied = new MemoryJournal { FailAt = 1 }; var idle = new ControlledBackend();
        await using (var queue = new FileJobCoordinator(idle, denied))
        { Reject<IOException>(() => queue.Submit(new(queue.Epoch, spec))); Check(queue.Snapshot().Jobs.Length == 0 && idle.Calls == 0, "Rejected admission writes must not start file operations."); }
        denied = new MemoryJournal { FailAt = 2 }; idle = new ControlledBackend();
        await using (var queue = new FileJobCoordinator(idle, denied))
        { queue.Submit(new(queue.Epoch, spec)); await Eventually(() => queue.Snapshot().Jobs.Single().State == FileJobState.Failed); Check(queue.Snapshot().Paused && idle.Calls == 0, "Planning history failures must pause the queue without a restart loop or file writes."); }
        idle = new ControlledBackend(); journal = new MemoryJournal();
        await using (var queue = new FileJobCoordinator(idle, journal))
        {
            queue.Control(new(queue.Epoch, Guid.Empty, "pause"));
            var entries = Enumerable.Range(0, FileJobRules.MaximumJobs).Select(_ => spec with { Id = Guid.NewGuid() }).ToArray();
            foreach (var entry in entries) queue.Submit(new(queue.Epoch, entry));
            Reject<RuntimeFailure>(() => queue.Submit(new(queue.Epoch, spec with { Id = Guid.NewGuid() })));
            Check(queue.Snapshot().Jobs.Length == 24 && idle.Calls == 0, "A paused full queue must bound retained work and refuse additional jobs.");
            journal.FailAt = journal.Writes + 1;
            queue.Control(new(queue.Epoch, entries[0].Id, "cancel"));
            Check(queue.Snapshot().Jobs.Single(j => j.Job.Id == entries[0].Id).State == FileJobState.Cancelled, "A failed cancellation-history write must still respect cancellation in the live queue.");
            queue.Submit(new(queue.Epoch, spec with { Id = Guid.NewGuid() }));
            Check(queue.Snapshot().Jobs.Length == 24 && !queue.Snapshot().Jobs.Any(j => j.Job.Id == entries[0].Id), "Only terminal history may be evicted to admit a new job.");
            queue.Submit(new(queue.Epoch, entries[0]));
            Check(queue.Snapshot().Jobs.Length == 24 && idle.Calls == 0, "Evicted jobs retain idempotency tombstones for the Core session.");
        }
        idle = new ControlledBackend(); journal = new MemoryJournal { Records = [null!, new(spec, FileJobState.Complete, DateTimeOffset.UtcNow, null!, null!)] };
        await using (var queue = new FileJobCoordinator(idle, journal))
        { Check(queue.Snapshot().Jobs.Single() is { Detail.Length: 0, Progress.TotalBytes: 0 } && idle.Calls == 0, "Invalid history entries must be isolated and nullable display fields normalized without starting work."); }
        var disk = new FileJobsJournal(Path.Combine(root, "journal")); var unfinished = new FileJobRecord(spec, FileJobState.Running, DateTimeOffset.UtcNow, "Copying", new("Copying", "", 10, 100, 0, 0, 1)); disk.Write([unfinished]);
        idle = new ControlledBackend();
        await using (var queue = new FileJobCoordinator(idle, disk))
        { Check(queue.Snapshot().Jobs.Single().State == FileJobState.Interrupted && idle.Calls == 0, "Restart must disclose interruptions without automatically repeating file operations."); }
        disk.Write([unfinished with { State = FileJobState.Complete }]); File.WriteAllText(Path.Combine(root, "journal", "file-jobs.json"), "broken");
        Check(disk.Read().Single().State == FileJobState.Running && disk.Notice.Contains("backup"), "Damaged job history must recover the existing backup.");
        Check(RuntimeOperations.Allows("desktop", RuntimeOperations.ControlFileJob) && !RuntimeOperations.Allows("files", RuntimeOperations.ControlFileJob) && !RuntimeOperations.Allows("controlcenter", RuntimeOperations.SubmitFileJob), "Queue controls must retain component permissions.");
    }
}
