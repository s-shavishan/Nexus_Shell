using Microsoft.UI.Xaml;
using Nexus.Shell.Services;
using System.Threading;

namespace Nexus.Shell;

public partial class App : Application
{
    private Desktop.DesktopEnvironment? _environment;
    private Desktop.FilesEnvironment? _files;
    private CoreProcessSession? _core;
    private Mutex? _instance;
    private Desktop.StartupWindow? _startup;
    private bool _startupCancelled;
    private DesktopSessionMode _launchMode;
    public App()
    {
        UnhandledException += (_, args) => Log.Write("Unhandled UI exception; native message: " + args.Message, args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Write("Unhandled process exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => Log.Write("Unobserved task exception", args.Exception);
        // Subscribe before loading either application's resources or its window.
        try
        {
            DebugSettings.IsXamlResourceReferenceTracingEnabled = true;
            DebugSettings.XamlResourceReferenceFailed += (_, args) => Log.Write("XAML resource reference: " + args.Message);
        }
        catch (Exception ex) { Log.Write("XAML resource tracing unavailable", ex); }
        try { InitializeComponent(); }
        catch (Exception ex)
        {
            ReportStartupFailure("loading App.xaml resources", ex);
            throw;
        }
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var command = Environment.GetCommandLineArgs();
        if (command.Contains("--files-worker"))
        {
            try
            {
                _files = new(command.Skip(1).ToArray());
                _files.Stopped += () => { _files = null; Exit(); };
                await _files.StartAsync();
            }
            catch (Exception ex)
            { ReportStartupFailure("opening isolated Files", ex); Environment.ExitCode = 1; _files?.Stop(); Exit(); }
            return;
        }
        string user = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        _instance = new Mutex(true, @"Local\WhiteDreams.Nexus.Shell." + user, out bool first);
        if (!first)
        {
            var handle = Interop.NativeMethods.FindWindow(null, Desktop.DesktopWindow.NativeWindowTitle);
            if (handle != IntPtr.Zero)
                Interop.DesktopIntegration.PostMessage(handle, Interop.DesktopIntegration.SummonMessage, UIntPtr.Zero, IntPtr.Zero);
            _instance.Dispose(); _instance = null;
            Exit();
            return;
        }
        string? version = typeof(App).Assembly.GetName().Version?.ToString(3);
        Log.Write($"Nexus Shell {version} started; {Environment.OSVersion}; {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        try
        {
            Log.Write("Root PRI files: " + string.Join(", ", Directory.EnumerateFiles(AppContext.BaseDirectory, "*.pri").Select(Path.GetFileName)));
            Log.Write("Loose MainWindow.xbf present: " + File.Exists(Path.Combine(AppContext.BaseDirectory, "MainWindow.xbf")) + "; XBF may instead be embedded in the app PRI");
        }
        catch (Exception ex) { Log.Write("Could not inspect startup resource files", ex); }
        string stage = "starting Nexus Core";
        try
        {
            _launchMode = command.Contains("--nexus-session") ? DesktopSessionMode.NexusSession
                : command.Contains("--desktop-shell") ? DesktopSessionMode.DesktopShell : DesktopSessionMode.Preview;
            _startup = new(); _startup.Cancelled += CancelStartup;
            _core = new();
            var loaded = await _core.InitializeAsync(_startup.Report);
            if (_startupCancelled) return;
            var session = new ShellSession(initialState: loaded.State, persist: _core.SaveAsync, recoveryMessage: loaded.RecoveryMessage);
            stage = "constructing the desktop environment";
            var mode = _launchMode;
            int tokenIndex = Array.IndexOf(command, "--host-token");
            string? token = tokenIndex >= 0 && tokenIndex + 1 < command.Length ? command[tokenIndex + 1] : null;
            int pidIndex = Array.IndexOf(command, "--host-pid");
            int? hostPid = pidIndex >= 0 && pidIndex + 1 < command.Length && int.TryParse(command[pidIndex + 1], out int parsedPid) ? parsedPid : null;
            _environment = new Desktop.DesktopEnvironment(session, _core, mode, token, hostPid);
            stage = "starting the desktop and taskbar";
            _environment.Stopped += () =>
            {
                _instance?.Dispose(); _instance = null; _core = null;
                Log.Write("Nexus desktop closed"); Exit();
            };
            _environment.Start(_startup.Report);
            _startup.Complete(); _startup = null;
            Log.Write("Desktop and taskbar started; Sections opens on demand");
        }
        catch (Exception ex)
        {
            if (_startupCancelled) return;
            _startup?.Complete(); _startup = null;
            // The managed host owns retries and its independent recovery
            // dialog. A blocking child dialog would stall that recovery.
            ReportStartupFailure(stage, ex, showDialog: _launchMode == DesktopSessionMode.Preview);
            if (_core is not null) { await _core.DisposeAsync(); _core = null; }
            _instance?.Dispose();
            _instance = null;
            Environment.ExitCode = 1;
            Exit();
        }
    }

    private async void CancelStartup()
    {
        if (_startupCancelled) return; _startupCancelled = true;
        Environment.ExitCode = (int)(_launchMode == DesktopSessionMode.Preview ? DesktopExitCode.Stop : DesktopExitCode.RestoreWindows);
        try { if (_core is not null) await _core.DisposeAsync(); }
        catch (Exception error) { Log.Write("Startup cancellation cleanup failed", error); }
        _core = null; _startup?.Complete(); _startup = null; _instance?.Dispose(); _instance = null; Exit();
    }
    private static void ReportStartupFailure(string stage, Exception error, bool showDialog = true)
    {
        Log.Write("Startup failed while " + stage, error);
        if (!showDialog) return;
        // A native dialog remains usable when WinUI's XAML cannot be loaded.
        try
        {
            string path = Path.Combine(StateStore.DirectoryPath, "nexus.log");
            Interop.NativeMethods.ShowStartupError(
                $"Nexus could not open while {stage}.\n\n{error.GetType().Name}: {error.Message}\n" +
                $"HRESULT: 0x{error.HResult:X8}\n\nDetails were written to:\n{path}\n\n" +
                "Close this message and share the latest startup section of that log.");
        }
        catch { /* Error reporting must not prevent the process from exiting. */ }
    }
}
