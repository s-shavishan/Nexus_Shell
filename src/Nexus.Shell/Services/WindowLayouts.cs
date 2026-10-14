using System.ComponentModel;
using System.Runtime.InteropServices;
using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

public sealed record WindowRect(int X, int Y, int Width, int Height);
public sealed record WindowMove(RunningWindow Window, LayoutRect Bounds, bool Maximized = false);

public sealed class WindowLayouts
{
    private readonly List<(RunningWindow Window, Placement Before)> _undo = [];
    public bool CanUndo => _undo.Count > 0;
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
    private static void Validate(RunningWindow window)
    {
        if (!Owns(window))
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
        if (moves.Count is < 1 or > SavedWindowLayouts.MaximumWindows || moves.Select(m => m.Window.Handle).Distinct().Count() != moves.Count)
            throw new ArgumentException("Choose one to twelve different windows.");
        var before = new List<(RunningWindow Window, Placement Before)>();
        foreach (var move in moves)
        {
            var window = move.Window; Validate(window);
            if ((GetWindowLongPtr(window.Handle, -16).ToInt64() & 0x00040000) == 0)
                throw new InvalidOperationException("A selected window does not support resizing.");
            if (move.Bounds.Width < 1 || move.Bounds.Height < 1) throw new ArgumentException("A window has no usable area.");
            var placement = new Placement { Length = (uint)Marshal.SizeOf<Placement>() };
            if (!GetWindowPlacement(window.Handle, ref placement)) throw new Win32Exception(Marshal.GetLastWin32Error());
            before.Add((window, placement));
        }
        try
        {
            for (int i = 0; i < moves.Count; i++)
            {
                var move = moves[i]; Validate(move.Window);
                var placement = before[i].Before;
                var rect = move.Bounds; placement.Flags = 4; placement.Show = 4; // ASYNCWINDOWPLACEMENT / SW_SHOWNOACTIVATE
                if (!SetWindowPlacement(move.Window.Handle, ref placement)) throw new Win32Exception(Marshal.GetLastWin32Error());
                // SetWindowPos takes screen coordinates; saved placement uses workspace
                // coordinates. Keeping these separate also handles cross-monitor moves.
                Validate(move.Window);
                if (!SetWindowPos(move.Window.Handle, IntPtr.Zero, rect.X, rect.Y, rect.Width, rect.Height, 0x4214))
                    throw new Win32Exception(Marshal.GetLastWin32Error()); // ASYNCWINDOWPOS | NOZORDER | NOACTIVATE | NOOWNERZORDER
                if (move.Maximized)
                {
                    Validate(move.Window);
                    // ShowWindowAsync reports whether the window WAS visible,
                    // not whether maximizing succeeded. A minimized/hidden app
                    // legitimately returns false here; treating it as failure
                    // caused false rollback and lost staged layouts.
                    _ = ShowWindowAsync(move.Window.Handle, 3);
                    Validate(move.Window);
                }
            }
        }
        catch (Exception applyError)
        {
            // Restore in reverse native move order, retaining the exact HWND
            // ownership guard. Do not claim success when a window closed or a
            // foreign process replaced its handle midway through the batch.
            int failed = 0;
            for (int i = before.Count - 1; i >= 0; i--)
                if (!TryRestore(before[i])) failed++;
            if (failed > 0)
                throw new InvalidOperationException(
                    $"Placement interrupted; {failed} previous window positions could not be restored. Check the affected apps.",
                    applyError);
            throw;
        }
        _undo.Clear(); _undo.AddRange(before); return moves.Count;
    }
    public int Undo()
    {
        int restored = 0;
        foreach (var entry in _undo.ToArray())
        {
            if (!Owns(entry.Window)) { _undo.Remove(entry); continue; }
            if (TryRestore(entry)) { restored++; _undo.Remove(entry); }
        }
        return restored;
    }
    private static bool TryRestore((RunningWindow Window, Placement Before) entry)
    {
        try { Validate(entry.Window); var placement = entry.Before; placement.Flags |= 4; return SetWindowPlacement(entry.Window.Handle, ref placement); }
        catch { return false; }
    }
    public static bool Lock() => LockWorkStation();
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Placement
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
