namespace Nexus.Shell.Services;

// Recognize a standalone Windows-key release. Other key events are observed
// only as held/chord state; their contents are neither stored nor remapped.
public sealed class StartKeyGesture
{
    public const int LeftWindows = 0x5B, RightWindows = 0x5C;
    private readonly bool[] _held = new bool[256];
    private int _others;
    private bool _left, _right, _chord;
    public void Reset() { Array.Clear(_held); _others = 0; _left = _right = _chord = false; }
    public void SeedHeld(int key)
    {
        if (key is LeftWindows or RightWindows) { _left |= key == LeftWindows; _right |= key == RightWindows; _chord = true; }
        else if (key is >= 8 and < 256 && !_held[key]) { _held[key] = true; _others++; }
    }
    public bool Observe(int key, bool down, bool injected = false)
    {
        if (injected) { if (_left || _right) _chord = true; return false; }
        if (key is not (LeftWindows or RightWindows))
        {
            if (key is >= 8 and < 256)
            {
                if (down && !_held[key]) { _held[key] = true; _others++; }
                else if (!down && _held[key]) { _held[key] = false; _others--; }
            }
            if (_left || _right) _chord = true;
            return false;
        }
        bool wasDown = key == LeftWindows ? _left : _right;
        if (down)
        {
            if (!wasDown) { _chord = _left || _right || _others != 0; if (key == LeftWindows) _left = true; else _right = true; }
            return false;
        }
        if (key == LeftWindows) _left = false; else _right = false;
        bool invoke = wasDown && !_left && !_right && !_chord && _others == 0;
        if (!_left && !_right) _chord = false;
        return invoke;
    }
}
