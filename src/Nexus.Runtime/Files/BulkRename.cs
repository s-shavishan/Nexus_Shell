// නමෝ බුද්ධාය 🤍
using System.Globalization;
using System.Text;

namespace Nexus.Runtime.Files;

public sealed record BulkRenameRules(string Prefix = "", string Suffix = "", string Find = "", string Replacement = "",
    bool IgnoreCase = false, bool Numbering = false, int Start = 1, int Digits = 3, string Separator = "_", bool PreserveExtension = true);
public sealed record RenameStamp(string Identity, string ParentIdentity, bool Directory, long Bytes,
    long Created, long Written, long Changed, uint Attributes);
public sealed record RenameMove(RenameStamp? UndoStamp, string Detail = "");

// Move must compare the expected identity/metadata on the same opened object it
// renames, claim a vacant sibling name without replacement, and never throw
// after a successful native rename. Missing post-move metadata disables Undo.
public interface IBulkRenameBackend
{
    RenameStamp Read(string path);
    void RequireVacant(string path);
    RenameMove Move(string source, string target, RenameStamp expected);
}

public enum RenamePreviewState { Ready, Unchanged, Blocked }
public enum RenameItemState { Renamed, Unchanged, Failed, NotAttempted, Cancelled }
public enum RenameUndoState { None, Available, Restored, Refused, Unavailable }
public sealed record RenamePreviewRow(int Number, string Source, string Target, RenameStamp? Stamp, RenamePreviewState State, string Detail);
public sealed record RenameReceiptRow(string Source, string Target, RenameItemState State, string Detail,
    RenameStamp? UndoStamp = null, RenameUndoState UndoState = RenameUndoState.None, string UndoDetail = "")
{
    public bool CanUndo => State == RenameItemState.Renamed && UndoStamp is not null && UndoState is RenameUndoState.Available or RenameUndoState.Refused;
}
public sealed class BulkRenamePlan
{
    internal BulkRenamePlan(BulkRenameRules rules, RenamePreviewRow[] rows)
    { Rules = rules; Rows = Array.AsReadOnly(rows); }
    public Guid Id { get; } = Guid.NewGuid();
    public BulkRenameRules Rules { get; }
    public IReadOnlyList<RenamePreviewRow> Rows { get; }
    public bool CanApply => Rows.Any(r => r.State == RenamePreviewState.Ready) && Rows.All(r => r.State != RenamePreviewState.Blocked);
}
public sealed record BulkRenameReceipt(Guid Id, DateTimeOffset CreatedAt, IReadOnlyList<RenameReceiptRow> Rows,
    bool Cancelled, DateTimeOffset? LastUndoAt = null)
{
    public int Renamed => Rows.Count(r => r.State == RenameItemState.Renamed);
    public int Restored => Rows.Count(r => r.UndoState == RenameUndoState.Restored);
    public bool CanUndo => Rows.Any(r => r.CanUndo);
}
public sealed record RenameProgress(string Phase, int Completed, int Total, string Path);

