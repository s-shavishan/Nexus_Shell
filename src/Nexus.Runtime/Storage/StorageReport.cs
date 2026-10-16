// නමෝ බුද්ධාය 🤍
using System.Globalization;
using System.Text;

namespace Nexus.Runtime.Storage;

public static class StorageReport
{
    public static string Size(long bytes) => bytes >= 1L << 40 ? $"{bytes / (double)(1L << 40):0.##} TiB"
        : bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.##} GiB"
        : bytes >= 1L << 20 ? $"{bytes / (double)(1L << 20):0.##} MiB"
        : bytes >= 1024 ? $"{bytes / 1024d:0.##} KiB" : bytes + " bytes";

    public static string CsvCell(string value)
    {
        string trimmed = value.TrimStart();
        if (trimmed.Length > 0 && "=+-@".Contains(trimmed[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public static void WriteCsv(StorageScanResult result, TextWriter writer)
    {
        void Row(string kind, string path, long bytes, long files, bool partial, string detail)
            => writer.WriteLine(string.Join(",", new[] { kind, path, bytes.ToString(CultureInfo.InvariantCulture), files.ToString(CultureInfo.InvariantCulture), partial.ToString(), detail }.Select(CsvCell)));
        writer.WriteLine("Kind,Path,LogicalBytes,Files,Partial,Detail");
        Row("Summary", result.Root, result.Bytes, result.Files, result.State != StorageScanState.Complete || result.Skipped > 0,
            result.State + "; skipped=" + result.Skipped + "; observed=" + result.ObservedAt.ToString("O") + "; logical sizes, not allocated disk usage");
        foreach (var folder in result.Tree) Row("Folder", folder.Path, folder.Bytes, folder.Files, folder.Partial, "Subfolders=" + folder.Folders);
        foreach (var file in result.LargestFiles) Row("LargestFile", file.Path, file.Bytes, 1, false, file.ModifiedUtc.ToString("O"));
        foreach (var type in result.FileTypes) Row("FileType", type.Extension, type.Bytes, type.Files, false, "");
        foreach (var skip in result.SkippedPaths) Row("SkippedSample", skip.Path, 0, 0, true, skip.Reason);
    }

    public static void ExportNew(StorageScanResult result, string destination)
    {
        if (!Path.IsPathFullyQualified(destination) || !Path.GetExtension(destination).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a full path ending in .csv.");
        destination = Path.GetFullPath(destination);
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".nexus-storage-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(true), leaveOpen: true))
                { WriteCsv(result, writer); writer.Flush(); }
                stream.Flush(true);
            }
            File.Move(temporary, destination, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
