using Nexus.Shell.Interop;
using Nexus.Shell.Services;

internal static class StartKeyChecks
{
    internal static void Run()
    {
        int count = 0;
        void Check(bool passed, string message) { count++; if (!passed) throw new Exception(message); }
        foreach (int win in new[] { StartKeyGesture.LeftWindows, StartKeyGesture.RightWindows })
        {
            var gesture = new StartKeyGesture();
            Check(!gesture.Observe(win, false), "Unmatched key-up must pass through.");
            Check(!gesture.Observe(win, true) && !gesture.Observe(win, true), "Key-down/repeat must not launch or suppress a chord.");
            Check(gesture.Observe(win, false) && !gesture.Observe(win, false), "A standalone press invokes exactly once on release.");
            foreach (int other in new[] { 0x52, 0x45, 0x44, 0x4C, 0x49, 0x53, 0x10, 0x11, 0x12, 0x25, 0x31, 0x87 })
            {
                gesture.Reset(); gesture.Observe(win, true);
                Check(!gesture.Observe(other, true) && !gesture.Observe(other, false) && !gesture.Observe(win, false), "Win combination was treated as Start.");
                gesture.Observe(other, true); gesture.Observe(win, true); gesture.Observe(other, false);
                Check(!gesture.Observe(win, false), "A key already held before Win must remain a chord.");
            }
            gesture.Reset(); gesture.SeedHeld(0x11); gesture.Observe(win, true);
            Check(!gesture.Observe(win, false), "A modifier held during hook installation must not activate Start.");
            gesture.Reset(); gesture.SeedHeld(win);
            Check(!gesture.Observe(win, false), "A Win key already held during connection must not activate Start.");
            gesture.Reset(); gesture.Observe(win, true); gesture.Observe(0x41, true, injected: true);
            Check(!gesture.Observe(win, false), "Another tool's injected chord must not activate Start.");
            gesture.Reset();
            Check(!gesture.Observe(win, true, injected: true) && !gesture.Observe(win, false, injected: true), "Injected Windows keys must not loop into Launchpad.");
            gesture.Observe(win, true); gesture.Observe(0x4C, true); gesture.Reset();
            gesture.Observe(win, true);
            Check(gesture.Observe(win, false), "Resync after a secure desktop must clear stale held keys.");
        }
        var dual = new StartKeyGesture(); dual.Observe(StartKeyGesture.LeftWindows, true); dual.Observe(StartKeyGesture.RightWindows, true);
        Check(!dual.Observe(StartKeyGesture.LeftWindows, false) && !dual.Observe(StartKeyGesture.RightWindows, false), "Two Windows keys form a chord, not two launches.");
        Check(StartKeyRouter.InputSize == 40 && StartKeyRouter.KeyboardOffset == 8, "Native SendInput layout must include INPUT's full x64 union.");
        Console.WriteLine($"PASS: {count} Start-key checks; standalone left/right Win, repeats, native chords, injected input, secure-desktop resync and native input layout. Actual input routing requires Windows acceptance.");
    }
}
