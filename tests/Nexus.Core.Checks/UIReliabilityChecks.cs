using Nexus.Shell.Models;
using Nexus.Shell.Services;

internal static class UIReliabilityChecks
{
    internal static async Task RunAsync()
    {
        int count = 0;
        void Check(bool value, string message) { count++; if (!value) throw new Exception(message); }
        var session = new SurfaceSession();
        Check(!session.Accepts(session.Capture(), "Sound"), "A never-opened surface cannot display a reply.");
        var first = session.Open("Sound"); var network = session.Open("Network");
        Check(!session.Accepts(first, "Sound") && session.Accepts(network, "Network"), "Navigation must isolate replies by opening and section.");
        session.Hide(); var reopened = session.Open("Network");
        Check(!session.Accepts(network, "Network") && session.Accepts(reopened, "Network"), "Reopening the same section must not accept a previous response.");

        string rendered = ""; int errors = 0;
        async Task Deliver(Task<string> response, long generation, string section)
        {
            try { string result = await response; if (session.Accepts(generation, section)) rendered = result; }
            catch { if (session.Accepts(generation, section)) errors++; }
        }
        for (int cycle = 0; cycle < 12; cycle++)
        {
            var obsolete = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            long old = session.Open("Sound"); var oldReply = Deliver(obsolete.Task, old, "Sound");
            session.Hide(); long current = session.Open("Sound");
            await Deliver(Task.FromResult("new " + cycle), current, "Sound");
            if (cycle % 2 == 0) obsolete.SetResult("old volume"); else obsolete.SetException(new IOException("old connection failed"));
            await oldReply;
            Check(rendered == "new " + cycle && errors == 0, "Late results and late failures must leave the reopened panel intact.");
        }
        await Deliver(Task.FromException<string>(new IOException("current read failed")), session.Capture(), "Sound");
        Check(errors == 1, "A current failure must still be visible rather than silently swallowed.");
        session.Hide(); await Deliver(Task.FromResult("hidden update"), session.Capture(), "Sound");
        Check(rendered == "new 11", "A hidden surface must stay hidden even with a matching capture.");

        var pins = new List<AppEntry>(); var app = new AppEntry("editor", "Editor", "C:\\Editor\\editor.exe", "x", "Development");
        Check(LaunchpadCatalog.TogglePin(pins, app) && pins.Single() == app, "Pinning must retain the launch identity.");
        Check(LaunchpadCatalog.IsPinned(pins, app with { Target = app.Target.ToUpperInvariant() }), "Windows target casing must not create another pin.");
        Check(!LaunchpadCatalog.TogglePin(pins, app with { Target = app.Target.ToUpperInvariant() }) && pins.Count == 0, "Unpinning must remove the matching target.");
        pins.AddRange(Enumerable.Range(0, 100).Select(i => app with { Id = "app-" + i, Target = "C:\\App" + i + "\\app.exe" }));
        var before = pins.ToArray(); bool rejected = false;
        try { LaunchpadCatalog.TogglePin(pins, app); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && pins.SequenceEqual(before), "A full dock must reject an extra pin without losing any existing entries.");
        Check(!LaunchpadCatalog.TogglePin(pins, before[0]) && pins.Count == 99, "Removing a pin must remain possible at capacity.");
        Check(LaunchpadCatalog.TogglePin(pins, app) && pins.Count == 100, "A freed pin slot must be immediately usable.");
        rejected = false; try { LaunchpadCatalog.TogglePin(pins, app with { Target = "" }); } catch (ArgumentException) { rejected = true; }
        Check(rejected && pins.Count == 100, "An invalid catalogue entry must not mutate pins.");

        Check(DesktopPresentation.ClockFormat(true, false) == "h:mm tt", "Compact menu bars must retain 12-hour time and AM/PM.");
        Check(DesktopPresentation.ClockFormat(true, true) == "HH:mm", "Compact 24-hour time must remain 24-hour.");
        Check(DesktopPresentation.ClockFormat(false, false).Contains("tt") && DesktopPresentation.ClockFormat(false, true).Contains("HH"), "Expanded clock formatting must follow the same preference.");
        var today = new DateTimeOffset(DateTime.Today.AddHours(12), TimeZoneInfo.Local.GetUtcOffset(DateTime.Today));
        Check(DesktopPresentation.AlertGroup(today, today) == "Today", "Current alerts must group under Today.");
        Check(DesktopPresentation.AlertGroup(today.AddDays(-1), today) == "Yesterday", "Yesterday must be calculated using the user's local date.");
        Check(DesktopPresentation.AlertGroup(today.AddDays(-4), today) is not ("Today" or "Yesterday"), "Older alerts must retain a date heading.");
        var inbox = new NotificationInbox(); int changes = 0; inbox.Changed += () => changes++;
        inbox.Clear(); inbox.Dismiss(Guid.NewGuid()); Check(changes == 0, "No-op inbox actions must not schedule paints and saves.");
        inbox.Publish("Device request failed", "Please refresh", NoticeKind.Warning); var id = inbox.Items[0].Id;
        inbox.MarkRead(); inbox.MarkRead(); inbox.Dismiss(id); inbox.Dismiss(id); inbox.Clear();
        Check(changes == 3, "Publish, first mark-read and actual dismissal must each notify once.");
        Console.WriteLine($"PASS: {count} UI reliability assertions; late replies/failures, panel reopening, bounded pinning, local dates, compact clock preference and coalesced inbox changes.");
    }
}
