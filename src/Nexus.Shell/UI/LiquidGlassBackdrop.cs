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
    private bool _connected, _failed;
    internal event Action? Failed;
    private Services.GlassRecipe _recipe = Services.GlassRecipe.For(Services.AuraPalette.For("Midnight"));
    private SystemBackdropTheme _theme = SystemBackdropTheme.Dark;
    internal void SetAppearance(Services.GlassRecipe recipe, bool light)
    {
        var next = light ? SystemBackdropTheme.Light : SystemBackdropTheme.Dark;
        if (_recipe == recipe && _theme == next) return;
        _recipe = recipe; _theme = next;
        try { if (_configuration is not null) _configuration.Theme = _theme; ApplyTint(); } catch (Exception error) { Fail(error); }
    }
    private void ApplyTint()
    {
        if (_controller is not { } controller) return;
        controller.TintColor = ShellTheme.Color(_recipe.Tint); controller.FallbackColor = ShellTheme.Color(_recipe.Fallback);
        controller.TintOpacity = _recipe.TintOpacity; controller.LuminosityOpacity = _recipe.LuminosityOpacity;
    }
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        try
        {
            base.OnTargetConnected(connectedTarget, xamlRoot);
            if (!DesktopAcrylicController.IsSupported()) { Fail(new NotSupportedException("Desktop acrylic is unavailable.")); return; }
            // Win10 needs the system dispatcher as well as WinUI's queue.
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().EnsureSystemDispatcherQueue();
            _configuration = new SystemBackdropConfiguration { IsInputActive = true, Theme = _theme };
            _controller = new DesktopAcrylicController { Kind = DesktopAcrylicKind.Thin };
            _controller.SetSystemBackdropConfiguration(_configuration);
            ApplyTint();
            if (!_controller.AddSystemBackdropTarget(connectedTarget)) throw new InvalidOperationException("Windows did not attach the acrylic target.");
            _connected = true;
        }
        catch (Exception error) { Fail(error); }
    }
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        _connected = false; _configuration = null;
        try { _controller?.RemoveSystemBackdropTarget(disconnectedTarget); } catch (Exception error) { Services.Log.Write("Glass target already disconnected", error); }
        ReleaseController();
        try { base.OnTargetDisconnected(disconnectedTarget); } catch (Exception error) { Services.Log.Write("Glass base target already disconnected", error); }
    }
    protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        // Palette updates arrive explicitly from the owner. No native target
        // lookup during configuration/detach notifications on Windows 10.
        if (!_connected || _failed) return;
        try { if (_configuration is not null) _configuration.Theme = _theme; ApplyTint(); }
        catch (Exception error) { Fail(error); }
    }
    private void ReleaseController()
    { var controller = _controller; _controller = null; try { controller?.Dispose(); } catch (Exception error) { Services.Log.Write("Glass controller cleanup failed", error); } }
    private void Fail(Exception error)
    {
        _connected = false; ReleaseController(); _configuration = null;
        if (_failed) return; _failed = true; Services.Log.Write("Liquid glass used the solid fallback", error);
        try { Failed?.Invoke(); } catch (Exception fallbackError) { Services.Log.Write("Glass fallback notification failed", fallbackError); }
    }
}
