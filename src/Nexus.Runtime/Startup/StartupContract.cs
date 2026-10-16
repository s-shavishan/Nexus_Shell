// නමෝ බුද්ධාය 🤍
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nexus.Runtime.Startup;

// Numeric values are persisted in the startup-originals journal.
public enum StartupSource { UserRun, UserRunOnce, MachineRun64, MachineRun32, MachineRunOnce64, MachineRunOnce32, UserStartupFolder, CommonStartupFolder }
public enum StartupValueKind { Other, String, ExpandString, File }
public enum StartupBackupPhase { Prepared, Paused, RestorePending }
public sealed record StartupEntry(StartupSource Source, string Name, string Command, StartupValueKind Kind, string Location)
{
    [JsonIgnore] public string Id => StartupRules.Hash((int)Source, Name.ToUpperInvariant());
    [JsonIgnore] public string Fingerprint => StartupRules.Hash((int)Source, Name, Command, (int)Kind, Location);
}
public sealed record StartupInventory(StartupEntry[] Entries, string[] Notices);
public sealed record StartupBackup(Guid Id, StartupEntry Original, StartupBackupPhase Phase, DateTimeOffset SavedAt);
public sealed record StartupJournalDocument(int Schema, string OwnerId, List<StartupBackup> Backups);
public sealed record StartupItem(StartupEntry Entry, Guid? BackupId, string Status, string Detail, bool CanPause, bool CanRestore, string CurrentCommand = "");
public sealed record StartupSnapshot(DateTimeOffset ObservedAt, StartupItem[] Items, string[] Notices, int SavedOriginals);

public interface IStartupBackend
{
    string OwnerId { get; }
    StartupInventory Read(CancellationToken cancellation);
    StartupEntry? ReadManaged(string name);
    void RemoveExpected(StartupEntry expected);
    void RestoreIfMissing(StartupEntry original);
}
public interface IStartupJournal
{
    IDisposable Acquire();
    StartupJournalDocument Load(string ownerId);
    void Save(StartupJournalDocument document);
}

public static class StartupRules
{
    public const string UserRunLocation = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run";
    public static string Hash(params object[] values) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(values))));
    public static string Scope(StartupSource source) => source switch
    {
        StartupSource.UserRun => "Your account · Run",
        StartupSource.UserRunOnce => "Your account · Run once",
        StartupSource.UserStartupFolder => "Your Startup folder",
        StartupSource.CommonStartupFolder => "All users · Startup folder",
        StartupSource.MachineRun32 => "All users · Run (32-bit)",
        StartupSource.MachineRun64 => "All users · Run (64-bit)",
        StartupSource.MachineRunOnce32 => "All users · Run once (32-bit)",
        StartupSource.MachineRunOnce64 => "All users · Run once (64-bit)", _ => "Unknown source"
    };
    public static string Refusal(StartupEntry entry)
    {
        if (entry.Source != StartupSource.UserRun)
            return entry.Source is StartupSource.UserStartupFolder or StartupSource.CommonStartupFolder
                ? "Startup-folder items are view-only here. Open their folder in Files or use Windows startup settings."
                : "All-user and Run once registrations are view-only. Use Windows startup settings or the app's own preferences.";
        if (entry.Location != UserRunLocation || entry.Kind is not (StartupValueKind.String or StartupValueKind.ExpandString)
            || string.IsNullOrWhiteSpace(entry.Name) || entry.Name.Length > 512 || entry.Name.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(entry.Command) || entry.Command.Length > 32767 || entry.Command.Any(char.IsControl))
            return "This registration is unsupported or unreadable. It has been retained.";
        string text = entry.Name + " " + entry.Command;
        if (new[] { "WhiteDreamsNexusShell", "Nexus.Shell.exe", "Nexus.DesktopHost.exe", "Nexus.Core.exe",
                "explorer.exe", "winlogon.exe", "lsass.exe", "csrss.exe", "services.exe", "svchost.exe", "SecurityHealth", "Windows Defender", "MsMpEng.exe" }
            .Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase)))
            return "Nexus and Windows infrastructure are protected here. Use Nexus Controls for Nexus sign-in settings.";
        return "";
    }
    public static void ValidateManaged(StartupEntry entry)
    {
        string refusal = Refusal(entry);
        if (refusal.Length > 0) throw new InvalidOperationException(refusal);
    }
}
