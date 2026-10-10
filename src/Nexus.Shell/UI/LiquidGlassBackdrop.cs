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
    private SystemBackdropConfiguration? _defaultConfiguration;
    private bool _connected, _failed;
    internal event Action? Failed;
    private Windows.UI.Color _tint = ShellTheme.Color("FF122039");
    internal void SetTint(Windows.UI.Color color)
    { _tint = color; try { if (_controller is not null) { _controller.TintColor = color; _controller.FallbackColor = color; } } catch (Exception error) { Fail(error); } }
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        try
        {
            base.OnTargetConnected(connectedTarget, xamlRoot);
            if (!DesktopAcrylicController.IsSupported()) { Fail(new NotSupportedException("Desktop acrylic is unavailable.")); return; }
            _defaultConfiguration = GetDefaultSystemBackdropConfiguration(connectedTarget, xamlRoot);
            _configuration = new SystemBackdropConfiguration { IsInputActive = true, Theme = _defaultConfiguration.Theme };
            _controller = new DesktopAcrylicController { TintColor = _tint, FallbackColor = _tint, TintOpacity = .24f, LuminosityOpacity = .18f };
            _controller.SetSystemBackdropConfiguration(_configuration);
            _controller.AddSystemBackdropTarget(connectedTarget);
            _connected = true;
        }
        catch (Exception error) { Fail(error); }
    }
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        _connected = false; _defaultConfiguration = null; _configuration = null;
        try { _controller?.RemoveSystemBackdropTarget(disconnectedTarget); } catch (Exception error) { Services.Log.Write("Glass target already disconnected", error); }
        ReleaseController();
        try { base.OnTargetDisconnected(disconnectedTarget); } catch (Exception error) { Services.Log.Write("Glass base target already disconnected", error); }
    }
    protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        // Win10 can deliver a configuration notification during detach. The
        // 2.0 base override throws E_INVALIDARG for that target. Read the cached
        // configuration only while attached; no native target lookup/re-entry.
        if (!_connected || _failed) return;
        try { if (_configuration is not null && _defaultConfiguration is not null) _configuration.Theme = _defaultConfiguration.Theme; }
        catch (Exception error) { Fail(error); }
    }
    private void ReleaseController()
    { var controller = _controller; _controller = null; try { controller?.Dispose(); } catch (Exception error) { Services.Log.Write("Glass controller cleanup failed", error); } }
    private void Fail(Exception error)
    {
        _connected = false; ReleaseController(); _configuration = _defaultConfiguration = null;
        if (_failed) return; _failed = true; Services.Log.Write("Liquid glass used the solid fallback", error);
        try { Failed?.Invoke(); } catch (Exception fallbackError) { Services.Log.Write("Glass fallback notification failed", fallbackError); }
    }
}
