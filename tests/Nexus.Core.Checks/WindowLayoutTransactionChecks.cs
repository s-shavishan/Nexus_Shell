using Nexus.Shell.Models;
using Nexus.Shell.Services;

internal static class WindowLayoutTransactionChecks
{
    private sealed class Native : IWindowLayoutNative
    {
        internal readonly Dictionary<IntPtr, (int Pid, WindowLayouts.Placement Placement)> Windows = [];
        internal readonly List<(string Step, IntPtr Window)> Commands = [];
        internal readonly HashSet<IntPtr> RejectRestore = [], ThrowRestore = [];
        internal string FailStep = "";
        internal IntPtr FailWindow;
        internal Action<string, RunningWindow>? Before;
        internal Native(IEnumerable<RunningWindow> windows)
        {
            foreach (var window in windows)
                Windows[window.Handle] = (window.ProcessId, new()
                {
                    Length = 44, Flags = 0, Show = (uint)(1 + window.Handle.ToInt32() % 3),
                    Minimum = new() { X = -1, Y = -2 }, Maximum = new() { X = -3, Y = -4 },
                    Normal = new() { Left = -900, Top = 40, Right = -300, Bottom = 440 }
                });
        }
        public bool Owns(RunningWindow window) => Windows.TryGetValue(window.Handle, out var current)
            && current.Pid == window.ProcessId;
        public bool CanResize(RunningWindow window) => FailStep != "Resize" || FailWindow != window.Handle;
        public WindowLayouts.Placement Capture(RunningWindow window)
        {
            Before?.Invoke("Capture", window);
            if (FailStep == "Capture" && FailWindow == window.Handle) throw new InvalidOperationException("Capture rejected");
            return Windows[window.Handle].Placement;
        }
        private bool Command(string step, RunningWindow window)
        {
            Commands.Add((step, window.Handle)); Before?.Invoke(step, window);
            return FailStep != step || FailWindow != window.Handle;
        }
        public void Prepare(RunningWindow window, WindowLayouts.Placement before)
        {
            if (!Command("Prepare", window)) throw new InvalidOperationException("Prepare rejected");
            before.Flags = 4; before.Show = 4;
            Windows[window.Handle] = (window.ProcessId, before);
        }
        public void Move(WindowMove move)
        {
            if (!Command("Move", move.Window)) throw new InvalidOperationException("Move rejected");
            var current = Windows[move.Window.Handle]; var placement = current.Placement;
            placement.Normal = new() { Left = move.Bounds.X, Top = move.Bounds.Y,
                Right = move.Bounds.X + move.Bounds.Width, Bottom = move.Bounds.Y + move.Bounds.Height };
            Windows[move.Window.Handle] = (current.Pid, placement);
        }
        public bool Maximize(RunningWindow window)
        {
            if (!Command("Maximize", window)) return false;
            var current = Windows[window.Handle]; var placement = current.Placement; placement.Show = 3;
            Windows[window.Handle] = (current.Pid, placement); return true;
        }
        public bool Restore(RunningWindow window, WindowLayouts.Placement before)
        {
            Command("Restore", window);
            if (ThrowRestore.Contains(window.Handle)) throw new InvalidOperationException("Restore threw");
            if (RejectRestore.Contains(window.Handle)) return false;
            Windows[window.Handle] = (window.ProcessId, before); return true;
        }
    }

