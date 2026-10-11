using Nexus.Runtime;
using Nexus.Shell.Models;
using Nexus.Shell.Services;

internal static class StateWriterStabilityChecks
{
    internal static async Task RunAsync()
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
        async Task Reject<T>(Func<Task> action) where T : Exception
        { try { await action(); } catch (T) { checks++; return; } throw new Exception("Expected " + typeof(T).Name); }
        var folder = Path.Combine(Path.GetTempPath(), "Nexus-writer-phase01-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var state = new CoreStateRepository(new StateStore(folder)); int commits = 0;
            var writer = new RevisionedStateWriter(0, () => Task.FromResult(state.Read()), request =>
            { commits++; return Task.FromResult(state.Commit(request)); });
            await Reject<InvalidDataException>(() => writer.SaveAsync(new() { QuickNote = new('x', StateStore.MaximumBytes + 1) }));
            Check(commits == 0 && writer.Revision == 0, "An oversized snapshot must not enter the uncertain commit queue.");
            await writer.SaveAsync(new() { QuickNote = "Corrected" });
            Check(commits == 1 && state.Read().State.QuickNote == "Corrected", "Correcting oversized content must restore saving without restarting Nexus.");

            bool reject = true; var ids = new List<Guid>();
            var remote = new RevisionedStateWriter(state.Revision, () => Task.FromResult(state.Read()), request =>
            {
                ids.Add(request.CommitId);
                if (reject) { reject = false; throw new RuntimeFailure("state-rejected", "Injected validation failure before write"); }
                return Task.FromResult(state.Commit(request));
            });
            await Reject<RuntimeFailure>(() => remote.SaveAsync(new() { QuickNote = "Rejected" }));
            await remote.SaveAsync(new() { QuickNote = "Accepted after correction" });
            Check(ids.Count == 2 && ids[0] != ids[1] && state.Read().State.QuickNote == "Accepted after correction",
                "An explicit rejection must not replay obsolete content on the next save.");

            var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bool first = true;
            var serial = new RevisionedStateWriter(state.Revision, () => Task.FromResult(state.Read()), async request =>
            { if (first) { first = false; await blocked.Task; } return state.Commit(request); });
            var saving = serial.SaveAsync(new() { QuickNote = "First" });
            var snapshot = new ShellState { QuickNote = "Captured at enqueue" };
            var waiting = serial.SaveAsync(snapshot); snapshot.QuickNote = "Later mutable edit"; blocked.SetResult();
            await Task.WhenAll(saving, waiting).WaitAsync(TimeSpan.FromSeconds(5));
            Check(state.Read().State.QuickNote == "Captured at enqueue", "Queued writes must freeze state before waiting for an earlier commit.");

            using var files = new FilesCoordinator(_ => throw new InvalidOperationException("No Files launch expected"), (_, _) => false);
            var router = new CoreRouter(state, files);
            long revision = state.Revision;
            var invalid = new RuntimeRequest(RuntimeProtocol.Version, Guid.NewGuid(), "desktop", Environment.ProcessId,
                RuntimeOperations.CommitState, RuntimeProtocol.Body(new StateCommit(new(), -1, Guid.NewGuid())));
            string code = "";
            try { await router.DispatchAsync(invalid, CancellationToken.None); } catch (RuntimeFailure e) { code = e.Code; }
            Check(code == "state-rejected" && state.Revision == revision, "Core must identify validation rejection without acknowledging or writing it.");
            var conflict = invalid with { Id = Guid.NewGuid(), Payload = RuntimeProtocol.Body(new StateCommit(new(), 0, Guid.NewGuid())) };
            try { await router.DispatchAsync(conflict, CancellationToken.None); } catch (RuntimeFailure e) { code = e.Code; }
            Check(code == "conflict" && state.Revision == revision, "Revision conflicts must retain their protective semantics.");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        Console.WriteLine($"PASS: {checks} Phase 01 state-writer assertions; rejected-save recovery, immutable queued snapshots and production Core rejection/conflict routing.");
    }
}
