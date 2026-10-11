namespace Nexus.Shell.Services;

// One reversible local edit per surface. Call mutations on a single worker;
// the UI disables competing edits while that worker is active.
public sealed class FileMutationHistory
{
    private sealed record Stamp(bool Directory, DateTime Created, DateTime Modified, long Bytes);
    private sealed record Edit(string Before, string After, Stamp Stamp, bool CreatedFolder);
    private Edit? _undo;
    public bool CanUndo => Volatile.Read(ref _undo) is not null;
    public string UndoLabel => Volatile.Read(ref _undo) is { } edit ? edit.CreatedFolder ? "Undo new folder" : "Undo rename" : "Nothing to undo";
    private static string LocalPath(string path)
    {
        if (!DesktopOrganization.ValidPlacePath(path) || path.Replace('/', '\\').StartsWith("\\\\", StringComparison.Ordinal))
            throw new ArgumentException("Use a local file or folder for this edit.");
        return Path.GetFullPath(path);
    }
    private static Stamp ReadStamp(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Use the original item rather than a linked folder for this edit.");
        bool directory = (attributes & FileAttributes.Directory) != 0;
        FileSystemInfo info = directory ? new DirectoryInfo(path) : new FileInfo(path);
        return new(directory, info.CreationTimeUtc, info.LastWriteTimeUtc, info is FileInfo file ? file.Length : 0);
    }
    private static void Vacant(string path)
    { if (File.Exists(path) || Directory.Exists(path)) throw new IOException("That name is already in use. Nothing was replaced."); }
    private void Remember(string before, string after, bool createdFolder)
    {
        // Read after the operation: some filesystems change creation/change
        // metadata during a rename. A metadata failure must not report a
        // completed move as failed or retain an unrelated older undo action.
        Edit? edit = null;
        try { edit = new(before, after, ReadStamp(after), createdFolder); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        Volatile.Write(ref _undo, edit);
    }
    public string Rename(string path, string name)
    {
        path = LocalPath(path); string parent = Path.GetDirectoryName(path) ?? throw new IOException("A drive cannot be renamed here.");
        string destination = FileCatalog.ChildPath(parent, name);
        if (string.Equals(path, destination, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose a different name. Case-only renaming is not supported here.");
        var stamp = ReadStamp(path); Vacant(destination);
        if (stamp.Directory) Directory.Move(path, destination); else File.Move(path, destination, overwrite: false);
        Remember(path, destination, false); return destination;
    }
    public string CreateFolder(string parent, string name)
    {
        parent = LocalPath(parent); string destination = FileCatalog.ChildPath(parent, name); Vacant(destination);
        if (!Directory.Exists(parent)) throw new DirectoryNotFoundException("The destination folder is unavailable.");
        // Move gives the final name an exclusive claim. CreateDirectory alone
        // could silently adopt a folder another process created after Vacant.
        string temporary = Path.Combine(parent, ".nexus-folder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            Directory.Move(temporary, destination);
            Remember("", destination, true); return destination;
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: false); }
    }
    public string Undo()
    {
        var edit = Volatile.Read(ref _undo) ?? throw new InvalidOperationException("There is no file edit to undo in this window.");
        var current = ReadStamp(edit.After);
        // Files edited/replaced since the operation are deliberately left alone.
        if (current.Directory != edit.Stamp.Directory || current.Created != edit.Stamp.Created
            || (!current.Directory || edit.CreatedFolder) && (current.Bytes != edit.Stamp.Bytes || current.Modified != edit.Stamp.Modified))
            throw new IOException("This item changed after the edit. Undo left it untouched.");
        if (edit.CreatedFolder) Directory.Delete(edit.After, recursive: false);
        else
        {
            Vacant(edit.Before);
            if (current.Directory) Directory.Move(edit.After, edit.Before); else File.Move(edit.After, edit.Before, overwrite: false);
        }
        Volatile.Write(ref _undo, null); return edit.CreatedFolder ? Path.GetDirectoryName(edit.After)! : edit.Before;
    }
}
