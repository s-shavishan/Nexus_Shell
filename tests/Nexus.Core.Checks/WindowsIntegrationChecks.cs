using System.Runtime.InteropServices;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;

internal static class WindowsIntegrationChecks
{
    internal static void Run()
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        var clock = new ManualClock();
        var leases = new List<Lease>(); bool deny = false; int requests = 0;
        var awake = new KeepAwakeSession(display =>
        {
            requests++;
            if (deny) throw new InvalidOperationException("Request denied");
            var lease = new Lease(display); leases.Add(lease); return lease;
        }, clock);
        Check(!awake.Active && awake.Remaining == TimeSpan.Zero && !awake.Tick(), "Keep awake must start off and perform no native work while idle.");
        foreach (int invalid in new[] { -1, 0, 1, 90, int.MaxValue })
        {
            bool rejected = false;
            try { awake.Start(invalid, true); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected && requests == 0, "Invalid durations must not acquire a Windows request.");
        }
        awake.Start(15, false);
        Check(awake.Active && !awake.KeepDisplayOn && !leases[0].Display, "Keeping the PC awake must not implicitly keep its display on.");
        clock.Advance(TimeSpan.FromMinutes(5)); clock.WallClock = clock.WallClock.AddDays(-2);
        Check(awake.Remaining == TimeSpan.FromMinutes(10), "Changing the Windows clock backwards must not extend a timer.");
        clock.WallClock = clock.WallClock.AddDays(5);
        Check(awake.Remaining == TimeSpan.FromMinutes(10), "Changing time zone/clock must not prematurely expire a timer.");
        deny = true;
        try { awake.Start(60, true); } catch (InvalidOperationException) { }
        Check(awake.Active && !awake.KeepDisplayOn && leases[0].Disposals == 0 && awake.Remaining == TimeSpan.FromMinutes(10),
            "A rejected replacement must preserve the existing request and deadline.");
        deny = false; awake.Start(30, true);
        Check(leases[0].Disposals == 1 && awake.KeepDisplayOn && leases[1].Display, "Successful replacement must release the previous request exactly once.");
        clock.Advance(TimeSpan.FromMinutes(30));
        Check(awake.Tick() && !awake.Active && leases[1].Disposals == 1 && awake.Remaining == TimeSpan.Zero,
            "Expiry must release power ownership even after the UI timer was delayed.");
        awake.Stop(); awake.Tick();
        Check(leases[1].Disposals == 1, "Repeated stop and heartbeat must never double-release a request.");
        awake.Start(120, false); awake.Stop("Stopped while locked or disconnected.");
        Check(!awake.Active && leases[2].Disposals == 1, "Session suspension must release a live request immediately.");
        awake.Start(60, true); awake.Dispose(); awake.Dispose();
        Check(!awake.Active && leases[3].Disposals == 1, "Shutdown must release once, including repeated cleanup.");
        bool disposed = false;
        try { awake.Start(15, false); } catch (ObjectDisposedException) { disposed = true; }
        Check(disposed, "A stopped shell must not reacquire keep-awake ownership.");

        Check(WindowsDesktopEvents.Classify(0x001A, 0x002F) == WindowsDesktopChange.None,
            "Nexus work-area writes must not trigger an endless system refresh loop.");
        Check(WindowsDesktopEvents.Classify(0x0219, 0x8001) == WindowsDesktopChange.None,
            "A device removal query must not be treated as removal or vetoed.");
        Check(WindowsDesktopEvents.Classify(0x0218, 4) == WindowsDesktopChange.Suspend
            && WindowsDesktopEvents.Classify(0x0218, 18).HasFlag(WindowsDesktopChange.Resume)
            && WindowsDesktopEvents.Classify(0x0218, 7).HasFlag(WindowsDesktopChange.Resume),
            "Automatic and user-triggered resume must both recover, while suspend is immediate cleanup.");
        Check(WindowsDesktopEvents.Classify(0x0050, 0) == WindowsDesktopChange.None,
            "An input request must not masquerade as a completed language change.");
        Check(WindowsDesktopEvents.Classify(0x0051, 0) == WindowsDesktopChange.InputLanguage,
            "Observed language changes must refresh the indicator.");
        Check(WindowsDesktopEvents.Classify(0xFFFF, ulong.MaxValue) == WindowsDesktopChange.None,
            "Unknown window messages must remain untouched.");

