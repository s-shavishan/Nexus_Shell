using Nexus.Shell.Models;
using Nexus.Shell.Services;

internal static class MajorDesktopChecks
{
    internal static void Run()
    {
        int count = 0;
        void Check(bool value, string message) { count++; if (!value) throw new Exception(message); }
        var original = new MinimizedDesktopMetrics(154, 0, 0, 5);
        var hidden = MinimizedWindowPolicy.Hidden(original);
        Check(hidden.Arrangement == 13 && hidden.Width == original.Width, "Minimized captions must move off screen while retaining the user's metrics.");
        Check(MinimizedWindowPolicy.Restore(hidden, original) == original, "Owned minimized arrangement must restore.");
        var resized = hidden with { Width = 190, HorizontalGap = 7 };
        Check(MinimizedWindowPolicy.Restore(resized, original) == resized with { Arrangement = original.Arrangement }, "DPI/user sizing changes must survive recovery.");
        var foreign = hidden with { Arrangement = 2 };
        Check(MinimizedWindowPolicy.Restore(foreign, original) == foreign, "An externally changed arrangement must be preserved.");
        Check(!MinimizedWindowPolicy.IsValid(original with { Arrangement = 16 }), "Damaged arrangement must be rejected before a native write.");
        string folder = Path.Combine(Path.GetTempPath(), "nexus-major-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "session.json"); var record = new DesktopSessionRecord(path);
            var saved = new DesktopSessionSnapshot(1, 7, Path.Combine(folder, "Nexus.DesktopHost.exe"), new(0, 0, 1920, 1080), new(0, 0, 1920, 1040), [], original);
            record.Save(saved); Check(record.Read(7)?.MinimizedMetrics == original, "The durable recovery journal must retain original minimized metrics.");
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(saved with { MinimizedMetrics = null }).Replace(",\"MinimizedMetrics\":null", ""));
            Check(record.Read(7)?.MinimizedMetrics is null, "A 1.8 recovery journal without minimized metrics must remain readable.");
            bool rejected = false; try { record.Save(saved with { MinimizedMetrics = original with { Arrangement = 16 } }); } catch (InvalidDataException) { rejected = true; }
            Check(rejected && record.Read(7)?.MinimizedMetrics is null, "Invalid metrics must leave the previous durable journal intact.");
        }
        finally { Directory.Delete(folder, recursive: true); }
        var inbox = new NotificationInbox(); int events = 0; inbox.Changed += () => events++;
        var now = DateTimeOffset.UtcNow;
        inbox.Publish("Save", "Failure", NoticeKind.Error, now); inbox.Publish("Save", "Failure", NoticeKind.Error, now.AddSeconds(2));
        Check(inbox.Items.Count == 1 && inbox.Unread == 1 && events == 1, "Repeated errors must not flood the inbox.");
        inbox.MarkRead(); int readEvents = events; inbox.MarkRead();
        Check(inbox.Unread == 0 && events == readEvents, "Repeated mark-read must not cause a UI refresh loop.");
        inbox.Quiet = true; inbox.Publish("Files", "Picker cancelled", now: now.AddSeconds(10));
        Check(inbox.Unread == 1, "Quiet mode must retain alerts for later inspection.");
        var snapshot = inbox.Items; inbox.Dismiss(inbox.Items[0].Id);
        Check(snapshot.Count == 2 && inbox.Items.Count == 1, "Dismissal must not mutate an existing read snapshot.");
        Parallel.For(0, 150, i => inbox.Publish("Event " + i, "Detail " + i, now: now.AddMinutes(1)));
        Check(inbox.Items.Count == 80 && inbox.Items.Select(item => item.Id).Distinct().Count() == 80, "Concurrent publishers must retain a bounded inbox with unique identities.");
        inbox.Clear(); Check(inbox.Items.Count == 0 && inbox.Unread == 0, "Clear must remove entries and the badge.");
        var historical = new DesktopNotice(Guid.NewGuid(), new string('x', 160), new string('y', 2400), NoticeKind.Warning, now, true);
        var restoredInbox = new NotificationInbox([historical, historical, historical with { Id = Guid.Empty }]);
        Check(restoredInbox.Items.Count == 1 && restoredInbox.Items[0].Title.Length == 120 && restoredInbox.Items[0].Message.Length == 2000 && restoredInbox.Unread == 0, "Restored alert history must deduplicate, bound text and preserve read state.");
        string historyFolder = Path.Combine(Path.GetTempPath(), "Nexus-notifications-" + Guid.NewGuid().ToString("N"));
        try
        {
            var historyStore = new StateStore(historyFolder); var historyState = new ShellState { NotificationHistory = restoredInbox.Items.ToList() };
            historyStore.Save(historyState); historyState.NotificationHistory.Clear();
            Check(new StateStore(historyFolder).Load().NotificationHistory.Single().Id == historical.Id, "Alert history must survive an actual atomic state save and reload.");
            var cleared = new StateStore(historyFolder).Load(); cleared.NotificationHistory.Clear(); historyStore.Save(cleared);
            Check(new StateStore(historyFolder).Load().NotificationHistory.Count == 0, "Cleared alerts must stay cleared after restart.");
        }
        finally { Directory.Delete(historyFolder, true); }
        var builtin = new AppEntry("notes", "Notes", "nexus:notes", "", "Utility");
        var apps = LaunchpadCatalog.Build([builtin], [builtin with { Name = "Pinned duplicate", Target = "NEXUS:NOTES" }], Enumerable.Range(0, 70).Select(i => new AppEntry("app" + i, "App " + i, "/app/" + i, "", i % 2 == 0 ? "Game" : "Development")));
        Check(apps.Count == 71 && apps[0] == builtin, "Launchpad must deduplicate a pinned builtin while retaining catalogue order.");
        Check(LaunchpadCatalog.Page(apps, 0).Count == 24 && LaunchpadCatalog.Page(apps, 2).Count == 23, "Launchpad must realize only one bounded page.");
        Check(LaunchpadCatalog.Page(apps, int.MaxValue).SequenceEqual(LaunchpadCatalog.Page(apps, 2)), "Stale page selection must clamp after a filter change.");
        Check(LaunchpadCatalog.Filter(apps, "APP 2", "Games").All(app => app.Category == "Game"), "Query and category filtering must compose.");
        Check(LaunchpadCatalog.Filter(apps, "no matching app", "All").Count == 0, "Empty searches must remain empty.");
        foreach (var monitor in new[] { new ShellRect(-1920, -200, 1920, 1080), new ShellRect(0, 0, 320, 480), new ShellRect(20, 20, 1, 1) })
        foreach (double scale in new[] { .5, 1, 1.5, 4, double.NaN })
        {
            foreach (var rectangle in new[] { DesktopLayout.LaunchpadBounds(monitor, scale), DesktopLayout.PanelBounds(monitor, scale), DesktopLayout.PanelBounds(monitor, scale, 540, 740), DesktopLayout.ManagedWorkArea(monitor, false, true, scale) })
                Check(rectangle.Width > 0 && rectangle.Height > 0 && rectangle.X >= monitor.X && rectangle.Y >= monitor.Y && rectangle.Right <= monitor.Right && rectangle.Bottom <= monitor.Bottom, "Major desktop surfaces must fit negative-origin/tiny/scaled monitors.");
        }
        var work = DesktopLayout.ManagedWorkArea(new(0, 0, 1920, 1080), false, true, 1.5);
        Check(work.Y == 57 && work.Bottom == 1080, "Maximized windows must leave the menu bar visible without reserving a floating-dock gap.");
        foreach (var monitor in new[] { new ShellRect(-1920, -200, 1920, 1080), new ShellRect(0, 0, 1920, 1080), new ShellRect(0, 0, 1, 1) })
        foreach (double scale in new[] { .5, 1, 1.5, 4, double.NaN })
        {
            var area = DesktopLayout.ManagedWorkArea(monitor, false, true, scale);
            foreach (bool attached in new[] { true, false })
            {
                var bar = MenuBarLayout.Bounds(monitor, scale, attached);
                Check(bar.Width > 0 && bar.Height > 0 && bar.X >= monitor.X && bar.Y >= monitor.Y && bar.Right <= monitor.Right && bar.Bottom <= Math.Max(monitor.Y + 1, area.Y), "Floating and attached bar must fit one fixed reservation.");
            }
        }
        var screen = new ShellRect(-1920, -200, 1920, 1080); var usable = DesktopLayout.ManagedWorkArea(screen, false, true, 1);
        Check(MenuBarLayout.Attached(usable with { Width = usable.Width / 2 }, usable, false, 1), "Left snap must attach the bar.");
        Check(MenuBarLayout.Attached(usable with { X = usable.X + usable.Width / 2, Width = usable.Width / 2 }, usable, false, 1), "Right snap must attach the bar.");
        Check(!MenuBarLayout.Attached(new(-1800, -40, 800, 600), usable, false, 1), "An ordinary window must leave the bar floating.");
        Check(MenuBarLayout.ClientBounds(new(usable.X - 8, usable.Y - 8, usable.Width + 16, usable.Height + 16), usable, true) == usable, "Maximized borderless client must not lose its title row beyond the work area.");
        Check(MenuBarLayout.ClientBounds(screen, usable, false) == screen, "Normal and full-screen placement must remain untouched.");
        Console.WriteLine($"PASS: {count} major desktop checks; minimized-caption recovery/ownership, bounded notifications, Launchpad paging/filtering and menu-bar geometry.");
    }
}
