// නමෝ බුද්ධාය 🤍
using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Nexus.Runtime;
using Nexus.Runtime.Transfers;

namespace Nexus.Shell.Interop;

// Eager CF_HDROP data survives the Files window/process. There are no delayed
// render callbacks or clipboard history writes. Explorer uses this same format.
internal static class FileClipboardService
{
    private const uint FileDrop = 15;
    private static uint EffectFormat => RegisterClipboardFormat("Preferred DropEffect");
    internal static uint Sequence => GetClipboardSequenceNumber();
    internal static bool HasFiles => IsClipboardFormatAvailable(FileDrop);
    internal static void Write(IntPtr owner, IReadOnlyList<string> paths, FileJobKind kind)
    {
        if (owner == IntPtr.Zero || paths.Count is < 1 or > FileJobRules.MaximumSources || kind is not (FileJobKind.Copy or FileJobKind.Move))
            throw new RuntimeFailure("file-clipboard", "Select one to 24 files or folders.");
        string[] normalized = paths.Select(FileJobRules.PathName).Distinct(FileJobRules.Paths).ToArray();
        byte[] names = Encoding.Unicode.GetBytes(string.Join('\0', normalized) + "\0\0");
        byte[] drop = new byte[20 + names.Length]; // DROPFILES is 20 bytes on x86 and x64.
        BinaryPrimitives.WriteUInt32LittleEndian(drop, 20);
        BinaryPrimitives.WriteUInt32LittleEndian(drop.AsSpan(16), 1); // fWide
        names.CopyTo(drop, 20);
        uint format = EffectFormat;
        if (format == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr files = Allocate(drop), effect = IntPtr.Zero;
        try
        {
            effect = Allocate(BitConverter.GetBytes(kind == FileJobKind.Move ? 2u : 1u));
            Open(owner);
            try
            {
                if (!EmptyClipboard()) throw new Win32Exception(Marshal.GetLastWin32Error());
                // Publish intent before paths. A failed Cut never becomes Copy.
                if (SetClipboardData(format, effect) == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                effect = IntPtr.Zero; // Windows owns it from here.
                if (SetClipboardData(FileDrop, files) == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                files = IntPtr.Zero;
            }
            finally { CloseClipboard(); }
        }
        finally { if (files != IntPtr.Zero) GlobalFree(files); if (effect != IntPtr.Zero) GlobalFree(effect); }
    }
    internal static FileClipboardContent Read(IntPtr owner)
    {
        Open(owner);
        try
        {
            IntPtr drop = GetClipboardData(FileDrop);
            if (drop == IntPtr.Zero) throw new RuntimeFailure("file-clipboard", "Copy or cut files in Nexus Files or Windows Explorer first. Virtual items and text paths cannot be pasted as files.");
            uint count = DragQueryFile(drop, uint.MaxValue, null, 0);
            if (count is < 1 or > FileJobRules.MaximumSources)
                throw new RuntimeFailure("file-clipboard", "Paste one to 24 files or folders at a time.");
            var paths = new string[count];
            for (uint i = 0; i < count; i++)
            {
                uint length = DragQueryFile(drop, i, null, 0);
                if (length is < 1 or > 4096) throw new RuntimeFailure("file-clipboard", "A clipboard path is unavailable or too long.");
                var text = new StringBuilder((int)length + 1);
                if (DragQueryFile(drop, i, text, length + 1) != length)
                    throw new RuntimeFailure("file-clipboard", "The file clipboard could not be read. Copy the items again.");
                paths[i] = FileJobRules.PathName(text.ToString());
            }
            uint preference = 1, format = EffectFormat;
            if (format == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            IntPtr effect = GetClipboardData(format);
            if (effect != IntPtr.Zero)
            {
                if (GlobalSize(effect).ToUInt64() < 4) throw new RuntimeFailure("file-clipboard", "The clipboard's file operation is incomplete. Copy or cut the items again.");
                IntPtr data = GlobalLock(effect);
                if (data == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                try { preference = unchecked((uint)Marshal.ReadInt32(data)); }
                finally { GlobalUnlock(effect); }
            }
            uint requested = preference & 7;
            if (requested != 0 && (requested & 1) == 0 && requested != 2)
                throw new RuntimeFailure("file-clipboard", "This clipboard requests a link. Use Copy or Cut to transfer files.");
            return new(Sequence, requested == 2 ? FileJobKind.Move : FileJobKind.Copy, paths);
        }
        finally { CloseClipboard(); }
    }
    // Comparison and emptying happen under the same clipboard lock. A newer
    // text/file copy can never be erased after a transfer acknowledgement.
    internal static bool ClearCut(IntPtr owner, uint sequence)
    {
        if (!OpenClipboard(owner)) return false;
        try { return sequence != 0 && Sequence == sequence && EmptyClipboard(); }
        finally { CloseClipboard(); }
    }
    private static void Open(IntPtr owner)
    { if (owner == IntPtr.Zero || !OpenClipboard(owner)) throw new InvalidOperationException("The Windows clipboard is busy. Try again."); }
    private static IntPtr Allocate(byte[] bytes)
    {
        IntPtr handle = GlobalAlloc(0x0002, (UIntPtr)bytes.Length);
        if (handle == IntPtr.Zero) throw new OutOfMemoryException();
        IntPtr data = GlobalLock(handle);
        if (data == IntPtr.Zero) { GlobalFree(handle); throw new Win32Exception(Marshal.GetLastWin32Error()); }
        try { Marshal.Copy(bytes, 0, data, bytes.Length); }
        finally { GlobalUnlock(handle); }
        return handle;
    }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetClipboardData(uint format, IntPtr data);
    [DllImport("user32.dll")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
    [DllImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", CharSet = CharSet.Unicode, SetLastError = true)] private static extern uint RegisterClipboardFormat(string name);
    [DllImport("shell32.dll", EntryPoint = "DragQueryFileW", CharSet = CharSet.Unicode)] private static extern uint DragQueryFile(IntPtr drop, uint index, StringBuilder? path, uint capacity);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalUnlock(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern UIntPtr GlobalSize(IntPtr memory);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalFree(IntPtr memory);
}
