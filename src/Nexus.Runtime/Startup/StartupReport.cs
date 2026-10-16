// නමෝ බුද්ධාය 🤍
using System.Text;

namespace Nexus.Runtime.Startup;

public static class StartupReport
{
    public static void WriteCsv(StartupSnapshot snapshot, TextWriter writer)
    {
        static string Cell(string value)
        {
            string trimmed = value.TrimStart();
            if (trimmed.Length > 0 && "=+-@".Contains(trimmed[0])) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        writer.WriteLine("Name,Scope,Status,OriginalOrRegisteredCommand,CurrentCommand,ValueKind,Location,Detail,ObservedAt");
        foreach (var item in snapshot.Items)
            writer.WriteLine(string.Join(",", new[] { item.Entry.Name, StartupRules.Scope(item.Entry.Source), item.Status, item.Entry.Command,
                item.CurrentCommand, item.Entry.Kind.ToString(), item.Entry.Location, item.Detail, snapshot.ObservedAt.ToString("O") }.Select(Cell)));
        foreach (string notice in snapshot.Notices)
            writer.WriteLine(string.Join(",", new[] { "Inventory notice", "", "Partial inventory", "", "", "", "", notice, snapshot.ObservedAt.ToString("O") }.Select(Cell)));
    }
    public static void ExportNew(StartupSnapshot snapshot, string destination)
    {
        if (!Path.IsPathFullyQualified(destination) || !Path.GetExtension(destination).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a full .csv destination path.");
        destination = Path.GetFullPath(destination);
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".nexus-startup-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(true), leaveOpen: true)) { WriteCsv(snapshot, writer); writer.Flush(); }
                stream.Flush(true);
            }
            File.Move(temporary, destination, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
