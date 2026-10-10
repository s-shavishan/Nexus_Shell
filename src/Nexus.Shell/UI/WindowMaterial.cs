using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Desktop;
using Nexus.Shell.Services;
using Nexus.Shell.Models;

namespace Nexus.Shell.UI;

// Windows owns material composition and its accessibility/battery fallback.
// Each HWND gets its own backdrop; no cross-window shared controller lifetime.
internal sealed class WindowMaterial
{
    private bool _failed, _enabled;
    private bool _observedWindow, _closed;
    private LiquidGlassBackdrop? _backdrop;
    internal void Apply(Window window, Border frame, DesktopEnvironment environment, string role = "Frame")
        => Apply(window, frame, environment.Theme, environment.Session.State, role);
    internal void Apply(Window window, Border frame, ShellTheme theme, ShellState state, string role = "Frame")
    {
        if (_closed) return;
        if (!_observedWindow) { _observedWindow = true; window.Closed += (_, _) => _closed = true; }
        bool glass = !_failed && state.NativeGlass && !state.ReducedEffects && !theme.HighContrast;
        try
        {
            glass = glass && Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported();
            frame.RequestedTheme = theme.ElementTheme;
            if (glass != _enabled)
            {
                _backdrop = glass ? new LiquidGlassBackdrop() : null;
                if (_backdrop is { } source) source.Failed += () => frame.DispatcherQueue.TryEnqueue(() =>
                { if (_closed || !ReferenceEquals(source, _backdrop)) return; _failed = true; _enabled = false; try { window.SystemBackdrop = null; frame.Background = theme.Material(role, false); } catch (Exception error) { Log.Write("Glass fallback arrived after window close", error); } });
                window.SystemBackdrop = _backdrop; _enabled = glass;
            }
            _backdrop?.SetTint(ShellTheme.Color("FF" + theme.Palette.Panel[2..]));
            frame.Background = theme.Material(role, glass);
            frame.BorderBrush = theme.Edge;
        }
        catch (Exception ex)
        {
            _failed = true; _enabled = false;
            try { window.SystemBackdrop = null; frame.Background = theme.Material(role, false); } catch (Exception error) { Log.Write("Window glass fallback cleanup failed", error); }
            Log.Write("Window material unavailable; using the palette fallback", ex);
        }
    }
}