    internal static void Run()
    {
        int checks = 0;
        void Check(bool pass, string message)
        { checks++; if (!pass) throw new InvalidOperationException(message); }
        void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); } catch (Exception error) when (error is InvalidOperationException or ArgumentException) { rejected = true; }
            Check(rejected, message);
        }
        static RunningWindow[] Windows(int count) => Enumerable.Range(1, count)
            .Select(i => new RunningWindow(new IntPtr(i), "App " + i, "app", i + 100)).ToArray();
        static WindowMove[] Moves(RunningWindow[] windows) => windows.Select(window =>
            new WindowMove(window, new(-1400 + window.Handle.ToInt32() * 40, -200, 300, 250), true)).ToArray();
        Check(System.Runtime.InteropServices.Marshal.SizeOf<WindowLayouts.Placement>() == 44,
            "Testable placement snapshots must retain the native WINDOWPLACEMENT layout.");

        // Every native mutation stage can fail at every batch position.
        // Untouched windows must receive NO rollback call at all.
        for (int count = 1; count <= SavedWindowLayouts.MaximumWindows; count++)
        foreach (string step in new[] { "Prepare", "Move", "Maximize" })
        for (int position = 1; position <= count; position++)
        {
            var windows = Windows(count); var native = new Native(windows);
            var original = native.Windows.ToDictionary(pair => pair.Key, pair => pair.Value.Placement);
            var layouts = new WindowLayouts(native);
            native.FailStep = step; native.FailWindow = new(position);
            Reject(() => layouts.Apply(Moves(windows)), "A rejected " + step + " request cannot count as a successful arrangement.");
            var restored = native.Commands.Where(command => command.Step == "Restore").Select(command => command.Window).ToArray();
            Check(restored.SequenceEqual(windows.Take(position).Reverse().Select(window => window.Handle)),
                "Rollback must restore attempted windows in reverse order, including a partially accepted failing command.");
            Check(native.Windows.All(pair => pair.Value.Placement.Equals(original[pair.Key])),
                "Successful rollback must preserve original show state, normal rectangle and min/max placement.");
            Check(!layouts.HasPendingRecovery && !layouts.CanUndo,
                "A failed first arrangement with accepted rollback must not create a successful Undo history.");
            Check(native.Commands.Where(command => command.Step != "Restore").All(command => command.Window.ToInt64() <= position),
                "No later window may receive native mutations after a rejection.");
        }

        {
            var windows = Windows(4); var native = new Native(windows); var layouts = new WindowLayouts(native);
            var original = native.Windows.ToDictionary(pair => pair.Key, pair => pair.Value.Placement);
            Check(layouts.Apply(Moves(windows)) == 4 && layouts.CanUndo, "A successful batch must retain an Undo snapshot.");
            var previous = native.Windows.ToDictionary(pair => pair.Key, pair => pair.Value.Placement);
            native.FailStep = "Move"; native.FailWindow = windows[1].Handle;
            native.RejectRestore.Add(windows[0].Handle);
            Reject(() => layouts.Apply(Moves(windows).Select(move => move with { Bounds = new(10, 20, 500, 400) }).ToArray()),
                "Partial rollback must be surfaced as a failure.");
            Check(layouts.HasPendingRecovery && layouts.CanUndo, "Rejected rollback needs an explicit retry snapshot.");
            int commands = native.Commands.Count;
            Reject(() => layouts.Apply(Moves(windows)), "A new Apply cannot bury unresolved positions.");
            Check(native.Commands.Count == commands, "Blocked Apply must not issue any native command.");
            var failedRetry = layouts.UndoDetailed();
            Check(failedRetry == new WindowRestoreResult(1, 0, 0, 1) && layouts.HasPendingRecovery,
                "A rejected retry must retain exactly its failed window, not replay accepted restorations.");
            native.RejectRestore.Clear(); native.FailStep = "";
            Check(layouts.UndoDetailed() == new WindowRestoreResult(1, 1, 0, 0) && !layouts.HasPendingRecovery,
                "Explicit retry must request the failed transaction's previous position.");
            Check(native.Windows.All(pair => pair.Value.Placement.Equals(previous[pair.Key])) && layouts.CanUndo,
                "Recovering a failed new arrangement must preserve the earlier successful Undo history.");
            Check(layouts.Undo() == 4 && !layouts.CanUndo
                && native.Windows.All(pair => pair.Value.Placement.Equals(original[pair.Key])),
                "The original successful arrangement must remain undoable after failure recovery.");
        }

        {
            var windows = Windows(3); var native = new Native(windows); var layouts = new WindowLayouts(native);
            layouts.Apply(Moves(windows)); native.Commands.Clear();
            native.Windows[windows[1].Handle] = (9000, native.Windows[windows[1].Handle].Placement);
            native.ThrowRestore.Add(windows[0].Handle);
            Check(layouts.UndoDetailed() == new WindowRestoreResult(3, 1, 1, 1),
                "Undo must distinguish accepted restoration, a reused HWND and a native exception.");
            Check(layouts.HasPendingRecovery && native.Commands.All(command => command.Window != windows[1].Handle),
                "A reused HWND cannot receive restoration and failed ordinary Undo must block overwrite.");
            native.ThrowRestore.Clear(); native.Commands.Clear();
            Check(layouts.UndoDetailed() == new WindowRestoreResult(1, 1, 0, 0)
                && native.Commands.Single().Window == windows[0].Handle && !layouts.CanUndo,
                "Retry must affect only the still-pending owned window.");
        }

        {
            var windows = Windows(3); var native = new Native(windows); var layouts = new WindowLayouts(native);
            native.Before = (step, window) =>
            {
                if (step == "Move" && window == windows[0]) native.Windows.Remove(windows[1].Handle);
            };
            Reject(() => layouts.Apply(Moves(windows)), "A window closing after preflight must interrupt Apply.");
            Check(native.Commands.Where(command => command.Step == "Restore").Select(command => command.Window)
                .SequenceEqual(new[] { windows[0].Handle }), "Ownership loss before the next command must not roll back untouched apps.");
            Check(!layouts.CanUndo, "A closed untouched window cannot become a recovery entry.");
        }

        foreach (string failure in new[] { "Capture", "Resize" })
        {
            var windows = Windows(3); var native = new Native(windows) { FailStep = failure, FailWindow = new(3) };
            var layouts = new WindowLayouts(native);
            Reject(() => layouts.Apply(Moves(windows)), "Every window must pass capture/capability preflight before mutation.");
            Check(native.Commands.Count == 0 && !layouts.CanUndo, "Preflight failure cannot issue rollback or native layout commands.");
        }

        {
            var windows = Windows(2); var native = new Native(windows); var layouts = new WindowLayouts(native);
            var requests = Moves(windows).ToList();
            native.Before = (step, _) => { if (step == "Capture") requests.Clear(); };
            Check(layouts.Apply(requests) == 2, "Apply must use its captured request snapshot if the caller changes the source list.");
            Check(layouts.Undo() == 2, "Snapshot isolation must retain both actual original windows for Undo.");
        }

        {
            var windows = Windows(2); var native = new Native(windows); var layouts = new WindowLayouts(native);
            foreach (var invalid in new IReadOnlyList<WindowMove>[]
            {
                [], [Moves(windows)[0], Moves(windows)[0]],
                [new(windows[0], new(0, 0, 0, 300))],
                [new(windows[0], new(int.MaxValue, 0, 10, 300))],
                [new(windows[0], new(0, int.MaxValue, 300, 10))],
                Moves(Windows(13))
            }) Reject(() => layouts.Apply(invalid), "Invalid batch size, duplicate handle or overflowing bounds must be rejected.");
            Check(native.Commands.Count == 0 && !layouts.CanUndo, "Rejected input cannot change Undo state or native windows.");
        }
        Console.WriteLine($"PASS: {checks} window placement transaction checks; rejection at every batch step, touched-only reverse rollback, retryable restoration, retained prior Undo, stale HWNDs, preflight and request isolation. No Windows app moved.");
    }
}
