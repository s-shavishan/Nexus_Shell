namespace Nexus.Shell.Services;

public static class StartupRegistration
{
    private static NexusStartupPolicy Policy()
    {
        var settings = new WindowsDesktopSettings();
        return new(settings, () => new DesktopShellRegistration(settings, DesktopShellRegistration.DefaultBackupPath).OwnsCurrentSetting);
    }
    private static string Shell => Path.Combine(AppContext.BaseDirectory, "Nexus.Shell.exe");
    private static string Host => Path.Combine(AppContext.BaseDirectory, "Nexus.DesktopHost.exe");
    public static bool IsEnabled() => Policy().Mode != NexusStartupMode.Disabled;
    public static bool IsDesktopEnabled() => Policy().Mode == NexusStartupMode.Desktop;
    public static void SetDesktopEnabled(bool enabled) => Policy().Set(enabled ? NexusStartupMode.Desktop : NexusStartupMode.Disabled, Shell, Host);
    public static void SetEnabled(bool enabled)
    {
        var policy = Policy();
        policy.Set(!enabled ? NexusStartupMode.Disabled : policy.Mode == NexusStartupMode.Desktop ? NexusStartupMode.Desktop : NexusStartupMode.Preview, Shell, Host);
    }
    public static bool UsesCurrentVersion() => Policy().Uses(Shell, Host);
}
