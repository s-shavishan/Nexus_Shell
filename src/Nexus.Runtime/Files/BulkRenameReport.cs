// නමෝ බුද්ධාය 🤍
using System.Text;

namespace Nexus.Runtime.Files;

public static class BulkRenameReport
{
    public static void WriteCsv(BulkRenameReceipt receipt, TextWriter writer)
    {
        static string Cell(string text)
        {
            string trimmed = text.TrimStart();
            if (trimmed.Length > 0 && "=+-@".Contains(trimmed[0])) text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
        writer.WriteLine("Batch,CreatedAt,Cancelled,OriginalPath,TargetPath,ApplyStatus,Detail,UndoStatus,UndoDetail,LastUndoAt");
        foreach (var row in receipt.Rows)
            writer.WriteLine(string.Join(",", new[] { receipt.Id.ToString(), receipt.CreatedAt.ToString("O"), receipt.Cancelled.ToString(),
                row.Source, row.Target, row.State.ToString(), row.Detail, row.UndoState.ToString(), row.UndoDetail, receipt.LastUndoAt?.ToString("O") ?? "" }.Select(Cell)));
    }
    public static void ExportNew(BulkRenameReceipt receipt, string destination)
    {
        if (!Path.IsPathFullyQualified(destination) || !Path.GetExtension(destination).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a full .csv destination path.");
        destination = Path.GetFullPath(destination);
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".nexus-rename-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(true), leaveOpen: true)) { WriteCsv(receipt, writer); writer.Flush(); }
                stream.Flush(true);
            }
            File.Move(temporary, destination, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
