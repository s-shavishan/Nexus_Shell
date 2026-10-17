// නමෝ බුද්ධාය 🤍
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Nexus.Shell.Interop;

internal sealed record QuickLookRead(FileStream Stream, long Bytes, DateTime ModifiedUtc);
internal static class QuickLookFile
{
    internal static QuickLookRead Open(string path)
    {
        if (!OperatingSystem.IsWindows() || !Path.IsPathFullyQualified(path) || path.Length < 3 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\')
            throw new IOException("Built-in preview requires a full local Windows file path.");
        path = Path.GetFullPath(path);
        if (path.Length > 32700 || path[3..].Contains(':') || GetDriveTypeW(Path.GetPathRoot(path)!) is not (2 or 3 or 6))
            throw new IOException("Preview an available local drive file, not a network, optical or alternate-stream path.");
        // Read only, share read only, async, backup semantics, open the link
        // itself and avoid cloud recall. The original file is never changed.
        var handle = CreateFileW("\\\\?\\" + path, 0x80000000, 1, IntPtr.Zero, 3, 0x40000000 | 0x02000000 | 0x00200000 | 0x00100000, IntPtr.Zero);
        try
        {
            if (handle.IsInvalid) throw Error("Cannot read this file");
            if (GetFileType(handle) != 1) throw new IOException("Only local disk files can be previewed.");
            if (!GetFileInformationByHandleEx(handle, 0, out var basic, (uint)Marshal.SizeOf<BasicInformation>())) throw Error("Cannot verify this file");
            if ((basic.Attributes & (0x10u | 0x400u | 0x1000u | 0x40000u | 0x400000u)) != 0)
                throw new IOException("Folders, linked/reparse, offline and cloud-recall items are not read by Quick Look.");
            var final = new StringBuilder(32768);
            uint count = GetFinalPathNameByHandleW(handle, final, (uint)final.Capacity, 0);
            if (count == 0 || count >= final.Capacity) throw Error("Cannot verify the opened file path");
            if (!final.ToString().Equals("\\\\?\\" + path, StringComparison.OrdinalIgnoreCase))
                throw new IOException("This path resolves through an alias/link or changed location. Open the original local folder.");
            var stream = new FileStream(handle, FileAccess.Read, 65536, isAsync: true);
            handle = null!; // FileStream owns the SafeFileHandle from here.
            try { return new(stream, stream.Length, DateTime.FromFileTimeUtc(basic.Written)); }
            catch { stream.Dispose(); throw; }
        }
        finally { handle?.Dispose(); }
    }
    private static IOException Error(string message)
    {
        int code = Marshal.GetLastWin32Error();
        return new(message + ": " + new Win32Exception(code).Message + $" (Windows {code}).");
    }
    [StructLayout(LayoutKind.Sequential)] private struct BasicInformation { public long Created, Accessed, Written, Changed; public uint Attributes; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern uint GetDriveTypeW(string root);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint GetFileType(SafeFileHandle file);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path, uint size, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandleEx(SafeFileHandle file, int kind, out BasicInformation info, uint size);
}
