using Nexus.Shell.Services;

internal static class ForegroundLookupChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool passed, string failure) { checks++; if (!passed) throw new Exception(failure); }
        var cache = new ForegroundLookupCache<string>();
        var snapshot = new object();
        var hwnd = new IntPtr(1357);
        int discoveries = 0;
        string? Discover()
        {
            discoveries++;
            return "browser-title-1";
        }
        for (int i = 0; i < 1500; i++)
        {
            var found = cache.Resolve(hwnd, snapshot, Discover, _ => true);
            Check(found == "browser-title-1", "Foreground identity must remain stable through repeat polls.");
        }
        Check(discoveries == 1, "A stable foreground HWND should avoid repeated dock-window snapshots.");
        Check(cache.Resolve(new IntPtr(1358), snapshot, () => { discoveries++; return "editor"; }, _ => true) == "editor"
            && discoveries == 2, "Switching the foreground HWND must resolve its process again.");
        Check(cache.Resolve(new IntPtr(1358), new object(), () => { discoveries++; return "editor-new"; }, _ => true) == "editor-new"
            && discoveries == 3, "A new native enumeration must invalidate an otherwise identical foreground handle.");
        Check(cache.Resolve(new IntPtr(1358), new object(), () => { discoveries++; return "new-process"; }, _ => true) == "new-process"
            && discoveries == 4, "Different snapshots must never use stale metadata.");

        var replaced = new object();
        Check(cache.Resolve(hwnd, replaced, () => { discoveries++; return "old-owner"; }, _ => true) == "old-owner",
            "The first lookup for an HWND must read its current identity.");
        Check(cache.Resolve(hwnd, replaced, () => { discoveries++; return "new-owner"; }, _ => false) == "new-owner",
            "A destroyed/reused HWND must not retain the old process identity.");
        Check(discoveries == 6, "Identity validity checks must trigger a fresh native candidate lookup.");

        var absent = new ForegroundLookupCache<string>();
        int missing = 0;
        string? Missing() { missing++; return null; }
        for (int i = 0; i < 20; i++)
            Check(absent.Resolve(hwnd, snapshot, Missing, _ => true) is null,
                "Unrecognized application HWNDs must remain harmless.");
        Check(missing == 20,
            "Unknown foreground handles must be rediscovered, because a utility can register after the first poll.");
        for (int i = 0; i < 500; i++)
            Check(absent.Resolve(IntPtr.Zero, snapshot, Missing, _ => true, safeEmpty: true) is null,
                "The empty foreground is safe to negative-cache.");
        Check(missing == 21, "Stable empty foreground must not allocate repeated dock candidate lists.");

        cache.Reset();
        Check(cache.Resolve(hwnd, replaced, () => { discoveries++; return "after-reset"; }, _ => true) == "after-reset",
            "Lifecycle reset must invalidate the retained foreground identity.");
        var failed = new ForegroundLookupCache<string>();
        bool threw = false;
        try { failed.Resolve(hwnd, snapshot, () => throw new InvalidOperationException("interrupted"), _ => true); }
        catch (InvalidOperationException) { threw = true; }
        Check(threw, "Interrupted discovery should propagate the error to the existing UI event safety boundary.");
        Check(failed.Resolve(hwnd, snapshot, () => "recovered", _ => true) == "recovered",
            "An interrupted scan must not poison subsequent native lookups.");

        Console.WriteLine($"PASS: {checks} foreground memoization checks; 1500 repeat polls, new snapshots, reused HWNDs, negative-cache safety and retry.");
    }
}
