using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Nexus.Shell.UI;

// Each window owns one controller. XAML initializes/accessibility-updates the
// configuration; Windows can fall back when transparency is unavailable.
internal sealed class LiquidGlassBackdrop : SystemBackdrop
{
    private DesktopAcrylicController? _controller;
    private SystemBackdropConfiguration? _configuration;
    internal event Action? Failed;
    private Windows.UI.Color _tint = ShellTheme.Color("FF122039");
    internal void SetTint(Windows.UI.Color color)
    { _tint = color; if (_controller is not null) { _controller.TintColor = color; _controller.FallbackColor = color; } }
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        try
        {
            base.OnTargetConnected(connectedTarget, xamlRoot);
            if (!DesktopAcrylicController.IsSupported()) { Failed?.Invoke(); return; }
            _configuration = new SystemBackdropConfiguration { IsInputActive = true, Theme = GetDefaultSystemBackdropConfiguration(connectedTarget, xamlRoot).Theme };
            _controller = new DesktopAcrylicController { TintColor = _tint, FallbackColor = _tint, TintOpacity = .24f, LuminosityOpacity = .18f };
            _controller.SetSystemBackdropConfiguration(_configuration);
            _controller.AddSystemBackdropTarget(connectedTarget);
        }
        catch (Exception error) { Services.Log.Write("Liquid glass used the solid fallback", error); _controller?.Dispose(); _controller = null; Failed?.Invoke(); }
    }
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        _controller?.RemoveSystemBackdropTarget(disconnectedTarget); _controller?.Dispose(); _controller = null; _configuration = null;
        base.OnTargetDisconnected(disconnectedTarget);
    }
    protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        base.OnDefaultSystemBackdropConfigurationChanged(target, xamlRoot);
        // Shell surfaces use WS_EX_NOACTIVATE. Keep the material legible and
        // consistent when another app has focus; system accessibility fallback
        // and transparency settings remain controlled by the backdrop engine.
        if (_configuration is not null) _configuration.Theme = GetDefaultSystemBackdropConfiguration(target, xamlRoot).Theme;
    }
}
