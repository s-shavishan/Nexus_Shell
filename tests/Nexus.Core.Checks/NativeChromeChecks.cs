using System.Runtime.InteropServices;
using Nexus.Shell.Interop;

internal static class NativeChromeChecks
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NcCalc { public ShellLayerInterop.Rect Proposed, OldWindow, OldClient; public IntPtr Position; }
    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
    private static extern IntPtr SendMessage(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);

    // Called only on Windows using DesktopNativeChecks' own test HWND.
    internal static void Run(IntPtr window)
    {
        using var chrome = new WindowChrome(window, resizable: true);
        var original = new ShellLayerInterop.Rect { Left = 100, Top = 100, Right = 700, Bottom = 500 };
        IntPtr data = Marshal.AllocHGlobal(Marshal.SizeOf<NcCalc>());
        try
        {
            Marshal.StructureToPtr(new NcCalc { Proposed = original, OldWindow = original, OldClient = original }, data, false);
            if (SendMessage(window, 0x0083, new UIntPtr(1), data) != IntPtr.Zero)
                throw new Exception("Nexus NCCALCSIZE must preserve client pixels instead of requesting WVR_REDRAW.");
            var proposed = Marshal.PtrToStructure<NcCalc>(data).Proposed;
            if (!proposed.Equals(original)) throw new Exception("Normal-window client bounds changed unexpectedly.");
            chrome.UseSystemFrame();
            Marshal.StructureToPtr(original, data, false);
            SendMessage(window, 0x0083, UIntPtr.Zero, data);
            var native = Marshal.PtrToStructure<ShellLayerInterop.Rect>(data);
            if (native.Top <= original.Top || native.Bottom > original.Bottom)
                throw new Exception("Windows title-bar fallback must restore native non-client calculation.");
            Console.WriteLine("PASS: production NCCALCSIZE pixel preservation and Windows-frame fallback (own native fixture).");
        }
        finally { Marshal.FreeHGlobal(data); }
    }
}
