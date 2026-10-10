using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;

namespace Nexus.Shell.UI;

internal sealed class ShellTheme
{
    public AuraPalette Palette { get; private set; } = AuraPalette.For("Midnight");
    public bool HighContrast { get; private set; }
    public bool Animations { get; private set; } = true;
    public bool Simple { get; private set; }
    public bool Transparency { get; private set; } = true;
    private Windows.UI.ViewManagement.UISettings? _settings;
    private readonly Dictionary<string, Brush> _surfaces = [];
    public ElementTheme ElementTheme => HighContrast ? ElementTheme.Default : Palette.IsLight ? ElementTheme.Light : ElementTheme.Dark;
    public bool Apply(string mood, bool simple = false)
    {
        bool contrast = HighContrast, animations = Animations, transparency = Transparency;
        if (NativeMethods.TryGetHighContrast(out bool value)) contrast = value;
        if (NativeMethods.TryGetAnimationsEnabled(out bool animation)) Animations = animation;
        try { _settings ??= new(); transparency = _settings.AdvancedEffectsEnabled; } catch { }
        var palette = AuraPalette.For(mood);
        bool changed = palette.Name != Palette.Name || contrast != HighContrast || animations != Animations || transparency != Transparency || simple != Simple || !_initialized;
        if (palette.Name != Palette.Name || !_initialized) _surfaces.Clear();
        Palette = palette; HighContrast = contrast; Simple = simple; Transparency = transparency;
        if (!changed) return false;
        var resources = Application.Current.Resources;
        foreach (var token in palette.Tokens)
        {
            void Update(ResourceDictionary dictionary)
            { if (dictionary.ContainsKey(token.Key) && dictionary[token.Key] is SolidColorBrush brush) brush.Color = Color(token.Value); }
            Update(resources);
            foreach (string name in new[] { "Light", "Dark" }) Update((ResourceDictionary)resources.ThemeDictionaries[name]);
        }
        void UpdateAction(ResourceDictionary dictionary)
        {
            if (dictionary.ContainsKey("NexusAction") && dictionary["NexusAction"] is LinearGradientBrush brush)
            { brush.GradientStops[0].Color = Color(palette.Accent); brush.GradientStops[1].Color = Color(simple ? palette.Accent : palette.AccentEnd); }
        }
        UpdateAction(resources);
        foreach (string name in new[] { "Light", "Dark" }) UpdateAction((ResourceDictionary)resources.ThemeDictionaries[name]);
        _initialized = true; return true;
    }
    private bool _initialized;
    public Brush Brush(string key) => (Brush)((ResourceDictionary)Application.Current.Resources.ThemeDictionaries[HighContrast ? "HighContrast" : "Dark"])[key];
    public Brush Glass(string role)
    {
        if (HighContrast || Simple) return Brush(role == "Frame" ? "NexusPanel" : "NexusSidebar");
        string key = "glass:" + role;
        if (_surfaces.TryGetValue(key, out var cached)) return cached;
        var recipe = GlassRecipe.For(Palette, role);
        var brush = Gradient(recipe.Start, recipe.End);
        brush.StartPoint = new(0, 0); brush.EndPoint = new(0, 1);
        brush.GradientStops.Insert(1, new() { Color = Color(recipe.Highlight), Offset = .05 });
        brush.GradientStops.Insert(2, new() { Color = Color(recipe.Start), Offset = .10 });
        return _surfaces[key] = brush;
    }
    public Brush Material(string role, bool glass)
    {
        if (HighContrast) return Brush(role == "Frame" ? "NexusPanel" : "NexusSidebar");
        if (glass && !Simple && Transparency) return Glass(role);
        string key = "opaque:" + role;
        if (_surfaces.TryGetValue(key, out var cached)) return cached;
        string color = role == "Card" ? Palette.Card : role == "Input" ? Palette.Tokens["NexusInput"] : Palette.Panel;
        return _surfaces[key] = new SolidColorBrush(Color("FF" + color[2..]));
    }
    public Brush Edge
    {
        get
        {
            if (HighContrast || Simple) return Brush("NexusBorder");
            const string key = "edge";
            if (_surfaces.TryGetValue(key, out var cached)) return cached;
            var edge = Gradient(Palette.IsLight ? "B0FFFFFF" : "76D8E9FF", Palette.IsLight ? "30313B52" : "183D527C");
            edge.StartPoint = new(0, 0); edge.EndPoint = new(0, 1);
            return _surfaces[key] = edge;
        }
    }
    // Reuse finite, lightweight paint across independent shell surfaces. No blur
    // or new gradient allocation is needed for a clock tick or window event.
    public Brush Surface(string name, bool simple = false)
    {
        string fallback = name == "Accent" ? "NexusAccent" : name == "Sidebar" ? "NexusSidebar" : "NexusPanel";
        if (HighContrast || simple || Simple) return Brush(fallback);
        if (_surfaces.TryGetValue(name, out var cached)) return cached;
        var (first, last) = name switch
        {
            "Accent" => (Palette.Accent, Palette.AccentEnd),
            "Hero" => (Palette.HeroStart, Palette.HeroEnd),
            "Dock" => (Palette.DockStart, Palette.DockEnd),
            "Sidebar" => (Palette.Tokens["NexusSidebar"], Palette.SidebarEnd),
            "Canvas" => (Palette.Canvas, Palette.WallpaperEnd),
            _ => (Palette.Panel, Palette.Panel)
        };
        return _surfaces[name] = Gradient(first, last);
    }
    public static Windows.UI.Color Color(string value)
    { var c = AuraColor.Parse(value); return Windows.UI.Color.FromArgb(c.A, c.R, c.G, c.B); }
    public static LinearGradientBrush Gradient(string first, string last)
    {
        var b = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 1) };
        b.GradientStops.Add(new() { Color = Color(first), Offset = 0 }); b.GradientStops.Add(new() { Color = Color(last), Offset = 1 }); return b;
    }
}
