// නමෝ බුද්ධාය 🤍
using Microsoft.Win32.SafeHandles;
using Nexus.Runtime.Transfers;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Nexus.Core.Transfers;

// The opened source excludes writes and renames. A completed move marks this
// exact file handle for deletion; a pathname recheck cannot delete a replacement.
internal sealed class WindowsTransferSource : ITransferSource
{
    private readonly SafeFileHandle _handle;
    public Stream Content { get; }
    private WindowsTransferSource(string path, bool move)
    {
        _handle = CreateFile(path, 0x80000000u | (move ? 0x00010000u : 0), 1, IntPtr.Zero, 3, 0x48200080, IntPtr.Zero);
        if (_handle.IsInvalid) { int error = Marshal.GetLastWin32Error(); _handle.Dispose(); throw new Win32Exception(error); }
        try
        {
            if (!GetFileInformationByHandle(_handle, out var info)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if ((info.Attributes & (0x10u | 0x400u | 0x1000u | 0x4000u)) != 0) throw new IOException("This source is a directory, link, encrypted or offline file.");
            if (move && (info.Attributes & 1) != 0) throw new IOException("This source is read-only. Copy it instead of moving it.");
            Content = new FileStream(_handle, FileAccess.Read, 128 * 1024, isAsync: true);
        }
        catch { _handle.Dispose(); throw; }
    }
    internal static ITransferSource Open(string path, bool move) => new WindowsTransferSource(path, move);
    public void CommitMove()
    {
        var disposition = new Disposition { Delete = 1 };
        if (!SetFileInformationByHandle(_handle, 4, ref disposition, 1)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public void Dispose() { Content.Dispose(); _handle.Dispose(); }
    [StructLayout(LayoutKind.Sequential)] private struct NativeTime { internal uint Low, High; }
    [StructLayout(LayoutKind.Sequential)] private struct FileInfo
    { internal uint Attributes; internal NativeTime Created, Accessed, Written; internal uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [StructLayout(LayoutKind.Sequential)] private struct Disposition { internal byte Delete; }
    internal static int InfoSize => Marshal.SizeOf<FileInfo>();
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)] private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInfo info);
    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetFileInformationByHandle(SafeFileHandle file, int type, ref Disposition info, uint size);
}
