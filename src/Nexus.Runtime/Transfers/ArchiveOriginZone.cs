using System.Text;

namespace Nexus.Runtime.Transfers;

// Preserve Windows' existing origin zone when packaging downloaded files or
// extracting a downloaded ZIP. Never silently drop it on an unsupported target.
internal static class ArchiveOriginZone
{
    internal static int Read(string path)
    {
        if (!OperatingSystem.IsWindows()) return 0;
        try
        {
            using var input = new FileStream(path + ":Zone.Identifier", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (input.Length > 8192) throw new IOException("The source's Windows origin metadata is too large to preserve.");
            byte[] bytes = new byte[8193]; int count = 0;
            while (count < bytes.Length)
            { int read = input.Read(bytes, count, bytes.Length - count); if (read == 0) break; count += read; }
            if (count > 8192) throw new IOException("The source's Windows origin metadata grew beyond its limit.");
            using var reader = new StreamReader(new MemoryStream(bytes, 0, count, false), Encoding.UTF8, true);
            int maximum = 0; bool found = false;
            foreach (string line in reader.ReadToEnd().Split('\n'))
            {
                var parts = line.Trim().Split('=', 2);
                if (parts.Length == 2 && parts[0].Trim().Equals("ZoneId", StringComparison.OrdinalIgnoreCase))
                { found = true; maximum = Math.Max(maximum, int.TryParse(parts[1].Trim(), out int zone) && zone is >= 0 and <= 4 ? zone : 3); }
            }
            return found ? maximum : 3;
        }
        catch (FileNotFoundException) { return 0; }
        catch (DirectoryNotFoundException) { return 0; }
        catch (IOException error) when ((error.HResult & 0xFFFF) is 1 or 50 or 123) { return 0; } // A filesystem without named streams has no source zone.
    }
    internal static void Write(string path, int zone)
    {
        if (!OperatingSystem.IsWindows() || zone <= 0) return;
        FileTransferEngine.RejectLinks(path);
        try
        {
            using var output = new FileStream(path + ":Zone.Identifier", FileMode.CreateNew, FileAccess.Write, FileShare.None);
            byte[] bytes = Encoding.UTF8.GetBytes("[ZoneTransfer]\r\nZoneId=" + zone + "\r\n");
            output.Write(bytes); output.Flush(true);
        }
        catch (IOException error) { throw new IOException("The destination could not preserve this item's Windows origin zone. Choose a Windows volume with alternate-stream support.", error); }
    }
}
