namespace Nexus.Shell.Services;

// UI replies belong to an opening and section, not merely to a matching name.
// Accepted hardware commands still complete; obsolete replies cannot repaint
// a newer opening, restore a cleared error, or bring a hidden panel back.
public sealed class SurfaceSession
{
    private long _generation;
    private string _section = "";
    private bool _open;
    public long Open(string section) { _section = section; _open = true; return ++_generation; }
    public long Capture() => _generation;
    public bool Accepts(long generation, string section) => _open && generation == _generation && section == _section;
    public void Hide() { _open = false; ++_generation; }
}
