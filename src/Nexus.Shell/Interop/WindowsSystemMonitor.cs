using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Nexus.Runtime.Monitoring;

namespace Nexus.Shell.Interop;

// Uses the shell's existing token. No debug privilege, elevation, WMI, command
// line, performance-counter service or executable-icon loading is involved.
internal sealed class WindowsSystemMonitor : ISystemMonitorSource, IProcessActionSource
{
    private const uint Query = 0x1000, Synchronize = 0x100000;
    private readonly byte[]? _user;
    private readonly uint? _session;
    internal WindowsSystemMonitor()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var own = OpenProcess(Query, false, Environment.ProcessId);
        if (!own.IsInvalid) (_user, _session) = Owner(own);
    }
    public SystemObservation Read(CancellationToken cancellation)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("System Monitor requires Windows.");
        SystemCpuCounters? cpu = GetSystemTimes(out ulong idle, out ulong kernel, out ulong user) ? new(idle, kernel, user) : null;
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        bool hasMemory = GlobalMemoryStatusEx(ref memory);
        var processes = new List<ProcessObservation>(); string notice = "";
        using var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot.IsInvalid) throw Error("Windows could not enumerate processes");
        var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>(), ExeFile = "" };
        bool next = Process32FirstW(snapshot, ref entry);
        if (!next && Marshal.GetLastWin32Error() != 18) throw Error("Windows could not read the process snapshot");
        while (next)
        {
            cancellation.ThrowIfCancellationRequested();
            if (processes.Count == 8192) { notice = "The process snapshot reached its 8,192-entry limit."; break; }
            int id = unchecked((int)entry.ProcessId);
            using var handle = OpenProcess(Query | Synchronize, false, id);
            ProcessObservation row;
            if (handle.IsInvalid) row = Unavailable(id, entry.ExeFile, "Windows denied access or this process exited.");
            else
            {
                try { row = Observe(handle, id, entry.ExeFile); }
                catch (Win32Exception) { row = Unavailable(id, entry.ExeFile, "This process exited or its details are unavailable."); }
            }
            processes.Add(row); next = Process32NextW(snapshot, ref entry);
        }
        if (!next && Marshal.GetLastWin32Error() is not (0 or 18)) notice = "Windows returned an incomplete process snapshot. Refresh to retry.";
        return new(cpu, hasMemory ? memory.TotalPhysical : 0, hasMemory ? memory.AvailablePhysical : 0,
            TimeSpan.FromMilliseconds(GetTickCount64()), processes.ToArray(), notice);
    }
    private static ProcessObservation Unavailable(int id, string name, string detail)
        => new(new(id, 0), name, "", null, Stopwatch.GetTimestamp(), null, null, false, false, null, detail);
    private ProcessObservation Observe(SafeProcessHandle handle, int id, string fallback = "")
    {
        uint state = WaitForSingleObject(handle, 0);
        if (state == 0) throw new Win32Exception(1168, "The selected process has exited. Refresh the list.");
        if (state != 258) throw Error("Windows could not check whether this process is running");
        if (!GetProcessTimes(handle, out long created, out _, out ulong kernel, out ulong user))
            throw Error("Windows could not read this process identity");
        var image = new StringBuilder(512); uint size = (uint)image.Capacity;
        bool hasPath = QueryFullProcessImageNameW(handle, 0, image, ref size);
        if (!hasPath && Marshal.GetLastWin32Error() == 122)
        { image = new(32768); size = (uint)image.Capacity; hasPath = QueryFullProcessImageNameW(handle, 0, image, ref size); }
        string path = hasPath ? image.ToString() : "";
        var counters = new MemoryCounters { Size = (uint)Marshal.SizeOf<MemoryCounters>() };
        bool hasMemory = K32GetProcessMemoryInfo(handle, ref counters, counters.Size);
        (byte[]? owner, uint? session) = Owner(handle);
        bool? critical = IsProcessCritical(handle, out bool isCritical) ? isCritical : null;
        return new(new(id, created), path.Length > 0 ? Path.GetFileName(path) : fallback, path,
            kernel <= ulong.MaxValue - user ? kernel + user : null, Stopwatch.GetTimestamp(),
            hasMemory ? counters.WorkingSet.ToUInt64() : null, hasMemory ? counters.PrivateUsage.ToUInt64() : null,
            _user is not null && owner is not null && _user.AsSpan().SequenceEqual(owner),
            _session is not null && session == _session, critical);
    }
    public IProcessActionTarget Open(ProcessIdentity identity, ProcessAction action)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Process controls require Windows.");
        var handle = OpenProcess(Query | Synchronize | (action == ProcessAction.EndProcess ? 1u : 0u), false, identity.Id);
        if (handle.IsInvalid) { handle.Dispose(); throw Error("Windows denied the process action or the process exited"); }
        return new Target(this, handle, identity.Id);
    }
    private sealed class Target(WindowsSystemMonitor owner, SafeProcessHandle handle, int id) : IProcessActionTarget
    {
        public ProcessObservation Read() => owner.Observe(handle, id);
        public ProcessActionResult EndProcess()
        {
            if (!TerminateProcess(handle, 1)) throw Error("Windows refused to end this process");
            bool exited = WaitForSingleObject(handle, 250) == 0;
            return new(true, exited, exited ? "The selected process ended." : "Termination was requested. Windows has not confirmed exit yet; refresh to observe it.");
        }
        public ProcessActionResult CloseWindows()
        {
            int sent = 0, visited = 0;
            EnumWindow callback = (window, _) =>
            {
                if (++visited > 8192 || WaitForSingleObject(handle, 0) != 258) return false;
                GetWindowThreadProcessId(window, out uint pid);
                if (pid == id && IsWindowVisible(window) && GetWindow(window, 4) == IntPtr.Zero
                    && PostMessageW(window, 0x0010, IntPtr.Zero, IntPtr.Zero)) sent++;
                return true;
            };
            bool completed = EnumWindows(callback, IntPtr.Zero); GC.KeepAlive(callback);
            if (sent == 0 && WaitForSingleObject(handle, 0) == 0) return new(false, true, "The selected process has already exited.");
            if (!completed && sent == 0 && WaitForSingleObject(handle, 0) == 258)
                return new(false, false, "Windows could not finish enumerating app windows. No close request was sent.");
            return new(sent > 0, WaitForSingleObject(handle, 0) == 0, sent > 0
                ? $"Close requested for {sent} app window(s). The app may ask you to save or remain open."
                : "No accessible app window was found. Background processes may require End process.");
        }
        public void Dispose() => handle.Dispose();
    }
    private static (byte[]? User, uint? Session) Owner(SafeProcessHandle process)
    {
        if (!OpenProcessToken(process, 8, out var token)) return (null, null);
        using (token)
        {
            GetTokenInformation(token, 1, IntPtr.Zero, 0, out uint required);
            if (required is < 8 or > 65536) return (null, null);
            IntPtr data = Marshal.AllocHGlobal((int)required);
            try
            {
                if (!GetTokenInformation(token, 1, data, required, out _)) return (null, null);
                IntPtr sid = Marshal.ReadIntPtr(data); uint length = GetLengthSid(sid);
                if (length is < 8 or > 68) return (null, null);
                byte[] bytes = new byte[length]; Marshal.Copy(sid, bytes, 0, bytes.Length);
                bool hasSession = GetTokenInformation(token, 12, data, required, out _);
                return (bytes, hasSession ? unchecked((uint)Marshal.ReadInt32(data)) : null);
            }
            finally { Marshal.FreeHGlobal(data); }
        }
    }
    public StorageSnapshot ReadStorage(CancellationToken cancellation)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Storage reporting requires Windows.");
        // Never probe network mounts. Removable/empty drives must not display a
        // Windows 'insert disk' dialog, including from a background worker.
        uint oldMode = GetThreadErrorMode();
        if (!SetThreadErrorMode(oldMode | 1, out _)) throw Error("Windows could not suppress drive error dialogs");
        try
        {
            uint mask = GetLogicalDrives(); if (mask == 0) throw Error("Windows could not enumerate drives");
            var volumes = new List<StorageVolume>();
            for (int letter = 0; letter < 26; letter++)
            {
                cancellation.ThrowIfCancellationRequested(); if ((mask & (1u << letter)) == 0) continue;
                string root = (char)('A' + letter) + @":\";
                uint type = GetDriveTypeW(root);
                string kind = type switch { 2 => "Removable", 3 => "Local drive", 4 => "Network", 5 => "Optical", 6 => "RAM disk", _ => "Drive" };
                if (type == 4) { volumes.Add(new(root, kind, null, null, null, "Network drive · capacity is not probed.")); continue; }
                if (type is not (2 or 3 or 5 or 6)) { volumes.Add(new(root, kind, null, null, null, "Drive information unavailable.")); continue; }
                if (GetDiskFreeSpaceExW(root, out ulong available, out ulong total, out ulong free))
                    volumes.Add(new(root, kind, total, available, free, "Capacity and available space reported for your Windows account."));
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    volumes.Add(new(root, kind, null, null, null, error is 21 or 1112 ? "No ready media." : "Capacity unavailable: " + new Win32Exception(error).Message));
                }
            }
            return new(volumes.ToArray());
        }
        finally { SetThreadErrorMode(oldMode, out _); }
    }
    private static Win32Exception Error(string message)
    { int error = Marshal.GetLastWin32Error(); return new(error, message + ": " + new Win32Exception(error).Message); }
    private sealed class KernelHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public KernelHandle() : base(true) { }
        protected override bool ReleaseHandle() => CloseHandle(handle);
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ProcessEntry
    {
        public uint Size, Usage, ProcessId; public UIntPtr Heap; public uint Module, Threads, Parent;
        public int Priority; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ExeFile;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryCounters
    { public uint Size, PageFaults; public UIntPtr PeakWorkingSet, WorkingSet, PeakPagedPool, PagedPool, PeakNonPagedPool, NonPagedPool, Pagefile, PeakPagefile, PrivateUsage; }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
    { public uint Length, Load; public ulong TotalPhysical, AvailablePhysical, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, AvailableExtended; }
    private delegate bool EnumWindow(IntPtr window, IntPtr parameter);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int id);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern KernelHandle CreateToolhelp32Snapshot(uint flags, uint id);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32FirstW(KernelHandle snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32NextW(KernelHandle snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetProcessTimes(SafeProcessHandle process, out long created, out long exited, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryFullProcessImageNameW(SafeProcessHandle process, uint flags, StringBuilder path, ref uint length);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool K32GetProcessMemoryInfo(SafeProcessHandle process, ref MemoryCounters counters, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsProcessCritical(SafeProcessHandle process, [MarshalAs(UnmanagedType.Bool)] out bool critical);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus memory);
    [DllImport("kernel32.dll")] private static extern ulong GetTickCount64();
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TerminateProcess(SafeProcessHandle process, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out KernelHandle token);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetTokenInformation(KernelHandle token, int type, IntPtr data, uint size, out uint required);
    [DllImport("advapi32.dll")] private static extern uint GetLengthSid(IntPtr sid);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumWindows(EnumWindow callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint id);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll", SetLastError = true, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint GetLogicalDrives();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern uint GetDriveTypeW(string root);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetDiskFreeSpaceExW(string root, out ulong available, out ulong total, out ulong free);
    [DllImport("kernel32.dll")] private static extern uint GetThreadErrorMode();
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetThreadErrorMode(uint mode, out uint old);
}
