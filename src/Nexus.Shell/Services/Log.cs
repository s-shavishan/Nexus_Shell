using System.Collections;
using System.Text;

namespace Nexus.Shell.Services;

public static class Log
{
    private static readonly object Gate = new();
    public static void Write(string message, Exception? error = null)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(StateStore.DirectoryPath);
                var path = Path.Combine(StateStore.DirectoryPath, "nexus.log");
                if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024) File.Move(path, path + ".previous", true);
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Describe(error)}{Environment.NewLine}");
            }
            catch { /* Logging must not prevent recovery or exit. */ }
        }
    }

    private static string Describe(Exception? error)
    {
        if (error is null) return "";
        var details = new StringBuilder().AppendLine().Append(error);
        // CsWinRT can attach the native restricted description to Exception.Data.
        // Keep only small textual values; do not stringify native COM objects.
        for (int depth = 0; error is not null && depth < 8; depth++, error = error.InnerException)
        {
            details.AppendLine().Append($"HRESULT[{depth}]: 0x{error.HResult:X8}");
            try
            {
                int count = 0;
                foreach (DictionaryEntry entry in error.Data)
                {
                    if (entry.Key is not string key || entry.Value is not string value) continue;
                    if (++count > 12) break;
                    details.AppendLine().Append(key[..Math.Min(key.Length, 120)]).Append(": ")
                        .Append(value[..Math.Min(value.Length, 4096)]);
                }
            }
            catch { /* Preserve the main exception if supplementary data is unavailable. */ }
        }
        return details.ToString();
    }
}
