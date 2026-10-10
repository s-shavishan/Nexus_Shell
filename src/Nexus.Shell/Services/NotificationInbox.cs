namespace Nexus.Shell.Services;

public enum NoticeKind { Information, Warning, Error }
public sealed record DesktopNotice(Guid Id, string Title, string Message, NoticeKind Kind, DateTimeOffset Created, bool Read = false);

// A bounded Nexus inbox. Windows application toasts remain owned by Windows.
public sealed class NotificationInbox
{
    private readonly List<DesktopNotice> _items = [];
    private readonly object _gate = new();
    public bool Quiet { get; set; }
    public event Action? Changed;
    public IReadOnlyList<DesktopNotice> Items { get { lock (_gate) return _items.ToArray(); } }
    public int Unread { get { lock (_gate) return _items.Count(item => !item.Read); } }
    public void Publish(string title, string message, NoticeKind kind = NoticeKind.Information, DateTimeOffset? now = null)
    {
        var time = now ?? DateTimeOffset.Now;
        lock (_gate)
        {
            if (_items.Any(item => item.Title == title && item.Message == message && time - item.Created < TimeSpan.FromSeconds(5))) return;
            _items.Insert(0, new(Guid.NewGuid(), title[..Math.Min(title.Length, 120)], message[..Math.Min(message.Length, 2000)], kind, time));
            if (_items.Count > 80) _items.RemoveRange(80, _items.Count - 80);
        }
        Changed?.Invoke();
    }
    public void MarkRead() { bool changed; lock (_gate) { changed = _items.Any(item => !item.Read); for (int i = 0; i < _items.Count; i++) _items[i] = _items[i] with { Read = true }; } if (changed) Changed?.Invoke(); }
    public void Dismiss(Guid id) { lock (_gate) _items.RemoveAll(item => item.Id == id); Changed?.Invoke(); }
    public void Clear() { lock (_gate) _items.Clear(); Changed?.Invoke(); }
}
