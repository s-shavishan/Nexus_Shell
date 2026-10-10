using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace Nexus.Shell.Services;

internal static class DesktopPolicyElevation
{
    internal static async Task<bool> ApplyAsync(bool enable)
    {
        string host = Path.Combine(AppContext.BaseDirectory, "Nexus.DesktopHost.exe");
        if (!File.Exists(host)) throw new FileNotFoundException("The desktop policy helper is missing.", host);
        string sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("The current Windows account is unavailable.");
        var start = new ProcessStartInfo(host) { UseShellExecute = true, Verb = "runas", WorkingDirectory = AppContext.BaseDirectory };
        start.ArgumentList.Add("--policy-action"); start.ArgumentList.Add(enable ? "enable" : "restore");
        start.ArgumentList.Add("--user-sid"); start.ArgumentList.Add(sid);
        try
        {
            using var process = Process.Start(start) ?? throw new InvalidOperationException("The desktop policy helper did not start.");
            await process.WaitForExitAsync();
            if (process.ExitCode != 0) throw new InvalidOperationException("The desktop policy change did not finish. Your recovery record is retained; Windows may enforce a policy that this account cannot change.");
            return true;
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223) { return false; }
    }
}
