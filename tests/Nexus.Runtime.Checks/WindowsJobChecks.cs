using System.Diagnostics;

internal static class WindowsJobChecks
{
    internal static async Task RunAsync()
    {
        if (!OperatingSystem.IsWindows()) { Console.WriteLine("SKIP: Windows job cleanup and external-app breakaway require Windows."); return; }
        string folder = Path.Combine(Path.GetTempPath(), "Nexus-job-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string gate = Path.Combine(folder, "go"), childRecord = Path.Combine(folder, "child.txt");
        string Quote(string path) => "'" + path.Replace("'", "''") + "'";
        Process? parent = null, child = null;
        try
        {
            var job = new FilesProcessJob();
            try
            {
                var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, CreateNoWindow = true };
                start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-Command");
                start.ArgumentList.Add("while (-not (Test-Path -LiteralPath " + Quote(gate) + ")) { Start-Sleep -Milliseconds 50 }; " +
                    "$p = Start-Process -FilePath ($env:SystemRoot + '\\System32\\ping.exe') -ArgumentList @('-n','30','127.0.0.1') -PassThru -WindowStyle Hidden; " +
                    "$p.Id | Set-Content -LiteralPath " + Quote(childRecord) + "; Start-Sleep -Seconds 30");
                parent = Process.Start(start) ?? throw new Exception("Job fixture did not start.");
                job.Add(parent); File.WriteAllText(gate, "ready");
                var elapsed = Stopwatch.StartNew(); int childId = 0;
                while (elapsed.Elapsed < TimeSpan.FromSeconds(10))
                {
                    if (File.Exists(childRecord) && int.TryParse(File.ReadAllText(childRecord).Trim(), out childId)) break;
                    await Task.Delay(50);
                }
                if (childId <= 0) throw new Exception("The external-app fixture did not become ready.");
                child = Process.GetProcessById(childId); _ = child.Handle;
                job.Dispose();
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await parent.WaitForExitAsync(deadline.Token);
                if (child.HasExited) throw new Exception("Closing the Files job must preserve an external application launched by Files.");
                Console.WriteLine("PASS: actual Windows job cleanup kills the assigned worker and preserves its external child.");
            }
            finally { job.Dispose(); }
        }
        finally
        {
            foreach (var process in new[] { parent, child })
                if (process is not null) { try { if (!process.HasExited) process.Kill(entireProcessTree: false); } finally { process.Dispose(); } }
            Directory.Delete(folder, true);
        }
    }
}
