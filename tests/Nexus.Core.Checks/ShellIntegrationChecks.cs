using Nexus.Shell.Services;
using Nexus.Shell.Interop;

internal static class ShellIntegrationChecks
{
    internal static void Run()
    {
        int checks = 0; void Check(bool pass, string message) { checks++; if (!pass) throw new InvalidOperationException(message); }
        var ten = WindowsShellPolicy.Route(WindowsShellSurface.Calendar, 19045); var eleven = WindowsShellPolicy.Route(WindowsShellSurface.Calendar, 22000);
        Check(ten.Shortcut == "Win+Alt+D" && eleven.Shortcut == "Win+N", "Calendar must use the separate Windows 10 clock or the combined Windows 11 panel.");
        Check(WindowsShellPolicy.Route(WindowsShellSurface.Notifications, 19045).Shortcut == "Win+A" && WindowsShellPolicy.Route(WindowsShellSurface.Notifications, 26100).Shortcut == "Win+N", "Notifications must not launch Windows 11 Quick Settings.");
        foreach (int build in new[] { 19041, 19045, 22000, 22631, 26100 })
        foreach (var surface in Enum.GetValues<WindowsShellSurface>())
        {
            var route = WindowsShellPolicy.Route(surface, build); var sender = new Sender();
            Check(WindowsShellPolicy.Request(route, sender) == WindowsShellRequestState.Requested, "A fully admitted shortcut should report only Requested.");
            Check(sender.Batches.Single().Count(k => !k.Up) == route.Keys.Length && sender.Batches.Single().GroupBy(k => k.Key).All(g => g.Count() == 2 && !g.First().Up && g.Last().Up), "Every injected key must be released in the same batch.");
            for (int prefix = 1; prefix < route.Keys.Length * 2; prefix++)
            {
                sender = new Sender { Prefix = prefix }; Check(WindowsShellPolicy.Request(route, sender) == WindowsShellRequestState.PartialInput, "Partial shortcut admission must not be described as success.");
                var pressed = new HashSet<ushort>(); foreach (var key in sender.Batches[0].Take(prefix)) { if (key.Up) pressed.Remove(key.Key); else pressed.Add(key.Key); }
                Check(sender.Batches.Count == (pressed.Count == 0 ? 1 : 2) && (pressed.Count == 0 || sender.Batches[1].All(k => k.Up && pressed.Contains(k.Key)) && sender.Batches[1].Length == pressed.Count), "Cleanup may release only the successfully injected prefix; it may never replay the toggle or release unrelated keys.");
            }
            Check(route.Fallback.StartsWith("ms-settings:"), "Each Windows panel requires a usable settings fallback.");
        }
        var missing = new Sender { ShellAvailable = false }; Check(WindowsShellPolicy.Request(ten, missing) == WindowsShellRequestState.ShellUnavailable && missing.Batches.Count == 0, "Missing Explorer must not trigger input injection.");
        var held = new Sender { ModifiersUp = false }; Check(WindowsShellPolicy.Request(eleven, held) == WindowsShellRequestState.KeysHeld && held.Batches.Count == 0, "Held physical modifiers must be preserved.");
        held = new Sender { HeldKey = 0x4E }; Check(WindowsShellPolicy.Request(eleven, held) == WindowsShellRequestState.KeysHeld && held.Batches.Count == 0, "A held shortcut letter must never be released by a synthetic key-up.");
        var denied = new Sender { Prefix = 0 }; Check(WindowsShellPolicy.Request(eleven, denied) == WindowsShellRequestState.InputRejected && denied.Batches.Count == 1, "A denied batch must never be retried.");
        Check(WindowsShellBridge.InputSize == 40, "The x64 INPUT union must include its complete mouse/keyboard union.");
        Check(FileContextPolicy.For("Music.FLAC", false).OpenLabel == "Play" && FileContextPolicy.For("image.PNG", false).OpenWith, "Media and images require file-type-aware open actions.");
        Check(!FileContextPolicy.For("Setup.EXE", false).OpenWith && !FileContextPolicy.For("Installer.msi", false).OpenWith && !FileContextPolicy.For("shortcut.lnk", false).OpenWith, "Executable, installer and shortcut contexts must not offer inappropriate Open with actions.");
        Check(FileContextPolicy.For("folder.jpg", true) is { Folder: true, OpenWith: false, OpenLabel: "Open folder" }, "Folder identity must win over its filename extension.");
        Check(FileContextPolicy.For("unknown.custom", false).OpenWith && FileContextPolicy.For("archive.ZIP", false).OpenLabel == "Open archive", "Unknown documents and archives need their genuine Windows associations and shell options.");
        Console.WriteLine($"PASS: {checks} Windows shell integration checks; dual-version routes, exact-prefix input cleanup, no automatic replay, unavailable Explorer, held keys and file-type contexts. No OS shortcuts injected.");
    }
    private sealed class Sender : IWindowsShortcutSender
    {
        public bool ShellAvailable { get; set; } = true;
        public bool ModifiersUp { get; set; } = true;
        internal int Prefix = int.MaxValue;
        internal ushort HeldKey;
        public bool KeyUp(ushort key) => key != HeldKey;
        internal List<ShellKey[]> Batches = [];
        public int Send(IReadOnlyList<ShellKey> keys) { Batches.Add(keys.ToArray()); return Math.Min(Prefix, keys.Count); }
    }
}
