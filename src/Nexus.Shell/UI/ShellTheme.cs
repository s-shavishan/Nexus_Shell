using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Nexus.Shell.Interop;
using Nexus.Shell.Services;

namespace Nexus.Shell.UI;

internal sealed class ShellTheme
{
    public AuraPalette Palette { get; private set; } = AuraPalette.For("Solstice");
    public bool HighContrast { get; private set; }
    public bool Animations { get; private set; } = true;
    public ElementTheme ElementTheme => HighContrast ? ElementTheme.Default : Palette.IsLight ? ElementTheme.Light : ElementTheme.Dark;
    public bool Apply(string mood)
    {
        bool contrast = HighContrast;
        if (NativeMethods.TryGetHighContrast(out bool value)) contrast = value;
        if (NativeMethods.TryGetAnimationsEnabled(out bool animation)) Animations = animation;
        var palette = AuraPalette.For(mood);
        bool changed = palette.Name != Palette.Name || contrast != HighContrast || !_initialized;
        Palette = palette; HighContrast = contrast;
        if (!changed) return false;
        var resources = Application.Current.Resources;
        foreach (var token in palette.Tokens)
        {
            void Update(ResourceDictionary dictionary)
            { if (dictionary.ContainsKey(token.Key) && dictionary[token.Key] is SolidColorBrush brush) brush.Color = Color(token.Value); }
            Update(resources);
            foreach (string name in new[] { "Light", "Dark" }) Update((ResourceDictionary)resources.ThemeDictionaries[name]);
        }
        _initialized = true; return true;
    }
    private bool _initialized;
    public Brush Brush(string key) => (Brush)((ResourceDictionary)Application.Current.Resources.ThemeDictionaries[HighContrast ? "HighContrast" : "Dark"])[key];
    public static Windows.UI.Color Color(string value)
    { var c = AuraColor.Parse(value); return Windows.UI.Color.FromArgb(c.A, c.R, c.G, c.B); }
    public static LinearGradientBrush Gradient(string first, string last)
    {
        var b = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(1, 1) };
        b.GradientStops.Add(new() { Color = Color(first), Offset = 0 }); b.GradientStops.Add(new() { Color = Color(last), Offset = 1 }); return b;
    }
}
