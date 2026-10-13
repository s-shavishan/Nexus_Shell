using System.Runtime.InteropServices;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;

internal static class NativeChromeChecks
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NcCalc { public ShellLayerInterop.Rect Proposed, OldWindow, OldClient; public IntPtr Position; }
    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
    private static extern IntPtr SendMessage(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);

    // Called only on Windows using DesktopNativeChecks' own test HWND.
    internal static void Run(IntPtr window)
    {
        int healthyStateObservers = 0;
        Action<bool> faultyState = _ => throw new InvalidOperationException("Fixture: state observer unavailable");
        Action<bool> healthyState = _ => healthyStateObservers++;
        using var chrome = new WindowChrome(window, resizable: true,
            stateChanged: faultyState + healthyState);
        if (!chrome.RegionDecorationsEnabled || healthyStateObservers == 0)
            throw new Exception("A throwing native state observer disabled GDI decoration or suppressed healthy observers.");
        var original = new ShellLayerInterop.Rect { Left = 100, Top = 100, Right = 700, Bottom = 500 };
        IntPtr data = Marshal.AllocHGlobal(Marshal.SizeOf<NcCalc>());
        try
        {
            Marshal.StructureToPtr(new NcCalc { Proposed = original, OldWindow = original, OldClient = original }, data, false);
            if (SendMessage(window, 0x0083, new UIntPtr(1), data) != IntPtr.Zero)
                throw new Exception("Nexus NCCALCSIZE must preserve client pixels instead of requesting WVR_REDRAW.");
            var proposed = Marshal.PtrToStructure<NcCalc>(data).Proposed;
            if (!proposed.Equals(original)) throw new Exception("Normal-window client bounds changed unexpectedly.");
            int restores = 0; chrome.Restored += () => restores++;
            var size = new IntPtr((400L << 16) | 600);
            SendMessage(window, 0x0005, UIntPtr.Zero, size);
            SendMessage(window, 0x0005, new UIntPtr(2), size);
            if (restores != 0) throw new Exception("Normal/maximized size messages must not request restore motion.");
            SendMessage(window, 0x0005, new UIntPtr(1), IntPtr.Zero);
            SendMessage(window, 0x0005, UIntPtr.Zero, size);
            SendMessage(window, 0x0005, UIntPtr.Zero, size);
            if (restores != 1) throw new Exception("A minimize/restore sequence must request restore motion exactly once.");
            SendMessage(window, NativeMethods.NexusRestoreMessage, UIntPtr.Zero, IntPtr.Zero);
            if (restores != 2) throw new Exception("Explicit Nexus restore requests must remain available.");
            int survivingRestoreObservers = 0;
            chrome.Restored += () => throw new InvalidOperationException("Fixture: stale WinUI restore subscriber");
            chrome.Restored += () => survivingRestoreObservers++;
            // Native subclass callbacks must isolate individual subscribers,
            // rather than propagate through user32 or strand another listener.
            SendMessage(window, NativeMethods.NexusRestoreMessage, UIntPtr.Zero, IntPtr.Zero);
            if (restores != 3 || survivingRestoreObservers != 1 || !chrome.RegionDecorationsEnabled)
                throw new Exception("A throwing restore subscriber lost native notifications or disabled decoration.");
            // Exercise the exact native component against this test-owned HWND.
            // SetWindowRgn and DWM can synchronously send extra position/size
            // notifications. Count all region requests, not just direct setter
            // calls: a real change must increase the count, but need not add
            // exactly one. An identical setting must add zero by itself.
            int requests = chrome.RegionRefreshRequests;
            void ExpectUnchanged(string failure)
            {
                if (chrome.RegionRefreshRequests != requests)
                    throw new Exception(failure
                        + $" (before={requests}, after={chrome.RegionRefreshRequests}).");
            }
            void ExpectChanged(string failure)
            {
                int observed = chrome.RegionRefreshRequests;
                if (observed <= requests)
                    throw new Exception(failure + $" (before={requests}, after={observed}).");
                requests = observed;
            }

            chrome.SetCorners(false, 14);
            ExpectUnchanged("An unchanged window radius requested another GDI region update.");
            chrome.SetCorners(false, 18);
            ExpectChanged("A real corner change did not request native reconciliation.");
            chrome.SetCorners(false, 18);
            ExpectUnchanged("Repeated appearance settings unnecessarily refreshed the native region.");
            var bounds = new ShellRect(0, 0, 240, 160);
            chrome.SetClip(bounds, 22);
            ExpectChanged("An initial custom clip must update native geometry.");
            chrome.SetClip(bounds, 22);
            ExpectUnchanged("Identical clipping settings triggered repeated GDI work.");
            chrome.SetClip(bounds with { Width = 220 }, 22);
            ExpectChanged("Changed frame geometry must refresh native clipping.");
            chrome.SetCorners(true);
            ExpectChanged("High Contrast must remove rounded native regions.");
            chrome.SetCorners(true);
            ExpectUnchanged("Repeated High Contrast appearance should not refresh an unchanged region.");
            chrome.SetFullscreen(true);
            ExpectChanged("A fullscreen transition must reconcile the native window.");
            chrome.SetFullscreen(true);
            ExpectUnchanged("Repeated fullscreen appearance should not refresh native regions.");
            chrome.SetFullscreen(false);
            ExpectChanged("Returning to windowed mode must recover normal native clipping.");

            // Interact only with the fixture HWND: a live sizing edge releases
            // the old region without synchronous paint; completion/cancellation
            // restores one rounded clip with normal repaint semantics.
            foreach (uint finish in new uint[] { 0x0232, 0x001F, 0x0215 })
            {
                Marshal.StructureToPtr(original, data, false);
                SendMessage(window, 0x0231, UIntPtr.Zero, IntPtr.Zero); // WM_ENTERSIZEMOVE
                SendMessage(window, 0x0214, new UIntPtr(1), data);       // WM_SIZING
                if (!chrome.LiveResizeSuspended || chrome.LastRegionRedraw != false)
                    throw new Exception("Live native sizing must release stale clipping without a synchronous GDI repaint.");
                int suspendedRequests = chrome.RegionRefreshRequests;
                for (int frame = 0; frame < 120; frame++)
                    SendMessage(window, 0x0005, UIntPtr.Zero, size); // WM_SIZE burst, test HWND
                if (chrome.RegionRefreshRequests != suspendedRequests)
                    throw new Exception("The sizing drag queued needless GDI clipping reconciliation during intermediate WM_SIZE notifications.");
                SendMessage(window, finish, UIntPtr.Zero, IntPtr.Zero);
                if (chrome.LiveResizeSuspended || chrome.LastRegionRedraw != true)
                    throw new Exception($"Native sizing completion/cancellation {finish:X4} did not restore repainted clipping.");
            }

            chrome.UseSystemFrame();
            Marshal.StructureToPtr(original, data, false);
            SendMessage(window, 0x0083, UIntPtr.Zero, data);
            var native = Marshal.PtrToStructure<ShellLayerInterop.Rect>(data);
            if (native.Top <= original.Top || native.Bottom > original.Bottom)
                throw new Exception("Windows title-bar fallback must restore native non-client calculation.");
            Console.WriteLine("PASS: production NCCALCSIZE pixel preservation and Windows-frame fallback (own native fixture).");
            Console.WriteLine("PASS: production native restore notification semantics (sent WM_SIZE and Nexus restore messages on own fixture; no physical input).");
        }
        finally { Marshal.FreeHGlobal(data); }
    }
}
