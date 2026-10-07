using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Services;
using Nexus.Shell.UI;

namespace Nexus.Shell;

public sealed partial class MainWindow
{
    private AcrylicBrush? _auraGlass;
    private ThemeShadow? _workspaceShadow;
    private bool _glassUnavailable;
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
        DesktopRoot.RequestedTheme = _highContrast ? ElementTheme.Default : palette.Name == "Opal" ? ElementTheme.Light : ElementTheme.Dark;
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
        // Windows preferences remain the authority for high-contrast resources.
        if (_highContrast) _selection.Color = ((SolidColorBrush)Resource("NexusAccent")).Color;
        ConfigureAuraGlass(palette);
    }
    private void ConfigureAuraGlass(AuraPalette palette)
    {
        if (!_state.NativeGlass || _state.ReducedEffects || _highContrast || !_isActive || _glassUnavailable)
        { _auraGlass = null; return; }
        try
        {
            _auraGlass ??= new AcrylicBrush();
            _auraGlass.TintOpacity = palette.Name == "Opal" ? .62 : .68;
            _auraGlass.TintLuminosityOpacity = palette.Name == "Opal" ? .82 : .52;
            _auraGlass.TintColor = AuraColorValue("FF" + palette.Panel[2..]);
            _auraGlass.FallbackColor = AuraColorValue("FF" + palette.Panel[2..]);
            _auraGlass.AlwaysUseFallback = false;
        }
        catch (Exception ex)
        {
            _auraGlass = null; _glassUnavailable = true;
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
            HomeBorder.Translation = new(0, 0, simple ? 0 : 24);
        }
        catch (Exception ex) { Log.Write("Workspace shadow unavailable", ex); HomeBorder.Shadow = null; }
        foreach (var surface in _surfaceDefaults)
            surface.Key.Background = _highContrast ? Resource("NexusPanel") : simple ? _solidPanel
                : Resource(surface.Key == SpaceCard ? "NexusCard" : "NexusShell");
        HeroCard.Background = _highContrast ? Resource("NexusCard") : simple ? _solidCard
            : AuraGradient(AuraPalette.For(_state.Wallpaper).HeroStart, AuraPalette.For(_state.Wallpaper).HeroEnd);
        Brush floating = _highContrast ? Resource("NexusPanel") : simple ? _solidPanel : (Brush?)_auraGlass ?? Resource("NexusShell");
        try
        {
            MenuBar.Background = floating; DockBorder.Background = floating;
            ControlPanel.Background = floating; CommandPanel.Background = floating;
            HomeBorder.Background = floating;
            foreach (var widget in new[] { DesktopClockCard, SpaceCard, DesktopFocusCard, DesktopWorkspaceCard })
            {
                widget.Background = floating;
                widget.Shadow = simple ? null : _workspaceShadow;
                widget.Translation = new(0, 0, simple ? 0 : 12);
            }
            Sidebar.Background = _highContrast ? Resource("NexusPanel") : simple ? _solidPanel : Resource("NexusSidebar");
            WindowChrome.Background = _highContrast ? Resource("NexusPanel") : simple ? _solidPanel : Resource("NexusSidebar");
        }
        catch (Exception ex) when (_auraGlass is not null)
        {
            _auraGlass = null; _glassUnavailable = true;
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
            button.Foreground = Resource(_highContrast && selected ? "NexusAccentText" : selected ? "NexusAccent" : "NexusText");
        }
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
        dialog.CornerRadius = new CornerRadius(24);
        dialog.PrimaryButtonStyle = (Style)Application.Current.Resources["AuraPrimaryButton"];
        dialog.CloseButtonStyle = (Style)Application.Current.Resources["AuraSurfaceButton"];
        dialog.Resources["ContentDialogBackground"] = Resource("NexusPanel");
        dialog.Resources["ContentDialogBorderBrush"] = Resource("NexusBorder");
    }
}
