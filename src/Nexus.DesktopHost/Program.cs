using Nexus.Shell.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[SupportedOSPlatform("windows")]
internal static class Program
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public uint Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)] private static extern int MessageBox(IntPtr owner, string text, string title, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWorkArea(uint action, uint parameter, ref Rect area, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ExitWindowsEx(uint flags, uint reason);
    private static void ResetWorkArea()
    {
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (GetMonitorInfo(MonitorFromWindow(IntPtr.Zero, 1), ref info)) SetWorkArea(0x002F, 0, ref info.Monitor, 2);
    }
    private static void Log(string message)
    {
        try { string folder = Path.GetDirectoryName(DesktopShellRegistration.DefaultBackupPath)!; Directory.CreateDirectory(folder); File.AppendAllText(Path.Combine(folder, "desktop-host.log"), DateTimeOffset.Now + " " + message + Environment.NewLine); } catch { }
    }
    private static void RestoreWindows(bool notify)
    {
        var registration = new DesktopShellRegistration(new WindowsDesktopSettings(), DesktopShellRegistration.DefaultBackupPath);
        registration.Restore(); ResetWorkArea();
        Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { UseShellExecute = true });
        if (notify) MessageBox(IntPtr.Zero, "Nexus could not keep its desktop running. Your previous desktop sign-in setting was restored.\n\nSee desktop-host.log in the Nexus data folder.", "Nexus desktop recovery", 0x30);
    }
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows()) return 1;
        try
        {
            if (args.Contains("--restore-windows")) { RestoreWindows(false); return 0; }
            string shell = Path.Combine(AppContext.BaseDirectory, "Nexus.Shell.exe");
            if (!File.Exists(shell)) throw new FileNotFoundException("The Nexus application is missing.", shell);
            using var singleton = new Mutex(true, @"Local\WhiteDreams.Nexus.DesktopHost." + System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value, out bool first);
            if (!first) return 0;
            var budget = new ShellRestartBudget();
            while (true)
            {
                ResetWorkArea();
                string token = Guid.NewGuid().ToString("N");
                using var pulse = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\WhiteDreams.Nexus.Pulse." + token);
                var start = new ProcessStartInfo(shell) { WorkingDirectory = AppContext.BaseDirectory, UseShellExecute = false };
                start.ArgumentList.Add("--desktop-shell"); start.ArgumentList.Add("--host-token"); start.ArgumentList.Add(token);
                using var app = Process.Start(start) ?? throw new InvalidOperationException("Nexus did not start.");
                var uptime = Stopwatch.StartNew(); var sincePulse = Stopwatch.StartNew(); bool received = false, unresponsive = false;
                Log("Started desktop process " + app.Id);
                while (!app.HasExited)
                {
                    if (pulse.WaitOne(1000)) { received = true; sincePulse.Restart(); }
                    if (!budget.IsUnresponsive(uptime.Elapsed, sincePulse.Elapsed, received)) continue;
                    unresponsive = true; Log("Desktop heartbeat timed out");
                    app.Kill(entireProcessTree: false); app.WaitForExit(); break;
                }
                int code = app.ExitCode; Log("Desktop ended with code " + code);
                if (!unresponsive && code == (int)DesktopExitCode.RestoreWindows) { RestoreWindows(false); return 0; }
                if (!unresponsive && code == (int)DesktopExitCode.SignOut)
                { ResetWorkArea(); if (!ExitWindowsEx(0, 0)) RestoreWindows(true); return 0; }
                if (!unresponsive && code == (int)DesktopExitCode.Restart) continue;
                if (!budget.MayRestart(uptime.Elapsed)) { RestoreWindows(true); return 1; }
                Thread.Sleep(1000);
            }
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            try { RestoreWindows(true); }
            catch (Exception recovery) { Log(recovery.ToString()); MessageBox(IntPtr.Zero, "Desktop recovery could not finish. Press Ctrl+Alt+Delete, open Task Manager, then run explorer.exe.\n\n" + recovery.Message, "Nexus recovery", 0x10); }
            return 1;
        }
    }
}
