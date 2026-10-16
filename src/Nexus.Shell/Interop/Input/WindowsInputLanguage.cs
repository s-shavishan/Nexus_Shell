// නමෝ බුද්ධාය 🤍
using System.Globalization;
using System.Runtime.InteropServices;
using Nexus.Shell.Models;

namespace Nexus.Shell.Interop;

internal sealed record WindowsInputLayout(IntPtr Handle, string Code, string Name);

internal static class WindowsInputLanguage
{
    [StructLayout(LayoutKind.Sequential)] internal struct GuiThreadInfo
    {
        public uint Size, Flags;
        public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
        public ShellLayerInterop.Rect CaretRect;
    }
    internal static WindowsInputLayout Describe(IntPtr layout)
    {
        string code = "IME", name = "Windows input method";
        int language = (int)(layout.ToInt64() & 0xFFFF);
        try
        {
            // Transient/custom input IDs are not stable LCIDs; retain the HKL
            // rather than claiming it uniquely identifies a language or IME.
            if (language != 0 && language is not (0x2000 or 0x2400))
            {
                var culture = CultureInfo.GetCultureInfo(language);
                code = culture.ThreeLetterISOLanguageName.ToUpperInvariant(); name = culture.DisplayName;
            }
        }
        catch (CultureNotFoundException) { }
        return new(layout, layout == IntPtr.Zero ? "—" : code, name);
    }
    internal static WindowsInputLayout Current(RunningWindow? target)
    {
        uint thread = 0;
        if (target is not null && NativeMethods.OwnsWindow(target))
            thread = GetWindowThreadProcessId(target.Handle, out _);
        return Describe(GetKeyboardLayout(thread));
    }
    internal static IReadOnlyList<WindowsInputLayout> Installed()
    {
        int count = Math.Clamp(GetKeyboardLayoutList(0, null), 0, 64);
        if (count == 0) return [];
        var layouts = new IntPtr[count];
        int read = Math.Clamp(GetKeyboardLayoutList(count, layouts), 0, count);
        return layouts.Take(read).Where(handle => handle != IntPtr.Zero).Distinct().Select(Describe).ToArray();
    }
    internal static bool Request(RunningWindow target, IntPtr layout)
    {
        if (!NativeMethods.OwnsWindow(target) || !Installed().Any(item => item.Handle == layout)) return false;
        uint thread = GetWindowThreadProcessId(target.Handle, out uint process);
        if (thread == 0 || process != target.ProcessId) return false;
        var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
        IntPtr destination = target.Handle;
        if (GetGUIThreadInfo(thread, ref info) && info.Focus != IntPtr.Zero)
        {
            uint focusThread = GetWindowThreadProcessId(info.Focus, out uint focusProcess);
            if (focusThread == thread && focusProcess == process && (info.Focus == target.Handle || IsChild(target.Handle, info.Focus)))
                destination = info.Focus;
        }
        // Posting cannot hang Nexus on an unresponsive application. Windows
        // integrity checks still apply, and apps may decline this request.
        return NativeMethods.OwnsWindow(target) && PostMessage(destination, 0x0050, UIntPtr.Zero, layout);
    }
    [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint thread);
    [DllImport("user32.dll", SetLastError = true)] private static extern int GetKeyboardLayoutList(int count, [Out] IntPtr[]? layouts);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsChild(IntPtr parent, IntPtr child);
    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(IntPtr window, uint message, UIntPtr wp, IntPtr lp);
}
