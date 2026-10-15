using System.ComponentModel;
using System.Runtime.InteropServices;
using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

public sealed record WindowRect(int X, int Y, int Width, int Height);
public sealed record WindowMove(RunningWindow Window, LayoutRect Bounds, bool Maximized = false);
public readonly record struct WindowRestoreResult(int Requested, int Queued, int Unavailable, int Pending);

// The transaction uses the same adapter on Windows and in failure-injection
// checks. No native commands are replaced by successful test-only branches.
internal interface IWindowLayoutNative
{
    bool Owns(RunningWindow window);
    bool CanResize(RunningWindow window);
    WindowLayouts.Placement Capture(RunningWindow window);
    void Prepare(RunningWindow window, WindowLayouts.Placement before);
    void Move(WindowMove move);
    bool Maximize(RunningWindow window);
    bool Restore(RunningWindow window, WindowLayouts.Placement before);
}

public sealed class WindowLayouts
{
    private readonly List<(RunningWindow Window, Placement Before)> _undo = [];
    private readonly List<(RunningWindow Window, Placement Before)> _recovery = [];
    private readonly IWindowLayoutNative _native;
    public bool CanUndo => _recovery.Count > 0 || _undo.Count > 0;
    public bool HasPendingRecovery => _recovery.Count > 0;
    public WindowLayouts() : this(new NativeLayout()) { }
    internal WindowLayouts(IWindowLayoutNative native) => _native = native ?? throw new ArgumentNullException(nameof(native));
    public static WindowRect[] Plan(WindowRect area, int count, string layout)
    {
        if (count is < 1 or > 4 || area.Width < 160 || area.Height < 160) throw new ArgumentException("Choose one to four windows and a usable display.");
        int columns = layout == "Stack" ? 1 : layout == "Grid" ? Math.Min(2, count) : count;
        int rows = (count + columns - 1) / columns;
        return Enumerable.Range(0, count).Select(i =>
        {
            int column = i % columns, row = i / columns;
            int left = area.X + area.Width * column / columns, right = area.X + area.Width * (column + 1) / columns;
            int top = area.Y + area.Height * row / rows, bottom = area.Y + area.Height * (row + 1) / rows;
            return new WindowRect(left, top, right - left, bottom - top);
        }).ToArray();
    }
    private void Validate(RunningWindow window)
    {
        if (!_native.Owns(window))
            throw new InvalidOperationException("A selected window closed or changed. Refresh the window list.");
    }
    private static bool Owns(RunningWindow window) => window.Handle != IntPtr.Zero && window.ProcessId > 0 && IsWindow(window.Handle)
        && GetWindowThreadProcessId(window.Handle, out uint pid) != 0 && pid == window.ProcessId;
    public int Arrange(IReadOnlyList<RunningWindow> windows, string layout)
    {
        if (windows.Count is < 1 or > 4 || windows.Select(w => w.Handle).Distinct().Count() != windows.Count)
            throw new ArgumentException("Select one to four different windows.");
        var monitor = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        foreach (var window in windows) Validate(window);
        if (!GetMonitorInfo(MonitorFromWindow(windows[0].Handle, 2), ref monitor)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var area = new WindowRect(monitor.Work.Left, monitor.Work.Top, monitor.Work.Right - monitor.Work.Left, monitor.Work.Bottom - monitor.Work.Top);
        var plan = Plan(area, windows.Count, layout);
        return Apply(windows.Select((window, i) => new WindowMove(window, new(plan[i].X, plan[i].Y, plan[i].Width, plan[i].Height))).ToArray());
    }
    public int Apply(IReadOnlyList<WindowMove> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);
        if (HasPendingRecovery) throw new InvalidOperationException("Use Retry previous positions before arranging again.");
        if (moves.Count is < 1 or > SavedWindowLayouts.MaximumWindows)
            throw new ArgumentException("Choose one to twelve different windows.");
        var requested = moves.ToArray();
        if (requested.Any(move => move is null || move.Window is null)
            || requested.Select(move => move.Window.Handle).Distinct().Count() != requested.Length)
            throw new ArgumentException("Choose one to twelve different windows.");
        // Reject invalid geometry before any native reads or mutations. Keep
        // the immutable request snapshot if a caller edits its source list.
        foreach (var move in requested)
            if (move.Bounds.Width < 1 || move.Bounds.Height < 1
                || (long)move.Bounds.X + move.Bounds.Width > int.MaxValue
                || (long)move.Bounds.Y + move.Bounds.Height > int.MaxValue)
                throw new ArgumentException("A window has no usable area.");
        var before = new List<(RunningWindow Window, Placement Before)>();
        foreach (var move in requested)
        {
            var window = move.Window; Validate(window);
            if (!_native.CanResize(window))
                throw new InvalidOperationException("A selected window does not support resizing.");
            var placement = _native.Capture(window);
            Validate(window);
            before.Add((window, placement));
        }
        int touched = 0;
        try
        {
            for (int i = 0; i < requested.Length; i++)
            {
                var move = requested[i]; Validate(move.Window);
                // A failed native call may already have queued work. Restore
                // every attempted window, but never touch later batch entries.
                touched = i + 1;
                _native.Prepare(move.Window, before[i].Before);
                Validate(move.Window);
                _native.Move(move);
                if (move.Maximized)
                {
                    Validate(move.Window);
                    if (!_native.Maximize(move.Window)) throw new InvalidOperationException("Windows did not accept a maximize request.");
                }
                Validate(move.Window);
            }
        }
        catch (Exception applyError)
        {
            // Restore in reverse native move order, retaining the exact HWND
            // ownership guard. Do not claim success when a window closed or a
            // foreign process replaced its handle midway through the batch.
            int queued = 0, unavailable = 0;
            for (int i = touched - 1; i >= 0; i--)
                switch (TryRestore(before[i]))
                {
                    case RestoreOutcome.Queued: queued++; break;
                    case RestoreOutcome.Unavailable: unavailable++; break;
                    case RestoreOutcome.Failed: _recovery.Add(before[i]); break;
                }
            _recovery.Reverse();
            throw new InvalidOperationException(
                applyError.Message + $" Restoring {queued} previous positions; {_recovery.Count} need retry; {unavailable} windows closed or changed.", applyError);
        }
        _undo.Clear(); _undo.AddRange(before); return requested.Length;
    }
    public int Undo() => UndoDetailed().Queued;
    public WindowRestoreResult UndoDetailed()
    {
        var pending = HasPendingRecovery ? _recovery : _undo;
        int requested = pending.Count, queued = 0, unavailable = 0;
        foreach (var entry in pending.AsEnumerable().Reverse().ToArray())
        {
            switch (TryRestore(entry))
            {
                case RestoreOutcome.Queued: queued++; pending.Remove(entry); break;
                case RestoreOutcome.Unavailable: unavailable++; pending.Remove(entry); break;
                // Retain native failures so Undo can retry them explicitly.
            }
        }
        int remaining = pending.Count;
        // An ordinary Undo can also be rejected. Promote those exact entries
        // into recovery so a new Apply cannot overwrite their only snapshot.
        if (ReferenceEquals(pending, _undo) && remaining > 0)
        { _recovery.AddRange(_undo); _undo.Clear(); }
        return new(requested, queued, unavailable, remaining);
    }
    private enum RestoreOutcome { Queued, Unavailable, Failed }
    private RestoreOutcome TryRestore((RunningWindow Window, Placement Before) entry)
    {
        try
        {
            if (!_native.Owns(entry.Window)) return RestoreOutcome.Unavailable;
            return _native.Restore(entry.Window, entry.Before) ? RestoreOutcome.Queued : RestoreOutcome.Failed;
        }
        catch { return RestoreOutcome.Failed; }
    }
    private sealed class NativeLayout : IWindowLayoutNative
    {
        public bool Owns(RunningWindow window) => WindowLayouts.Owns(window);
        public bool CanResize(RunningWindow window) => (GetWindowLongPtr(window.Handle, -16).ToInt64() & 0x00040000) != 0;
        public Placement Capture(RunningWindow window)
        {
            var placement = new Placement { Length = (uint)Marshal.SizeOf<Placement>() };
            if (!GetWindowPlacement(window.Handle, ref placement)) throw new Win32Exception(Marshal.GetLastWin32Error());
            return placement;
        }
        public void Prepare(RunningWindow window, Placement placement)
        {
            placement.Flags = 4; placement.Show = 4; // WPF_ASYNCWINDOWPLACEMENT / SW_SHOWNOACTIVATE
            if (!SetWindowPlacement(window.Handle, ref placement)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        public void Move(WindowMove move)
        {
            var rect = move.Bounds;
            // SetWindowPos takes screen coordinates, not WINDOWPLACEMENT's
            // workspace coordinates. Keep both APIs' coordinate domains intact.
            if (!SetWindowPos(move.Window.Handle, IntPtr.Zero, rect.X, rect.Y, rect.Width, rect.Height, 0x4214))
                throw new Win32Exception(Marshal.GetLastWin32Error()); // ASYNCWINDOWPOS | NOZORDER | NOACTIVATE | NOOWNERZORDER
        }
        public bool Maximize(RunningWindow window)
        {
            // ShowWindowAsync reports successful operation START, unlike
            // ShowWindow's previous-visibility return. Acceptance is not proof
            // the other app has processed the queued show-window event yet.
            return ShowWindowAsync(window.Handle, 3);
        }
        public bool Restore(RunningWindow window, Placement placement)
        { placement.Flags |= 4; return SetWindowPlacement(window.Handle, ref placement); }
    }
    public static bool Lock() => LockWorkStation();
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct Placement
    { public uint Length, Flags, Show; public Point Minimum, Maximum; public Rect Normal; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public uint Size; public Rect Bounds, Work; public uint Flags; }
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowPlacement(IntPtr window, ref Placement placement);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPlacement(IntPtr window, ref Placement placement);
    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool LockWorkStation();
}
