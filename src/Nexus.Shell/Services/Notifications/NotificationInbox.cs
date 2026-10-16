// නමෝ බුද්ධාය 🤍
namespace Nexus.Shell.Services;

// A bounded Nexus inbox. Windows application toasts remain owned by Windows.
public sealed class NotificationInbox
{
    private readonly List<DesktopNotice> _items = [];
    private readonly object _gate = new();
    private DesktopNotice[] _dismissed = [];
    public NotificationInbox(IEnumerable<DesktopNotice>? history = null) => _items.AddRange(NotificationHistory.Normalize(history));
    public bool Quiet { get; set; }
    public event Action? Changed;
    public IReadOnlyList<DesktopNotice> Items { get { lock (_gate) return _items.ToArray(); } }
    public int Unread { get { lock (_gate) return _items.Count(item => !item.Read && !Snoozed(item, DateTimeOffset.Now)); } }
    public bool CanUndoDismiss { get { lock (_gate) return _dismissed.Length > 0; } }
    private static bool Snoozed(DesktopNotice item, DateTimeOffset now) => item.SnoozedUntil is { } until && until > now;
    public IReadOnlyList<DesktopNotice> Query(string query, string filter, DateTimeOffset? now = null)
    {
        query = query.Trim(); var time = now ?? DateTimeOffset.Now;
        lock (_gate) return _items.Where(item => (filter == "Snoozed" ? Snoozed(item, time) : !Snoozed(item, time))
            && (filter != "Unread" || !item.Read) && (filter != "Issues" || item.Kind != NoticeKind.Information)
            && (query.Length == 0 || item.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) || item.Message.Contains(query, StringComparison.CurrentCultureIgnoreCase))).ToArray();
    }
    public void Snooze(Guid id, TimeSpan delay, DateTimeOffset? now = null)
    {
        if (delay <= TimeSpan.Zero || delay > TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(delay));
        bool changed = false;
        lock (_gate) { int index = _items.FindIndex(i => i.Id == id); if (index >= 0) { _items[index] = _items[index] with { SnoozedUntil = (now ?? DateTimeOffset.Now) + delay, Read = false }; changed = true; } }
        if (changed) Changed?.Invoke();
    }
    public void Wake(Guid id)
    {
        bool changed = false;
        lock (_gate) { int index = _items.FindIndex(i => i.Id == id); if (index >= 0) { _items[index] = _items[index] with { SnoozedUntil = null, Read = false }; changed = true; } }
        if (changed) Changed?.Invoke();
    }
    public void RefreshDue(DateTimeOffset? now = null)
    {
        bool changed = false; var time = now ?? DateTimeOffset.Now;
        lock (_gate) for (int i = 0; i < _items.Count; i++) if (_items[i].SnoozedUntil is { } until && until <= time)
        { _items[i] = _items[i] with { SnoozedUntil = null, Read = false }; changed = true; }
        if (changed) Changed?.Invoke();
    }
    public void Publish(string title, string message, NoticeKind kind = NoticeKind.Information, DateTimeOffset? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title); ArgumentNullException.ThrowIfNull(message);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        title = title[..Math.Min(title.Length, 120)]; message = message[..Math.Min(message.Length, 2000)];
        var time = now ?? DateTimeOffset.Now;
        lock (_gate)
        {
            if (_items.Any(item => item.Title == title && item.Message == message && item.Kind == kind
                && time >= item.Created && time - item.Created < TimeSpan.FromSeconds(5))) return;
            _items.Insert(0, new(Guid.NewGuid(), title, message, kind, time));
            if (_items.Count > 80) _items.RemoveRange(80, _items.Count - 80);
        }
        Changed?.Invoke();
    }
    public void MarkRead() { bool changed; lock (_gate) { changed = _items.Any(item => !item.Read); for (int i = 0; i < _items.Count; i++) _items[i] = _items[i] with { Read = true }; } if (changed) Changed?.Invoke(); }
    public void MarkRead(IEnumerable<Guid> ids)
    {
        var selected = ids.ToHashSet(); bool changed = false;
        lock (_gate) for (int i = 0; i < _items.Count; i++) if (!_items[i].Read && selected.Contains(_items[i].Id)) { _items[i] = _items[i] with { Read = true }; changed = true; }
        if (changed) Changed?.Invoke();
    }
    public void Dismiss(Guid id)
    { bool changed; lock (_gate) { var selected = _items.Where(i => i.Id == id).ToArray(); changed = selected.Length > 0; if (changed) { _dismissed = selected; _items.RemoveAll(i => i.Id == id); } } if (changed) Changed?.Invoke(); }
    public void Clear()
    { bool changed; lock (_gate) { changed = _items.Count != 0; if (changed) { _dismissed = _items.ToArray(); _items.Clear(); } } if (changed) Changed?.Invoke(); }
    public void UndoDismiss()
    {
        bool changed;
        lock (_gate) { changed = _dismissed.Length > 0; if (changed) { var restored = NotificationHistory.Normalize(_items.Concat(_dismissed)); _items.Clear(); _items.AddRange(restored); _dismissed = []; } }
        if (changed) Changed?.Invoke();
    }
}
