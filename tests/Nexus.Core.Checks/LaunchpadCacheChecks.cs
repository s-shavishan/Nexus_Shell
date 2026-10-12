using System.Globalization;
using Nexus.Shell.Models;
using Nexus.Shell.Services;

internal static class LaunchpadCacheChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
        var builtin = new AppEntry("files", "Files", "nexus:files", "", "System");
        var apps = Enumerable.Range(0, 650).Select(i => new AppEntry("app" + i, "App " + i.ToString("D3"), "C:\\Apps\\" + i + ".exe", "", i % 2 == 0 ? "Utility" : "Game")).ToArray();
        var state = new ShellState { PinnedApps = [], RecentApps = [], HiddenAppTargets = [], AppCollections = [], LaunchpadOrder = "Name" };
        var cache = new LaunchpadCatalogCache([builtin]);
        Check(cache.Update(state.PinnedApps, apps), "A new installed-app snapshot must update the catalog.");
        var catalog = cache.Catalog; var all = cache.Filter(state, "All", "");
        Check(all.SequenceEqual(DesktopOrganization.Apps(LaunchpadCatalog.Build([builtin], [], apps), state, "All", "")), "Caching changed catalog filtering or the 600-item bound.");
        Check(all.Count == 600 && LaunchpadCatalog.Page(all, 0).Count == 24, "Retained data must preserve the bounded catalog and realized page.");
        for (int i = 0; i < 500; i++)
        {
            Check(!cache.Update(state.PinnedApps, apps) && ReferenceEquals(catalog, cache.Catalog), "An unchanged catalog was rebuilt.");
            Check(ReferenceEquals(all, cache.Filter(state, "All", "  ")), "An unchanged query did not reuse its result.");
        }
        foreach (string section in new[] { "All", "Pinned", "Recent", "System", "Tools", "Games", "Hidden", "collection:missing" })
        foreach (string query in new[] { "", "App 00", "no matches" })
            Check(cache.Filter(state, section, query).SequenceEqual(DesktopOrganization.Apps(cache.Catalog, state, section, query)), "Cached section/query routing changed results.");

        state.PinnedApps.Add(apps[20] with { Name = "Pinned name" });
        Check(cache.Filter(state, "Pinned", "").Single().Name == "Pinned name", "Pin changes did not invalidate catalog precedence.");
        state.PinnedApps.Clear();
        Check(cache.Filter(state, "Pinned", "").Count == 0 && cache.Catalog.Single(a => a.Target == apps[20].Target).Name == apps[20].Name, "Removing a pin retained its old catalog override.");
        state.HiddenAppTargets.Add(apps[0].Target); cache.Invalidate();
        Check(cache.Filter(state, "All", "").All(a => a.Target != apps[0].Target), "State invalidation retained a newly hidden app.");
        Check(cache.Filter(state, "Hidden", "").Single().Target == apps[0].Target, "Hidden routing failed after invalidation.");
        state.AppCollections.Add(new("tools", "Tools", [apps[1].Target])); cache.Invalidate();
        Check(cache.Filter(state, "collection:tools", "").Single().Target == apps[1].Target, "Collection membership was stale.");
        state.AppCollections[0].Targets.Add(apps[2].Target); cache.Invalidate();
        Check(cache.Filter(state, "collection:tools", "").Count == 2, "Mutable collection changes were not refreshed.");
        state.RecentApps.Add(apps[3]); state.RememberRecentItems = true; cache.Invalidate();
        Check(cache.Filter(state, "Recent", "").Count == 1, "Recent items disappeared while enabled.");
        state.RememberRecentItems = false; state.RecentApps.Clear(); cache.Invalidate();
        Check(cache.Filter(state, "Recent", "").Count == 0, "Privacy opt-out retained recent results.");
        state.LaunchpadOrder = "Catalog"; cache.Invalidate();
        Check(cache.Filter(state, "All", "").First().Target == builtin.Target, "Ordering changes were not refreshed.");
        var changed = apps.ToArray(); changed[4] = changed[4] with { Name = "Renamed app" };
        Check(cache.Update(state.PinnedApps, changed) && cache.Catalog.Any(a => a.Name == "Renamed app"), "Catalog refresh retained stale names.");
        changed[4] = changed[4] with { Name = "Caller mutated snapshot" };
        Check(cache.Catalog.Any(a => a.Name == "Renamed app"), "Caller mutation changed the retained snapshot.");
        Check(cache.Update(state.PinnedApps, changed) && cache.Catalog.Any(a => a.Name == "Caller mutated snapshot"), "Explicitly updating an edited snapshot was ignored.");
        Check(cache.Update(state.PinnedApps, []) && cache.Catalog.Count == 1, "An empty installed-app refresh retained removed apps.");

        var culture = CultureInfo.CurrentCulture;
        try
        {
            cache.Update([], [new("i", "FILE", "file.exe", "", "Utility")]);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var english = cache.Filter(state, "All", "i");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var turkish = cache.Filter(state, "All", "i");
            Check(!ReferenceEquals(english, turkish) && turkish.SequenceEqual(DesktopOrganization.Apps(cache.Catalog, state, "All", "i")), "Culture-dependent search reused the wrong result.");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        var other = new ShellState { PinnedApps = [], HiddenAppTargets = ["nexus:files"], AppCollections = [], RecentApps = [] };
        Check(cache.Filter(other, "All", "").All(a => a.Target != builtin.Target), "A different session reused another state's results.");
        Console.WriteLine($"PASS: {checks} Launchpad cache assertions; unchanged renders, bounded pages, pins, hidden apps, mutable collections, privacy, refresh, snapshot isolation and culture changes.");
    }
}
