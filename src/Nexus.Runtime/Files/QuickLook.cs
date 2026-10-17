// නමෝ බුද්ධාය 🤍
using System.Text;

namespace Nexus.Runtime.Files;

public enum QuickLookKind { Unsupported, Text, Image, Pdf, Audio, Video }
public enum QuickLookEncoding { Auto, Utf8, Utf16LittleEndian, Utf16BigEndian, Windows1252 }
public sealed record QuickLookFormat(QuickLookKind Kind, string ContentType);
public sealed record QuickLookText(string Text, string Encoding, bool Truncated, int BytesRead);
public sealed record QuickLookSize(uint Width, uint Height);

public static class QuickLookPolicy
{
    public const int MaximumItems = 1000, MaximumTextBytes = 256 * 1024, MaximumTextCharacters = 65536, MaximumTextLines = 2000;
    public const long MaximumImageBytes = 32 * 1024 * 1024, MaximumPdfBytes = 64 * 1024 * 1024, MaximumMediaBytes = 4L * 1024 * 1024 * 1024;
    public const ulong MaximumImagePixels = 80_000_000;
    public const uint MaximumRenderEdge = 2400;
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".txt", ".md", ".markdown", ".log", ".csv", ".tsv", ".json", ".xml", ".xaml", ".svg", ".html", ".htm", ".css", ".js", ".jsx", ".ts", ".tsx", ".cs", ".csproj", ".sln", ".vb", ".py", ".rb", ".go", ".rs", ".java", ".c", ".cpp", ".h", ".hpp", ".sql", ".yaml", ".yml", ".toml", ".ini", ".conf", ".config", ".ps1", ".bat", ".cmd", ".sh", ".gitignore", ".editorconfig", ".env" };
    public static QuickLookFormat Format(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (TextExtensions.Contains(extension) || new[] { ".gitignore", ".editorconfig", ".env", "README", "LICENSE", "Dockerfile" }.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            return new(QuickLookKind.Text, "text/plain");
        return extension switch
        {
            ".png" or ".jpg" or ".jpeg" or ".jpe" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".webp" or ".heic" or ".avif" => new(QuickLookKind.Image, ""),
            ".pdf" => new(QuickLookKind.Pdf, "application/pdf"),
            ".mp3" => new(QuickLookKind.Audio, "audio/mpeg"), ".wav" => new(QuickLookKind.Audio, "audio/wav"),
            ".m4a" => new(QuickLookKind.Audio, "audio/mp4"), ".aac" => new(QuickLookKind.Audio, "audio/aac"),
            ".flac" => new(QuickLookKind.Audio, "audio/flac"), ".ogg" or ".opus" => new(QuickLookKind.Audio, "audio/ogg"),
            ".wma" => new(QuickLookKind.Audio, "audio/x-ms-wma"),
            ".mp4" or ".m4v" => new(QuickLookKind.Video, "video/mp4"), ".mov" => new(QuickLookKind.Video, "video/quicktime"),
            ".avi" => new(QuickLookKind.Video, "video/x-msvideo"), ".wmv" => new(QuickLookKind.Video, "video/x-ms-wmv"),
            ".mkv" => new(QuickLookKind.Video, "video/x-matroska"), ".webm" => new(QuickLookKind.Video, "video/webm"),
            _ => new(QuickLookKind.Unsupported, "")
        };
    }
    public static void ValidateLength(QuickLookKind kind, long bytes)
    {
        if (bytes < 0) throw new IOException("File length is unavailable.");
        long maximum = kind switch { QuickLookKind.Image => MaximumImageBytes, QuickLookKind.Pdf => MaximumPdfBytes,
            QuickLookKind.Audio or QuickLookKind.Video => MaximumMediaBytes, _ => long.MaxValue };
        if (bytes > maximum) throw new IOException("This file exceeds the built-in preview limit. Open it in its usual application.");
        if (bytes == 0 && kind is not (QuickLookKind.Text or QuickLookKind.Unsupported)) throw new IOException("This file is empty.");
    }
    public static QuickLookSize RenderSize(double width, double height, double zoom = 1)
    {
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0 || !double.IsFinite(zoom) || zoom is < 0.1 or > 4)
            throw new IOException("The document returned invalid preview dimensions.");
        double scale = Math.Min(zoom, MaximumRenderEdge / Math.Max(width, height));
        return new((uint)Math.Clamp(Math.Round(width * scale), 1, MaximumRenderEdge), (uint)Math.Clamp(Math.Round(height * scale), 1, MaximumRenderEdge));
    }
    public static void ValidateImage(uint width, uint height)
    {
        if (width == 0 || height == 0 || (ulong)width * height > MaximumImagePixels)
            throw new IOException("The image dimensions exceed the built-in preview limit.");
    }
    public static async Task<QuickLookText> ReadTextAsync(Stream stream, QuickLookEncoding choice, CancellationToken cancellation = default)
    {
        if (!stream.CanRead || !stream.CanSeek) throw new IOException("Text preview requires a readable, seekable local file.");
        stream.Position = 0;
        byte[] buffer = new byte[MaximumTextBytes];
        int count = 0;
        while (count < buffer.Length)
        {
            cancellation.ThrowIfCancellationRequested();
            int read = await stream.ReadAsync(buffer.AsMemory(count), cancellation).ConfigureAwait(false);
            if (read == 0) break;
            count += read;
        }
        cancellation.ThrowIfCancellationRequested();
        return Decode(buffer.AsSpan(0, count), choice, stream.Length > count);
    }
    public static QuickLookText Decode(ReadOnlySpan<byte> bytes, QuickLookEncoding choice, bool truncated)
    {
        if (bytes.Length > MaximumTextBytes) throw new ArgumentException("Text input exceeds the preview byte limit.");
        if (!Enum.IsDefined(choice)) throw new ArgumentException("Choose a supported text encoding.");
        Encoding encoding; int skip = 0;
        if (choice == QuickLookEncoding.Auto && bytes.StartsWith(new byte[] { 0xFF, 0xFE, 0, 0 })) { encoding = new UTF32Encoding(false, true, true); skip = 4; }
        else if (choice == QuickLookEncoding.Auto && bytes.StartsWith(new byte[] { 0, 0, 0xFE, 0xFF })) { encoding = new UTF32Encoding(true, true, true); skip = 4; }
        else if (choice == QuickLookEncoding.Utf16LittleEndian || choice == QuickLookEncoding.Auto && bytes.StartsWith(new byte[] { 0xFF, 0xFE }))
        { encoding = new UnicodeEncoding(false, true, true); if (bytes.StartsWith(new byte[] { 0xFF, 0xFE })) skip = 2; }
        else if (choice == QuickLookEncoding.Utf16BigEndian || choice == QuickLookEncoding.Auto && bytes.StartsWith(new byte[] { 0xFE, 0xFF }))
        { encoding = new UnicodeEncoding(true, true, true); if (bytes.StartsWith(new byte[] { 0xFE, 0xFF })) skip = 2; }
        else if (choice == QuickLookEncoding.Windows1252)
        { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); encoding = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback); }
        else { encoding = new UTF8Encoding(false, true); if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) skip = 3; }
        char[] characters = new char[encoding.GetMaxCharCount(bytes.Length)];
        int used;
        try { encoding.GetDecoder().Convert(bytes[skip..], characters, !truncated, out _, out used, out _); }
        catch (DecoderFallbackException) { throw new IOException("This text does not match the selected encoding. Try another encoding or open it in your editor."); }
        string text = new string(characters, 0, used).Replace("\r\n", "\n").Replace('\r', '\n');
        if (text.Contains('\0') || text.Count(c => char.IsControl(c) && c is not ('\n' or '\t' or '\f')) > Math.Max(2, text.Length / 100))
            throw new IOException("This looks like binary content; it is not displayed as text.");
        int limit = Math.Min(text.Length, MaximumTextCharacters), lines = 1;
        for (int i = 0; i < limit; i++) if (text[i] == '\n' && ++lines > MaximumTextLines) { limit = i; break; }
        if (limit > 0 && char.IsHighSurrogate(text[limit - 1])) limit--;
        return new(text[..limit], encoding.WebName, truncated || limit < text.Length, bytes.Length);
    }
}