        Check(Marshal.SizeOf<WindowsPowerRequest.ReasonContext>() == (IntPtr.Size == 8 ? 32 : 24), "The complete power-request union must match the Windows ABI.");
        Check(Marshal.SizeOf<WindowsInputLanguage.GuiThreadInfo>() == (IntPtr.Size == 8 ? 72 : 48), "GUITHREADINFO must match the Windows ABI.");
        Check(WindowsInputLanguage.Describe(new IntPtr(0x04090409)).Code == "ENG", "The indicator should identify an ordinary installed language.");
        Check(WindowsInputLanguage.Describe(new IntPtr(0x2000)).Code == "IME" && WindowsInputLanguage.Describe(IntPtr.Zero).Code == "—",
            "Transient and absent layouts must not invent a language.");

        Check(WindowsToolCatalog.All.Select(tool => tool.Id).Distinct().Count() == WindowsToolCatalog.All.Count, "Tool IDs must be unique across every surface.");
        Check(WindowsToolCatalog.Find("..\\cmd.exe") is null && WindowsToolCatalog.Search("& calc.exe").Count() == 0,
            "Search/input text must not resolve to arbitrary executable actions.");
        foreach (var tool in WindowsToolCatalog.All)
        {
            Check(DesktopSearchCatalog.Find(tool.Name).Any(command => command.Id == "system:" + tool.Id), "Every Windows tool must be reachable through Spotlight.");
            if (tool.Kind is WindowsToolKind.SystemExecutable or WindowsToolKind.ControlPanelApplet)
                Check(WindowsToolCatalog.SafeNativeTarget(tool),
                    "Native tools must use allowlisted System32 executables or an explicitly approved Control Panel applet, without paths or arguments.");
            if (tool.Kind == WindowsToolKind.Settings)
                Check(tool.Target.StartsWith("ms-settings:", StringComparison.Ordinal), "Settings tools must use the Windows settings protocol.");
        }
        var adapters = WindowsToolCatalog.Find("network-adapters");
        Check(adapters is { Kind: WindowsToolKind.ControlPanelApplet, Target: "ncpa.cpl" }
            && WindowsToolCatalog.SafeNativeTarget(adapters),
            "Windows Network adapters must resolve to the approved System32 Control Panel applet.");
        Check(!WindowsToolCatalog.SafeNativeTarget(new("injected-cpl", "Injected", "", "", "",
                WindowsToolKind.ControlPanelApplet, "evil.cpl"))
            && !WindowsToolCatalog.SafeNativeTarget(new("network-adapters", "Injected", "", "", "",
                WindowsToolKind.ControlPanelApplet, @"..\\ncpa.cpl"))
            && !WindowsToolCatalog.SafeNativeTarget(new("injected-exe", "Injected", "", "", "",
                WindowsToolKind.SystemExecutable, @"C:\\Windows\\System32\\cmd.exe"))
            && !WindowsToolCatalog.SafeNativeTarget(new("injected-arg", "Injected", "", "", "",
                WindowsToolKind.SystemExecutable, "Taskmgr.exe /k")),
            "Native tool validation must reject arbitrary applets, paths and injected command-line arguments.");
        if (OperatingSystem.IsWindows())
        {
            // Read-only native coverage; CI never changes a layout or power policy.
            Check(WindowsInputLanguage.Installed().All(layout => layout.Handle != IntPtr.Zero), "Native keyboard enumeration returned an invalid layout.");
            Check(WindowsInputLanguage.Current(null).Handle != IntPtr.Zero, "Windows should expose this UI thread's current input layout.");
        }
        Console.WriteLine("PASS: Windows tool discovery, keep-awake ownership/expiry/failure, native event boundaries and interop layouts.");
    }
    private sealed class Lease(bool display) : IDisposable
    { internal bool Display => display; internal int Disposals; public void Dispose() => Disposals++; }
    private sealed class ManualClock : TimeProvider
    {
        private long _timestamp;
        internal DateTimeOffset WallClock = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public override DateTimeOffset GetUtcNow() => WallClock;
        internal void Advance(TimeSpan elapsed) { _timestamp += elapsed.Ticks; WallClock += elapsed; }
    }
}
