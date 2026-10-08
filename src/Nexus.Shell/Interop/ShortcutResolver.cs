using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Nexus.Shell.Interop;

// IShellLink is an in-process Windows service. It does not instantiate Explorer.
internal static class ShortcutResolver
{
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")] private class ShellLink { }
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLink
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, IntPtr findData, uint flags);
        void GetIDList(out IntPtr list); void SetIDList(IntPtr list);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string value);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string value);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string value);
        void GetHotkey(out short value); void SetHotkey(short value);
        void GetShowCmd(out int value); void SetShowCmd(int value);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder value, int count, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string value, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr window, uint flags); void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
    internal static (string Target, string Arguments, string WorkingDirectory) Read(string path)
    {
        object link = new ShellLink();
        try
        {
            ((IPersistFile)link).Load(path, 0);
            var shell = (IShellLink)link; var target = new StringBuilder(32768); var args = new StringBuilder(32768); var working = new StringBuilder(32768);
            shell.GetPath(target, target.Capacity, IntPtr.Zero, 4); shell.GetArguments(args, args.Capacity); shell.GetWorkingDirectory(working, working.Capacity);
            return (Environment.ExpandEnvironmentVariables(target.ToString()), args.ToString(), working.ToString());
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }
}
