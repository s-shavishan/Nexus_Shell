using Nexus.Core.Settings;
using Nexus.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

internal static class WindowsSettingsChecks
{
    internal static Task RunAsync()
    {
        if (OperatingSystem.IsWindows()) return RunWindowsAsync();
        Console.WriteLine("SKIP: native settings reads and HWND ownership require Windows."); return Task.CompletedTask;
    }
    [SupportedOSPlatform("windows")]
    private static Task RunWindowsAsync()
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { RunWindows(); finished.SetResult(); } catch (Exception error) { finished.SetException(error); } }) { IsBackground = true, Name = "Nexus native settings checks" };
        thread.Start(); return finished.Task;
    }
    [SupportedOSPlatform("windows")]
    private static void RunWindows()
    {
        // An invisible test-owned HWND exercises the production display guard.
        // All device requests below are reads: CI must not alter host settings.
        IntPtr window = CreateWindowEx(0, "STATIC", "Nexus settings checks", 0, 0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (window == IntPtr.Zero) throw new Exception("Could not create the native settings test window.");
        try
        {
            using var backend = new WindowsSettingsBackend(Environment.ProcessId);
            var service = new SystemSettingsCoordinator(backend);
            int reads = 0, unavailable = 0;
            foreach (string section in SettingsRules.Sections)
            {
                try
                {
                    var snapshot = service.ExecuteAsync(new(section, Window: window.ToInt64()), CancellationToken.None).GetAwaiter().GetResult();
                    if (snapshot.Section != section || snapshot.Epoch != service.Epoch) throw new Exception("Invalid native " + section + " snapshot.");
                    bool valid = section switch
                    { "Sound" => snapshot.Sound is not null, "Display" => snapshot.Brightness is not null && snapshot.Displays is not null,
                        "Network" => snapshot.Network is not null, "Bluetooth" => snapshot.Bluetooth is not null, "Power" => snapshot.Power is not null, _ => false };
                    if (!valid) throw new Exception("The native " + section + " read returned no capability state.");
                    reads++;
                }
                catch (RuntimeFailure error) when (error.Code is "settings-device" or "settings-timeout")
                { unavailable++; Console.WriteLine("CAPABILITY: " + section + " · " + error.Message); }
            }
            using var wrongOwner = new WindowsSettingsBackend(Environment.ProcessId + 1);
            bool rejected = false;
            try { wrongOwner.ExecuteAsync(new("Display", Window: window.ToInt64()), CancellationToken.None).GetAwaiter().GetResult(); }
            catch (RuntimeFailure error) when (error.Code == "settings-window") { rejected = true; }
            if (!rejected) throw new Exception("A foreign HWND owner was accepted for display settings.");
            Console.WriteLine($"PASS: {reads} native settings snapshots, {unavailable} explicitly unavailable sections, and foreign HWND rejection; no hardware writes performed.");
        }
        finally { DestroyWindow(window); }
    }
    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(uint exStyle, string className, string name, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(IntPtr window);
}
