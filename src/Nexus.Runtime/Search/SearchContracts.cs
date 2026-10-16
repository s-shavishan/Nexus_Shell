// නමෝ බුද්ධාය 🤍
using System.Text.RegularExpressions;

namespace Nexus.Runtime;

public enum SearchCategory { All, Files, Folders, Documents, Images, Audio, Video, Archives, Code, Commands, Windows }
public enum SearchScope { MyFiles, IndexedDrives }
public enum SearchStatus { Ready, Unavailable, Indexing, Busy, TimedOut, Unsupported, Failed }
public sealed record SearchRequest(string Query, SearchCategory Category = SearchCategory.All, SearchScope Scope = SearchScope.MyFiles, int Offset = 0);
public sealed record SearchHit(string Path, string Name, bool IsFolder);
public sealed record SearchReply(SearchStatus Status, SearchHit[] Items, uint Total = 0, int Offset = 0, string EngineVersion = "")
{
    public static SearchReply Empty(SearchStatus status) => new(status, []);
}
public sealed record SearchPlan(SearchRequest Request, string Expression, string Root);
public interface IFileSearchBackend
{
    Task<SearchReply> QueryAsync(SearchPlan plan, CancellationToken cancellation);
}

// Search by literal name tokens. Engine commands, content scans and wildcard
// expressions typed by a user cannot expand the cost or scope of a query.
public static class SearchPolicy
{
    public const int PageSize = 50, MaximumQueryLength = 256, MaximumOffset = 10000, MaximumPathLength = 4096;
    public static SearchPlan Create(SearchRequest request, string profile)
    {
        if (request.Query is null || request.Query.Length > MaximumQueryLength || request.Query.Any(char.IsControl)
            || !Enum.IsDefined(request.Category) || request.Category > SearchCategory.Code || !Enum.IsDefined(request.Scope)
            || request.Offset < 0 || request.Offset > MaximumOffset || request.Offset % PageSize != 0)
            throw new RuntimeFailure("search-input", "Search needs a name of up to 256 characters and a valid category and page.");
        request = request with { Query = request.Query.Trim() };
        string root = "";
        if (request.Scope == SearchScope.MyFiles)
        {
            root = profile.TrimEnd('\\') + "\\";
            if (!IsLocalPath(root)) throw new RuntimeFailure("search-scope", "Your Windows profile folder is unavailable. Choose indexed drives to search other local files.");
        }
        string category = request.Category switch
        {
            SearchCategory.Files => "file:", SearchCategory.Folders => "folder:",
            SearchCategory.Documents => "file: ext:pdf;txt;md;rtf;doc;docx;xls;xlsx;csv;ppt;pptx;odt;ods;odp;epub",
            SearchCategory.Images => "file: ext:jpg;jpeg;png;gif;webp;bmp;tif;tiff;svg;heic;avif;ico",
            SearchCategory.Audio => "file: ext:mp3;wav;flac;aac;ogg;m4a;opus;wma;aiff",
            SearchCategory.Video => "file: ext:mp4;mkv;mov;avi;webm;wmv;m4v;mpeg;mpg",
            SearchCategory.Archives => "file: ext:zip;7z;rar;tar;gz;bz2;xz;iso",
            SearchCategory.Code => "file: ext:cs;js;ts;tsx;jsx;py;html;css;json;xml;yml;yaml;cpp;c;h;rs;go;java;ps1;sql;sh",
            _ => ""
        };
        string scope = root.Length == 0 ? "path:regex:\"^[A-Za-z]:\\\\\"" : "path:regex:\"^" + Literal(root) + "\"";
        string tokens = string.Join(" ", request.Query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(word => "nopath:regex:\"" + Literal(word) + "\""));
        return new(request, scope + " " + category + " " + tokens, root);
    }
    private static string Literal(string text) => Regex.Escape(text).Replace("\"", "\\x22", StringComparison.Ordinal);
    public static bool IsLocalPath(string? path)
    {
        if (path is null || path.Length is < 3 or > MaximumPathLength || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\'
            || path.Any(c => char.IsControl(c) || c is '/' or '"' or '<' or '>' or '|' or '*' or '?') || path[2..].Contains(':')) return false;
        var parts = path[3..].TrimEnd('\\').Split('\\');
        return path.Length == 3 || parts.All(p => p.Length > 0 && p is not "." and not ".." && !p.EndsWith(' ') && !p.EndsWith('.') && !IsDevice(p));
    }
    private static bool IsDevice(string part)
    {
        string stem = part.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$"
            || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && "123456789¹²³".Contains(stem[3]));
    }
    public static bool InScope(string path, string root) => IsLocalPath(path) && (root.Length == 0 || path.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    public static string Name(string path) => path.TrimEnd('\\').Split('\\')[^1];
    public static string Parent(string path)
    { int last = path.TrimEnd('\\').LastIndexOf('\\'); return last <= 2 ? path[..3] : path[..last]; }
}
