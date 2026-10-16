// නමෝ බුද්ධාය 🤍
using System.Text.Json;

namespace Nexus.Shell.Services;

// A validated immutable menu catalog. The JSON contains *classifications*
// only: it cannot declare executable commands, custom DLLs, filesystem writes
// or new action handlers. Broken/partial hot reloads retain the last good map.
// Menu construction resolves cached dictionaries, without filesystem I/O.
public sealed record FileTypeMenuProfile(
    string Id, string Label, string Primary, string[] Actions, string[]? Extensions = null);

public sealed record FileTypeMenuTarget(string Path, bool IsFolder);
public sealed record FileTypeMenuPlan(string ProfileId, string Label, string Primary,
    IReadOnlyList<string> CommonActions, int SelectionCount);

public static class FileTypeContextCatalog
{
    private sealed record CatalogDocument(string Schema, FileTypeMenuProfile[] Profiles, FileTypeMenuProfile Fallback);
    private sealed record CatalogSnapshot(IReadOnlyDictionary<string, FileTypeMenuProfile> Extensions,
        FileTypeMenuProfile Fallback);
    private static readonly FileTypeMenuProfile Unknown = new("fallback.unknown", "File",
        "open", ["open_with"]);
    private static readonly FileTypeMenuProfile Folder = new("folder", "Folder", "open", []);
    // A menu's first entry is always a safe Windows association invocation.
    // Config cannot repurpose that entry as an arbitrary action ID.
    private static readonly HashSet<string> PrimaryActions = new(StringComparer.Ordinal)
    { "open", "play", "run" };
    private static readonly HashSet<string> SecondaryActions = new(StringComparer.Ordinal)
    { "edit", "open_with" };
    private static readonly string CatalogPath = Path.Combine(AppContext.BaseDirectory,
        "Assets", "Configuration", "file_context_profiles.json");
    private static CatalogSnapshot _snapshot = new(
        new Dictionary<string, FileTypeMenuProfile>(StringComparer.OrdinalIgnoreCase), Unknown);
    private static FileSystemWatcher? _watcher;
    private static int _initialized, _reloadQueued;
    public static string LastError { get; private set; } = "";

