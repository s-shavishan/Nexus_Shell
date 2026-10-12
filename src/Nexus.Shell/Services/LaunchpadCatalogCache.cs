using System.Globalization;
using Nexus.Shell.Models;

namespace Nexus.Shell.Services;

// A view-owned, in-memory presentation cache. No scan, persisted index or
// background work. The owner invalidates state-derived results on Session.Changed.
public sealed class LaunchpadCatalogCache
{
    private readonly AppEntry[] _builtins;
    private AppEntry[] _pins = [], _discovered = [];
    private ShellState? _filteredState;
    private (string Section, string Query, string Culture)? _filterKey;
    private IReadOnlyList<AppEntry> _filtered = [];
    public IReadOnlyList<AppEntry> Catalog { get; private set; }

    public LaunchpadCatalogCache(IEnumerable<AppEntry> builtins)
    {
        _builtins = builtins.ToArray();
        Catalog = LaunchpadCatalog.Build(_builtins, [], []);
    }
    public bool Update(IEnumerable<AppEntry> pins, IReadOnlyList<AppEntry>? discovered = null)
    {
        var next = discovered ?? _discovered;
        if (_pins.SequenceEqual(pins) && _discovered.SequenceEqual(next)) return false;
        _pins = pins.ToArray(); _discovered = next.ToArray();
        Catalog = LaunchpadCatalog.Build(_builtins, _pins, _discovered);
        Invalidate(); return true;
    }
    public void Invalidate() { _filteredState = null; _filterKey = null; _filtered = []; }
    public IReadOnlyList<AppEntry> Filter(ShellState state, string section, string query)
    {
        Update(state.PinnedApps);
        var key = (section, query.Trim(), CultureInfo.CurrentCulture.Name);
        if (ReferenceEquals(_filteredState, state) && _filterKey == key) return _filtered;
        _filtered = DesktopOrganization.Apps(Catalog, state, section, key.Item2);
        _filteredState = state; _filterKey = key; return _filtered;
    }
}
