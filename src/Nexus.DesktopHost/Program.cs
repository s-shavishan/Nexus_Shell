using Nexus.Shell.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public uint Size; public Rect Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct Appbar { public uint Size; public IntPtr Window; public uint Callback, Edge; public Rect Area; public IntPtr Parameter; }
    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)] private static extern int MessageBox(IntPtr owner, string text, string title, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWorkArea(uint action, uint parameter, ref Rect area, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ExitWindowsEx(uint flags, uint reason);
    [DllImport("shell32.dll")] private static extern UIntPtr SHAppBarMessage(uint message, ref Appbar data);
    private static int SessionId => Process.GetCurrentProcess().SessionId;
    private static DesktopSessionRecord SessionRecord => new(DesktopSessionRecord.PathFor(SessionId));
    private static DesktopRectangle Rectangle(Rect area) => new(area.Left, area.Top, area.Right, area.Bottom);
    private static Rect Rectangle(DesktopRectangle area) => new() { Left = area.Left, Top = area.Top, Right = area.Right, Bottom = area.Bottom };
    private static MonitorInfo Monitor()
    {
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(IntPtr.Zero, 1), ref info)) throw new InvalidOperationException("Could not read the primary desktop monitor.");
        return info;
    }
    private static void ResetWorkArea()
    { var area = Monitor().Monitor; if (!SetWorkArea(0x002F, 0, ref area, 2)) throw new InvalidOperationException("Could not reset the desktop working area."); }
    private static Rect WindowsWorkArea(MonitorInfo current)
    {
        var area = current.Monitor; var bar = new Appbar { Size = (uint)Marshal.SizeOf<Appbar>() };
        if ((SHAppBarMessage(4, ref bar).ToUInt64() & 1) != 0 || SHAppBarMessage(5, ref bar) == UIntPtr.Zero) return area;
        // Emergency fallback without a saved session, or after the display changed.
        switch (bar.Edge)
        {
            case 0: area.Left = Math.Clamp(bar.Area.Right, area.Left, area.Right - 1); break;
            case 1: area.Top = Math.Clamp(bar.Area.Bottom, area.Top, area.Bottom - 1); break;
            case 2: area.Right = Math.Clamp(bar.Area.Left, area.Left + 1, area.Right); break;
            case 3: area.Bottom = Math.Clamp(bar.Area.Top, area.Top + 1, area.Bottom); break;
        }
        return area;
    }
    private static void RestoreWorkArea(MonitorInfo? original, DesktopSessionSnapshot? snapshot = null)
    {
        var current = Monitor();
        var area = original is { } saved && saved.Monitor.Equals(current.Monitor) ? saved.Work
            : snapshot is not null && snapshot.Monitor == Rectangle(current.Monitor) ? Rectangle(snapshot.Work) : WindowsWorkArea(current);
        if (!SetWorkArea(0x002F, 0, ref area, 2)) throw new InvalidOperationException("Could not restore the desktop working area.");
    }
    private static void Log(string message)
    {
        try { string folder = Path.GetDirectoryName(DesktopShellRegistration.DefaultBackupPath)!; Directory.CreateDirectory(folder); File.AppendAllText(Path.Combine(folder, "desktop-host.log"), DateTimeOffset.Now + " " + message + Environment.NewLine); } catch { }
    }
    private static int RestoreWindows(bool restoreSignIn, DesktopSurfaceLease? lease, MonitorInfo? original, bool notify)
    {
        DesktopSessionSnapshot? snapshot = null; List<Exception> recordErrors = [];
        try { snapshot = SessionRecord.Read(SessionId); } catch (Exception ex) { recordErrors.Add(ex); }
        bool surfacesRestored = false, areaRestored = false;
        Action? policy = restoreSignIn ? () => new DesktopShellRegistration(new WindowsDesktopSettings(), DesktopShellRegistration.DefaultBackupPath).Restore() : null;
        var result = DesktopSessionRecovery.Restore(policy,
            () =>
            {
                using var surfaces = new WindowsDesktopSurfaces();
                if (lease is not null) lease.Restore();
                else if (snapshot is not null) DesktopSurfaceLease.RestoreSaved(surfaces, snapshot.Surfaces);
                else surfaces.RestoreDefaultSurfaces();
                surfacesRestored = true;
            },
            () => { RestoreWorkArea(original, snapshot); areaRestored = true; },
            () => { if (!WindowsDesktopSurfaces.DesktopExists) Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { UseShellExecute = true }); });
        if (surfacesRestored && areaRestored && result.WindowsDesktopRequested && recordErrors.Count == 0)
            try { SessionRecord.Delete(); } catch (Exception ex) { recordErrors.Add(ex); }
        result = result with { Errors = [.. recordErrors, .. result.Errors] };
        foreach (var error in result.Errors) Log("Recovery: " + error);
        if (result.Errors.Count > 0)
        {
            string text = result.WindowsDesktopRequested ? "Windows desktop recovery was requested for this session." : "Windows desktop could not be started. Press Ctrl+Alt+Delete, open Task Manager, then run explorer.exe.";
            if (!result.SignInRestored) text += "\n\nYour saved sign-in setting could not be restored. This does not prevent the current Windows desktop from opening. The previous setting and recovery record are retained.";
            MessageBox(IntPtr.Zero, text + "\n\n" + string.Join("\n", result.Errors.Select(e => e.Message)), "Nexus recovery", 0x30);
        }
        else if (notify) MessageBox(IntPtr.Zero, "Nexus could not keep its desktop running. Windows desktop recovery is complete for this session.\n\nSee desktop-host.log in the Nexus data folder.", "Nexus desktop recovery", 0x30);
        return result.Errors.Count == 0 ? 0 : 1;
    }
    private static void WaitForPreview(string[] args)
    {
        int at = Array.IndexOf(args, "--wait-for-preview");
        if (at < 0) return;
        if (at + 1 >= args.Length || !int.TryParse(args[at + 1], out int id) || id <= 0) throw new ArgumentException("The preview handoff is invalid.");
        Process previous;
        try { previous = Process.GetProcessById(id); } catch (ArgumentException) { return; }
        using (previous)
        {
            if (previous.HasExited) return;
            if (previous.SessionId != Process.GetCurrentProcess().SessionId
                || !string.Equals(previous.MainModule?.FileName, Path.Combine(AppContext.BaseDirectory, "Nexus.Shell.exe"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The preview handoff does not belong to this Nexus folder and session.");
            if (!previous.WaitForExit(20000)) throw new TimeoutException("The existing Nexus preview is still open. Exit it, then launch Nexus desktop again.");
        }
    }
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows()) return 1;
        bool sessionOnly = args.Contains("--nexus-session") || args.Contains("--restore-session");
        DesktopSurfaceLease? lease = null; MonitorInfo? original = null; Process? active = null; WindowsDesktopSurfaces? surfaces = null;
        try
        {
            if (args.Contains("--restore-windows") || args.Contains("--restore-session"))
            { WaitForPreview(args); return RestoreWindows(!sessionOnly, null, null, false); }
            string shell = Path.Combine(AppContext.BaseDirectory, "Nexus.Shell.exe");
            if (!File.Exists(shell)) throw new FileNotFoundException("The Nexus application is missing.", shell);
            using var singleton = new Mutex(true, @"Local\WhiteDreams.Nexus.DesktopHost." + System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value, out bool first);
            if (!first) return 0;
            if (sessionOnly) WaitForPreview(args);
            if (WindowsDesktopSurfaces.NexusExists)
            { MessageBox(IntPtr.Zero, "Nexus is already open. Use Sections → Personalize → Use Nexus for this session, or exit the preview before launching Nexus desktop.", "Nexus desktop", 0x40); return 0; }
            // Recover an interrupted old session before replacing its saved state.
            if (SessionRecord.Read(SessionId) is not null && RestoreWindows(false, null, null, false) != 0)
                throw new InvalidOperationException("The previous desktop session still needs recovery.");
            original = Monitor();
            var snapshot = new DesktopSessionSnapshot(1, SessionId, Environment.ProcessPath!, Rectangle(original.Value.Monitor), Rectangle(original.Value.Work), []);
            SessionRecord.Save(snapshot);
            surfaces = new();
            lease = new(surfaces, saved => SessionRecord.Save(snapshot with { Surfaces = saved }));
            var budget = new ShellRestartBudget();
            while (true)
            {
                ResetWorkArea();
                string token = Guid.NewGuid().ToString("N");
                using var pulse = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\WhiteDreams.Nexus.Pulse." + token);
                var start = new ProcessStartInfo(shell) { WorkingDirectory = AppContext.BaseDirectory, UseShellExecute = false };
                start.ArgumentList.Add(sessionOnly ? "--nexus-session" : "--desktop-shell");
                start.ArgumentList.Add("--host-token"); start.ArgumentList.Add(token);
                start.ArgumentList.Add("--host-pid"); start.ArgumentList.Add(Environment.ProcessId.ToString());
                active = Process.Start(start) ?? throw new InvalidOperationException("Nexus did not start.");
                var uptime = Stopwatch.StartNew(); var sincePulse = Stopwatch.StartNew(); bool received = false, unresponsive = false;
                Log("Started desktop process " + active.Id + (sessionOnly ? " for this session" : " at sign-in"));
                while (!active.HasExited)
                {
                    if (pulse.WaitOne(1000))
                    {
                        if (!received) lease.TakeOver();
                        received = true; sincePulse.Restart();
                    }
                    if (received) lease.Maintain();
                    if (!budget.IsUnresponsive(uptime.Elapsed, sincePulse.Elapsed, received)) continue;
                    unresponsive = true; Log("Desktop heartbeat timed out");
                    active.Kill(entireProcessTree: false); active.WaitForExit(); break;
                }
                int code = active.ExitCode; active.Dispose(); active = null;
                Log("Desktop ended with code " + code);
                if (!unresponsive && code == (int)DesktopExitCode.RestoreWindows)
                    return RestoreWindows(!sessionOnly, lease, original, false);
                if (!unresponsive && code == (int)DesktopExitCode.SignOut)
                { lease.Restore(); RestoreWorkArea(original); SessionRecord.Delete(); if (!ExitWindowsEx(0, 0)) return RestoreWindows(!sessionOnly, lease, original, true); return 0; }
                lease.Restore(); RestoreWorkArea(original);
                if (!unresponsive && code == (int)DesktopExitCode.Restart) continue;
                if (!budget.MayRestart(uptime.Elapsed)) { RestoreWindows(!sessionOnly, lease, original, true); return 1; }
                Thread.Sleep(1000);
            }
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            try { if (active is not null && !active.HasExited) { active.Kill(entireProcessTree: false); active.WaitForExit(5000); } } catch (Exception stop) { Log(stop.ToString()); }
            RestoreWindows(!sessionOnly, lease, original, true);
            return 1;
        }
        finally { active?.Dispose(); surfaces?.Dispose(); }
    }
}
