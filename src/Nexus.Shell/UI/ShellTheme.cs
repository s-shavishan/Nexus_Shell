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
    private readonly Dictionary<string, Brush> _surfaces = [];
    public ElementTheme ElementTheme => HighContrast ? ElementTheme.Default : Palette.IsLight ? ElementTheme.Light : ElementTheme.Dark;
    public bool Apply(string mood, bool simple = false)
    {
        bool contrast = HighContrast, animations = Animations;
        if (NativeMethods.TryGetHighContrast(out bool value)) contrast = value;
        if (NativeMethods.TryGetAnimationsEnabled(out bool animation)) Animations = animation;
        var palette = AuraPalette.For(mood);
        bool changed = palette.Name != Palette.Name || contrast != HighContrast || animations != Animations || simple != Simple || !_initialized;
        if (palette.Name != Palette.Name || !_initialized) _surfaces.Clear();
        Palette = palette; HighContrast = contrast; Simple = simple;
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
