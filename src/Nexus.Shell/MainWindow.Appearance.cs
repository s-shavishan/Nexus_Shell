using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Services;
using Nexus.Shell.UI;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    private AcrylicBrush? _auraGlass;
    private bool _glassUnavailable;
    private static Windows.UI.Color AuraColorValue(string hex) => ShellTheme.Color(hex);
    private static LinearGradientBrush AuraGradient(string first, string last) => ShellTheme.Gradient(first, last);
    private void ApplyAuraPalette()
    {
        var theme = _environment.Theme;
        theme.Apply(_state.Wallpaper);
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
        if (!_state.NativeGlass || _state.ReducedEffects || _highContrast || !_isActive || _glassUnavailable)
        { _auraGlass = null; return; }
        try
        {
            _auraGlass ??= new AcrylicBrush();
            _auraGlass.TintColor = _auraGlass.FallbackColor = AuraColorValue("FF" + palette.Panel[2..]);
            _auraGlass.TintOpacity = palette.IsLight ? .74 : .70;
            _auraGlass.TintLuminosityOpacity = palette.IsLight ? .84 : .55;
            _auraGlass.AlwaysUseFallback = false;
        }
        catch (Exception ex)
        { _auraGlass = null; _glassUnavailable = true; Log.Write("Sections glass unavailable; using the solid theme", ex); }
    }
    private void ApplyAuraSurfaces(bool simple)
    {
        Brush panel = _highContrast ? Resource("NexusPanel") : simple ? _solidPanel : (Brush?)_auraGlass ?? Resource("NexusPanel");
        try { HomeBorder.Background = ControlPanel.Background = CommandPanel.Background = panel; }
        catch (Exception ex) when (_auraGlass is not null)
        {
            _auraGlass = null; _glassUnavailable = true;
            Log.Write("Sections material connection failed; using solid surfaces", ex);
            HomeBorder.Background = ControlPanel.Background = CommandPanel.Background = _solidPanel;
        }
        Sidebar.Background = WindowChrome.Background = WindowFooter.Background = Resource(_highContrast ? "NexusPanel" : "NexusSidebar");
        var palette = _environment.Theme.Palette;
        HeroCard.Background = _highContrast ? Resource("NexusCard") : simple ? _solidCard : AuraGradient(palette.HeroStart, palette.HeroEnd);
        UpdateNavigation();
        GlassStatus.Text = _glassUnavailable ? "Solid fallback · native glass is unavailable on this system."
            : _state.NativeGlass ? "Glass follows Windows availability, high contrast, reduced effects and window focus."
            : "Enable native glass for the Sections window and its floating controls.";
    }
    private void UpdateNavigation()
    {
        foreach (var button in _navigation)
        {
            bool selected = (string)button.Tag == _page;
            button.Background = selected ? _selection : _transparent;
            button.Foreground = Resource(_highContrast && selected ? "NexusAccentText" : selected ? "NexusSelectedText" : "NexusText");
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
