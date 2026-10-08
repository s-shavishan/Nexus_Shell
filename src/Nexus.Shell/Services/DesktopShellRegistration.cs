using Microsoft.Win32;
using System.Text.Json;
using System.Runtime.Versioning;

namespace Nexus.Shell.Services;

public sealed record ShellRegistryValue(string Text, RegistryValueKind Kind = RegistryValueKind.String);
public interface IUserDesktopSettings
{
    ShellRegistryValue? Shell { get; set; }
    ShellRegistryValue? NexusStartup { get; set; }
}
[SupportedOSPlatform("windows")]
public sealed class WindowsDesktopSettings : IUserDesktopSettings
{
    public const string ShellKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static ShellRegistryValue? Read(string path, string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path);
        if (key is null || !key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase)) return null;
        var kind = key.GetValueKind(name);
        if (kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString)) throw new InvalidOperationException("The existing desktop setting has an unsupported type.");
        return new((string)key.GetValue(name, "", RegistryValueOptions.DoNotExpandEnvironmentNames)!, kind);
    }
    private static void Write(string path, string name, ShellRegistryValue? value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(path);
        if (value is null) key.DeleteValue(name, false); else key.SetValue(name, value.Text, value.Kind);
    }
    public ShellRegistryValue? Shell { get => Read(ShellKey, "Shell"); set => Write(ShellKey, "Shell", value); }
    public ShellRegistryValue? NexusStartup { get => Read(RunKey, "WhiteDreamsNexusShell"); set => Write(RunKey, "WhiteDreamsNexusShell", value); }
    public static DesktopCapabilities Capabilities()
    {
        if (!OperatingSystem.IsWindows()) return new(false, "", 0);
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        int.TryParse(key?.GetValue("CurrentBuildNumber") as string, out int build);
        int revision = key?.GetValue("UBR") is int value ? value : 0;
        return new(true, key?.GetValue("EditionID") as string ?? "", build, revision);
    }
}
public sealed record DesktopShellBackup(int Format, string Command, ShellRegistryValue? PreviousShell, ShellRegistryValue? PreviousStartup);
public sealed class DesktopShellRegistration(IUserDesktopSettings settings, string backupPath)
{
    public static string DefaultBackupPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WhiteDreams", "NexusShell", "desktop-shell-backup.json");
    public static string CommandFor(string hostPath)
    {
        if (!Path.IsPathFullyQualified(hostPath) || hostPath.Contains('"') || hostPath.IndexOfAny(['\r','\n','\0']) >= 0)
            throw new ArgumentException("Use a complete local path to Nexus.DesktopHost.exe.");
        return '"' + hostPath + '"';
    }
    private DesktopShellBackup? ReadBackup()
    {
        if (!File.Exists(backupPath)) return null;
        var info = new FileInfo(backupPath);
        if (info.Length > 65536) throw new InvalidDataException("The desktop recovery record is too large.");
        var backup = JsonSerializer.Deserialize<DesktopShellBackup>(File.ReadAllText(backupPath));
        if (backup is null || backup.Format != 1 || string.IsNullOrWhiteSpace(backup.Command)) throw new InvalidDataException("The desktop recovery record is invalid.");
        foreach (var value in new[] { backup.PreviousShell, backup.PreviousStartup })
            if (value is not null && (value.Text is null || value.Kind is not (RegistryValueKind.String or RegistryValueKind.ExpandString)))
                throw new InvalidDataException("The saved desktop registry value is invalid.");
        return backup;
    }
    private void SaveBackup(DesktopShellBackup backup)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        string temporary = backupPath + ".tmp";
        try
        {
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(backup);
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { file.Write(data); file.Flush(true); }
            File.Move(temporary, backupPath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static bool ExplorerDefault(ShellRegistryValue? value) => value is null || value.Text.Trim().Trim('"').Equals("explorer.exe", StringComparison.OrdinalIgnoreCase)
        || value.Text.Trim().Trim('"').Equals(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), StringComparison.OrdinalIgnoreCase);
    public bool Uses(string hostPath) => settings.Shell?.Text == CommandFor(hostPath);
    public bool OwnsCurrentSetting => ReadBackup() is { } backup && settings.Shell?.Text == backup.Command;
    public void Enable(string hostPath, DesktopCapabilities capabilities)
    {
        if (!capabilities.SupportsCustomInterface) throw new NotSupportedException("Desktop sign-in requires a supported Windows Pro, Enterprise or Education edition.");
        if (!File.Exists(hostPath)) throw new FileNotFoundException("The published Nexus desktop host is missing.", hostPath);
        string command = CommandFor(hostPath); var prior = ReadBackup(); var current = settings.Shell; var startup = settings.NexusStartup;
        bool ours = prior is not null && current?.Text == prior.Command;
        if (!ours && !ExplorerDefault(current)) throw new InvalidOperationException("Another custom desktop is configured. Nexus will not replace its policy.");
        var backup = ours ? prior! with { Command = command } : new DesktopShellBackup(1, command, current, settings.NexusStartup);
        // Write recovery data before changing sign-in. Only this user's two values are touched.
        SaveBackup(backup);
        try { settings.Shell = new(command); settings.NexusStartup = null; }
        catch (Exception setupError)
        {
            try
            {
                if (settings.Shell?.Text == command) settings.Shell = current;
                if (settings.NexusStartup is null) settings.NexusStartup = startup;
                // A failed path update must leave recovery pointing at the still-active host.
                if (ours && prior is not null && settings.Shell == current) SaveBackup(prior);
            }
            catch (Exception rollbackError) { throw new AggregateException("Desktop setup failed and rollback could not finish. The recovery record is available.", setupError, rollbackError); }
            throw;
        }
    }
    public bool Restore()
    {
        var backup = ReadBackup();
        if (backup is null) return false;
        var current = settings.Shell;
        if (current != backup.PreviousShell)
        {
            if (current?.Text != backup.Command) throw new InvalidOperationException("The desktop policy changed after Nexus was configured; recovery will not overwrite it.");
            settings.Shell = backup.PreviousShell;
        }
        if (settings.NexusStartup is null) settings.NexusStartup = backup.PreviousStartup;
        return true;
    }
}
