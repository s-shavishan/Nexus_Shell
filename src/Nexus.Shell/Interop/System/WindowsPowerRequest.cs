// නමෝ බුද්ධාය 🤍
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Nexus.Shell.Interop;

internal static class WindowsPowerRequest
{
    // Keep the whole REASON_CONTEXT union: 32 bytes on x64, 24 on x86.
    [StructLayout(LayoutKind.Sequential)] internal struct ReasonContext
    { public uint Version, Flags; public ReasonUnion Reason; }
    [StructLayout(LayoutKind.Explicit)] internal struct ReasonUnion
    { [FieldOffset(0)] public IntPtr Simple; [FieldOffset(0)] public DetailedReason Detailed; }
    [StructLayout(LayoutKind.Sequential)] internal struct DetailedReason
    { public IntPtr Module; public uint Id, Count; public IntPtr Strings; }
    private sealed class RequestHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public RequestHandle() : base(true) { }
        protected override bool ReleaseHandle() => CloseHandle(handle);
    }
    private sealed class Lease(RequestHandle handle, bool display) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            try { if (display) PowerClearRequest(handle, 0); PowerClearRequest(handle, 1); }
            finally { handle.Dispose(); }
        }
    }
    internal static IDisposable Acquire(bool display)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows power requests require Windows.");
        IntPtr reason = Marshal.StringToHGlobalUni("Nexus temporary keep-awake session requested by the user");
        RequestHandle? handle = null;
        try
        {
            var context = new ReasonContext { Flags = 1, Reason = new ReasonUnion { Simple = reason } };
            handle = PowerCreateRequest(ref context);
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!PowerSetRequest(handle, 1)) throw new Win32Exception(Marshal.GetLastWin32Error()); // SystemRequired
            if (display && !PowerSetRequest(handle, 0)) // DisplayRequired also needs SystemRequired
            {
                int error = Marshal.GetLastWin32Error(); PowerClearRequest(handle, 1);
                throw new Win32Exception(error);
            }
            var lease = new Lease(handle, display); handle = null; return lease;
        }
        finally { handle?.Dispose(); Marshal.FreeHGlobal(reason); }
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern RequestHandle PowerCreateRequest(ref ReasonContext context);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PowerSetRequest(RequestHandle handle, int type);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PowerClearRequest(RequestHandle handle, int type);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
}
