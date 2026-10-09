using Nexus.Shell.Models;
using System.Text;
using System.Text.Json;

namespace Nexus.Shell.Services;

public sealed class StateStore
{
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WhiteDreams", "NexusShell");
    private const int MaximumBytes = 2 * 1024 * 1024;
    private readonly string _directoryPath;
    public StateStore(string? directoryPath = null) => _directoryPath = directoryPath ?? DirectoryPath;
    public string FilePath => Path.Combine(_directoryPath, "settings.json");
    public string BackupPath => FilePath + ".backup";
    public string RecoveryMessage { get; private set; } = "";
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    private readonly object _writeLock = new();
    private bool _finalized;

    public ShellState Load()
    {
        RecoveryMessage = "";
        if (File.Exists(FilePath))
        {
            try
            {
                string json = ReadBounded(FilePath);
                var state = ReadState(json);
                using var document = JsonDocument.Parse(json);
                foreach (var migration in new[] { ("Profiles", "0.5.0"), ("ExploreSpaces", "0.8.0"), ("Tasks", "0.4.0") })
                {
                    if (document.RootElement.TryGetProperty(migration.Item1, out _)) continue;
                    string backup = Path.Combine(_directoryPath, "settings.before-" + migration.Item2 + ".json");
                    try { if (!File.Exists(backup)) File.Copy(FilePath, backup, false); }
                    catch (Exception ex) { Log.Write("Could not create settings migration backup", ex); }
                }
                if (!document.RootElement.TryGetProperty("ExploreSpaces", out _))
                { state.Wallpaper = "Opal"; state.NativeGlass = true; }
                return NormalizeState(state);
            }
            catch (Exception ex)
            {
                Log.Write("Settings could not be read; checking recovery copy", ex);
                try { File.Copy(FilePath, FilePath + ".unreadable-" + Guid.NewGuid().ToString("N"), false); } catch { }
            }
        }
        else if (!File.Exists(BackupPath)) return NormalizeState(new());
        try
        {
            var recovered = NormalizeState(ReadState(ReadBounded(BackupPath)));
            RecoveryMessage = "Your settings were recovered from the last saved backup. The damaged copy is kept in the Nexus data folder.";
            return recovered;
        }
        catch (Exception ex) { Log.Write("No readable settings recovery copy", ex); }
        RecoveryMessage = "Nexus could not restore your settings. It opened a fresh workspace; any unreadable copy is kept in the data folder.";
        return NormalizeState(new());
    }

    private static string ReadBounded(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumBytes) throw new InvalidDataException("Settings exceed the 2 MB limit.");
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }
    private ShellState ReadState(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Settings must contain a workspace object.");
        return JsonSerializer.Deserialize<ShellState>(json, _json) ?? throw new InvalidDataException("Settings are empty.");
    }
    private static ShellState NormalizeState(ShellState state)
    {
            state.DisplayName = string.IsNullOrWhiteSpace(state.DisplayName) ? "Shan" : state.DisplayName.Trim();
            state.DisplayName = state.DisplayName[..Math.Min(state.DisplayName.Length, 40)];
            state.PinnedApps ??= [];
            state.Activity ??= [];
            state.Wallpaper = AuraPalette.Moods.Contains(state.Wallpaper) ? state.Wallpaper : "Midnight";
            state.QuickNote ??= "";
            state.QuickNote = state.QuickNote[..Math.Min(state.QuickNote.Length, 10_000)];
            state.FocusDay ??= "";
            state.FocusCompleted = Math.Clamp(state.FocusCompleted, 0, 1000);
            WorkspaceState.Normalize(state);
            ExploreWorkspace.Normalize(state);
            DesktopWorkspace.Normalize(state);
            ShellExperience.Normalize(state);
            state.UsageSeconds = (state.UsageSeconds ?? []).Where(a => !string.IsNullOrWhiteSpace(a.Key) && a.Key.Length <= 260 &&
                    double.IsFinite(a.Value) && a.Value >= 0 && a.Value <= 1_000_000_000)
                .DistinctBy(a => a.Key, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(a => a.Value).Take(300).ToDictionary(a => a.Key, a => a.Value, StringComparer.OrdinalIgnoreCase);
            state.Activity = state.Activity.Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Message)).Take(200)
                .Select(a => a with { Message = a.Message[..Math.Min(a.Message.Length, 1024)] }).ToList();
            state.PinnedApps = state.PinnedApps.Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Target) && !string.IsNullOrWhiteSpace(a.Name))
                .DistinctBy(a => a.Target, StringComparer.OrdinalIgnoreCase).Take(100)
                .Select(a => a with { Glyph = string.IsNullOrEmpty(a.Glyph) ? "\uE8A5" : a.Glyph, Category = a.Category ?? "App" }).ToList();
        return state;
    }
    public void Save(ShellState state)
    {
        lock (_writeLock) { if (!_finalized) WriteAtomic(state); }
    }
    public void SaveFinal(ShellState state)
    {
        lock (_writeLock)
        {
            if (_finalized) return;
            // Older queued saves cannot overwrite the final UI-thread snapshot.
            _finalized = true;
            WriteAtomic(state);
        }
    }
    private void WriteAtomic(ShellState state)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(state, _json);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Settings exceed the 2 MB limit. The previous save is intact.");
        Directory.CreateDirectory(_directoryPath);
        string temporary = FilePath + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            bool oldReadable = false;
            if (File.Exists(FilePath))
            {
                try { ReadState(ReadBounded(FilePath)); oldReadable = true; }
                catch (Exception) { /* Preserve the existing recovery copy when the main file is damaged. */ }
            }
            if (oldReadable) File.Replace(temporary, FilePath, BackupPath);
            else File.Move(temporary, FilePath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
