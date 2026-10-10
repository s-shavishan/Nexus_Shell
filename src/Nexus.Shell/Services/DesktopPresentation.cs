using System.Globalization;

namespace Nexus.Shell.Services;

public static class DesktopPresentation
{
    public static string ClockFormat(bool compact, bool clock24Hour)
        => compact ? clock24Hour ? "HH:mm" : "h:mm tt" : clock24Hour ? "ddd d MMM  HH:mm" : "ddd d MMM  h:mm tt";
    public static string AlertGroup(DateTimeOffset created, DateTimeOffset now)
    {
        var day = created.ToLocalTime().Date;
        var today = now.ToLocalTime().Date;
        return day == today ? "Today" : day == today.AddDays(-1) ? "Yesterday" : created.ToLocalTime().ToString("dddd, d MMM", CultureInfo.CurrentCulture);
    }
}
