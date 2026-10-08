using Nexus.Shell.Interop;
using Nexus.Shell.Models;
using Nexus.Shell.Services;
using System.Runtime.InteropServices;

internal static class DesktopFoundationChecks
{
    internal static void Run(Action<bool, string> check)
    {
        string folder = Path.Combine(Path.GetTempPath(), "Nexus-session-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new StateStore(folder);
            var session = new ShellSession(store);
            var shared = session.State;
            shared.QuickNote = "Before opening Sections";
            int changes = 0;
            session.Changed += () => changes++;
            double remaining = 321;
            Func<ShellState> firstWindow = () => { shared.FocusRemainingSeconds = remaining; return shared.Snapshot(); };
            session.AttachSnapshot(firstWindow);
            shared.QuickNote = "Edited in Sections";
            session.NotifyChanged(); session.SaveAsync().GetAwaiter().GetResult();
            check(new StateStore(folder).Load().FocusRemainingSeconds == 321, "A desktop save must capture the live Sections timer checkpoint.");
            session.Capture(); session.DetachSnapshot(firstWindow);
            remaining = 0; // The closed window is no longer a snapshot provider.
            shared.CompactDock = true;
            session.NotifyChanged(); session.SaveAsync().GetAwaiter().GetResult();
            var afterClose = new StateStore(folder).Load();
            check(changes == 2 && afterClose.QuickNote == "Edited in Sections" && afterClose.CompactDock && afterClose.FocusRemainingSeconds == 321,
                "Closing Sections must leave the shared session writable and retain notes, timer checkpoint and taskbar preferences.");
            Func<ShellState> secondWindow = () => { shared.FocusRemainingSeconds = 222; return shared.Snapshot(); };
            session.AttachSnapshot(secondWindow);
            session.DetachSnapshot(firstWindow); // A stale close must not detach a newer window.
            check(session.Capture().FocusRemainingSeconds == 222, "A stale close must not detach the reopened window’s snapshot provider.");
            shared.QuickNote = "Reopened Sections";
            var pending = new List<Task>();
            for (int i = 0; i < 32; i++) { shared.DisplayName = "Draft " + i; pending.Add(session.SaveAsync()); }
            Task.WhenAll(pending).GetAwaiter().GetResult();
            check(new StateStore(folder).Load().DisplayName == "Draft 31", "Queued saves must preserve invocation order.");
            for (int i = 0; i < 16; i++) { shared.DisplayName = "Older queued " + i; pending.Add(session.SaveAsync()); }
            shared.DisplayName = "Final desktop";
            session.DetachSnapshot(secondWindow); session.SaveFinal();
            Task.WhenAll(pending).GetAwaiter().GetResult();
            shared.DisplayName = "Too late"; session.NotifyChanged(); session.SaveAsync().GetAwaiter().GetResult();
            var final = new StateStore(folder).Load();
            check(final.DisplayName == "Final desktop" && final.QuickNote == "Reopened Sections" && changes == 2,
                "Only desktop shutdown may finalize saving, and queued or later writes must not overwrite its final snapshot.");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        foreach (var setup in new[] {
            (new ShellRect(0, 0, 1920, 1080), new ShellRect(0, 972, 1920, 68), 1.0),
            (new ShellRect(-2560, -120, 2560, 1440), new ShellRect(-2560, 1152, 2560, 102), 1.5),
            (new ShellRect(0, 0, 640, 480), new ShellRect(0, 344, 640, 136), 2.0) })
        {
            var menu = DesktopLayout.MenuBounds(setup.Item1, setup.Item2, setup.Item3);
            check(menu.X >= setup.Item1.X && menu.Right <= setup.Item1.Right && menu.Y >= setup.Item1.Y && menu.Bottom < setup.Item2.Y,
                "Start must fit above the taskbar on small displays, negative monitor origins and scaled displays.");
        }
        check(Marshal.SizeOf<ShellLayerInterop.AppBarData>() == (IntPtr.Size == 8 ? 48 : 36)
            && Marshal.SizeOf<ShellLayerInterop.WindowPos>() == (IntPtr.Size == 8 ? 40 : 28)
            && Marshal.SizeOf<ShellLayerInterop.MonitorInfo>() == 40,
            "Native desktop and appbar structures must preserve Win32 pointer alignment.");
        var icons = DesktopCatalog.Read();
        check(icons.Count <= 67 && icons[0].Id == "sections" && icons[0].Target == "nexus:sections" && icons.Select(i => i.Id).Distinct().Count() == icons.Count,
            "The desktop must always expose Sections as a shortcut and bound external file discovery.");
        Console.WriteLine("PASS: desktop session saves after Sections closes/reopens, ordered/final writes, scaled Start bounds, native appbar layout and desktop shortcuts.");
    }
}