public static class BulkRenameNames
{
    public const int MaximumItems = 100;
    public static void ValidateRules(BulkRenameRules rules)
    {
        foreach (string text in new[] { rules.Prefix, rules.Suffix, rules.Find, rules.Replacement })
            if (text is null || text.Length > 160) throw new ArgumentException("Each rename rule is limited to 160 characters.");
        if (rules.Separator is null || rules.Separator.Length > 16 || rules.Digits is < 1 or > 9 || rules.Start is < 0 or > 999999900)
            throw new ArgumentException("Use a start from 0 to 999999900, 1–9 number digits and a separator up to 16 characters.");
        if (rules.Find.Length == 0 && rules.Replacement.Length > 0)
            throw new ArgumentException("Enter text to find before entering a replacement.");
    }
    public static string Name(string original, bool directory, BulkRenameRules rules, int index)
    {
        string extension = "";
        int dot = original.LastIndexOf('.');
        if (!directory && rules.PreserveExtension && dot > 0)
        { extension = original[dot..]; original = original[..dot]; }
        if (rules.Find.Length > 0)
            original = original.Replace(rules.Find, rules.Replacement, rules.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        return rules.Prefix + original + rules.Suffix + (rules.Numbering
            ? rules.Separator + checked(rules.Start + index).ToString("D" + rules.Digits, CultureInfo.InvariantCulture) : "") + extension;
    }
    public static void ValidateLeaf(string name)
    {
        string stem = name.Split('.')[0].TrimEnd(' ');
        bool device = new[] { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$" }.Contains(stem, StringComparer.OrdinalIgnoreCase)
            || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && "123456789¹²³".Contains(stem[3]);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name is "." or ".." || name.EndsWith('.') || name.EndsWith(' ')
            || name.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*']) >= 0 || name.Any(char.IsControl) || device)
            throw new ArgumentException("Use a name of 1–255 characters without Windows reserved names, separators, control characters or trailing dots/spaces.");
        try { _ = new UnicodeEncoding(false, false, true).GetByteCount(name); }
        catch (EncoderFallbackException) { throw new ArgumentException("The name contains an incomplete Unicode character."); }
    }
}

// Session receipts are immutable and retained until explicitly cleared. Nothing
// is replayed after restart. The UI offers export before discarding its receipt.
public sealed class BulkRenameSession(IBulkRenameBackend backend)
{
    private BulkRenamePlan? _plan;
    private bool _consumed;
    private int _working;
    public BulkRenameReceipt? Receipt { get; private set; }
    public BulkRenamePlan? Plan => _plan;
    private IDisposable Enter()
    {
        if (Interlocked.CompareExchange(ref _working, 1, 0) != 0) throw new InvalidOperationException("A rename operation is already running.");
        return new Lease(() => Volatile.Write(ref _working, 0));
    }
    private sealed class Lease(Action release) : IDisposable { public void Dispose() => release(); }
    public void ClearReceipt()
    {
        using var lease = Enter();
        Receipt = null; _plan = null; _consumed = false;
    }
    public BulkRenamePlan Preview(IReadOnlyList<string> sources, BulkRenameRules rules, CancellationToken cancellation = default)
    {
        using var lease = Enter();
        _plan = null;
        BulkRenameNames.ValidateRules(rules);
        if (sources.Count is < 1 or > BulkRenameNames.MaximumItems)
            throw new ArgumentException("Select 1–100 sibling files or folders.");
        if (sources.Any(p => !Path.IsPathFullyQualified(p))) throw new ArgumentException("Use full source paths.");
        var paths = sources.Select(p => Path.TrimEndingDirectorySeparator(Path.GetFullPath(p))).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ThenBy(p => p, StringComparer.Ordinal).ToArray();
        if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length)
            throw new ArgumentException("Select each item only once.");
        string parent = Path.GetDirectoryName(paths[0]) ?? throw new ArgumentException("A drive cannot be renamed here.");
        if (paths.Any(p => !string.Equals(Path.GetDirectoryName(p), parent, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Select items from one folder only.");
        var rows = new List<RenamePreviewRow>();
        for (int i = 0; i < paths.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            string source = paths[i], target = source;
            RenameStamp? stamp = null;
            try
            {
                stamp = backend.Read(source);
                string original = Path.GetFileName(source), name = BulkRenameNames.Name(original, stamp.Directory, rules, i);
                target = Path.Combine(parent, name);
                BulkRenameNames.ValidateLeaf(name);
                if (target.Length > 32700) throw new ArgumentException("The resulting full path is too long.");
                if (name == original) rows.Add(new(i + 1, source, target, stamp, RenamePreviewState.Unchanged, "No change"));
                else
                {
                    if (name.Equals(original, StringComparison.OrdinalIgnoreCase)) throw new IOException("Case-only renames require a distinct name in a separate batch.");
                    backend.RequireVacant(target);
                    rows.Add(new(i + 1, source, target, stamp, RenamePreviewState.Ready, "Ready"));
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            { rows.Add(new(i + 1, source, target, stamp, RenamePreviewState.Blocked, error.Message)); }
        }
        var duplicates = rows.GroupBy(r => r.Target, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).SelectMany(g => g).Select(r => r.Number).ToHashSet();
        var identities = rows.Where(r => r.Stamp is not null).GroupBy(r => r.Stamp!.Identity, StringComparer.Ordinal).Where(g => g.Count() > 1).SelectMany(g => g).Select(r => r.Number).ToHashSet();
        string? parentIdentity = rows.FirstOrDefault(r => r.Stamp is not null)?.Stamp?.ParentIdentity;
        _plan = new(rules, rows.Select(r => duplicates.Contains(r.Number) ? r with { State = RenamePreviewState.Blocked, Detail = "Two selected items produce the same target name." }
            : identities.Contains(r.Number) ? r with { State = RenamePreviewState.Blocked, Detail = "Selected names share a file identity. Rename hard links separately." }
            : r.Stamp is not null && r.Stamp.ParentIdentity != parentIdentity ? r with { State = RenamePreviewState.Blocked, Detail = "The containing folder changed during preview." } : r).ToArray());
        _consumed = false;
        return _plan;
    }
    public BulkRenameReceipt Apply(BulkRenamePlan plan, CancellationToken cancellation = default, Action<RenameProgress>? progress = null)
    {
        using var lease = Enter();
        if (!ReferenceEquals(plan, _plan) || _consumed || !plan.CanApply) throw new InvalidOperationException("Build a fresh valid preview before applying.");
        if (Receipt is not null) throw new InvalidOperationException("Export or review and explicitly clear the previous receipt before a new batch.");
        _consumed = true;
        var createdAt = DateTimeOffset.UtcNow;
        var errors = new Dictionary<int, string>();
        foreach (var row in plan.Rows.Where(r => r.State == RenamePreviewState.Ready))
        {
            if (cancellation.IsCancellationRequested) break;
            Notify(progress, new("Checking", row.Number - 1, plan.Rows.Count, row.Source));
            try
            {
                if (backend.Read(row.Source) != row.Stamp) throw new IOException("The item or containing folder changed after preview. Build a new preview.");
                backend.RequireVacant(row.Target);
            }
            catch (Exception error) { errors[row.Number] = error.Message; }
        }
        var rows = new List<RenameReceiptRow>();
        foreach (var row in plan.Rows)
        {
            RenameReceiptRow result;
            if (row.State == RenamePreviewState.Unchanged) result = new(row.Source, row.Target, RenameItemState.Unchanged, "No change");
            else if (errors.Count > 0) result = new(row.Source, row.Target, errors.ContainsKey(row.Number) ? RenameItemState.Failed : RenameItemState.NotAttempted,
                errors.GetValueOrDefault(row.Number, "Preflight refused this batch. No items were renamed."));
            else if (cancellation.IsCancellationRequested) result = new(row.Source, row.Target, RenameItemState.Cancelled, "Cancelled before this item; no change.");
            else
            {
                try
                {
                    var moved = backend.Move(row.Source, row.Target, row.Stamp!);
                    result = new(row.Source, row.Target, RenameItemState.Renamed, moved.Detail.Length == 0 ? "Renamed" : moved.Detail,
                        moved.UndoStamp, moved.UndoStamp is null ? RenameUndoState.Unavailable : RenameUndoState.Available);
                }
                catch (Exception error) { result = new(row.Source, row.Target, RenameItemState.Failed, error.Message); }
            }
            rows.Add(result);
            Receipt = new(plan.Id, createdAt, Array.AsReadOnly(rows.ToArray()), rows.Any(r => r.State == RenameItemState.Cancelled));
            Notify(progress, new("Renaming", rows.Count, plan.Rows.Count, row.Source));
        }
        return Receipt!;
    }
    public BulkRenameReceipt Undo(CancellationToken cancellation = default, Action<RenameProgress>? progress = null)
    {
        using var lease = Enter();
        var receipt = Receipt ?? throw new InvalidOperationException("There is no batch receipt to undo.");
        var rows = receipt.Rows.ToArray();
        for (int i = rows.Length - 1; i >= 0; i--)
        {
            if (cancellation.IsCancellationRequested) break;
            var row = rows[i];
            if (!row.CanUndo) continue;
            try
            {
                backend.Move(row.Target, row.Source, row.UndoStamp!);
                rows[i] = row with { UndoState = RenameUndoState.Restored, UndoDetail = "Original name restored" };
            }
            catch (Exception error) { rows[i] = row with { UndoState = RenameUndoState.Refused, UndoDetail = error.Message }; }
            Receipt = receipt with { Rows = Array.AsReadOnly(rows.ToArray()), LastUndoAt = DateTimeOffset.UtcNow };
            Notify(progress, new("Undo", rows.Length - i, rows.Length, row.Target));
        }
        return Receipt!;
    }
    private static void Notify(Action<RenameProgress>? callback, RenameProgress progress)
    {
        // Rendering/progress failures must never change a completed mutation's receipt.
        try { callback?.Invoke(progress); } catch (Exception) { }
    }
}

// One physical background operation in the Files worker, even if its view closes
// or its cancellation is requested while a filesystem call is still blocked.
public static class BulkRenameWork
{
    private static int _busy;
    public static async Task<T> RunAsync<T>(Func<T> action)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            throw new InvalidOperationException("Another bulk rename operation is still finishing. Try again after it returns.");
        try { return await Task.Run(action).ConfigureAwait(false); }
        finally { Volatile.Write(ref _busy, 0); }
    }
}
