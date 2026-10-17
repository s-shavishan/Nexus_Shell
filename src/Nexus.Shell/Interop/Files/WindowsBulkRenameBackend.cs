// නමෝ බුද්ධාය 🤍
using Microsoft.Win32.SafeHandles;
using Nexus.Runtime.Files;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Nexus.Shell.Interop;

// Mutations operate on a verified open source and an anchored sibling directory.
// No replacement flag, fallback path-based move, link traversal or elevation.
internal sealed class WindowsBulkRenameBackend : IBulkRenameBackend
{
    private const uint ReadAttributes = 0x80, DeleteAccess = 0x10000;
    private const uint OpenFlags = 0x02000000 | 0x00200000 | 0x00100000; // backup semantics, open reparse point, no recall
    private const uint UnavailableAttributes = 0x400 | 0x1000 | 0x40000 | 0x400000; // reparse/offline/recall

    public RenameStamp Read(string path)
    {
        path = Local(path);
        using var parent = Parent(path);
        using var source = Open(path, ReadAttributes, 7);
        return Stamp(source, path, Identity(parent));
    }
    public void RequireVacant(string path)
    {
        path = Local(path);
        BulkRenameNames.ValidateLeaf(Path.GetFileName(path));
        uint attributes = GetFileAttributesW(Extended(path));
        if (attributes != uint.MaxValue) throw new IOException("That target name is already in use. Nothing will be replaced.");
        int error = Marshal.GetLastWin32Error();
        if (error != 2) throw Failure("Cannot verify the target name", error);
    }
    public RenameMove Move(string source, string target, RenameStamp expected)
    {
        source = Local(source); target = Local(target);
        if (!string.Equals(Path.GetDirectoryName(source), Path.GetDirectoryName(target), StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Bulk rename requires a different sibling name.");
        string name = Path.GetFileName(target);
        BulkRenameNames.ValidateLeaf(name);
        using var parent = Parent(source);
        string parentIdentity = Identity(parent);
        // Omitting WRITE and DELETE sharing refuses existing writers/mappings and
        // keeps this opened identity from being retargeted until the call ends.
        using var file = Open(source, ReadAttributes | DeleteAccess, 1);
        if (Stamp(file, source, parentIdentity) != expected)
            throw new IOException("This item or its folder changed since preview/rename. It was left untouched.");
        RequireVacant(target);
        byte[] bytes = Encoding.Unicode.GetBytes(name);
        int offset = Marshal.OffsetOf<RenameInformation>(nameof(RenameInformation.FirstCharacter)).ToInt32();
        int size = checked(offset + bytes.Length + 2);
        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.Copy(new byte[size], 0, buffer, size);
            Marshal.WriteIntPtr(buffer, Marshal.OffsetOf<RenameInformation>(nameof(RenameInformation.Parent)).ToInt32(), parent.DangerousGetHandle());
            Marshal.WriteInt32(buffer, Marshal.OffsetOf<RenameInformation>(nameof(RenameInformation.NameBytes)).ToInt32(), bytes.Length);
            Marshal.Copy(bytes, 0, IntPtr.Add(buffer, offset), bytes.Length);
            // Flags/ReplaceIfExists stays zero. A racing destination fails in
            // the kernel; no pre-check/write race can replace an existing item.
            if (!SetFileInformationByHandle(file, 3, buffer, (uint)size)) throw Failure("Rename refused", Marshal.GetLastWin32Error());
            try { return new(Stamp(file, target, parentIdentity)); }
            catch (Exception error) { return new(null, "Renamed, but Undo metadata could not be verified: " + error.Message); }
        }
        finally { Marshal.FreeHGlobal(buffer); GC.KeepAlive(parent); }
    }
    private static SafeFileHandle Parent(string path)
    {
        string parentPath = Path.GetDirectoryName(path) ?? throw new IOException("A drive cannot be renamed here.");
        var handle = Open(parentPath, ReadAttributes, 3); // keep the containing directory from being renamed
        try
        {
            var info = Basic(handle);
            if ((info.Attributes & (UnavailableAttributes | 0x10)) != 0x10)
                throw new IOException("Use an available original folder, not a linked or cloud-recall location.");
            VerifyPath(handle, parentPath);
            if (!GetCaseInformation(handle, 23, out var sensitive, 4))
                throw Failure("This filesystem cannot verify case-insensitive rename safety", Marshal.GetLastWin32Error());
            if ((sensitive.Flags & 1) != 0) throw new IOException("Bulk rename is unavailable in case-sensitive folders. Use a normal Windows folder.");
            return handle;
        }
        catch { handle.Dispose(); throw; }
    }
    private static RenameStamp Stamp(SafeFileHandle handle, string path, string parentIdentity)
    {
        var basic = Basic(handle);
        if ((basic.Attributes & UnavailableAttributes) != 0)
            throw new IOException("Linked, offline and cloud-recall items are not renamed here.");
        VerifyPath(handle, path);
        if (!GetStandardInformation(handle, 1, out var standard, (uint)Marshal.SizeOf<StandardInformation>()))
            throw Failure("Cannot verify file metadata", Marshal.GetLastWin32Error());
        if (standard.DeletePending != 0) throw new IOException("This item is pending deletion.");
        return new(Identity(handle), parentIdentity, (basic.Attributes & 0x10) != 0, standard.Bytes,
            basic.Created, basic.Written, basic.Changed, basic.Attributes);
    }
    private static BasicInformation Basic(SafeFileHandle handle)
    {
        if (!GetBasicInformation(handle, 0, out var basic, (uint)Marshal.SizeOf<BasicInformation>()))
            throw Failure("Cannot read item metadata", Marshal.GetLastWin32Error());
        return basic;
    }
    private static string Identity(SafeFileHandle handle)
    {
        if (!GetIdentityInformation(handle, 18, out var id, (uint)Marshal.SizeOf<IdentityInformation>()))
            throw Failure("This filesystem cannot verify stable file identity", Marshal.GetLastWin32Error());
        if ((id.Low | id.High) == 0) throw new IOException("The filesystem returned an unavailable file identity.");
        return $"{id.Volume:X16}:{id.High:X16}{id.Low:X16}";
    }
    private static void VerifyPath(SafeFileHandle handle, string expected)
    {
        var text = new StringBuilder(32768);
        uint length = GetFinalPathNameByHandleW(handle, text, (uint)text.Capacity, 0);
        if (length == 0 || length >= text.Capacity) throw Failure("Cannot verify the opened path", Marshal.GetLastWin32Error());
        if (!string.Equals(text.ToString().TrimEnd('\\'), Extended(expected).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            throw new IOException("The path resolves through an alias/link or changed location. Open its original folder and try again.");
    }
    private static string Local(string path)
    {
        if (!OperatingSystem.IsWindows() || !Path.IsPathFullyQualified(path) || path.Length < 3 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\')
            throw new ArgumentException("Use a full local Windows drive path for bulk rename.");
        string full = Path.GetFullPath(path);
        if (full.Length > 32700 || full[3..].Contains(':')) throw new ArgumentException("Use a local path without alternate data streams.");
        uint type = GetDriveTypeW(Path.GetPathRoot(full)!);
        if (type is not (2 or 3 or 6)) throw new IOException("Use an available local writable drive; network and optical locations are not supported.");
        return full;
    }
    private static string Extended(string path) => "\\\\?\\" + path;
    private static SafeFileHandle Open(string path, uint access, uint share)
    {
        var handle = CreateFileW(Extended(path), access, share, IntPtr.Zero, 3, OpenFlags, IntPtr.Zero);
        if (!handle.IsInvalid) return handle;
        int error = Marshal.GetLastWin32Error(); handle.Dispose();
        throw Failure("Cannot open item with the required permissions/sharing", error);
    }
    private static IOException Failure(string message, int error) => new(message + ": " + new Win32Exception(error).Message + $" (Windows {error}).");

    [StructLayout(LayoutKind.Sequential)] private struct BasicInformation { public long Created, Accessed, Written, Changed; public uint Attributes; }
    [StructLayout(LayoutKind.Sequential)] private struct StandardInformation { public long Allocated, Bytes; public uint Links; public byte DeletePending, Directory; }
    [StructLayout(LayoutKind.Sequential)] private struct IdentityInformation { public ulong Volume, Low, High; }
    [StructLayout(LayoutKind.Sequential)] private struct CaseInformation { public uint Flags; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct RenameInformation { public uint Flags; public IntPtr Parent; public uint NameBytes; public char FirstCharacter; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetFileAttributesW(string path);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern uint GetDriveTypeW(string root);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path, uint size, uint flags);
    [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetBasicInformation(SafeFileHandle file, int kind, out BasicInformation info, uint size);
    [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetStandardInformation(SafeFileHandle file, int kind, out StandardInformation info, uint size);
    [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetIdentityInformation(SafeFileHandle file, int kind, out IdentityInformation info, uint size);
    [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCaseInformation(SafeFileHandle file, int kind, out CaseInformation info, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetFileInformationByHandle(SafeFileHandle file, int kind, IntPtr info, uint size);
}
