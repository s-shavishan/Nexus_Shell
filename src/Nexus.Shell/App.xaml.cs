using Microsoft.UI.Xaml;
using Nexus.Shell.Services;
using System.Threading;

namespace Nexus.Shell;

public partial class App : Application
{
    private Window? _window;
    private Mutex? _instance;
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

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        string user = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        _instance = new Mutex(true, @"Local\WhiteDreams.Nexus.Shell." + user, out bool first);
        if (!first)
        {
            var handle = Interop.NativeMethods.FindWindow(null, "White Dreams Nexus Shell");
            if (handle != IntPtr.Zero) Interop.NativeMethods.Activate(handle);
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
        string stage = "constructing MainWindow";
        try
        {
            _window = new MainWindow();
            _window.Closed += (_, _) => { _instance?.Dispose(); _instance = null; Log.Write("Nexus Shell closed"); };
            stage = "activating MainWindow";
            _window.Activate();
            Log.Write("MainWindow activation completed");
        }
        catch (Exception ex)
        {
            ReportStartupFailure(stage, ex);
            _instance?.Dispose();
            _instance = null;
            Environment.ExitCode = 1;
            Exit();
        }
    }

    private static void ReportStartupFailure(string stage, Exception error)
    {
        Log.Write("Startup failed while " + stage, error);
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
