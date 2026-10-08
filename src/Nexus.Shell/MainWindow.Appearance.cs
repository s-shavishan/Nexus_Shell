using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Services;
using Nexus.Shell.UI;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    private AcrylicBrush? _auraGlass;
    private AcrylicBrush? _shellGlass, _widgetGlass;
    private ThemeShadow? _workspaceShadow;
    private bool _glassUnavailable;
    private string? _appliedMood;
    private bool _appliedContrast;
    private static Windows.UI.Color AuraColorValue(string hex)
    {
        var color = AuraColor.Parse(hex);
        return Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);
    }
    private static LinearGradientBrush AuraGradient(string first, string last)
    {
        var brush = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 1) };
        brush.GradientStops.Add(new() { Color = AuraColorValue(first), Offset = 0 });
        brush.GradientStops.Add(new() { Color = AuraColorValue(last), Offset = 1 });
        return brush;
    }
    private void ApplyAuraPalette()
    {
        var palette = AuraPalette.For(_state.Wallpaper);
        DesktopRoot.RequestedTheme = _highContrast ? ElementTheme.Default : palette.IsLight ? ElementTheme.Light : ElementTheme.Dark;
        bool colorsChanged = _appliedMood != palette.Name || _appliedContrast != _highContrast;
        if (colorsChanged)
        {
            var resources = Application.Current.Resources;
            foreach (var item in palette.Tokens)
            {
                void Update(ResourceDictionary dictionary)
                {
                    if (dictionary.ContainsKey(item.Key) && dictionary[item.Key] is SolidColorBrush brush)
                        brush.Color = AuraColorValue(item.Value);
                }
                Update(resources);
                foreach (string theme in new[] { "Light", "Dark" }) Update((ResourceDictionary)resources.ThemeDictionaries[theme]);
            }
            _selection.Color = AuraColorValue(palette.Tokens["NexusSelection"]);
            _solidPanel.Color = AuraColorValue("FF" + palette.Panel[2..]);
            _solidCard.Color = AuraColorValue("FF" + palette.Card[2..]);
            _chosenWallpaper = AuraGradient(palette.Canvas, palette.WallpaperEnd);
            HeroCard.Background = AuraGradient(palette.HeroStart, palette.HeroEnd);
            ((AppAccentConverter)DesktopRoot.Resources["AppAccent"]).UseAura(palette, _highContrast,
                _highContrast ? ((SolidColorBrush)Resource("NexusCard")).Color : default);
            AuraPaletteLabel.Text = "NEXUS / " + palette.Name.ToUpperInvariant();
            ApplyWallpaperPalette(palette);
            // Windows preferences remain the authority for high-contrast resources.
            if (_highContrast) _selection.Color = ((SolidColorBrush)Resource("NexusAccent")).Color;
            _appliedMood = palette.Name; _appliedContrast = _highContrast;
        }
        ConfigureAuraGlass(palette);
    }
    private void ConfigureAuraGlass(AuraPalette palette)
    {
        if (!_state.NativeGlass || _state.ReducedEffects || _highContrast || !_isActive || _glassUnavailable)
        { _auraGlass = _shellGlass = _widgetGlass = null; return; }
        try
        {
            AcrylicBrush Material(AcrylicBrush? brush, string tint, double opacity, double luminosity)
            {
                brush ??= new AcrylicBrush();
                brush.TintColor = brush.FallbackColor = AuraColorValue("FF" + tint[2..]);
                brush.TintOpacity = opacity; brush.TintLuminosityOpacity = luminosity;
                brush.AlwaysUseFallback = false;
                return brush;
            }
            _auraGlass = Material(_auraGlass, palette.Panel, palette.IsLight ? .74 : .70, palette.IsLight ? .84 : .55);
            _shellGlass = Material(_shellGlass, palette.Tokens["NexusShell"], palette.IsLight ? .60 : .58, palette.IsLight ? .76 : .50);
            _widgetGlass = Material(_widgetGlass, palette.Card, palette.IsLight ? .82 : .74, palette.IsLight ? .86 : .58);
        }
        catch (Exception ex)
        {
            _auraGlass = _shellGlass = _widgetGlass = null; _glassUnavailable = true;
            Log.Write("Native Aura glass unavailable; using the solid theme", ex);
        }
    }
    private void ApplyAuraSurfaces(bool simple)
    {
        try
        {
            if (!simple && _workspaceShadow is null)
            {
                _workspaceShadow = new ThemeShadow();
                _workspaceShadow.Receivers.Add(WallpaperAccents);
            }
            HomeBorder.Shadow = simple ? null : _workspaceShadow;
            HomeBorder.Translation = new(0, 0, simple ? 0 : 20);
        }
        catch (Exception ex) { Log.Write("Workspace shadow unavailable", ex); HomeBorder.Shadow = null; }
        foreach (var surface in _surfaceDefaults)
            surface.Key.Background = _highContrast ? Resource("NexusPanel") : simple ? _solidPanel
                : Resource(surface.Key == SpaceCard ? "NexusCard" : "NexusShell");
        HeroCard.Background = _highContrast ? Resource("NexusCard") : simple ? _solidCard
            : AuraGradient(AuraPalette.For(_state.Wallpaper).HeroStart, AuraPalette.For(_state.Wallpaper).HeroEnd);
        Brush floating = _highContrast ? Resource("NexusPanel") : simple ? _solidPanel : (Brush?)_auraGlass ?? Resource("NexusShell");
        Brush chrome = _highContrast ? Resource("NexusPanel") : simple ? _solidPanel : (Brush?)_shellGlass ?? Resource("NexusShell");
        Brush widgetMaterial = _highContrast ? Resource("NexusPanel") : simple ? _solidCard : (Brush?)_widgetGlass ?? Resource("NexusCard");
        try
        {
            MenuBar.Background = chrome; DockBorder.Background = chrome;
            ControlPanel.Background = floating; CommandPanel.Background = floating;
            HomeBorder.Background = floating;
            foreach (var widget in new[] { DesktopClockCard, SpaceCard, DesktopFocusCard, DesktopWorkspaceCard })
            {
                widget.Background = widgetMaterial;
                widget.Shadow = simple ? null : _workspaceShadow;
                widget.Translation = new(0, 0, simple ? 0 : 10);
            }
            Sidebar.Background = _highContrast ? Resource("NexusPanel") : simple ? _solidPanel : Resource("NexusSidebar");
            WindowChrome.Background = _highContrast ? Resource("NexusPanel") : simple ? _solidPanel : Resource("NexusSidebar");
        }
        catch (Exception ex) when (_auraGlass is not null)
        {
            _auraGlass = _shellGlass = _widgetGlass = null; _glassUnavailable = true;
            Log.Write("Aura material connection failed; using solid surfaces", ex);
            MenuBar.Background = _solidPanel; DockBorder.Background = _solidPanel;
            ControlPanel.Background = _solidPanel; CommandPanel.Background = _solidPanel;
            HomeBorder.Background = _solidPanel;
            foreach (var widget in new[] { DesktopClockCard, SpaceCard, DesktopFocusCard, DesktopWorkspaceCard })
                widget.Background = _solidPanel;
        }
        AuraFocusPill.Background = _highContrast ? Resource("NexusPanel") : Resource("NexusCard");
        UpdateNavigation();
        GlassStatus.Text = _glassUnavailable ? "Solid fallback · native glass is unavailable on this system."
            : _state.NativeGlass ? "Glass follows Windows availability, high contrast, reduced effects and window focus."
            : "Layered surfaces · enable native glass for the workspace and floating controls.";
    }
    private void UpdateNavigation()
    {
        foreach (var button in _navigation)
        {
            bool selected = (string)button.Tag == _page;
            button.Background = selected ? _selection : _transparent;
            button.Foreground = Resource(_highContrast && selected ? "NexusAccentText" : selected ? "NexusSelectedText" : "NexusText");
        }
        UpdateDockSelection();
    }
    private void Glass_Toggled(object sender, RoutedEventArgs args)
    {
        if (!_ready || _syncingPersonalization) return;
        _state.NativeGlass = GlassSwitch.IsOn; ApplyEffects(); SaveState();
    }
    private void PolishDialog(ContentDialog dialog)
    {
        dialog.Background = Resource("NexusPanel"); dialog.Foreground = Resource("NexusText");
        dialog.BorderBrush = Resource("NexusBorder"); dialog.BorderThickness = new Thickness(1);
        dialog.CornerRadius = new CornerRadius(20);
        dialog.PrimaryButtonStyle = (Style)Application.Current.Resources["AuraPrimaryButton"];
        dialog.CloseButtonStyle = (Style)Application.Current.Resources["AuraSurfaceButton"];
        dialog.SecondaryButtonStyle = (Style)Application.Current.Resources["AuraSurfaceButton"];
        dialog.Resources["ContentDialogBackground"] = Resource("NexusPanel");
        dialog.Resources["ContentDialogBorderBrush"] = Resource("NexusBorder");
    }
}
