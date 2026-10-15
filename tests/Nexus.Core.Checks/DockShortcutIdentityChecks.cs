using Nexus.Shell.Services;

internal static class DockShortcutIdentityChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool pass, string message)
        { checks++; if (!pass) throw new InvalidOperationException(message); }
        static DockShortcutIdentityCache.Request Start(DockShortcutIdentityCache cache, string target, long now)
            => cache.TryBegin(target, now) ?? throw new InvalidOperationException("Expected a shortcut lookup slot.");

        // Completion must survive without ANY dispatcher callback. This is
        // the rejected-TryEnqueue production path, not a fake successful UI run.
        using (var cache = new DockShortcutIdentityCache())
        {
            cache.Reconcile(["Browser.lnk", "Editor.lnk", "Tools.lnk"]);
            Check(cache.TryBegin("Unpinned.lnk", 0) is null, "Unpinned shortcuts cannot consume a worker.");
            var browser = Start(cache, "Browser.lnk", 0);
            var editor = Start(cache, "Editor.lnk", 0);
            Check(cache.TryBegin("BROWSER.LNK", 0) is null, "Target casing cannot create duplicate STA work.");
            Check(cache.TryBegin("Tools.lnk", 0) is null, "Only two native lookups may be in flight.");
            Task.Run(() => cache.Publish(browser, @"C:\Apps\browser.exe")).GetAwaiter().GetResult();
            Check(!cache.TryGetTarget("Browser.lnk", out _), "Workers must not change dispatcher-owned identity state.");
            Check(cache.Drain(100) && cache.TryGetTarget("BROWSER.LNK", out var resolved)
                && resolved == @"C:\Apps\browser.exe", "A heartbeat must recover success after a rejected UI wake-up.");
            Check(cache.TryBegin("Browser.lnk", 100) is null, "Successful identities must not be reread on every heartbeat.");
            var tools = Start(cache, "Tools.lnk", 100);
            cache.Publish(editor, null);
            Check(!cache.Drain(100) && cache.TryBegin("Editor.lnk", 2099) is null,
                "Failed completion must free its slot and retain the two-second backoff.");
            var editorRetry = Start(cache, "Editor.lnk", 2100);
            cache.Publish(editorRetry, "not-an-executable.txt");
            Check(!cache.Drain(2100) && !cache.TryGetTarget("Editor.lnk", out _)
                && cache.TryBegin("Editor.lnk", 12099) is null,
                "Invalid shortcut targets must remain unresolved and increase retry cooldown.");
            _ = Start(cache, "Editor.lnk", 12100);
            cache.Publish(tools, @"C:\Apps\tools.EXE");
            Check(cache.Drain(12101), "A second pin must recover while another pin is retrying.");
        }

        using (var cache = new DockShortcutIdentityCache())
        {
            cache.Reconcile(["Browser.lnk"]);
            var removed = Start(cache, "Browser.lnk", 0);
            cache.Reconcile([]);
            cache.Reconcile(["Browser.lnk"]);
            Check(cache.TryBegin("Browser.lnk", 1) is null,
                "Unpin/re-pin must not release the old native worker before it finishes.");
            cache.Publish(removed, @"C:\Old\browser.exe");
            Check(!cache.Drain(2) && !cache.TryGetTarget("Browser.lnk", out _),
                "A removed pin's late result cannot claim a newly pinned shortcut at the same path.");
            var current = Start(cache, "Browser.lnk", 2);
            cache.Publish(removed, @"C:\Old\browser.exe");
            Check(!cache.Drain(3) && cache.TryBegin("Browser.lnk", 3) is null,
                "An old duplicate completion cannot release the new request's slot.");
            cache.Publish(current, @"C:\New\browser.exe");
            Check(cache.Drain(4) && cache.TryGetTarget("Browser.lnk", out var target)
                && target == @"C:\New\browser.exe", "The replacement pin must accept only its current request.");
            cache.Reconcile(["BROWSER.LNK"]);
            Check(cache.TryGetTarget("Browser.lnk", out _), "Ordinary pin metadata/casing refresh must retain successful work.");
            cache.Reconcile([]);
            Check(!cache.TryGetTarget("Browser.lnk", out _), "Unpin must evict successful identities as well as failures.");
        }

        using (var cache = new DockShortcutIdentityCache())
        {
            cache.Reconcile(["Browser.lnk", "Editor.lnk", "Tools.lnk"]);
            var beforeRescan = Start(cache, "Browser.lnk", 0);
            var finished = Start(cache, "Editor.lnk", 0);
            cache.Publish(finished, @"C:\Old\editor.exe");
            Check(cache.Drain(1), "Rescan fixture must start with a cached successful lookup.");
            cache.Reconcile(["Browser.lnk", "Editor.lnk", "Tools.lnk"], invalidate: true);
            Check(!cache.TryGetTarget("Editor.lnk", out _) && cache.TryBegin("Browser.lnk", 2) is null,
                "Manual rescan evicts success but retains real in-flight reservations.");
            var newEditor = Start(cache, "Editor.lnk", 2);
            Check(cache.TryBegin("Tools.lnk", 2) is null, "Rescan cannot bypass the two-worker bound.");
            cache.Publish(beforeRescan, @"C:\Old\browser.exe");
            cache.Publish(newEditor, @"C:\New\editor.exe");
            Check(cache.Drain(3) && !cache.TryGetTarget("Browser.lnk", out _)
                && cache.TryGetTarget("Editor.lnk", out var target) && target == @"C:\New\editor.exe",
                "Only lookups admitted after rescan may replace the refreshed identity cache.");
            _ = Start(cache, "Browser.lnk", 3);
            _ = Start(cache, "Tools.lnk", 3);
        }

        // Exercise the real mailbox from background threads across repeated
        // removal/rescan generations, with stale and duplicate completions.
        using (var cache = new DockShortcutIdentityCache())
        {
            for (int generation = 0; generation < 200; generation++)
            {
                cache.Reconcile(["One.lnk", "Two.lnk", "Three.lnk"], invalidate: true);
                var one = Start(cache, "One.lnk", generation);
                var two = Start(cache, "Two.lnk", generation);
                Check(cache.TryBegin("Three.lnk", generation) is null, "Recovery bursts must preserve the worker cap.");
                if (generation % 2 == 0) cache.Reconcile(["Two.lnk", "Three.lnk"]);
                Task.WhenAll(Task.Run(() => cache.Publish(one, @"C:\Apps\one.exe")),
                    Task.Run(() => cache.Publish(two, @"C:\Apps\two.exe"))).GetAwaiter().GetResult();
                Check(cache.Drain(generation)
                    && cache.TryGetTarget("One.lnk", out _) == (generation % 2 != 0)
                    && cache.TryGetTarget("Two.lnk", out _), "Background completions must respect each current pin generation.");
                Check(!cache.Drain(generation), "A stable heartbeat must not repeat completed identity writes.");
                var three = Start(cache, "Three.lnk", generation);
                cache.Publish(one, @"C:\Stale\one.exe");
                Check(!cache.Drain(generation), "Duplicate results must not revive removed pins or overwrite current results.");
                Check(cache.TryBegin("Three.lnk", generation) is null,
                    "A request remains reserved until its actual completion.");
                cache.Publish(three, @"C:\Apps\three.exe");
                Check(cache.Drain(generation) && cache.TryGetTarget("Three.lnk", out _),
                    "Each completed worker must release admission for the next recovery generation.");
            }
        }

        var disposed = new DockShortcutIdentityCache();
        disposed.Reconcile(["Late.lnk"]);
        var late = Start(disposed, "Late.lnk", 0);
        disposed.Dispose();
        Task.Run(() => disposed.Publish(late, @"C:\Apps\late.exe")).GetAwaiter().GetResult();
        disposed.Reconcile(["Late.lnk"]);
        Check(!disposed.Drain(1) && !disposed.TryGetTarget("Late.lnk", out _)
            && disposed.TryBegin("Late.lnk", 1) is null, "Shutdown must ignore late results and prevent new STA work.");
        disposed.Dispose();
        Console.WriteLine($"PASS: {checks} dock shortcut completion checks; rejected wake-up recovery, bounded workers, retry cooldown, stale unpin/rescan results, duplicate completion and shutdown.");
    }
}
