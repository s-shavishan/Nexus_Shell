using System.Runtime.InteropServices;
using Nexus.Runtime.Transfers;
using Nexus.Shell.Interop;

// Explicit opt-in: this fixture replaces the test account's clipboard. It never
// runs in normal portable/native IPC checks or on a user's desktop at startup.
internal static class WindowsFileClipboardChecks
{
    internal static void Run()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The clipboard fixture needs Windows.");
        int checks = 0;
        void Check(bool pass, string message) { checks++; if (!pass) throw new Exception(message); }
        IntPtr owner = CreateWindowEx(0, "STATIC", "Nexus clipboard fixture", 0, 0, 0, 1, 1, new IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (owner == IntPtr.Zero) throw new Exception("Could not create the fixture's clipboard-owner window.");
        string root = Path.Combine(Path.GetTempPath(), "Nexus-native-clipboard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "資料 with spaces.txt"), folder = Path.Combine(root, "Folder");
        File.WriteAllText(file, "Native clipboard fixture"); Directory.CreateDirectory(folder);
        uint ownedSequence = 0;
        try
        {
            FileClipboardService.Write(owner, [file, folder], FileJobKind.Copy);
            Check(FileClipboardService.HasFiles, "Copy must publish native CF_HDROP.");
            var copy = FileClipboardService.Read(owner);
            Check(copy.Sequence != 0 && copy.Kind == FileJobKind.Copy && copy.Paths.SequenceEqual([file, folder]), "Windows must decode Unicode file/folder paths and Copy intent.");
            FileClipboardService.Write(owner, [file], FileJobKind.Move);
            var cut = FileClipboardService.Read(owner);
            Check(cut.Sequence != copy.Sequence && cut.Kind == FileJobKind.Move && cut.Paths.Single() == file, "Cut must publish Preferred DropEffect MOVE without deleting its source.");
            Check(File.Exists(file), "Cut alone cannot mutate source files.");
            FileClipboardService.Write(owner, [folder], FileJobKind.Copy);
            Check(!FileClipboardService.ClearCut(owner, cut.Sequence) && FileClipboardService.Read(owner).Paths.Single() == folder,
                "Completing an old Cut cannot clear a newer clipboard selection.");
            FileClipboardService.Write(owner, [file], FileJobKind.Move);
            uint sequence = FileClipboardService.Sequence;
            Check(!FileClipboardService.ClearCut(owner, 0), "An unknown sequence cannot clear the clipboard.");
            Check(FileClipboardService.ClearCut(owner, sequence) && !FileClipboardService.HasFiles,
                "An acknowledged Cut can consume only the still-current clipboard under its native lock.");
            FileClipboardService.Write(owner, [file], FileJobKind.Copy);
            ownedSequence = FileClipboardService.Sequence;
            Check(DestroyWindow(owner), "Fixture window must be destroyed on its creating thread."); owner = IntPtr.Zero;
            Check(FileClipboardService.HasFiles, "Eager native file data must survive its owner window closing.");
            owner = CreateWindowEx(0, "STATIC", "Nexus clipboard reader fixture", 0, 0, 0, 1, 1, new IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            Check(FileClipboardService.Read(owner).Paths.Single() == file, "A new window must read the original eager CF_HDROP data after its writer closes.");
            Console.WriteLine($"PASS: {checks} opt-in Windows file-clipboard checks. Manual Windows Explorer round trips remain required.");
        }
        finally
        {
            if (owner != IntPtr.Zero) { FileClipboardService.ClearCut(owner, ownedSequence); DestroyWindow(owner); }
            // CF_HDROP holds names, not open source handles. No source data is
            // ever removed by copying/cutting or clearing these fixture values.
            Directory.Delete(root, true);
        }
    }
    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowEx(uint ex, string kind, string name, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(IntPtr window);
}
