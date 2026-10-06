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
                File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{(error is null ? "" : Environment.NewLine + error)}{Environment.NewLine}");
            }
            catch { /* Logging must not prevent recovery or exit. */ }
        }
    }
}
