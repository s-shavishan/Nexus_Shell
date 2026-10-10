using Nexus.Shell.Services;
using System.Runtime.InteropServices;

internal static class StartupChecks
{
    private sealed class Settings : IUserDesktopSettings
    {
        public ShellRegistryValue? Shell { get; set; }
        public ShellRegistryValue? NexusStartup { get; set; }
    }
    internal static void Run()
    {
        int count = 0;
        void Check(bool result, string message) { count++; if (!result) throw new Exception(message); }
        string folder = Path.Combine(Path.GetTempPath(), "Nexus-startup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            string shell = Path.Combine(folder,"Nexus.Shell.exe"), host = Path.Combine(folder,"Nexus.DesktopHost.exe");
            File.WriteAllText(shell,"fixture"); File.WriteAllText(host,"fixture");
            var settings = new Settings { Shell = new("explorer.exe") }; bool owns = false;
            var startup = new NexusStartupPolicy(settings,()=>owns);
            Check(startup.Mode == NexusStartupMode.Disabled,"No entry means disabled.");
            startup.Set(NexusStartupMode.Desktop,shell,host);
            Check(startup.Mode == NexusStartupMode.Desktop && settings.NexusStartup?.Text == '"'+host+"\" --nexus-session","Desktop startup uses the supervised host.");
            Check(settings.Shell?.Text == "explorer.exe","Startup cannot change shell policy.");
            Check(startup.Uses(shell,host),"Recognize the selected folder.");
            Check(!startup.Uses(Path.Combine(folder,"other","Nexus.Shell.exe"),Path.Combine(folder,"other","Nexus.DesktopHost.exe")),"Do not report an old folder as this version.");
            startup.Set(NexusStartupMode.Preview,shell,host);
            Check(startup.Mode == NexusStartupMode.Preview && startup.Uses(shell,host),"Preserve legacy preview startup.");
            owns = true; bool rejected = false;
            try { startup.Set(NexusStartupMode.Desktop,shell,host); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && startup.Mode == NexusStartupMode.Preview,"Refuse conflicting startup while shell replacement is owned.");
            startup.Set(NexusStartupMode.Disabled,shell,host);
            Check(startup.Mode == NexusStartupMode.Disabled,"Disable startup independently."); owns = false;
            foreach (var foreign in new[] { new ShellRegistryValue("foreign command"), new ShellRegistryValue('"'+host+"\" --restore-windows"), new ShellRegistryValue('"'+shell+'"',ShellRegistryKind.ExpandString) })
            {
                settings.NexusStartup = foreign;
                Check(startup.Mode == NexusStartupMode.Unknown,"Recognize unsupported commands.");
                foreach (var mode in new[] { NexusStartupMode.Desktop,NexusStartupMode.Disabled })
                {
                    rejected = false;
                    try { startup.Set(mode,shell,host); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected && settings.NexusStartup == foreign,"Preserve foreign entries on enable and disable.");
                }
            }
            settings.NexusStartup = null; rejected = false;
            try { startup.Set(NexusStartupMode.Desktop,shell,Path.Combine(folder,"missing","Nexus.DesktopHost.exe")); } catch (FileNotFoundException) { rejected = true; }
            Check(rejected && settings.NexusStartup is null,"A missing host cannot enable startup.");
            rejected = false;
            try { NexusStartupPolicy.DesktopCommand(Path.Combine(folder,new string('x',260),"Nexus.DesktopHost.exe")); } catch (ArgumentException) { rejected = true; }
            Check(rejected,"Refuse a Run command beyond the Windows character bound.");
            int calls = 0, reports = 0;
            WindowSwitcherPolicy.Request(()=>calls++,_=>reports++);
            Check(calls == 1 && reports == 0,"Supported switcher calls proceed.");
            WindowSwitcherPolicy.Request(()=>throw new NotImplementedException(),_=>reports++);
            WindowSwitcherPolicy.Request(()=>throw new NotImplementedException(),_=>reports++);
            Check(reports == 1,"The observed E_NOTIMPL permits native fallback.");
            rejected = false;
            try { WindowSwitcherPolicy.Request(()=>throw new COMException("Other failure",unchecked((int)0x80004005)),_=>reports++); } catch (COMException) { rejected = true; }
            Check(rejected && reports == 1,"Unrelated errors remain visible.");
            var policy = DesktopPolicyRequest.Parse(["--policy-action","restore","--user-sid","S-1-5-21-100"]);
            policy.VerifyUser("S-1-5-21-100"); Check(!policy.Enable,"Parse only the requested helper action.");
            rejected = false;
            try { policy.VerifyUser("S-1-5-21-200"); } catch (System.Security.SecurityException) { rejected = true; }
            Check(rejected,"Reject another elevated account before registry access.");
            rejected = false;
            try { DesktopPolicyRequest.Parse(["--policy-action","anything","--user-sid","S-1-5-21-100"]); } catch (ArgumentException) { rejected = true; }
            Check(rejected,"Reject undeclared helper actions.");
            Console.WriteLine("PASS: " + count + " startup checks; supervised/legacy launch, folder selection, foreign-entry preservation, conflict refusal, switcher fallback, and policy-account binding.");
        }
        finally { Directory.Delete(folder,true); }
    }
}
