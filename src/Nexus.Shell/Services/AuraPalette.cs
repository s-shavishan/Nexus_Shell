using System.Globalization;

namespace Nexus.Shell.Services;

public readonly record struct AuraColor(byte A, byte R, byte G, byte B)
{
    public static AuraColor Parse(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 8) throw new ArgumentException("Aura colors use eight ARGB digits.", nameof(hex));
        byte Read(int start) => byte.Parse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new(Read(0), Read(2), Read(4), Read(6));
    }
    public AuraColor Over(AuraColor background)
    {
        double alpha = A / 255.0;
        byte Blend(byte foreground, byte below) => (byte)Math.Round(foreground * alpha + below * (1 - alpha));
        return new(255, Blend(R, background.R), Blend(G, background.G), Blend(B, background.B));
    }
    public double Luminance
    {
        get
        {
            static double Linear(byte value) { double v = value / 255.0; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
            return .2126 * Linear(R) + .7152 * Linear(G) + .0722 * Linear(B);
        }
    }
    public static double Contrast(AuraColor foreground, AuraColor background)
    {
        double a = foreground.Over(background).Luminance, b = background.Luminance;
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
}

public sealed record AuraPalette(string Name, string Canvas, string Panel, string Card, string Accent, string Secondary,
    string Muted, string HeroStart, string HeroEnd, string WallpaperEnd)
{
    public const string Text = "FFF4F7FE";
    public const string AccentText = "FF171C29";
    public const string Border = "406D809A";
    public const string Input = "F0141B29";
    public static AuraPalette For(string mood) => mood switch
    {
        "Opal" => new("Opal", "FF293668", "DAF0F1F8", "EAF8F8FC", "FF3163BC", "FF526E99", "FF4E5A70", "FFF0F2FB", "FFE3EAFB", "FF3F2B67"),
        "Aurora" => new("Lagoon", "FF101C22", "EF1B2E35", "E5233941", "FF8DDFD3", "FFAFBEF6", "FFBACDD2", "FF294751", "FF254E4B", "FF17343C"),
        "Slate" => new("Graphite", "FF121820", "EF202A36", "E52B3643", "FFAACCF4", "FFC3BBE8", "FFB9C5D4", "FF2D3D50", "FF243340", "FF192A36"),
        _ => new("Pearl", "FF121722", "EF222A39", "E52B3445", "FFC7BEF7", "FF9BDED8", "FFB8C3D6", "FF343B54", "FF29494C", "FF1B2C3A")
    };
    public Dictionary<string, string> Tokens
    {
      get
      {
       var tokens = new Dictionary<string, string>
       {
        ["NexusText"] = Name == "Opal" ? "FF1B273D" : Text, ["NexusMuted"] = Muted, ["NexusAccent"] = Accent, ["NexusAccentText"] = Name == "Opal" ? "FFFFFFFF" : AccentText,
        ["NexusSecondary"] = Secondary, ["NexusPanel"] = Panel, ["NexusCard"] = Card, ["NexusBorder"] = Border,
        ["NexusSidebar"] = Name == "Opal" ? "D8E8EAF3" : "B01B2335", ["NexusInput"] = Name == "Opal" ? "E8F8FAFF" : Input, ["NexusSelection"] = Name == "Opal" ? "253B62B7" : "384F657F",
        ["NexusShell"] = Name == "Opal" ? "D2F0F0F8" : "D61A2331", ["NexusIcon"] = "FF34465C", ["NexusHeroStart"] = HeroStart,
        ["NexusDesktopText"] = "FFF5F8FF", ["NexusDesktopMuted"] = "FFD2DDED",
        ["NexusHeroEnd"] = HeroEnd, ["NexusOverlay"] = "BC090E18"
       };
       foreach (string state in new[] { "", "PointerOver", "Pressed" })
       {
           tokens["AccentButtonBackground" + state] = Accent;
           tokens["AccentButtonForeground" + state] = tokens["NexusAccentText"];
           tokens["AccentButtonBorderBrush" + state] = Accent;
           tokens["ToggleSwitchFillOn" + state] = Accent;
           tokens["ToggleSwitchStrokeOn" + state] = Accent;
           tokens["ToggleSwitchKnobFillOn" + state] = tokens["NexusAccentText"];
           tokens["ToggleSwitchFillOff" + state] = Card;
           tokens["ToggleSwitchStrokeOff" + state] = Border;
           tokens["ToggleSwitchKnobFillOff" + state] = Muted;
           if (state.Length > 0)
           {
               tokens["ButtonBackground" + state] = tokens["NexusSelection"];
               tokens["ButtonForeground" + state] = tokens["NexusText"];
               tokens["ButtonBorderBrush" + state] = Border;
           }
       }
       return tokens;
      }
    }
}
