using System.Text.Json;

namespace Nexus.Shell.Services;

public readonly record struct DesktopRectangle(int Left, int Top, int Right, int Bottom)
{
    public bool Contains(DesktopRectangle value) => Left < Right && Top < Bottom
        && value.Left < value.Right && value.Top < value.Bottom
        && value.Left >= Left && value.Top >= Top && value.Right <= Right && value.Bottom <= Bottom;
}
public sealed record DesktopSessionSnapshot(int Format, int SessionId, string HostPath,
    DesktopRectangle Monitor, DesktopRectangle Work, IReadOnlyList<WindowsDesktopSurface> Surfaces, MinimizedDesktopMetrics? MinimizedMetrics = null);

// Unlike the sign-in backup, this record never requires a registry write.
public sealed class DesktopSessionRecord(string path)
{
    public static string PathFor(int sessionId) => Path.Combine(Path.GetDirectoryName(DesktopShellRegistration.DefaultBackupPath)!, "desktop-session-" + sessionId + ".json");
    private static void Validate(DesktopSessionSnapshot snapshot)
    {
        if (snapshot.Format != 1 || snapshot.SessionId < 0 || string.IsNullOrWhiteSpace(snapshot.HostPath)
            || !Path.IsPathFullyQualified(snapshot.HostPath) || Path.GetFileName(snapshot.HostPath) != "Nexus.DesktopHost.exe"
            || !snapshot.Monitor.Contains(snapshot.Work) || snapshot.MinimizedMetrics is { } metrics && !MinimizedWindowPolicy.IsValid(metrics) || snapshot.Surfaces is null || snapshot.Surfaces.Count > 256
            || snapshot.Surfaces.Any(s => s.Handle == 0 || s.ProcessId <= 0 || s.ClassName is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")))
            throw new InvalidDataException("The saved desktop session is invalid.");
    }
    public DesktopSessionSnapshot? Read(int sessionId)
    {
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 65536) throw new InvalidDataException("The saved desktop session is too large.");
        var saved = JsonSerializer.Deserialize<DesktopSessionSnapshot>(File.ReadAllText(path))
            ?? throw new InvalidDataException("The saved desktop session is empty.");
        Validate(saved);
        if (saved.SessionId != sessionId) throw new InvalidDataException("The saved desktop belongs to another Windows session.");
        return saved;
    }
    public void Save(DesktopSessionSnapshot snapshot)
    {
        Validate(snapshot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        try
        {
            byte[] data = JsonSerializer.SerializeToUtf8Bytes(snapshot);
            if (data.Length > 65536) throw new InvalidDataException("The saved desktop session is too large.");
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(data); stream.Flush(true); }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Delete() { if (File.Exists(path)) File.Delete(path); }
}
