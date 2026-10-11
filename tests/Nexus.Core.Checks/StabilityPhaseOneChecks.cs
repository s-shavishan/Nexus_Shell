using Nexus.Shell.Models;
using Nexus.Shell.Services;

internal static class StabilityPhaseOneChecks
{
    internal static async Task RunAsync()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        async Task Reject<T>(Task task) where T : Exception
        { try { await task.WaitAsync(TimeSpan.FromSeconds(5)); } catch (T) { checks++; return; } throw new Exception("Expected " + typeof(T).Name); }
        static TaskCompletionSource<int> Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        using (var queue = new LatestWorkQueue())
        {
            int running = 0, maximum = 0, starts = 0;
            var blocked = Signal();
            async Task<int> Work(int result, Task? wait = null)
            {
                starts++; maximum = Math.Max(maximum, Interlocked.Increment(ref running));
                try { if (wait is not null) await wait; return result; }
                finally { Interlocked.Decrement(ref running); }
            }
            var first = queue.RunAsync(_ => Work(-1, blocked.Task));
            var superseded = Enumerable.Range(0, 200).Select(i => queue.RunAsync(_ => Work(i))).ToArray();
            Check(starts == 1 && maximum == 1, "Rapid navigation must not start parallel uncancellable folder reads.");
            blocked.SetResult(0);
            await Reject<OperationCanceledException>(first);
            int cancelled = 0;
            foreach (var request in superseded[..^1])
            { try { await request.WaitAsync(TimeSpan.FromSeconds(5)); } catch (OperationCanceledException) { cancelled++; } }
            Check(cancelled == 199, "Every intermediate request must be discarded.");
            Check(await superseded[^1].WaitAsync(TimeSpan.FromSeconds(5)) == 199 && starts == 2 && maximum == 1,
                "Only the latest pending destination may run after the slow read releases its slot.");
            await Reject<IOException>(queue.RunAsync<int>(_ => throw new IOException("device disconnected")));
            Check(await queue.RunAsync(_ => Task.FromResult(7)) == 7, "A failed read must release the slot for recovery.");
            var failing = Signal();
            var staleFailure = queue.RunAsync<int>(async _ => { await failing.Task; throw new IOException("old location failed"); });
            var replacement = queue.RunAsync(_ => Task.FromResult(8)); failing.SetResult(0);
            await Reject<OperationCanceledException>(staleFailure);
            Check(await replacement == 8, "An old location's failure must not replace the current location's result.");
            var cancelledRead = Signal();
            var abandoned = queue.RunAsync(_ => cancelledRead.Task); queue.Cancel(); cancelledRead.SetResult(9);
            await Reject<OperationCanceledException>(abandoned);
            Check(await queue.RunAsync(_ => Task.FromResult(10)) == 10, "Cancellation must leave the queue reusable.");
        }
        var closing = new LatestWorkQueue(); var release = Signal();
        var active = closing.RunAsync(_ => release.Task); bool ranAfterClose = false;
        var waiting = closing.RunAsync(_ => { ranAfterClose = true; return Task.FromResult(0); });
        closing.Dispose(); closing.Dispose(); release.SetResult(0);
        await Reject<OperationCanceledException>(active); await Reject<OperationCanceledException>(waiting);
        Check(!ranAfterClose, "Closing a surface must not start queued IO or publish an active result.");
        await Reject<ObjectDisposedException>(closing.RunAsync(_ => Task.FromResult(0)));

        var now = DateTimeOffset.Parse("2026-10-11T00:00:00Z");
        var inbox = new NotificationInbox(); int changed = 0; inbox.Changed += () => changed++;
        string title = new('t', 140), message = new('m', 2100);
        inbox.Publish(title, message, now: now); inbox.Publish(title, message, now: now.AddSeconds(1));
        Check(inbox.Items.Count == 1 && changed == 1, "Truncated notices must deduplicate before waking the UI or consuming the bounded inbox.");
        inbox.Publish(title, message, NoticeKind.Error, now.AddSeconds(2));
        Check(inbox.Items.Count == 2 && inbox.Items[0].Kind == NoticeKind.Error, "A severity escalation must not be swallowed by an informational notice.");
        inbox.Publish(title, message, now: now.AddMinutes(-1));
        Check(inbox.Items.Count == 3, "A backwards clock adjustment must not indefinitely suppress a repeated notice.");
        inbox.Publish(title, message, now: now.AddSeconds(5));
        Check(inbox.Items.Count == 4, "A notice at the deduplication boundary must be delivered.");
        Check(inbox.Items.All(i => i.Title.Length == 120 && i.Message.Length == 2000), "All persisted notices must stay bounded.");
        var serialized = new NotificationInbox(NotificationHistory.Normalize(inbox.Items));
        Check(serialized.Items.Count == 4, "Fixed notice behavior must survive history normalization.");

        bool cancelledBeforePath = false;
        try { FileCatalog.Read("\0", cancellation: new CancellationToken(true)); }
        catch (OperationCanceledException) { cancelledBeforePath = true; }
        Check(cancelledBeforePath, "An already cancelled read must stop before path validation or filesystem access.");
        string folder = Path.Combine(Path.GetTempPath(), "Nexus-phase01-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            Check(FileCatalog.Read(folder).Entries.Count == 0, "Empty folders remain readable.");
            string[] invalid = ["//?/C:/Nexus", "//./C:/Nexus", @"\\?\C:\Nexus", @"\\.\C:\Nexus", "relative", folder + "\0"];
            Check(invalid.All(p => !DesktopOrganization.ValidPlacePath(p)), "Device prefixes in either separator form and invalid paths must be rejected.");
            var state = new ShellState { QuickNote = "Keep my workspace", Places = invalid.Select(p => new PinnedPlace(Guid.NewGuid().ToString("N"), "Bad", p)).ToList() };
            state.Places.Add(new(Guid.NewGuid().ToString("N"), "Working folder", folder + Path.DirectorySeparatorChar));
            var store = new StateStore(Path.Combine(folder, "state")); store.Save(state);
            var loaded = store.Load();
            Check(loaded.QuickNote == state.QuickNote && loaded.Places.Count == 1 && loaded.Places[0].Path == folder && store.RecoveryMessage.Length == 0,
                "A malformed place must be discarded individually without resetting the user's workspace.");
            bool uncRejected = false;
            try { new FileMutationHistory().CreateFolder("//server/share", "test"); }
            catch (ArgumentException) { uncRejected = true; }
            Check(uncRejected, "Forward-slash UNC paths must not bypass the local-edit policy.");
        }
        finally { Directory.Delete(folder, true); }

        var areas = new[] { new ShellRect(-1920, 40, 1920, 1000), new ShellRect(0, 40, 800, 560), new ShellRect(100, -900, 600, 800), new ShellRect(0, 0, 1, 1) };
        foreach (var area in areas)
        {
            var panels = new[] { .5, 1, 1.5, 2, 4, double.NaN, double.PositiveInfinity }
                .Select(scale => DesktopLayout.CenteredPanelBounds(area, scale, 790, 650)).ToArray();
            Check(panels.All(p => p.Width > 0 && p.Height > 0 && p.X >= area.X && p.Y >= area.Y && p.Right <= area.Right && p.Bottom <= area.Bottom),
                "Repositioned panels must fit the current work area at small sizes, negative origins and fallback DPI.");
        }
        Console.WriteLine($"PASS: {checks} Phase 01 desktop stability assertions, including a 201-request cancellation burst, disposal, real state recovery and 28 display geometries.");
    }
}
