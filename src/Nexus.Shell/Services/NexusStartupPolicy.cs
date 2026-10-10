namespace Nexus.Shell.Services;

public enum NexusStartupMode { Disabled, Preview, Desktop, Unknown }

// Sign-in launch is independent from shell policy. Unknown commands are retained.
public sealed class NexusStartupPolicy(IUserDesktopSettings settings, Func<bool> ownsShell)
{
    public static string DesktopCommand(string host) => Command(host, " --nexus-session");
    public static string PreviewCommand(string shell) => Command(shell, "");
    private static string Command(string path, string arguments)
    {
        string command = DesktopShellRegistration.CommandFor(path) + arguments;
        if (command.Length > 260) throw new ArgumentException("Move the full Nexus folder to a shorter path before enabling sign-in startup.");
        return command;
    }
    public NexusStartupMode Mode
    {
        get
        {
            var value = settings.NexusStartup;
            if (value is null) return NexusStartupMode.Disabled;
            if (value.Kind != ShellRegistryKind.String) return NexusStartupMode.Unknown;
            string text = value.Text;
            if (!text.StartsWith('"')) return NexusStartupMode.Unknown;
            int end = text.IndexOf('"', 1);
            if (end < 2) return NexusStartupMode.Unknown;
            string path = text[1..end], arguments = text[(end + 1)..];
            if (!Path.IsPathFullyQualified(path) || path.IndexOfAny(['\r','\n','\0']) >= 0) return NexusStartupMode.Unknown;
            string file = Path.GetFileName(path);
            if (file.Equals("Nexus.DesktopHost.exe", StringComparison.OrdinalIgnoreCase) && arguments == " --nexus-session") return NexusStartupMode.Desktop;
            if (file.Equals("Nexus.Shell.exe", StringComparison.OrdinalIgnoreCase) && arguments.Length == 0) return NexusStartupMode.Preview;
            return NexusStartupMode.Unknown;
        }
    }
    public bool Uses(string shell, string host) => settings.NexusStartup?.Text is { } text
        && (text.Equals(PreviewCommand(shell), StringComparison.OrdinalIgnoreCase) || text.Equals(DesktopCommand(host), StringComparison.OrdinalIgnoreCase));
    public void Set(NexusStartupMode mode, string shell, string host)
    {
        if (mode is not (NexusStartupMode.Disabled or NexusStartupMode.Preview or NexusStartupMode.Desktop)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (Mode == NexusStartupMode.Unknown) throw new InvalidOperationException("The Nexus startup entry contains an unrecognized command. It was retained; review it in Windows startup settings.");
        if (mode != NexusStartupMode.Disabled && ownsShell()) throw new InvalidOperationException("Nexus already replaces the sign-in desktop. Restore that setting before enabling startup alongside Windows.");
        if (mode == NexusStartupMode.Disabled) { settings.NexusStartup = null; return; }
        string path = mode == NexusStartupMode.Desktop ? host : shell;
        if (!File.Exists(path)) throw new FileNotFoundException("Keep the full published Nexus folder together before enabling startup.", path);
        settings.NexusStartup = new(mode == NexusStartupMode.Desktop ? DesktopCommand(host) : PreviewCommand(shell));
    }
}
