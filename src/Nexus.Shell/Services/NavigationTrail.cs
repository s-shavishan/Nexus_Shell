namespace Nexus.Shell.Services;

/// <summary>A bounded, in-session page history. Rebuilding a page adds no entry.</summary>
public sealed class NavigationTrail
{
    private readonly List<string> _pages = [];
    private int _index = -1;
    public const int Capacity = 24;
    public bool CanGoBack => _index > 0;
    public bool CanGoForward => _index >= 0 && _index < _pages.Count - 1;
    public string? Current => _index >= 0 ? _pages[_index] : null;
    public int Count => _pages.Count;

    public void Visit(string page)
    {
        if (string.IsNullOrWhiteSpace(page) || page == Current) return;
        if (CanGoForward) _pages.RemoveRange(_index + 1, _pages.Count - _index - 1);
        _pages.Add(page);
        if (_pages.Count > Capacity) _pages.RemoveAt(0);
        _index = _pages.Count - 1;
    }
    public string? Back() => CanGoBack ? _pages[--_index] : null;
    public string? Forward() => CanGoForward ? _pages[++_index] : null;
}
