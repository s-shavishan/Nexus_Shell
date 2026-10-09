namespace Nexus.Shell.Services;

public enum DesktopSessionMode { Preview, DesktopShell, NexusSession }
public enum DesktopExitCode { Stop = 0, Restart = 10, RestoreWindows = 20, SignOut = 30 }
public sealed record DesktopCapabilities(bool IsWindows, string Edition, int Build, int Revision = 0)
{
    public bool SupportsCustomInterface => IsWindows && (Build >= 19044 || (Build >= 19041 && Revision >= 1202)) &&
        (Edition.StartsWith("Professional", StringComparison.OrdinalIgnoreCase) || Edition.StartsWith("Enterprise", StringComparison.OrdinalIgnoreCase)
        || Edition.StartsWith("Education", StringComparison.OrdinalIgnoreCase) || Edition.StartsWith("IoTEnterprise", StringComparison.OrdinalIgnoreCase));
}
public sealed class ShellRestartBudget
{
    private int _failures;
    public bool MayRestart(TimeSpan uptime)
    { if (uptime >= TimeSpan.FromMinutes(5)) _failures = 0; return ++_failures <= 2; }
    public bool IsUnresponsive(TimeSpan uptime, TimeSpan sincePulse, bool receivedPulse) => receivedPulse
        ? sincePulse >= TimeSpan.FromSeconds(90) : uptime >= TimeSpan.FromSeconds(45);
}
