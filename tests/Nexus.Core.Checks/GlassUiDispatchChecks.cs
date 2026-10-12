using Nexus.Shell.Services;

internal static class GlassUiDispatchChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }

        var pending = new Queue<Action>();
        bool Queue(Action work) { pending.Enqueue(work); return true; }
        var errors = new List<string>();
        void Report(Exception error) => errors.Add(error.Message);
        int updates = 0;
        bool live = true;
        Check(GlassUiDispatch.TryPost(Queue, () => live, () => updates++, Report),
            "Live native state updates must enqueue successfully.");
        Check(pending.Count == 1, "An accepted dispatch must own a callback.");
        pending.Dequeue()();
        Check(updates == 1 && errors.Count == 0, "Live callbacks must update exactly once without spurious failure logging.");

        Check(GlassUiDispatch.TryPost(Queue, () => live, () => updates++, Report),
            "Detached native state callbacks can be queued before close.");
        live = false; pending.Dequeue()();
        Check(updates == 1, "A callback pending during close must not touch detached XAML.");
        live = true;

        Check(GlassUiDispatch.TryPost(Queue, () => true, () => throw new InvalidOperationException("XAML surface closed"), Report),
            "The dispatcher should accept a callback even if its future UI update will fail.");
        pending.Dequeue()();
        Check(errors.SequenceEqual(["XAML surface closed"]), "A WinUI callback exception must be contained and reported.");

        Check(GlassUiDispatch.TryPost(Queue, () => throw new InvalidOperationException("generation check failed"),
            () => updates++, Report), "A validity check runs only when the dispatcher executes.");
        pending.Dequeue()();
        Check(errors[^1] == "generation check failed" && updates == 1,
            "An unexpected generation-check exception must not escape the dispatcher.");

        Check(!GlassUiDispatch.TryPost(_ => false, () => true, () => updates++, Report),
            "A closing WinUI dispatcher must reject the update cleanly.");
        Check(updates == 1, "A rejected dispatch must never run UI code.");
        Check(!GlassUiDispatch.TryPost(_ => throw new InvalidOperationException("dispatcher shut down"),
            () => true, () => updates++, Report),
            "Synchronous queue exceptions must not propagate to native callback delivery.");
        Check(errors[^1] == "dispatcher shut down", "Dispatcher exceptions must remain observable.");

        Check(GlassUiDispatch.TryPost(Queue, () => true,
            () => throw new InvalidOperationException("update failed"),
            _ => throw new IOException("Logging unavailable")),
            "A broken diagnostic sink must not block callback admission.");
        pending.Dequeue()();
        Check(updates == 1, "A broken logging sink must not crash or perform unrelated updates.");

        for (int i = 0; i < 500; i++)
        {
            bool stillCurrent = true;
            Check(GlassUiDispatch.TryPost(Queue, () => stillCurrent, () => updates++, Report),
                "Rapid controller turnover must permit harmless event queueing.");
            stillCurrent = false;
            pending.Dequeue()();
        }
        Check(updates == 1 && pending.Count == 0,
            "Late callbacks from 500 replaced controllers must never repaint the active surface.");

        Console.WriteLine($"PASS: {checks} Liquid Glass UI dispatch assertions; stale callbacks, 500 close/replacement cycles, rejection, throwing XAML and logging recovery.");
    }
}
