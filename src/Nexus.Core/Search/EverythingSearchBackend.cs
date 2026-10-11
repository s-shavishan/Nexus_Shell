using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using Nexus.Runtime;

namespace Nexus.Core.Search;

// Runs in Core, on a short-lived message-pump thread. The shell never loads
// third-party binaries or builds a second file index. Every native send and
// reply wait has a deadline; one query at a time is enforced by the coordinator.
internal sealed class EverythingSearchBackend : IFileSearchBackend
{
    internal const string WindowClass = "EVERYTHING_TASKBAR_NOTIFICATION";
    internal const uint CopyData = 0x004A, Ipc = 0x0400, DatabaseLoaded = 401;
    private readonly Func<IntPtr> _find;
    private readonly TimeSpan _timeout;
    internal EverythingSearchBackend() : this(() => FindWindow(WindowClass, null), TimeSpan.FromSeconds(4)) { }
    internal EverythingSearchBackend(Func<IntPtr> find, TimeSpan timeout) { _find = find; _timeout = timeout; }
    public Task<SearchReply> QueryAsync(SearchPlan plan, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) return Task.FromResult(SearchReply.Empty(SearchStatus.Unsupported));
        var completion = new TaskCompletionSource<SearchReply>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.TrySetResult(Query(plan, cancellation)); }
            catch (OperationCanceledException) { completion.TrySetCanceled(cancellation); }
            catch (Exception) { completion.TrySetResult(SearchReply.Empty(SearchStatus.Failed)); }
        }) { IsBackground = true, Name = "Nexus Everything query" };
        thread.Start(); return completion.Task;
    }
    private SearchReply Query(SearchPlan plan, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        IntPtr engine = _find();
        if (engine == IntPtr.Zero) return SearchReply.Empty(SearchStatus.Unavailable);
        GetWindowThreadProcessId(engine, out uint enginePid);
        if (enginePid == 0) return SearchReply.Empty(SearchStatus.Unavailable);
        // HWND lookup is session-local. Retain its process handle to exclude
        // PID reuse while the query is in flight.
        using var own = Process.GetCurrentProcess();
        if (!ProcessIdToSessionId(enginePid, out uint session) || session != own.SessionId) return SearchReply.Empty(SearchStatus.Unavailable);
        // Query-limited + synchronize works across normal/elevated clients.
        // Process.Handle would request much broader access than a search needs.
        using var process = OpenProcess(0x00101000, false, enginePid);
        if (process.IsInvalid) return SearchReply.Empty(SearchStatus.Failed);
        bool Exited() => WaitForSingleObject(process, 0) != 258;
        var clock = Stopwatch.StartNew();
        uint Remaining() => (uint)Math.Max(0, (_timeout - clock.Elapsed).TotalMilliseconds);
        bool Send(uint message, nuint wparam, IntPtr lparam, out nuint result)
        {
            cancellation.ThrowIfCancellationRequested();
            uint remaining = Remaining(); result = 0;
            // SMTO_ABORTIFHUNG | SMTO_ERRORONEXIT. Do not use SMTO_BLOCK:
            // Everything may synchronously deliver WM_COPYDATA during Send.
            return remaining > 0 && SendMessageTimeout(engine, message, wparam, lparam, 0x22, Math.Min(remaining, 500), out result) != IntPtr.Zero;
        }
        if (!Send(Ipc, 0, IntPtr.Zero, out var major) || !Send(Ipc, 1, IntPtr.Zero, out var minor)
            || !Send(Ipc, 2, IntPtr.Zero, out var revision)) return SearchReply.Empty(SearchStatus.TimedOut);
        string version = $"{major}.{minor}.{revision}";
        if (major < 1 || (major == 1 && (minor < 4 || (minor == 4 && revision < 1))))
            return new(SearchStatus.Unsupported, [], EngineVersion: version);
        if (!Send(Ipc, DatabaseLoaded, IntPtr.Zero, out var loaded)) return new(SearchStatus.TimedOut, [], EngineVersion: version);
        if (loaded == 0) return new(SearchStatus.Indexing, [], EngineVersion: version);
        uint replyId = (uint)RandomNumberGenerator.GetInt32(1, int.MaxValue);
        SearchReply? reply = null;
        // Per-query window + nonce prevent a late reply from satisfying a later
        // query. Never retain pointers passed across WM_COPYDATA.
        WindowProcedure procedure = (window, message, sender, data) =>
        {
            if (message != CopyData) return DefWindowProc(window, message, sender, data);
            try
            {
                if (reply is not null || data == IntPtr.Zero || Exited()) return IntPtr.Zero;
                // The SDK reply contract identifies a query by dwData, not by
                // a mandatory sender HWND. Accept a null sender; if supplied,
                // it must belong to the selected engine process. This is not
                // an authentication boundary against same-session processes.
                if (sender != 0)
                { GetWindowThreadProcessId((IntPtr)sender, out uint senderPid); if (senderPid != enginePid) return IntPtr.Zero; }
                var copy = Marshal.PtrToStructure<CopyDataPacket>(data);
                if (copy.Id != replyId) return IntPtr.Zero;
                if (copy.Size is < 20 or > EverythingQueryCodec.MaximumReplyBytes || copy.Data == IntPtr.Zero)
                { reply = new(SearchStatus.Failed, [], EngineVersion: version); return IntPtr.Zero; }
                byte[] bytes = new byte[copy.Size]; Marshal.Copy(copy.Data, bytes, 0, bytes.Length);
                reply = EverythingQueryCodec.Decode(bytes, plan, version); return new IntPtr(1);
            }
            catch (Exception) { reply = new(SearchStatus.Failed, [], EngineVersion: version); return IntPtr.Zero; }
        };
        string className = "Nexus.Search.Reply." + Guid.NewGuid().ToString("N");
        IntPtr instance = GetModuleHandle(null), windowHandle = IntPtr.Zero;
        bool querySent = false;
        var windowClass = new NativeWindowClass { Size = (uint)Marshal.SizeOf<NativeWindowClass>(), Procedure = procedure, Instance = instance, ClassName = className };
        if (RegisterClassEx(ref windowClass) == 0) return SearchReply.Empty(SearchStatus.Failed);
        try
        {
            windowHandle = CreateWindowEx(0, className, "", 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, instance, IntPtr.Zero);
            if (windowHandle == IntPtr.Zero) return SearchReply.Empty(SearchStatus.Failed);
            // Only this private reply window accepts lower-integrity COPYDATA;
            // required when Nexus and the Everything client have different
            // elevation. The payload is bounded and never executed on receipt.
            if (!ChangeWindowMessageFilterEx(windowHandle, CopyData, 1, IntPtr.Zero)) return SearchReply.Empty(SearchStatus.Failed);
            byte[] query = EverythingQueryCodec.Encode(plan, unchecked((uint)windowHandle.ToInt64()), replyId);
            IntPtr data = Marshal.AllocHGlobal(query.Length), packet = Marshal.AllocHGlobal(Marshal.SizeOf<CopyDataPacket>());
            try
            {
                Marshal.Copy(query, 0, data, query.Length);
                Marshal.StructureToPtr(new CopyDataPacket { Id = EverythingQueryCodec.QueryMessage, Size = query.Length, Data = data }, packet, false);
                querySent = true;
                if (!Send(CopyData, (nuint)windowHandle, packet, out var accepted)) return new(SearchStatus.TimedOut, [], EngineVersion: version);
                if (accepted == 0) return new(SearchStatus.Unsupported, [], EngineVersion: version);
            }
            finally { Marshal.FreeHGlobal(packet); Marshal.FreeHGlobal(data); }
            IntPtr[] handles = [cancellation.WaitHandle.SafeWaitHandle.DangerousGetHandle()];
            while (reply is null)
            {
                cancellation.ThrowIfCancellationRequested();
                if (Exited() || Remaining() == 0) return new(SearchStatus.TimedOut, [], EngineVersion: version);
                uint wait = MsgWaitForMultipleObjectsEx(1, handles, Math.Min(Remaining(), 100), 0x04FF, 0x0004);
                if (wait == 0xFFFFFFFF) return SearchReply.Empty(SearchStatus.Failed);
                while (PeekMessage(out var message, windowHandle, 0, 0, 1))
                { TranslateMessage(ref message); DispatchMessage(ref message); }
            }
            cancellation.ThrowIfCancellationRequested(); return reply;
        }
        finally
        {
            if (windowHandle != IntPtr.Zero)
            {
                if (querySent && reply is null) CancelPending(engine, windowHandle);
                DestroyWindow(windowHandle);
            }
            UnregisterClass(className, instance); GC.KeepAlive(procedure);
        }
    }
    private static void CancelPending(IntPtr engine, IntPtr replyWindow)
    {
        // Best effort only: an unresponsive engine must not delay Core cleanup.
        // Replacing the query, rather than merely destroying its receiver, tells
        // a responsive Everything instance to abandon the previous search.
        byte[] bytes = EverythingQueryCodec.Cancel(unchecked((uint)replyWindow.ToInt64()));
        IntPtr data = Marshal.AllocHGlobal(bytes.Length), packet = Marshal.AllocHGlobal(Marshal.SizeOf<CopyDataPacket>());
        try
        {
            Marshal.Copy(bytes, 0, data, bytes.Length);
            Marshal.StructureToPtr(new CopyDataPacket { Id = EverythingQueryCodec.QueryMessage, Size = bytes.Length, Data = data }, packet, false);
            SendMessageTimeout(engine, CopyData, (nuint)replyWindow, packet, 0x22, 100, out _);
        }
        finally { Marshal.FreeHGlobal(packet); Marshal.FreeHGlobal(data); }
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate IntPtr WindowProcedure(IntPtr window, uint message, nuint wparam, IntPtr lparam);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct NativeWindowClass
    {
        public uint Size, Style; public WindowProcedure Procedure; public int ClassExtra, WindowExtra; public IntPtr Instance, Icon, Cursor, Background;
        public string? MenuName; public string ClassName; public IntPtr SmallIcon;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct CopyDataPacket { public nuint Id; public int Size; public IntPtr Data; }
    [StructLayout(LayoutKind.Sequential)] internal struct NativeMessage { public IntPtr Window; public uint Message; public nuint WParam; public IntPtr LParam; public uint Time; public int X, Y; public uint Private; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string className, string? title);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern IntPtr SendMessageTimeout(IntPtr window, uint message, nuint wparam, IntPtr lparam, uint flags, uint timeout, out nuint result);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern ushort RegisterClassEx(ref NativeWindowClass windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool UnregisterClass(string className, IntPtr instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr CreateWindowEx(uint extended, string className, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll")] internal static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr DefWindowProc(IntPtr window, uint message, nuint wparam, IntPtr lparam);
    [DllImport("user32.dll")] private static extern bool ChangeWindowMessageFilterEx(IntPtr window, uint message, uint action, IntPtr change);
    [DllImport("user32.dll")] internal static extern uint MsgWaitForMultipleObjectsEx(uint count, IntPtr[] handles, uint milliseconds, uint mask, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool PeekMessage(out NativeMessage message, IntPtr window, uint first, uint last, uint remove);
    [DllImport("user32.dll")] internal static extern bool TranslateMessage(ref NativeMessage message);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr DispatchMessage(ref NativeMessage message);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string? name);
    [DllImport("kernel32.dll")] private static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);
    [DllImport("kernel32.dll")] private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);
    [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);
}
