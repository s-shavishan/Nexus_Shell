using Nexus.Runtime;
using Nexus.Shell.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;

internal static class Program
{
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    private static void Log(string message, Exception? error = null)
    {
        try
        {
            string folder = StateStore.DirectoryPath; Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "core.log");
            if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024) File.Move(path, path + ".previous", true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{(error is null ? "" : Environment.NewLine + error)}{Environment.NewLine}");
        }
        catch { }
    }
    private sealed class Worker(Process process) : IFilesProcess
    {
        public int Id => process.Id;
        public bool HasExited => process.HasExited;
        public int ExitCode => process.ExitCode;
        public void Stop() { if (!process.HasExited) process.Kill(entireProcessTree: false); }
        public void Dispose() => process.Dispose();
    }
    private static async Task<int> Main(string[] args)
    {
        if (!OperatingSystem.IsWindows()) return 1;
        try
        {
            if (args.Length != 4 || args[0] != "--parent-pid" || !int.TryParse(args[1], out int parentId) || parentId <= 0
                || args[2] != "--endpoint" || !Guid.TryParseExact(args[3], "N", out Guid endpoint))
                throw new ArgumentException("Start Nexus Core through the Nexus desktop.");
            using var parent = Process.GetProcessById(parentId);
            using var own = Process.GetCurrentProcess();
            if (parent.HasExited || parent.SessionId != own.SessionId || !string.Equals(parent.MainModule?.FileName,
                Path.Combine(AppContext.BaseDirectory, "Nexus.Shell.exe"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The Core parent is not the Nexus desktop in this folder and session.");
            _ = parent.Handle;
            string pipeName = "WhiteDreams.Nexus.Core." + own.SessionId + "." + endpoint.ToString("N");
            using var repository = new CoreStateRepository(new StateStore());
            using var job = new FilesProcessJob();
            using var files = new FilesCoordinator(toolId =>
            {
                var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "Nexus.Shell.exe")) { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory };
                start.ArgumentList.Add("--files-worker"); start.ArgumentList.Add("--core-pipe"); start.ArgumentList.Add(pipeName);
                start.ArgumentList.Add("--core-pid"); start.ArgumentList.Add(Environment.ProcessId.ToString());
                start.ArgumentList.Add("--tool-id"); start.ArgumentList.Add(toolId.ToString("N"));
                var process = Process.Start(start) ?? throw new InvalidOperationException("The Files process did not start.");
                try { job.Add(process); }
                catch { try { if (!process.HasExited) process.Kill(entireProcessTree: false); } finally { process.Dispose(); } throw; }
                Log("Started Files process " + process.Id); return new Worker(process);
            }, (window, processId) => GetWindowThreadProcessId(new IntPtr(window), out uint actual) != 0 && actual == processId);
            using var settingsBackend = new Nexus.Core.Settings.WindowsSettingsBackend(parentId, error => Log("Core device settings failed", error));
            var router = new CoreRouter(repository, files, new SystemSettingsCoordinator(settingsBackend));
            using var stop = new CancellationTokenSource();
            var server = new RuntimeServer(pipeName, (pipe, request) => PipePeer.IsClient(pipe, request.ProcessId)
                && (request.Role == "desktop" ? request.ProcessId == parentId && !parent.HasExited : files.IsWorker(request.ProcessId)), router.DispatchAsync,
                error => Log("Core request failed", error));
            var running = server.RunAsync(stop.Token);
            Log("Core ready for desktop process " + parentId + "; revision " + repository.Revision);
            try
            {
                while (!parent.HasExited && !router.StopRequested && !running.IsCompleted)
                { files.Monitor(); await Task.Delay(250).ConfigureAwait(false); }
            }
            finally { stop.Cancel(); }
            await running.ConfigureAwait(false);
            Log("Core stopped"); return 0;
        }
        catch (Exception ex) { Log("Core startup or session failed", ex); return 1; }
    }
}
