using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using Nexus.Core.Search;
using Nexus.Runtime;
using static Nexus.Core.Search.EverythingSearchBackend;

// Native transport contract test, independent of an installed Everything copy.
// It creates its own invisible IPC server; no indexing or filesystem changes.
internal static class WindowsSearchChecks
{
    private static void Check(bool valid, string name) { if (!valid) throw new Exception(name); }
    internal static async Task RunAsync()
    {
        if (!OperatingSystem.IsWindows()) { Console.WriteLine("SKIP: Everything WM_COPYDATA transport checks require Windows."); return; }
        using var engine = new Engine(); await engine.Ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var backend = new EverythingSearchBackend(() => engine.Window, TimeSpan.FromSeconds(2));
        var plan = SearchPolicy.Create(new("Phase02"), @"C:\Users\Shan");
        var reply = await backend.QueryAsync(plan, default).WaitAsync(TimeSpan.FromSeconds(5));
        Check(reply.Status == SearchStatus.Ready && reply.Items.Single().Name == "Phase02.txt", "Native query/reply must return the fixture file.");
        Check(engine.Expression == plan.Expression && engine.HeaderValid, "Native transport must preserve query limits, HWND, flags and Unicode.");
        engine.Mode = 1;
        Check((await backend.QueryAsync(plan, default)).Status == SearchStatus.Indexing, "Unloaded indexes must report Indexing.");
        engine.Mode = 2;
        Check((await backend.QueryAsync(plan, default)).Status == SearchStatus.Unsupported, "Old engine version must be rejected before query.");
        engine.Mode = 3;
        Check((await backend.QueryAsync(plan, default)).Status == SearchStatus.Failed, "Malformed native reply must be contained.");
        engine.Mode = 4;
        Check((await backend.QueryAsync(plan, default)).Status == SearchStatus.Ready, "A wrong query nonce must be ignored before accepting the current reply.");
        engine.Mode = 5;
        Check((await backend.QueryAsync(plan, default)).Status == SearchStatus.Ready, "The documented reply nonce works with a null sender HWND.");
        engine.Mode = 6;
        var bounded = new EverythingSearchBackend(() => engine.Window, TimeSpan.FromMilliseconds(500));
        Check((await bounded.QueryAsync(plan, default).WaitAsync(TimeSpan.FromSeconds(4))).Status == SearchStatus.TimedOut, "A missing reply must terminate within a deadline.");
        using (var cancellation = new CancellationTokenSource())
        {
            var pending = backend.QueryAsync(plan, cancellation.Token); cancellation.CancelAfter(100);
            try { await pending.WaitAsync(TimeSpan.FromSeconds(4)); throw new Exception("Native cancellation ignored."); }
            catch (OperationCanceledException) { }
        }
        Check(engine.Cancellations >= 2, "Timeout and caller cancellation must attempt to replace the old engine query before closing the receiver.");
        engine.Mode = 0;
        Check((await backend.QueryAsync(plan, default)).Status == SearchStatus.Ready, "Native adapter must recover after timeout and cancellation.");
        var absent = new EverythingSearchBackend(() => IntPtr.Zero, TimeSpan.FromSeconds(1));
        Check((await absent.QueryAsync(plan, default)).Status == SearchStatus.Unavailable, "Missing client must report Unavailable.");
        if (engine.Error is { } error) throw new Exception("Native fixture failed.", error);
        Console.WriteLine("PASS: native Everything transport; Unicode query/reply, version/readiness, malformed data, stale nonce, null sender, deadlines, cancellation and recovery. Test-owned engine; real Everything acceptance is separate.");
    }
    private sealed class Engine : IDisposable
    {
        internal readonly TaskCompletionSource Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal IntPtr Window;
        internal volatile int Mode;
        internal string Expression = "";
        internal bool HeaderValid;
        internal int Cancellations;
        internal Exception? Error;
        private readonly Thread _thread;
        private volatile bool _stop;
        internal Engine() { _thread = new Thread(Run) { IsBackground = true }; _thread.Start(); }
        private void Run()
        {
            WindowProcedure procedure = Handle;
            string name = "Nexus.Tests.Search." + Guid.NewGuid().ToString("N"); IntPtr module = GetModuleHandle(null);
            var native = new NativeWindowClass { Size = (uint)Marshal.SizeOf<NativeWindowClass>(), Procedure = procedure, Instance = module, ClassName = name };
            try
            {
                if (RegisterClassEx(ref native) == 0) throw new Exception("Test IPC class registration failed.");
                Window = CreateWindowEx(0, name, "", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, module, IntPtr.Zero);
                if (Window == IntPtr.Zero) throw new Exception("Test IPC window creation failed.");
                Ready.TrySetResult();
                while (!_stop)
                {
                    MsgWaitForMultipleObjectsEx(0, [], 50, 0x04FF, 0x0004);
                    while (PeekMessage(out var message, Window, 0, 0, 1)) { TranslateMessage(ref message); DispatchMessage(ref message); }
                }
            }
            catch (Exception error) { Error = error; Ready.TrySetException(error); }
            finally { if (Window != IntPtr.Zero) DestroyWindow(Window); UnregisterClass(name, module); GC.KeepAlive(procedure); }
        }
        private IntPtr Handle(IntPtr window, uint message, nuint wparam, IntPtr lparam)
        {
            try
            {
                if (message == Ipc) return new IntPtr(wparam switch { 0 => 1, 1 => Mode == 2 ? 3 : 4, 2 => 1, DatabaseLoaded => Mode == 1 ? 0 : 1, _ => 0 });
                if (message != CopyData) return DefWindowProc(window, message, wparam, lparam);
                var copy = Marshal.PtrToStructure<CopyDataPacket>(lparam);
                if (copy.Id != 18 || copy.Size < 30) throw new Exception("Invalid query packet.");
                byte[] query = new byte[copy.Size]; Marshal.Copy(copy.Data, query, 0, query.Length);
                uint Field(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(query.AsSpan(offset));
                if (Field(16) == 0 && Field(4) == 0) { Interlocked.Increment(ref Cancellations); return new IntPtr(1); }
                HeaderValid = Field(0) == unchecked((uint)wparam) && Field(8) == 0 && Field(12) == 0 && Field(16) == 50 && Field(20) == 4 && Field(24) == 1;
                Expression = Encoding.Unicode.GetString(query[28..]).TrimEnd('\0');
                if (Mode == 6) return new IntPtr(1);
                byte[] bytes = SearchChecks.Fixture([@"C:\Users\Shan\Phase02.txt"]);
                if (Mode == 3) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), uint.MaxValue);
                if (Mode == 4) Reply((IntPtr)wparam, Field(4) + 1, bytes, window);
                Reply((IntPtr)wparam, Field(4), bytes, Mode == 5 ? IntPtr.Zero : window);
                return new IntPtr(1);
            }
            catch (Exception error) { Error = error; return IntPtr.Zero; }
        }
        private static void Reply(IntPtr target, uint id, byte[] bytes, IntPtr sender)
        {
            IntPtr data = Marshal.AllocHGlobal(bytes.Length), packet = Marshal.AllocHGlobal(Marshal.SizeOf<CopyDataPacket>());
            try
            {
                Marshal.Copy(bytes, 0, data, bytes.Length); Marshal.StructureToPtr(new CopyDataPacket { Id = id, Size = bytes.Length, Data = data }, packet, false);
                SendMessageTimeout(target, CopyData, (nuint)sender, packet, 0x22, 500, out _);
            }
            finally { Marshal.FreeHGlobal(packet); Marshal.FreeHGlobal(data); }
        }
        public void Dispose() { _stop = true; if (!_thread.Join(TimeSpan.FromSeconds(3))) throw new Exception("Native search fixture did not stop."); }
    }
}
