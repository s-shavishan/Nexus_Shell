using Microsoft.Win32;

namespace Nexus.Shell.Services;

public static class StartupRegistration
{
    private const string Name = "WhiteDreamsNexusShell";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(Name) is string;
    }
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            string path = Environment.ProcessPath ?? throw new InvalidOperationException("The running executable path is unavailable.");
            key.SetValue(Name, '"' + path + '"');
        }
        else key.DeleteValue(Name, throwOnMissingValue: false);
    }
}
