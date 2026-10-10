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
    internal void Apply(Window window, Border frame, DesktopEnvironment environment)
        => Apply(window, frame, environment.Theme, environment.Session.State);
    internal void Apply(Window window, Border frame, ShellTheme theme, ShellState state)
    {
        bool glass = !_failed && state.NativeGlass && !state.ReducedEffects && !theme.HighContrast;
        try
        {
            if (glass != _enabled)
            { window.SystemBackdrop = glass ? new DesktopAcrylicBackdrop() : null; _enabled = glass; }
            frame.Background = glass ? new SolidColorBrush(ShellTheme.Color(theme.Palette.IsLight ? "6AF0F2FA" : "6A101A2F")) : theme.Surface("Sidebar");
        }
        catch (Exception ex)
        { _failed = true; _enabled = false; window.SystemBackdrop = null; frame.Background = theme.Surface("Sidebar"); Log.Write("Window material unavailable; using the palette fallback", ex); }
    }
}
