namespace Nexus.Shell.Services;

public enum NoticeKind { Information, Warning, Error }
public sealed record DesktopNotice(Guid Id, string Title, string Message, NoticeKind Kind, DateTimeOffset Created, bool Read = false);

public static class NotificationHistory
{
    public static List<DesktopNotice> Normalize(IEnumerable<DesktopNotice>? entries) => (entries ?? [])
        .Where(item => item is not null && item.Id != Guid.Empty && !string.IsNullOrWhiteSpace(item.Title) && item.Message is not null && Enum.IsDefined(item.Kind))
        .DistinctBy(item => item.Id).OrderByDescending(item => item.Created).Take(80)
        .Select(item => item with { Title = item.Title[..Math.Min(item.Title.Length, 120)], Message = item.Message[..Math.Min(item.Message.Length, 2000)] }).ToList();
}
