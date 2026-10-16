// නමෝ බුද්ධාය 🤍
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Nexus.Core.Windows;

// Runs only on the supervised Windows file helper's STA. Windows owns progress,
// cancellation, recycle storage and recovery. Never implement a Delete fallback.
internal static class WindowsRecycleOperation
{
    [SupportedOSPlatform("windows")]
    internal static void Run(IReadOnlyList<string> paths, IntPtr owner, Action admit)
    {
        var items = new List<IShellItem>();
        IFileOperation? operation = null;
        try
        {
            foreach (string path in paths)
            {
                // Network shares and removable media commonly have no recycle
                // storage. Keep this command on fixed local Windows volumes.
                if (path.StartsWith("\\\\", StringComparison.Ordinal)
                    || new DriveInfo(Path.GetPathRoot(path)!).DriveType != DriveType.Fixed)
                    throw new IOException("This location does not support Nexus recycling. Use Windows Explorer to review its deletion options.");
                if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("A selected item is no longer available. Refresh before recycling.");
                Guid iid = typeof(IShellItem).GUID;
                Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out var item));
                items.Add(item);
            }
            operation = (IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("3AD05575-8857-4850-9277-11B85BDB8E09"), true)!)!;
            Marshal.ThrowExceptionForHR(operation.SetOwnerWindow(owner));
            // RECYCLEONDELETE + ADDUNDORECORD, with Windows confirmation and
            // a permanent-deletion warning if Windows cannot recycle an item.
            // No NOCONFIRMATION, NOERRORUI or silent permanent-delete retry.
            Marshal.ThrowExceptionForHR(operation.SetOperationFlags(0x00080000 | 0x20000000 | 0x00004000));
            foreach (var item in items) Marshal.ThrowExceptionForHR(operation.DeleteItem(item, IntPtr.Zero));
            admit(); // Every target is staged before any mutation is admitted.
            Marshal.ThrowExceptionForHR(operation.PerformOperations());
            Marshal.ThrowExceptionForHR(operation.GetAnyOperationsAborted(out bool aborted));
            if (aborted) throw new OperationCanceledException("Windows cancelled or skipped part of this recycle operation. Review the folder and Recycle Bin before trying again.");
        }
        finally
        {
            if (operation is not null) Marshal.FinalReleaseComObject(operation);
            foreach (var item in items) Marshal.FinalReleaseComObject(item);
        }
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, out IShellItem item);
    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig] int BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, out IntPtr result);
        [PreserveSig] int GetParent(out IShellItem parent);
        [PreserveSig] int GetDisplayName(uint kind, out IntPtr name);
        [PreserveSig] int GetAttributes(uint mask, out uint attributes);
        [PreserveSig] int Compare(IShellItem other, uint hint, out int order);
    }
    [ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOperation
    {
        [PreserveSig] int Advise(IntPtr sink, out uint cookie);
        [PreserveSig] int Unadvise(uint cookie);
        [PreserveSig] int SetOperationFlags(uint flags);
        [PreserveSig] int SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        [PreserveSig] int SetProgressDialog(IntPtr dialog);
        [PreserveSig] int SetProperties(IntPtr properties);
        [PreserveSig] int SetOwnerWindow(IntPtr owner);
        [PreserveSig] int RenameItem(IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr sink);
        [PreserveSig] int RenameItems([MarshalAs(UnmanagedType.IUnknown)] object items, [MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int MoveItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, IntPtr sink);
        [PreserveSig] int MoveItems([MarshalAs(UnmanagedType.IUnknown)] object items, IShellItem destination);
        [PreserveSig] int CopyItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, IntPtr sink);
        [PreserveSig] int CopyItems([MarshalAs(UnmanagedType.IUnknown)] object items, IShellItem destination);
        [PreserveSig] int DeleteItem(IShellItem item, IntPtr sink);
        [PreserveSig] int DeleteItems([MarshalAs(UnmanagedType.IUnknown)] object items);
        [PreserveSig] int NewItem(IShellItem destination, uint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string? template, IntPtr sink);
        [PreserveSig] int PerformOperations();
        [PreserveSig] int GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
    }
}
