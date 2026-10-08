using System.Runtime.InteropServices;

namespace Nexus.Shell.Interop;

internal static class RecycleBinService
{
    [StructLayout(LayoutKind.Sequential)] private struct BinInfo { public uint Size; public long Bytes, Items; }
    [DllImport("shell32.dll", EntryPoint = "SHQueryRecycleBinW", CharSet = CharSet.Unicode)] private static extern int Query(string? root, ref BinInfo info);
    [DllImport("shell32.dll", EntryPoint = "SHEmptyRecycleBinW", CharSet = CharSet.Unicode)] private static extern int Empty(IntPtr owner, string? root, uint flags);
    internal static (long Items, long Bytes) Read()
    {
        var info = new BinInfo { Size = (uint)Marshal.SizeOf<BinInfo>() };
        Marshal.ThrowExceptionForHR(Query(null, ref info)); return (info.Items, info.Bytes);
    }
    // Confirmation belongs to Nexus; the Windows backend shows no dialog or progress UI.
    internal static void EmptyConfirmed(IntPtr owner) => Marshal.ThrowExceptionForHR(Empty(owner, null, 1 | 2 | 4));
}
