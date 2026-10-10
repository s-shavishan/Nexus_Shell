using Microsoft.Win32;
using Nexus.Shell.Models;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Nexus.Shell.Services;

public static class AppCatalog
{
    public static List<AppEntry> Defaults()
    {
        var apps = new List<AppEntry>
        {
            new("files", "Files", "nexus:files", "\uE8B7", "System"),
            new("browser", "Browser", "https://www.google.com", "\uE774", "Browser"),
            new("terminal", "Terminal", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"), "\uE756", "Development"),
            new("settings", "Settings", "ms-settings:", "\uE713", "System"),
            new("calculator", "Calculator", "nexus:calculator", "\uE8EF", "Utility"),
            new("notes", "Notes", "nexus:notes", "\uE70B", "Utility")
        };
        var firefox = FindApp("firefox.exe", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Mozilla Firefox", "firefox.exe"));
        var code = FindApp("Code.exe", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe"));
        var steam = FindApp("steam.exe", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe"));
        if (firefox is not null) apps.Insert(1, new("firefox", "Firefox", firefox, "\uE774", "Browser"));
        if (code is not null) apps.Insert(2, new("code", "VS Code", code, "\uE943", "Development"));
        if (steam is not null) apps.Add(new("steam", "Steam", steam, "\uE7FC", "Game"));
        return apps;
    }

    private static string? FindApp(string executable, string fallback)
    {
        if (File.Exists(fallback)) return fallback;
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\" + executable);
                var value = (key?.GetValue(null) as string)?.Trim().Trim('"');
                if (value is not null && File.Exists(value)) return value;
            }
            catch (Exception ex) { Log.Write("App path lookup skipped: " + executable, ex); }
        }
        return null;
    }

    public static List<AppEntry> DiscoverShortcuts(CancellationToken cancellation = default)
    {
        var results = new Dictionary<string, AppEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu) })
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) continue;
            try
            {
                var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
                foreach (var path in Directory.EnumerateFiles(folder, "*.lnk", options).Take(500))
                {
                    cancellation.ThrowIfCancellationRequested();
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (name.Contains("uninstall", StringComparison.OrdinalIgnoreCase)) continue;
                    results.TryAdd(name, FromFile(path, "App"));
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Log.Write("Start menu discovery failed", ex); }
        }
        return results.Values.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static AppEntry FromFile(string path, string category)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToUpperInvariant())))[..16];
        return new(hash, Path.GetFileNameWithoutExtension(path), path, category == "Game" ? "\uE7FC" : "\uE8A5", category);
    }

    public static void OpenSettings(string uri)
    {
        if (!uri.StartsWith("ms-settings:", StringComparison.Ordinal)) throw new ArgumentException("A Windows Settings URI is required.");
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }
}
