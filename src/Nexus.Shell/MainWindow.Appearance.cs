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
    private static Windows.UI.Color AuraColorValue(string hex) => ShellTheme.Color(hex);
    private static LinearGradientBrush AuraGradient(string first, string last) => ShellTheme.Gradient(first, last);
    private void ApplyAuraPalette()
    {
        var theme = _environment.Theme;
        theme.Apply(_state.Wallpaper, _state.ReducedEffects);
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
        if (!_state.NativeGlass || _state.ReducedEffects || _highContrast || _glassUnavailable)
        { if (_auraGlass is not null) SystemBackdrop = null; _auraGlass = null; return; }
        try
        {
            if (!Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported()) { _glassUnavailable = true; return; }
            if (_auraGlass is null)
            {
                var source = _auraGlass = new UI.LiquidGlassBackdrop();
                source.Failed += () => DesktopRoot.DispatcherQueue.TryEnqueue(() =>
                { if (!ReferenceEquals(source, _auraGlass)) return; _glassUnavailable = true; _auraGlass = null; try { SystemBackdrop = null; if (DesktopRoot.IsLoaded) ApplyAuraSurfaces(true); } catch (Exception error) { Log.Write("Sections glass fallback arrived after close", error); } });
                SystemBackdrop = _auraGlass;
            }
            _auraGlass.SetTint(AuraColorValue("FF" + palette.Panel[2..]));
        }
        catch (Exception error)
        { _auraGlass = null; _glassUnavailable = true; try { SystemBackdrop = null; } catch (Exception cleanup) { Log.Write("Sections glass cleanup failed", cleanup); } Log.Write("Sections glass unavailable; using solid surfaces", error); }
    }
    private void ApplyAuraSurfaces(bool simple)
    {
        bool glass = _auraGlass is not null && !simple;
        var theme = _environment.Theme;
        Brush panel = glass ? theme.Glass("Panel") : _highContrast ? Resource("NexusPanel") : _solidPanel;
        HomeBorder.Background = ControlPanel.Background = CommandPanel.Background = panel;
        DesktopRoot.Background = glass ? theme.Glass("Frame") : Resource("NexusPanel");
        Sidebar.Background = glass ? theme.Glass("Panel") : theme.Surface("Sidebar", simple);
        WindowChrome.Background = WindowFooter.Background = glass ? theme.Glass("Frame") : Resource("NexusPanel");
        HeroCard.Background = _highContrast ? Resource("NexusCard") : simple ? _solidCard : theme.Surface("Hero");
        ApplyOverviewAppearance(); UpdateNavigation();
        GlassStatus.Text = _glassUnavailable ? "Solid fallback · native glass is unavailable on this system."
            : _state.NativeGlass ? "Liquid glass follows Windows transparency and accessibility settings." : "Enable liquid glass for Nexus windows and desktop controls.";
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
