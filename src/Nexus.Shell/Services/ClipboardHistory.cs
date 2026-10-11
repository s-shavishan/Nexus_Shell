namespace Nexus.Shell.Services;

public sealed record ClipboardItem(Guid Id, string Text, DateTimeOffset Copied, bool Pinned)
{
    public string Preview => Text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ')[..Math.Min(Text.Length, 180)];
}

// Session-only memory. This object is deliberately absent from ShellState.
public sealed class ClipboardHistory
{
    public const int MaximumItems = 32, MaximumPins = 8, MaximumCharacters = 16_384;
    private readonly List<ClipboardItem> _items = [];
    public long Revision { get; private set; }
    public bool Enabled { get; private set; }
    public IReadOnlyList<ClipboardItem> Items => _items.ToArray();
    public void SetEnabled(bool enabled) { if (Enabled == enabled) return; Enabled = enabled; Clear(); }
    public bool Add(string? text, DateTimeOffset now)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(text) || text.Length > MaximumCharacters || text.Contains('\0')) return false;
        var existing = _items.FirstOrDefault(item => item.Text == text);
        if (existing is not null) _items.Remove(existing);
        _items.Insert(0, existing is null ? new(Guid.NewGuid(), text, now, false) : existing with { Copied = now });
        while (_items.Count > MaximumItems) _items.RemoveAt(_items.FindLastIndex(item => !item.Pinned));
        Revision++; return true;
    }
    public bool TogglePin(Guid id)
    {
        int index = _items.FindIndex(item => item.Id == id); if (index < 0) return false;
        var item = _items[index]; if (!item.Pinned && _items.Count(i => i.Pinned) >= MaximumPins) return false;
        _items[index] = item with { Pinned = !item.Pinned }; Revision++; return true;
    }
    public void Remove(Guid id) { if (_items.RemoveAll(item => item.Id == id) > 0) Revision++; }
    public void Clear() { _items.Clear(); Revision++; }
    public IReadOnlyList<ClipboardItem> Search(string? query) => _items.Where(item => string.IsNullOrWhiteSpace(query)
        || item.Text.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)).OrderByDescending(item => item.Pinned).ThenByDescending(item => item.Copied).ToArray();
}
