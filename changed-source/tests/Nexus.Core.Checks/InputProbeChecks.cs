using System.Runtime.InteropServices;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;

internal static class InputProbeChecks
{
    internal static void Run()
    {
        int count = 0;
        void Check(bool pass, string message) { count++; if (!pass) throw new Exception(message); }
        void Reject(string[] args)
        {
            bool rejected = false; try { InputProbeOptions.Parse(args); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "The input experiment must refuse missing consent, mixed modes and invalid cycle counts.");
        }
        Reject([]); Reject(["--verify-input"]); Reject(["--allow-test-input"]);
        foreach (string mode in new[] { "--verify-ui", "--desktop-shell", "--nexus-session", "--files-worker", "--control-center-worker" })
            Reject(["--verify-input", "--allow-test-input", mode]);
        foreach (string bad in new[] { "0", "501", "-1", "+1", "NaN", "2147483648", "1.5", " 20" })
            Reject(["--verify-input", "--allow-test-input", "--cycles", bad]);
        Reject(["--verify-input", "--allow-test-input", "--cycles"]);
        Reject(["--verify-input", "--allow-test-input", "--cycles", "1", "--cycles", "20"]);
        Reject(["--verify-input", "--report", "--allow-test-input"]);
        Reject(["--verify-input", "--allow-test-input", "--report"]);
        Reject(["--verify-input", "--allow-test-input", "--report", ""]);
        Reject(["--verify-input", "--allow-test-input", "--allow-test-input"]);
        Reject(["--verify-input", "--allow-test-input", "--unexpected"]);
        Reject(["--verify-input", "--allow-test-input", "--report", "a.json", "--report", "b.json"]);
        Check(InputProbeOptions.Parse(["Nexus.Shell.exe", "--verify-input", "--allow-test-input", "--report", "report with spaces.json"]).Cycles == 20,
            "Report values and an executable prefix must not change explicit consent parsing.");
        Check(InputProbeOptions.Parse(["--verify-input", "--allow-test-input"]).Cycles == 20, "Smoke tests must default to a bounded 20 cycles.");
        foreach (string good in new[] { "1", "20", "500" })
            Check(InputProbeOptions.Parse(["--verify-input", "--allow-test-input", "--cycles", good]).Cycles == int.Parse(good), "Explicit valid cycle counts must be honored.");

        foreach (var desktop in new[] { new ShellRect(0, 0, 1920, 1080), new(-1920, -200, 4480, 1440), new(-2560, -1440, 5120, 2880) })
        {
            Check(InputProbeCoordinates.Normalize(desktop.X, desktop.Y, desktop) == (0, 0), "Negative monitor origins must normalize correctly.");
            Check(InputProbeCoordinates.Normalize(desktop.Right - 1, desktop.Bottom - 1, desktop) == (65535, 65535), "The last physical virtual-screen pixel must map within ABSOLUTE bounds.");
            int previous = -1;
            for (int step = 0; step < 100; step++)
            {
                var point = InputProbeCoordinates.Normalize(desktop.X + (desktop.Width - 1) * step / 99, desktop.Y + desktop.Height / 2, desktop);
                Check(point.X >= previous && point.X <= 65535 && point.Y is >= 0 and <= 65535, "Virtual-screen input must be monotonic and bounded."); previous = point.X;
            }
            foreach (var point in new[] { (desktop.X - 1, desktop.Y), (desktop.Right, desktop.Y), (desktop.X, desktop.Bottom) })
            {
                bool rejected = false; try { InputProbeCoordinates.Normalize(point.Item1, point.Item2, desktop); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Check(rejected, "Out-of-screen points must be refused before input injection.");
            }
        }
        foreach (var bad in new[] { new ShellRect(0, 0, 0, 1080), new(0, 0, 1920, 1), new(0, 0, -1, 1080) })
        {
            bool rejected = false; try { InputProbeCoordinates.Normalize(0, 0, bad); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Unusable virtual desktops must fail closed.");
        }
        Check(Marshal.SizeOf<InputProbeDriver.Input>() == 40 && Marshal.OffsetOf<InputProbeDriver.Input>(nameof(InputProbeDriver.Input.Data)).ToInt32() == 8,
            "x64 SendInput requires a 40-byte INPUT with an aligned union.");
        Check(Marshal.SizeOf<InputProbeDriver.GuiInfo>() == 72 && Marshal.OffsetOf<InputProbeDriver.GuiInfo>(nameof(InputProbeDriver.GuiInfo.CaretRect)).ToInt32() == 56,
            "x64 GUI-thread queries require the actual pointer/RECT layout.");
        if (!OperatingSystem.IsWindows())
        {
            bool rejected = false; try { _ = new InputProbeDriver(new IntPtr(1)); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "The native driver must refuse unsupported hosts before loading user32.");
        }
        Console.WriteLine($"PASS: {count} input-probe consent, bounds, virtual-screen coordinates and x64 interop assertions. No Windows input was injected by these portable checks.");
    }
}
