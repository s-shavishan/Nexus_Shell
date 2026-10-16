// නමෝ බුද්ධාය 🤍
namespace Nexus.Runtime.Files;

public sealed record FileTab(Guid Id, string Location)
{
    public string Title => Location == "nexus:recycle" ? "Recycle Bin"
        : Path.GetFileName(Path.TrimEndingDirectorySeparator(Location)) is { Length: > 0 } name ? name : Location;
}

/// <summary>Bounded session-only tab identities. File access stays in the Files worker.</summary>
public sealed class FileTabSession
{
    public const int MaximumTabs = 12, MaximumClosedTabs = 12;
    private readonly List<FileTab> _tabs = [];
    private readonly List<string> _closed = [];
    public IReadOnlyList<FileTab> Tabs => _tabs.AsReadOnly();
    public Guid ActiveId { get; private set; }
    public FileTab Active => _tabs.First(t => t.Id == ActiveId);
    public bool CanReopen => _closed.Count > 0 && _tabs.Count < MaximumTabs;

    public static string NormalizeLocation(string location)
    {
        if (location == "nexus:recycle") return location;
        if (string.IsNullOrWhiteSpace(location) || location.Length > 32767 || location.Any(char.IsControl)
            || !Path.IsPathFullyQualified(location))
            throw new ArgumentException("Choose a full folder path or Recycle Bin.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(location));
    }

    public FileTab Open(string location)
    {
        location = NormalizeLocation(location);
        if (_tabs.Count >= MaximumTabs)
            throw new InvalidOperationException("Files has 12 tabs. Close a tab before opening another.");
        var tab = new FileTab(Guid.NewGuid(), location);
        _tabs.Add(tab);
        ActiveId = tab.Id;
        return tab;
    }

    public FileTab? Find(string location)
    {
        location = NormalizeLocation(location);
        return _tabs.FirstOrDefault(t => string.Equals(t.Location, location, StringComparison.OrdinalIgnoreCase));
    }

    public void Activate(Guid id)
    {
        if (!_tabs.Any(t => t.Id == id)) throw new InvalidOperationException("This tab has closed.");
        ActiveId = id;
    }

    public void Cycle(int direction)
    {
        if (_tabs.Count == 0) return;
        int index = _tabs.FindIndex(t => t.Id == ActiveId);
        ActiveId = _tabs[(index + (direction < 0 ? _tabs.Count - 1 : 1)) % _tabs.Count].Id;
    }

    public void Update(Guid id, string location)
    {
        location = NormalizeLocation(location);
        int index = _tabs.FindIndex(t => t.Id == id);
        if (index >= 0) _tabs[index] = _tabs[index] with { Location = location };
    }

    public bool Close(Guid id)
    {
        int index = _tabs.FindIndex(t => t.Id == id);
        if (index < 0 || _tabs.Count == 1) return false;
        _closed.Add(_tabs[index].Location);
        if (_closed.Count > MaximumClosedTabs) _closed.RemoveAt(0);
        _tabs.RemoveAt(index);
        if (ActiveId == id) ActiveId = _tabs[Math.Min(index, _tabs.Count - 1)].Id;
        return true;
    }

    public FileTab Reopen()
    {
        if (!CanReopen) throw new InvalidOperationException("There is no closed tab to reopen, or Files already has 12 tabs.");
        var tab = Open(_closed[^1]);
        _closed.RemoveAt(_closed.Count - 1);
        return tab;
    }
}
