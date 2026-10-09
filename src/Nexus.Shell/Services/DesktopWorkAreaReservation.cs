namespace Nexus.Shell.Services;

// Remember our intended geometry, not Explorer's independently reported area.
// Repeated desktop refreshes must never become repeated global layout writes.
public sealed class DesktopWorkAreaReservation
{
    private ShellRect? _applied;
    public bool Apply(ShellRect area, Action<ShellRect> write)
    {
        if (_applied == area) return false;
        write(area);
        _applied = area;
        return true;
    }
    public void Invalidate() => _applied = null;
}
