using Nexus.Runtime;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using System.IO.Pipes;

await RuntimeChecks.RunAsync(args.Contains("--no-native-ipc"));

internal static class RuntimeChecks
{
    private static int _checks;
    private static void Check(bool value, string message) { _checks++; if (!value) throw new Exception(message); }
    private static void Reject<T>(Action action) where T : Exception
    { try { action(); } catch (T) { _checks++; return; } throw new Exception("Expected " + typeof(T).Name); }
    private static async Task RejectAsync<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T) { _checks++; return; } throw new Exception("Expected " + typeof(T).Name); }
    private static async Task Eventually(Func<bool> condition)
    { for (int n = 0; n < 200; n++) { if (condition()) return; await Task.Delay(10); } throw new Exception("Condition did not complete within two seconds."); }
    private sealed class Clock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        internal void Advance(TimeSpan elapsed) => _ticks += elapsed.Ticks;
    }
    private sealed class FakeProcess(int id) : IFilesProcess
    {
        public int Id { get; } = id;
        public bool HasExited { get; private set; }
        public int ExitCode => HasExited ? 1 : throw new InvalidOperationException();
        public void Stop() => HasExited = true;
        public void Dispose() { }
    }
    internal static async Task RunAsync(bool noNativeIpc)
    {
        await StateAsync(); await FilesAsync(); await SettingsChecks.RunAsync(); await ControlCenterChecks.RunAsync(); await ControlCenterWakeChecks.RunAsync(); await ConnectionChecks.RunAsync();
        if (noNativeIpc) Console.WriteLine("SKIP: actual named-pipe tests explicitly disabled for this restricted test environment.");
        else await PipesAsync();
        await WindowsJobChecks.RunAsync();
        await WindowsSettingsChecks.RunAsync();
        Console.WriteLine($"PASS: {_checks} runtime assertions; durable state, uncertain-save reconciliation, caller cancellation, and simulated Files recovery. Native checks skipped: {noNativeIpc || !OperatingSystem.IsWindows()}.");
    }
    private static async Task StateAsync()
    {
        string folder = Path.Combine(Path.GetTempPath(), "Nexus-state-" + Guid.NewGuid().ToString("N"));
        var store = new StateStore(folder);
        store.Save(new() { QuickNote = "Legacy note", DisplayName = "Shan", Wallpaper = "Midnight" });
        try
        {
            Guid firstId = Guid.NewGuid();
            using (var repository = new CoreStateRepository(store))
            {
                Check(repository.Read().State.QuickNote == "Legacy note" && repository.Revision == 0, "Migration must preserve existing notes.");
                Reject<RuntimeFailure>(() => { using var second = new CoreStateRepository(new StateStore(folder)); });
                var first = repository.Read().State; first.QuickNote = "Acknowledged note";
                var acknowledgement = repository.Commit(new(first, 0, firstId));
                Check(acknowledgement.Revision == 1 && new StateStore(folder).Load().PersistenceCommitId == firstId.ToString("N"), "Acknowledgement must follow durable metadata and content.");
                Check(repository.Commit(new(first, 0, firstId)).Revision == 1, "An identical uncertain retry must not create a second revision.");
                var changed = first.Snapshot(); changed.QuickNote = "Different content";
                Reject<RuntimeFailure>(() => repository.Commit(new(changed, 0, firstId)));
                Reject<RuntimeFailure>(() => repository.Commit(new(changed, 0, Guid.NewGuid())));
                var returned = repository.Read().State; returned.QuickNote = "Mutated read";
                Check(repository.Read().State.QuickNote == "Acknowledged note", "Readers cannot mutate Core's authoritative state.");
                Directory.CreateDirectory(store.FilePath + ".tmp");
                Reject<UnauthorizedAccessException>(() => repository.Commit(new(changed, 1, Guid.NewGuid())));
                Check(repository.Revision == 1 && new StateStore(folder).Load().QuickNote == "Acknowledged note", "An atomic write failure must preserve content and revision.");
                Directory.Delete(store.FilePath + ".tmp");
                var oversized = first.Snapshot(); oversized.QuickNote = new string('x', StateStore.MaximumBytes + 1);
                Reject<InvalidDataException>(() => repository.Commit(new(oversized, 1, Guid.NewGuid())));
                Check(repository.Revision == 1, "An oversized write must leave the durable revision unchanged.");
            }
            using (var restarted = new CoreStateRepository(new StateStore(folder)))
            {
                Check(restarted.Revision == 1 && restarted.Read().State.QuickNote == "Acknowledged note", "A Core restart must recover the acknowledged state.");
                bool loseAck = true;
                var writer = new RevisionedStateWriter(restarted.Revision, () => Task.FromResult(restarted.Read()), request =>
                {
                    var result = restarted.Commit(request);
                    if (loseAck) { loseAck = false; throw new IOException("Injected lost acknowledgement"); }
                    return Task.FromResult(result);
                });
                var draft = restarted.Read().State; draft.QuickNote = "Saved before disconnect";
                await RejectAsync<IOException>(() => writer.SaveAsync(draft));
                draft.QuickNote = "Newer edit after disconnect"; await writer.SaveAsync(draft);
                Check(writer.Revision == 3 && restarted.Read().State.QuickNote == draft.QuickNote, "A lost acknowledgement must be reconciled before newer edits are saved.");
                bool rejectBeforeCommit = true; var attempted = new List<Guid>();
                var before = new RevisionedStateWriter(restarted.Revision, () => Task.FromResult(restarted.Read()), request =>
                {
                    attempted.Add(request.CommitId);
                    if (rejectBeforeCommit) { rejectBeforeCommit = false; throw new IOException("Injected pre-commit disconnect"); }
                    return Task.FromResult(restarted.Commit(request));
                });
                draft.QuickNote = "Original pending contents";
                await RejectAsync<IOException>(() => before.SaveAsync(draft));
                draft.QuickNote = "Latest contents"; await before.SaveAsync(draft);
                Check(attempted.Count == 3 && attempted[0] == attempted[1] && attempted[1] != attempted[2], "An uncertain pre-commit retry must keep its original ID and snapshot.");
                Check(restarted.Read().State.QuickNote == "Latest contents" && before.Revision == 5, "Both ordered commits must complete without losing the newest edit.");
                string recovery = store.SaveRecoverySnapshot(draft);
                Check(File.Exists(recovery) && new StateStore(folder).Load().QuickNote == "Latest contents", "A recovery copy must not overwrite the authoritative file.");
            }
            using var recovered = new CoreStateRepository(new StateStore(folder));
            Check(recovered.RecoveryMessage.Contains("unsaved-session"), "Existing recovery copies must be discoverable at startup.");
            Console.WriteLine("PASS: durable revisions, profile writer lease, failed writes, legacy migration, lost acknowledgements, and recovery-copy discovery.");
        }
        finally { Directory.Delete(folder, true); }
    }
    private static async Task FilesAsync()
    {
        var clock = new Clock(); var processes = new List<FakeProcess>();
        using var files = new FilesCoordinator(_ => { var p = new FakeProcess(100 + processes.Count); processes.Add(p); return p; }, (window, pid) => window == pid * 10, clock);
        var firstId = Guid.NewGuid();
        var browse = files.OpenAsync(firstId, new(new(FileSelectionKind.Browse), "/first"), CancellationToken.None);
        var browser = processes[^1];
        Reject<InvalidDataException>(() => files.Ready(browser.Id, new(firstId, 1)));
        files.Ready(browser.Id, new(firstId, browser.Id * 10));
        Check((await browse).ProcessId == browser.Id, "A browser request must return its real worker identity.");
        var secondId = Guid.NewGuid(); var second = files.OpenAsync(secondId, new(new(FileSelectionKind.Browse), "/second"), CancellationToken.None);
        var navigation = await files.NextAsync(browser.Id, CancellationToken.None);
        Check(navigation?.Folder == "/second" && processes.Count == 1, "Navigation must reuse the existing browser process.");
        files.Ready(browser.Id, new(secondId, browser.Id * 10)); await second;
        files.Complete(browser.Id, new(firstId, [])); browser.Stop(); files.Monitor();
        Check(files.Health().Count == 0 && files.Health().IssueId == Guid.Empty, "Intentional browser closure must not be reported as a crash.");
        var heldId = Guid.NewGuid(); var held = files.OpenAsync(heldId, new(new(FileSelectionKind.OpenFile)), CancellationToken.None);
        var heldProcess = processes[^1]; files.Ready(heldProcess.Id, new(heldId, heldProcess.Id * 10));
        for (int n = 0; n < 30; n++)
        {
            Guid id = Guid.NewGuid(); var crashed = files.OpenAsync(id, new(new(FileSelectionKind.OpenFiles)), CancellationToken.None);
            var process = processes[^1]; files.Ready(process.Id, new(id, process.Id * 10)); process.Stop(); files.Monitor();
            await RejectAsync<RuntimeFailure>(async () => await crashed);
            Check(!held.IsCompleted && !heldProcess.HasExited && files.Health().Count == 1, "One crashed picker must not cancel another picker or leak its process record.");
        }
        files.Complete(heldProcess.Id, new(heldId, ["/chosen/file.txt"]));
        Check((await held).Paths.SequenceEqual(["/chosen/file.txt"]), "A surviving picker must return its selection.");
        heldProcess.Stop(); files.Monitor();
        using (var cancellation = new CancellationTokenSource())
        {
            var cancelled = files.OpenAsync(Guid.NewGuid(), new(new(FileSelectionKind.Folder)), cancellation.Token);
            var process = processes[^1]; cancellation.Cancel();
            await RejectAsync<OperationCanceledException>(async () => await cancelled);
            Check(process.HasExited, "Caller cancellation must stop only its picker worker."); files.Monitor();
        }
        var notReady = files.OpenAsync(Guid.NewGuid(), new(new(FileSelectionKind.Browse)), CancellationToken.None);
        clock.Advance(FilesCoordinator.ReadyTimeout); files.Monitor();
        await RejectAsync<RuntimeFailure>(async () => await notReady); files.Monitor();
        var hungId = Guid.NewGuid(); var hung = files.OpenAsync(hungId, new(new(FileSelectionKind.OpenFile)), CancellationToken.None);
        var hungProcess = processes[^1]; files.Ready(hungProcess.Id, new(hungId, hungProcess.Id * 10));
        clock.Advance(FilesCoordinator.PulseTimeout); files.Monitor();
        await RejectAsync<RuntimeFailure>(async () => await hung);
        Check(hungProcess.HasExited, "A missed UI heartbeat must terminate the hung tool, not the desktop."); files.Monitor();
        var pending = new List<Task<FilesResult>>();
        for (int n = 0; n < FilesCoordinator.MaximumProcesses; n++) pending.Add(files.OpenAsync(Guid.NewGuid(), new(new(FileSelectionKind.OpenFile)), CancellationToken.None));
        await RejectAsync<RuntimeFailure>(async () => await files.OpenAsync(Guid.NewGuid(), new(new(FileSelectionKind.OpenFile)), CancellationToken.None));
        files.Dispose(); foreach (var task in pending) await RejectAsync<OperationCanceledException>(async () => await task);
        Check(files.Health().Count == 0, "Session shutdown must release all tool records.");
        var restart = new RuntimeRestartBudget(clock);
        Check(restart.TryStart() && restart.TryStart() && restart.TryStart() && !restart.TryStart(), "Core restart loops must be bounded.");
        clock.Advance(TimeSpan.FromMinutes(1)); Check(restart.TryStart(), "A bounded restart pause must eventually permit recovery.");
        Console.WriteLine("PASS: 30 simulated isolated picker crashes, browser reuse, cancellation, readiness/heartbeat timeouts, process bounds, and restart cooldown.");
    }
    private static async Task PipesAsync()
    {
        foreach (int length in new[] { -1, 0, RuntimeProtocol.MaximumFrameBytes + 1 })
            await RejectAsync<InvalidDataException>(() => RuntimeProtocol.ReadAsync<RuntimeRequest>(new MemoryStream(BitConverter.GetBytes(length)), CancellationToken.None));
        await RejectAsync<EndOfStreamException>(() => RuntimeProtocol.ReadAsync<RuntimeRequest>(new MemoryStream([10, 0, 0, 0, 1]), CancellationToken.None));
        string folder = Path.Combine(Path.GetTempPath(), "Nexus-pipes-" + Guid.NewGuid().ToString("N"));
        string name = "Nexus-checks-" + Guid.NewGuid().ToString("N");
        try
        {
            using var state = new CoreStateRepository(new StateStore(folder));
            FakeProcess? picker = null;
            using var files = new FilesCoordinator(_ => picker = new FakeProcess(Environment.ProcessId), (_, _) => true);
            var router = new CoreRouter(state, files); using var stop = new CancellationTokenSource();
            var server = new RuntimeServer(name, (pipe, request) => request.ProcessId == Environment.ProcessId
                && (!OperatingSystem.IsWindows() || PipePeer.IsClient(pipe, request.ProcessId)), router.DispatchAsync);
            var running = server.RunAsync(stop.Token);
            try
            {
                var client = new RuntimeClient(name, "desktop", Environment.ProcessId);
                var loaded = await client.CallAsync<StateReadResult>(RuntimeOperations.ReadState, new { }, TimeSpan.FromSeconds(3));
                loaded.State.QuickNote = "Actual pipe commit";
                var ack = await client.CallAsync<StateCommitResult>(RuntimeOperations.CommitState, new StateCommit(loaded.State, 0, Guid.NewGuid()), TimeSpan.FromSeconds(3));
                Check(ack.Revision == 1 && new StateStore(folder).Load().QuickNote == "Actual pipe commit", "A real framed pipe request must persist before acknowledgement.");
                var worker = new RuntimeClient(name, "files", Environment.ProcessId);
                await RejectAsync<RuntimeFailure>(() => worker.CallAsync<StateReadResult>(RuntimeOperations.ReadState, new { }, TimeSpan.FromSeconds(3)));
                Check(state.Revision == 1, "An unauthorized tool operation must leave state unchanged.");
                using (var raw = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
                {
                    await raw.ConnectAsync(3000);
                    var id = Guid.NewGuid();
                    await RuntimeProtocol.WriteAsync(raw, new RuntimeRequest(999, id, "desktop", Environment.ProcessId, RuntimeOperations.Health, RuntimeProtocol.Body(new { })), CancellationToken.None);
                    var rejected = await RuntimeProtocol.ReadAsync<RuntimeResponse>(raw, CancellationToken.None);
                    Check(!rejected.Ok && rejected.Code == "version" && rejected.Id == id, "A protocol mismatch must fail explicitly.");
                }
                using (var cancel = new CancellationTokenSource())
                {
                    var waiting = client.CallAsync<FilesResult>(RuntimeOperations.OpenFiles, new FilesLaunch(new(FileSelectionKind.OpenFile)), null, cancel.Token);
                    await Eventually(() => picker is not null); cancel.Cancel();
                    await RejectAsync<OperationCanceledException>(async () => await waiting);
                    await Eventually(() => picker!.HasExited); files.Monitor();
                    Check(files.Health().Count == 0, "A real pipe disconnect must cancel and release its picker.");
                }
                var concurrent = Enumerable.Range(0, 40).Select(_ => client.CallAsync<RuntimeHealth>(RuntimeOperations.Health, new { }, TimeSpan.FromSeconds(5))).ToArray();
                Check((await Task.WhenAll(concurrent)).All(h => h.Revision == 1), "Admission limits must preserve valid responses under concurrent requests.");
                Console.WriteLine("PASS: actual named-pipe requests, role rejection, protocol mismatch, frame limits, disconnect cancellation, and 40 concurrent health requests.");
            }
            finally { stop.Cancel(); await running.WaitAsync(TimeSpan.FromSeconds(5)); }
        }
        finally { Directory.Delete(folder, true); }
    }
}