public sealed class QuickLookSession
{
    public IReadOnlyList<string> Paths { get; }
    public int Index { get; private set; }
    public string Path => Paths[Index];
    public bool CanPrevious => Index > 0;
    public bool CanNext => Index + 1 < Paths.Count;
    public QuickLookSession(IReadOnlyList<string> paths, string selected)
    {
        if (paths.Count is < 1 or > QuickLookPolicy.MaximumItems || paths.Any(p => !System.IO.Path.IsPathFullyQualified(p)))
            throw new ArgumentException("Preview 1–1000 files with full paths.");
        var copy = paths.Select(System.IO.Path.GetFullPath).ToArray();
        if (copy.Distinct(StringComparer.OrdinalIgnoreCase).Count() != copy.Length) throw new ArgumentException("Preview each file once.");
        Index = Array.FindIndex(copy, p => p.Equals(System.IO.Path.GetFullPath(selected), StringComparison.OrdinalIgnoreCase));
        if (Index < 0) throw new ArgumentException("The selected file is outside the captured preview list.");
        Paths = Array.AsReadOnly(copy);
    }
    public void Move(int direction)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        Index = Math.Clamp(Index + direction, 0, Paths.Count - 1);
    }
}

public static class QuickLookWork
{
    private static int _busy;
    public static IDisposable Enter()
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            throw new InvalidOperationException("A previous preview is still finishing. Close it or wait for its read to return, then retry.");
        return new Lease();
    }
    private sealed class Lease : IDisposable
    {
        private int _released;
        public void Dispose() { if (Interlocked.Exchange(ref _released, 1) == 0) Volatile.Write(ref _busy, 0); }
    }
}
