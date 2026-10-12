using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Services;
using Nexus.Shell.UI;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    private UI.LiquidGlassBackdrop? _auraGlass;
    private bool _glassUnavailable;
    private bool? _glassRequested;
    private static Windows.UI.Color AuraColorValue(string hex) => ShellTheme.Color(hex);
    private static LinearGradientBrush AuraGradient(string first, string last) => ShellTheme.Gradient(first, last);
    private void ApplyAuraPalette()
    {
        var theme = _environment.Theme;
        theme.Apply(_state.Wallpaper, _state.ReducedEffects, _state.GlassStyle);
        _highContrast = theme.HighContrast; _animationsEnabled = theme.Animations;
        DesktopRoot.RequestedTheme = theme.ElementTheme;
        var palette = theme.Palette;
        _selection.Color = _highContrast ? ((SolidColorBrush)Resource("NexusAccent")).Color : AuraColorValue(palette.Tokens["NexusSelection"]);
        _solidPanel.Color = AuraColorValue("FF" + palette.Panel[2..]);
        _solidCard.Color = AuraColorValue("FF" + palette.Card[2..]);
        ((AppAccentConverter)DesktopRoot.Resources["AppAccent"]).UseAura(palette, _highContrast,
            _highContrast ? ((SolidColorBrush)Resource("NexusCard")).Color : default);
        ConfigureAuraGlass(palette);
    }
    private void ConfigureAuraGlass(AuraPalette palette)
    {
        bool requested = _state.NativeGlass && !_state.ReducedEffects && !_highContrast && _environment.Theme.Transparency;
        if (_glassRequested != requested) { _glassRequested = requested; _glassUnavailable = false; }
        if (!requested || _glassUnavailable)
        { DesktopRoot.Background = _highContrast ? Resource("NexusPanel") : _solidPanel; if (_auraGlass is not null) SystemBackdrop = null; _auraGlass = null; return; }
        try
        {
            if (!Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported()) { _glassUnavailable = true; return; }
            if (_auraGlass is null)
            {
                DesktopRoot.Background = _highContrast ? Resource("NexusPanel") : _solidPanel;
                var source = _auraGlass = new UI.LiquidGlassBackdrop("Sections", _handle);
                source.SetAppearance(GlassRecipe.For(palette, style: _state.GlassStyle), palette.IsLight);
                source.Failed += () => DesktopRoot.DispatcherQueue.TryEnqueue(() =>
                { if (!ReferenceEquals(source, _auraGlass)) return; _glassUnavailable = true; _auraGlass = null; try { DesktopRoot.Background = _highContrast ? Resource("NexusPanel") : _solidPanel; SystemBackdrop = null; if (DesktopRoot.IsLoaded) ApplyAuraSurfaces(true); } catch (Exception error) { Log.Write("Sections glass fallback arrived after close", error); } });
                source.StateChanged += () => DesktopRoot.DispatcherQueue.TryEnqueue(() =>
                { if (ReferenceEquals(source, _auraGlass) && DesktopRoot.IsLoaded) { ApplyAuraSurfaces(false); Log.Write($"Glass[Sections] palette={_environment.Theme.Palette.Name} style={_state.GlassStyle} state={source.State}"); } });
                SystemBackdrop = _auraGlass;
            }
            _auraGlass.SetAppearance(GlassRecipe.For(palette, style: _state.GlassStyle), palette.IsLight);
        }
        catch (Exception error)
        { _auraGlass = null; _glassUnavailable = true; try { DesktopRoot.Background = _highContrast ? Resource("NexusPanel") : _solidPanel; SystemBackdrop = null; } catch (Exception cleanup) { Log.Write("Sections glass cleanup failed", cleanup); } Log.Write("Sections glass unavailable; using solid surfaces", error); }
    }
    private void ApplyAuraSurfaces(bool simple)
    {
        bool glass = _auraGlass?.State == "Active" && !simple;
        var theme = _environment.Theme;
        Brush panel = glass ? theme.Glass("Panel") : _highContrast ? Resource("NexusPanel") : _solidPanel;
        HomeBorder.Background = ControlPanel.Background = CommandPanel.Background = panel;
        DesktopRoot.Background = glass ? theme.Glass("Frame") : _highContrast ? Resource("NexusPanel") : _solidPanel;
        Sidebar.Background = glass ? theme.Glass("Panel") : theme.Surface("Sidebar", simple);
        WindowChrome.Background = WindowFooter.Background = glass ? theme.Glass("Frame") : _highContrast ? Resource("NexusPanel") : _solidPanel;
        HeroCard.Background = _highContrast ? Resource("NexusCard") : simple ? _solidCard : theme.Surface("Hero");
        ApplyOverviewAppearance(); UpdateNavigation();
        GlassStatus.Text = _highContrast ? "High contrast uses Windows system colors."
            : !_environment.Theme.Transparency ? "Windows transparency is off. Nexus uses solid surfaces."
            : _state.ReducedEffects ? "Reduced effects is using solid surfaces."
            : _glassUnavailable ? "Solid fallback · native glass is unavailable on this system."
            : !_state.NativeGlass ? "Enable glass for Nexus windows and desktop controls."
            : _auraGlass?.State == "Active" ? $"{_state.GlassStyle} glass · Windows reports acrylic active."
            : _auraGlass?.State is "Fallback" or "HighContrast" ? "Windows reports a solid backdrop fallback. Check graphics, transparency and power settings."
            : "Glass is preparing for this surface.";
    }
    private void UpdateNavigation()
    {
        foreach (var button in _navigation)
        {
            bool selected = (string)button.Tag == _page;
            button.Background = selected ? _environment.Theme.Surface("Accent") : _transparent;
            button.Foreground = Resource(selected ? "NexusAccentText" : "NexusText");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(button, selected ? "Current page" : "");
        }
    }
    private void Glass_Toggled(object sender, RoutedEventArgs args)
    { if (!_ready || _syncingPersonalization) return; _state.NativeGlass = GlassSwitch.IsOn; ApplyEffects(); SaveState(); }
    private void PolishDialog(ContentDialog dialog)
    {
        dialog.Background = Resource("NexusPanel"); dialog.Foreground = Resource("NexusText");
        dialog.BorderBrush = Resource("NexusBorder"); dialog.BorderThickness = new Thickness(1);
        dialog.CornerRadius = new CornerRadius(20);
        dialog.PrimaryButtonStyle = (Style)Application.Current.Resources["AuraPrimaryButton"];
        dialog.CloseButtonStyle = dialog.SecondaryButtonStyle = (Style)Application.Current.Resources["AuraSurfaceButton"];
        dialog.Resources["ContentDialogBackground"] = Resource("NexusPanel");
        dialog.Resources["ContentDialogBorderBrush"] = Resource("NexusBorder");
    }
}
