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
        InitializeComponent();
        UnhandledException += (_, args) => Log.Write("Unhandled UI exception", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Write("Unhandled process exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => Log.Write("Unobserved task exception", args.Exception);
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
        Log.Write("Nexus Shell 0.2.0 started; " + Environment.OSVersion);
        _window = new MainWindow();
        _window.Closed += (_, _) => { _instance?.Dispose(); _instance = null; Log.Write("Nexus Shell closed"); };
        _window.Activate();
    }
}
