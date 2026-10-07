using Nexus.Shell.Models;
using System.Text;
using System.Text.Json;

namespace Nexus.Shell.Services;

// Board records remain references. No folder indexing, URL fetching or file copying.
public static class ExploreWorkspace
{
    public const int SpaceLimit = 12, NoteLimit = 2000, ImportByteLimit = 2 * 1024 * 1024;
    public static readonly string[] Accents = ["Iris", "Ocean", "Mint", "Rose"];
    private static string Bound(string? value, int length) => (value ?? "").Trim()[..Math.Min((value ?? "").Trim().Length, length)];
    public static string Tags(string? value) => string.Join(", ", (value ?? "").Split(',')
        .Select(t => Bound(t.Trim().TrimStart('#'), 20)).Where(t => t.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase).Take(5));
    public static string ItemKey(SavedItem item)
    {
        if (item.Kind == "Note") return "Note:" + item.Id;
        if (item.Kind == "Link" && Uri.TryCreate(item.Target, UriKind.Absolute, out var uri))
            return "Link:" + uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant() + uri.PathAndQuery + uri.Fragment;
        return item.Kind + ":" + item.Target.ToUpperInvariant();
    }
    private static string SpaceItemKey(SavedItem item) => Bound(item.SpaceId, 64) + "\0" + ItemKey(item);
    public static bool SameReference(SavedItem first, SavedItem second) =>
        SpaceItemKey(first).Equals(SpaceItemKey(second), StringComparison.Ordinal);
    public static bool IsValid(SavedItem? item) => item is not null &&
        !string.IsNullOrWhiteSpace(item.Id) && item.Id.Length <= 64 && !string.IsNullOrWhiteSpace(item.Title) &&
        (item.Kind == "Note" || (!string.IsNullOrWhiteSpace(item.Target) && item.Target.Length <= 4096 &&
            (item.Kind == "Link" ? Uri.TryCreate(item.Target, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
                : (item.Kind is "File" or "Folder") && Path.IsPathFullyQualified(item.Target))));
    public static List<SavedItem> NormalizeItems(IEnumerable<SavedItem>? source) => (source ?? [])
        .Where(IsValid).DistinctBy(a => a.Id, StringComparer.Ordinal)
        .DistinctBy(SpaceItemKey, StringComparer.Ordinal).Take(WorkspaceState.MaximumItems)
        .Select(a => a with { Title = Bound(a.Title, 100), Target = a.Kind == "Note" ? "" : a.Target,
            Collection = WorkspaceState.CollectionName(a.Collection), SpaceId = Bound(a.SpaceId, 64),
            Note = Bound(a.Note, NoteLimit), Tags = Tags(a.Tags) }).ToList();
    public static void Normalize(ShellState state)
    {
        state.ExploreSpaces = (state.ExploreSpaces ?? []).Where(s => s is not null &&
                !string.IsNullOrWhiteSpace(s.Id) && s.Id.Length <= 64 && !string.IsNullOrWhiteSpace(s.Name))
            .DistinctBy(s => s.Id, StringComparer.Ordinal).Take(SpaceLimit)
            .Select(s => s with { Name = Bound(s.Name, 32), Description = Bound(s.Description, 120),
                Accent = Accents.Contains(s.Accent) ? s.Accent : "Iris" }).ToList();
        if (state.ExploreSpaces.Count == 0)
            state.ExploreSpaces = [new("personal", "Personal", "A little of your world.", "Iris"),
                new("study", "Study", "Resources for the things you are learning.", "Ocean"),
                new("build", "Build", "Ideas and references for your next project.", "Mint")];
        if (state.ExploreSpaces.All(s => s.Id != "personal"))
        {
            if (state.ExploreSpaces.Count == SpaceLimit) state.ExploreSpaces.RemoveAt(SpaceLimit - 1);
            state.ExploreSpaces.Insert(0, new("personal", "Personal", "A little of your world."));
        }
        var ids = state.ExploreSpaces.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        state.SavedItems = NormalizeItems(state.SavedItems).Select(a => ids.Contains(a.SpaceId) ? a : a with { SpaceId = "personal" }).ToList();
        if (!ids.Contains(state.ActiveExploreSpaceId ?? "")) state.ActiveExploreSpaceId = "personal";
        if (!state.SavedItems.Any(a => a.SpaceId == state.ActiveExploreSpaceId && a.Id == state.ExploreSelectedItemId)) state.ExploreSelectedItemId = "";
        state.ExploreView = state.ExploreView == "List" ? "List" : "Board";
        state.ExploreQuery = Bound(state.ExploreQuery, 100);
        state.ExploreCollection = Bound(state.ExploreCollection, 24);
        if (!state.SavedItems.Any(a => a.SpaceId == state.ActiveExploreSpaceId && a.Collection.Equals(state.ExploreCollection, StringComparison.OrdinalIgnoreCase)))
            state.ExploreCollection = "";
    }
    public static SavedItem Capture(string value, string spaceId)
    {
        value = value.Trim();
        if (value.Length == 0) throw new InvalidDataException("Paste a web address or write a note first.");
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            if (uri.AbsoluteUri.Length > 4096) throw new InvalidDataException("This address is too long.");
            return new(Guid.NewGuid().ToString("N"), Bound(uri.Host, 100), uri.AbsoluteUri, "Link", SpaceId: spaceId);
        }
        if (value.Length > NoteLimit) throw new InvalidDataException("Notes can contain up to 2,000 characters.");
        string title = value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "New note";
        return new(Guid.NewGuid().ToString("N"), Bound(title, 60), "", "Note", SpaceId: spaceId, Note: value);
    }
    public static bool Add(ShellState state, SavedItem item)
    {
        if (!IsValid(item)) throw new InvalidDataException("This item has an invalid address or path.");
        if (!state.ExploreSpaces.Any(s => s.Id == item.SpaceId)) throw new InvalidDataException("Choose an available space for this item.");
        if (state.SavedItems.Any(a => SameReference(a, item))) return false;
        if (state.SavedItems.Any(a => a.Id == item.Id)) throw new InvalidDataException("This item already has a saved identity.");
        if (state.SavedItems.Count >= WorkspaceState.MaximumItems) throw new InvalidDataException("Your board is full. Remove an item before adding another.");
        state.SavedItems.Insert(0, NormalizeItems([item])[0]);
        state.ExploreSelectedItemId = item.Id;
        return true;
    }
    public static SavedItem[] Filter(ShellState state) => state.SavedItems.Where(a =>
        a.SpaceId == state.ActiveExploreSpaceId && (!state.ExploreFavoritesOnly || a.Favorite) &&
        (state.ExploreCollection.Length == 0 || a.Collection.Equals(state.ExploreCollection, StringComparison.OrdinalIgnoreCase)) &&
        (state.ExploreQuery.Length == 0 || new[] { a.Title, a.Target, a.Note, a.Tags, a.Collection }
            .Any(v => v.Contains(state.ExploreQuery, StringComparison.CurrentCultureIgnoreCase))))
        .OrderByDescending(a => a.Favorite).ToArray();
    public static void RemoveSpace(ShellState state, string id)
    {
        if (id == "personal") throw new InvalidOperationException("Keep the Personal space available.");
        state.ExploreSpaces.RemoveAll(s => s.Id == id);
        state.SavedItems = state.SavedItems.Select(a => a.SpaceId == id ? a with { SpaceId = "personal" } : a).ToList();
        Normalize(state);
    }
    public sealed record SpaceArchive(int FormatVersion, ExploreSpace Space, List<SavedItem> Items);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static string Export(ShellState state)
    {
        var space = state.ExploreSpaces.First(s => s.Id == state.ActiveExploreSpaceId);
        return JsonSerializer.Serialize(new SpaceArchive(1, space, state.SavedItems.Where(a => a.SpaceId == space.Id).ToList()), Json);
    }
    public static SpaceArchive ReadImport(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > ImportByteLimit) throw new InvalidDataException("Space files must be smaller than 2 MB.");
        var data = JsonSerializer.Deserialize<SpaceArchive>(json, Json) ?? throw new InvalidDataException("This space file is empty.");
        if (data.FormatVersion != 1 || data.Space is null || string.IsNullOrWhiteSpace(data.Space.Name) || data.Items is null ||
            data.Items.Count > WorkspaceState.MaximumItems || data.Items.Any(a => !IsValid(a)))
            throw new InvalidDataException("This is not a supported NEXUS space file.");
        return data with { Space = data.Space with { Name = Bound(data.Space.Name, 32), Description = Bound(data.Space.Description, 120),
            Accent = Accents.Contains(data.Space.Accent) ? data.Space.Accent : "Iris" },
            Items = NormalizeItems(data.Items.Select(item => item with { SpaceId = "incoming" })) };
    }
    public static int Import(ShellState state, SpaceArchive data)
    {
        if (data.FormatVersion != 1 || data.Space is null || string.IsNullOrWhiteSpace(data.Space.Name) ||
            data.Items is null || data.Items.Any(item => !IsValid(item)))
            throw new InvalidDataException("This is not a supported NEXUS space file.");
        if (state.ExploreSpaces.Count >= SpaceLimit) throw new InvalidDataException("You can keep up to 12 spaces. Remove one before importing another.");
        // An imported space is independent; the same reference may belong to other spaces.
        if (data.Items.Count > WorkspaceState.MaximumItems) throw new InvalidDataException("This space exceeds the item limit.");
        var candidates = NormalizeItems(data.Items.Select(item => item with { SpaceId = "incoming" }));
        if (state.SavedItems.Count + candidates.Count > WorkspaceState.MaximumItems)
            throw new InvalidDataException("This space would exceed the 100-item board limit. Nothing was imported.");
        string id = Guid.NewGuid().ToString("N");
        state.ExploreSpaces.Add(data.Space with { Id = id, Name = Bound(data.Space.Name, 32), Description = Bound(data.Space.Description, 120) });
        state.SavedItems.InsertRange(0, candidates.Select(item => item with { Id = Guid.NewGuid().ToString("N"), SpaceId = id }));
        state.ActiveExploreSpaceId = id; state.ExploreSelectedItemId = ""; state.ExploreQuery = ""; state.ExploreCollection = ""; state.ExploreFavoritesOnly = false;
        Normalize(state);
        return candidates.Count;
    }
}
