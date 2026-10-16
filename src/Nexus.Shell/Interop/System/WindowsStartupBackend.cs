// නමෝ බුද්ධාය 🤍
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Nexus.Runtime.Startup;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace Nexus.Shell.Interop;

[SupportedOSPlatform("windows")]
internal sealed class WindowsStartupBackend : IStartupBackend
{
    private const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOnce = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
    private const int MaximumEntries = 1000;
    public string OwnerId { get; }
    internal WindowsStartupBackend()
    {
        using var identity = WindowsIdentity.GetCurrent();
        OwnerId = identity.User?.Value ?? throw new UnauthorizedAccessException("Windows could not identify this account.");
    }

    public StartupInventory Read(CancellationToken cancellation)
    {
        var entries = new List<StartupEntry>();
        var notices = new List<string>();
        void Notice(string text) { if (notices.Count < 50) notices.Add(text); }
        foreach (var source in new[]
        {
            (StartupSource.UserRun, RegistryHive.CurrentUser, RegistryView.Registry64, Run),
            (StartupSource.UserRunOnce, RegistryHive.CurrentUser, RegistryView.Registry64, RunOnce),
            (StartupSource.MachineRun64, RegistryHive.LocalMachine, RegistryView.Registry64, Run),
            (StartupSource.MachineRun32, RegistryHive.LocalMachine, RegistryView.Registry32, Run),
            (StartupSource.MachineRunOnce64, RegistryHive.LocalMachine, RegistryView.Registry64, RunOnce),
            (StartupSource.MachineRunOnce32, RegistryHive.LocalMachine, RegistryView.Registry32, RunOnce)
        })
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                using var root = RegistryKey.OpenBaseKey(source.Item2, source.Item3);
                using var key = root.OpenSubKey(source.Item4, writable: false);
                if (key is null) continue;
                if (key.ValueCount > 2048) { Notice(StartupRules.Scope(source.Item1) + ": more than 2,048 values; inspect this location in Windows."); continue; }
                string location = source.Item1 == StartupSource.UserRun ? StartupRules.UserRunLocation
                    : (source.Item2 == RegistryHive.CurrentUser ? "HKCU\\" : "HKLM\\") + source.Item4 + " (" + (source.Item3 == RegistryView.Registry32 ? "32-bit" : "64-bit") + ")";
                foreach (string name in key.GetValueNames().Order(StringComparer.CurrentCultureIgnoreCase))
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (entries.Count >= MaximumEntries) { Notice("Inventory limited to 1,000 registrations. Use Windows controls for additional locations."); break; }
                    try { if (ReadEntry(key, source.Item1, name, location) is { } entry) entries.Add(entry); }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException or Win32Exception)
                    { Notice(StartupRules.Scope(source.Item1) + " / " + name + ": " + error.Message); }
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException or Win32Exception)
            { Notice(StartupRules.Scope(source.Item1) + ": " + error.Message); }
        }
        foreach (var source in new[] { (StartupSource.UserStartupFolder, Environment.SpecialFolder.Startup), (StartupSource.CommonStartupFolder, Environment.SpecialFolder.CommonStartup) })
        {
            cancellation.ThrowIfCancellationRequested();
            string folder = Environment.GetFolderPath(source.Item2);
            if (folder.Length == 0) { Notice(StartupRules.Scope(source.Item1) + " was not reported by Windows."); continue; }
            try
            {
                foreach (var item in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", new EnumerationOptions { RecurseSubdirectories = false, IgnoreInaccessible = false, AttributesToSkip = 0 }))
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (entries.Count >= MaximumEntries) { Notice("Inventory limited to 1,000 registrations."); break; }
                    if (item.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase) || (item.Attributes & FileAttributes.Directory) != 0) continue;
                    entries.Add(new(source.Item1, item.Name, item.FullName, StartupValueKind.File, folder));
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { Notice(StartupRules.Scope(source.Item1) + ": " + error.Message); }
        }
        return new(entries.ToArray(), notices.ToArray());
    }

    public StartupEntry? ReadManaged(string name)
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        using var key = root.OpenSubKey(Run, writable: false);
        return key is null ? null : ReadEntry(key, StartupSource.UserRun, name, StartupRules.UserRunLocation);
    }

    private static StartupEntry? ReadEntry(RegistryKey key, StartupSource source, string name, string location)
    {
        uint bytes = 0;
        int error = RegQueryValueEx(key.Handle, name, IntPtr.Zero, out uint type, IntPtr.Zero, ref bytes);
        if (error == 2) return null;
        if (error != 0) throw new Win32Exception(error);
        if (bytes > 64 * 1024 || type is not (1 or 2))
            return new(source, name, bytes > 64 * 1024 ? "Registration exceeds 64 KiB; retained." : "Non-text registration; retained.", StartupValueKind.Other, location);
        object? value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value is null) return null;
        if (value is not string command) return new(source, name, "Registration changed type while being read; refresh.", StartupValueKind.Other, location);
        return new(source, name, command, type == 1 ? StartupValueKind.String : StartupValueKind.ExpandString, location);
    }

    public void RemoveExpected(StartupEntry expected) => Mutate(expected, restore: false);
    public void RestoreIfMissing(StartupEntry original) => Mutate(original, restore: true);
    private static void Mutate(StartupEntry original, bool restore)
    {
        StartupRules.ValidateManaged(original);
        using var transaction = CreateTransaction(IntPtr.Zero, IntPtr.Zero, 0, 0, 0, 5000, "Nexus user-confirmed startup change");
        if (transaction.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not start an atomic startup change. Use Windows startup settings.");
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
        const int rights = 0x0001 | 0x0002 | 0x0100; // Query/set this value, 64-bit view. No machine or policy key access.
        SafeRegistryHandle handle;
        int error = restore
            ? RegCreateKeyTransacted(root.Handle, Run, 0, null, 0, rights, IntPtr.Zero, out handle, out _, transaction, IntPtr.Zero)
            : RegOpenKeyTransacted(root.Handle, Run, 0, rights, out handle, transaction, IntPtr.Zero);
        using (handle)
        {
            if (error != 0) throw new Win32Exception(error, "Windows refused access to this account's startup registration.");
            using var key = RegistryKey.FromHandle(handle, RegistryView.Registry64);
            var current = ReadEntry(key, StartupSource.UserRun, original.Name, StartupRules.UserRunLocation);
            if (restore)
            {
                if (current is not null && current.Fingerprint != original.Fingerprint)
                    throw new InvalidOperationException("A newer command occupies this startup name. Nothing was overwritten.");
                if (current is null) key.SetValue(original.Name, original.Command, original.Kind == StartupValueKind.String ? RegistryValueKind.String : RegistryValueKind.ExpandString);
            }
            else
            {
                if (current?.Fingerprint != original.Fingerprint)
                    throw new InvalidOperationException("This registration changed or disappeared. Nothing was removed.");
                key.DeleteValue(original.Name, throwOnMissingValue: true);
            }
        }
        // A concurrent non-transactional change aborts the transaction instead
        // of allowing a check-then-write/delete race to replace another value.
        if (!CommitTransaction(transaction)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows did not commit the startup change. Refresh and review its saved original.");
    }

    [DllImport("advapi32.dll", EntryPoint = "RegQueryValueExW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegQueryValueEx(SafeRegistryHandle key, string name, IntPtr reserved, out uint type, IntPtr data, ref uint bytes);
    [DllImport("ktmw32.dll", EntryPoint = "CreateTransaction", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateTransaction(IntPtr attributes, IntPtr guid, uint options, uint isolationLevel, uint isolationFlags, uint timeout, string description);
    [DllImport("ktmw32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CommitTransaction(SafeFileHandle transaction);
    [DllImport("advapi32.dll", EntryPoint = "RegOpenKeyTransactedW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegOpenKeyTransacted(SafeRegistryHandle key, string path, uint options, int rights, out SafeRegistryHandle result, SafeFileHandle transaction, IntPtr parameter);
    [DllImport("advapi32.dll", EntryPoint = "RegCreateKeyTransactedW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int RegCreateKeyTransacted(SafeRegistryHandle key, string path, uint reserved, string? keyClass, uint options, int rights, IntPtr attributes, out SafeRegistryHandle result, out uint disposition, SafeFileHandle transaction, IntPtr parameter);
}
