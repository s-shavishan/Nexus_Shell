using Nexus.Shell.Models;
using System.Text.Json;

namespace Nexus.Shell.Services;

public sealed class StateStore
{
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WhiteDreams", "NexusShell");
    public string FilePath => Path.Combine(DirectoryPath, "settings.json");
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    private readonly object _writeLock = new();
    private bool _finalized;
    public ShellState Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            if (new FileInfo(FilePath).Length > 2 * 1024 * 1024) throw new InvalidDataException("Settings exceed the 2 MB limit.");
            var state = JsonSerializer.Deserialize<ShellState>(File.ReadAllText(FilePath), _json) ?? new();
            state.DisplayName = string.IsNullOrWhiteSpace(state.DisplayName) ? "Shan" : state.DisplayName.Trim();
            state.DisplayName = state.DisplayName[..Math.Min(state.DisplayName.Length, 40)];
            state.PinnedApps ??= [];
            state.Activity ??= [];
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
        catch (Exception ex)
        {
            Log.Write("Settings could not be read", ex);
            try { File.Copy(FilePath, FilePath + ".unreadable-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), false); } catch { }
            return new();
        }
    }
    public void Save(ShellState state)
    {
        // A final synchronous save waits for any older background write, then wins.
        lock (_writeLock)
        {
            if (_finalized) return;
            WriteAtomic(state);
        }
    }
    public void SaveFinal(ShellState state)
    {
        lock (_writeLock)
        {
            // An older Task.Run that has not started yet must never overwrite this.
            _finalized = true;
            WriteAtomic(state);
        }
    }
    private void WriteAtomic(ShellState state)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, _json));
        File.Move(temporary, FilePath, overwrite: true);
    }
}