    public static bool Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0) return true;
        Reload();
        try
        {
            var directory = Path.GetDirectoryName(CatalogPath);
            if (directory is null || !Directory.Exists(directory)) return true;
            _watcher = new FileSystemWatcher(directory, Path.GetFileName(CatalogPath))
            { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime };
            _watcher.Changed += (_, _) => QueueReload();
            _watcher.Created += (_, _) => QueueReload();
            _watcher.Renamed += (_, _) => QueueReload();
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception error) { LastError = error.Message; }
        return true;
    }

    private static void QueueReload()
    {
        if (Interlocked.Exchange(ref _reloadQueued, 1) != 0) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(160).ConfigureAwait(false);
                if (!Reload())
                {
                    // Editors often rename or truncate a JSON file before the
                    // final write. Keep the previous snapshot and retry once
                    // after the write has settled; never make a popup wait.
                    await Task.Delay(420).ConfigureAwait(false);
                    Reload();
                }
            }
            finally { Volatile.Write(ref _reloadQueued, 0); }
        });
    }

    public static bool Reload()
    {
        try
        {
            if (!File.Exists(CatalogPath)) { LastError = "Catalog unavailable; using safe defaults."; return false; }
            using var stream = File.Open(CatalogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length is <= 0 or > 262144)
                throw new InvalidDataException("File context catalog exceeds its size budget.");
            var document = JsonSerializer.Deserialize<CatalogDocument>(stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("The file context catalog is empty.");
            Interlocked.Exchange(ref _snapshot, ValidateDocument(document));
            LastError = "";
            return true;
        }
        catch (Exception error)
        {
            LastError = error.Message; // Preserve last known-good snapshot.
            return false;
        }
    }


    // Pure preflight: invalid user-edited catalogs can be rejected and tested
    // without replacing the live snapshot or installing an event watcher.
    public static bool ValidateJson(string json, out string error)
    {
        try
        {
            if (System.Text.Encoding.UTF8.GetByteCount(json) is <= 0 or > 262144)
                throw new InvalidDataException("File context catalog exceeds its size budget.");
            var document = JsonSerializer.Deserialize<CatalogDocument>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("The file context catalog is empty.");
            _ = ValidateDocument(document);
            error = "";
            return true;
        }
        catch (Exception failure)
        {
            error = failure.Message;
            return false;
        }
    }

    private static CatalogSnapshot ValidateDocument(CatalogDocument document)
    {
        if (document.Schema != "nexus.shell.filecontext/v1" || document.Profiles is null
            || document.Profiles.Length is < 1 or > 64)
            throw new InvalidDataException("Unrecognized file context schema or profile count.");
        Validate(document.Fallback);
        var extensions = new Dictionary<string, FileTypeMenuProfile>(StringComparer.OrdinalIgnoreCase);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in document.Profiles)
        {
            Validate(profile);
            if (!ids.Add(profile.Id)) throw new InvalidDataException("Duplicate profile ID: " + profile.Id);
            if (profile.Extensions is null || profile.Extensions.Length is < 1 or > 128)
                throw new InvalidDataException("Profile requires a bounded extension set: " + profile.Id);
            foreach (string ext in profile.Extensions)
            {
                if (string.IsNullOrWhiteSpace(ext) || ext.Length > 20 || !ext.StartsWith('.')
                    || ext.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '-' and not '_'))
                    throw new InvalidDataException("Invalid extension entry in " + profile.Id);
                if (!extensions.TryAdd(ext, profile))
                    throw new InvalidDataException("Ambiguous extension " + ext
                        + ". Resolve conflicts in the catalog before reloading.");
            }
        }
        if (document.Fallback.Id != "fallback.unknown"
            || document.Fallback.Primary != "open" || extensions.Count > 1500)
            throw new InvalidDataException("Missing default fallback or oversized catalog.");
        return new CatalogSnapshot(extensions, document.Fallback);

    }

    private static void Validate(FileTypeMenuProfile? profile)
    {
        if (profile is null || string.IsNullOrWhiteSpace(profile.Id)
            || profile.Id.Length > 64 || string.IsNullOrWhiteSpace(profile.Label)
            || profile.Label.Length > 64 || profile.Actions is null || profile.Actions.Length > 6
            || !PrimaryActions.Contains(profile.Primary)
            || profile.Actions.Any(a => !SecondaryActions.Contains(a))
            || profile.Actions.Distinct(StringComparer.Ordinal).Count() != profile.Actions.Length)
            throw new InvalidDataException("Invalid file menu profile/action.");
    }

    public static FileTypeMenuProfile Resolve(string path, bool folder)
    {
        if (folder) return Folder;
        var snapshot = Volatile.Read(ref _snapshot);
        string name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name)) return snapshot.Fallback;
        // Longest compound extension wins (.tar.gz before .gz) with no
        // executable/script heuristics and no disk probing on a UI frame.
        for (int pos = name.IndexOf('.'); pos >= 0; pos = name.IndexOf('.', pos + 1))
            if (snapshot.Extensions.TryGetValue(name[pos..], out var profile)) return profile;
        return snapshot.Fallback;
    }

    public static FileTypeMenuPlan Plan(IReadOnlyList<FileTypeMenuTarget> targets)
    {
        if (targets.Count is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(targets));
        var first = Resolve(targets[0].Path, targets[0].IsFolder);
        if (targets.Count == 1)
            return new(first.Id, first.Label, first.Primary, first.Actions, 1);
        // Multi-selection must never imply a dangerous mass-open/run operation.
        // Only actions explicitly supported by *every* selected type survive.
        var intersection = new HashSet<string>(first.Actions, StringComparer.Ordinal);
        foreach (var target in targets.Skip(1))
            intersection.IntersectWith(Resolve(target.Path, target.IsFolder).Actions);
        return new("selection.mixed", targets.Count + " selected items", "",
            first.Actions.Where(intersection.Contains).ToArray(), targets.Count);
    }
}
