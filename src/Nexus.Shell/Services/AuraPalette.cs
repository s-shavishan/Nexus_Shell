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
    public static readonly string[] Moods = ["Midnight", "Solstice", "Ember", "Opal", "Orbit", "Aurora", "Slate"];
    public bool IsLight => Name is "Solstice" or "Opal";
    public string AccentEnd => Name == "Midnight" ? "FF8195F4" : Name == "Ember" ? "FFBA2B52" : Accent;
    public string DockStart => Name == "Midnight" ? "DF283752" : Name == "Ember" ? "F2491D1D" : Panel;
    public string DockEnd => Name == "Midnight" ? "ED11182C" : Name == "Ember" ? "F22E1729" : Panel;
    public string SidebarEnd => Name == "Midnight" ? "DE131C31" : Name == "Ember" ? "F2221320" : Tokens["NexusSidebar"];
    public const string Text = "FFF4F7FE";
    public const string AccentText = "FF171C29";
    public const string Border = "406D809A";
    public const string Input = "F0141B29";
    public static AuraPalette For(string mood) => mood switch
    {
        "Midnight" => new("Midnight", "FF0A1020", "EE151E32", "F0263148", "FF8EAFFF", "FFB3C5FB", "FFAAB9D4", "FF253A62", "FF171D3A", "FF282453"),
        "Solstice" => new("Solstice", "FF9E482D", "F5FFF8F1", "F8FFFCF8", "FFAA461F", "FF805643", "FF665C56", "FFFFF5E9", "FFF3E5D6", "FF482C46"),
        "Ember" => new("Ember", "FF170B11", "F222111C", "F22D1624", "FFBF311B", "FFFFAE9B", "FFDCC2C8", "FF48201F", "FF351A2E", "FF281020"),
        "Opal" => new("Opal", "FF293668", "DAF0F1F8", "EAF8F8FC", "FF3163BC", "FF526E99", "FF4E5A70", "FFF0F2FB", "FFE3EAFB", "FF3F2B67"),
        "Aurora" or "Lagoon" => new("Lagoon", "FF101C22", "EF1B2E35", "E5233941", "FF8DDFD3", "FFAFBEF6", "FFBACDD2", "FF294751", "FF254E4B", "FF17343C"),
        "Slate" or "Graphite" => new("Graphite", "FF121820", "EF202A36", "E52B3643", "FFAACCF4", "FFC3BBE8", "FFB9C5D4", "FF2D3D50", "FF243340", "FF192A36"),
        _ => new("Pearl", "FF121722", "EF222A39", "E52B3445", "FFC7BEF7", "FF9BDED8", "FFB8C3D6", "FF343B54", "FF29494C", "FF1B2C3A")
    };
    public Dictionary<string, string> Tokens
    {
      get
      {
       var tokens = new Dictionary<string, string>
       {
        ["NexusText"] = Name == "Solstice" ? "FF302923" : IsLight ? "FF1B273D" : Name == "Ember" ? "FFFFF7F2" : Text, ["NexusMuted"] = Muted, ["NexusAccent"] = Accent, ["NexusAccentText"] = IsLight || Name == "Ember" ? "FFFFFFFF" : AccentText,
        ["NexusSecondary"] = Secondary, ["NexusPanel"] = Panel, ["NexusCard"] = Card, ["NexusBorder"] = IsLight ? "383F332D" : Name == "Midnight" ? "507D90B5" : Name == "Ember" ? "426E3E4B" : "426D809A",
        ["NexusSidebar"] = Name == "Solstice" ? "F0F1E5DA" : IsLight ? "EAE8EAF3" : Name == "Midnight" ? "E71B263F" : Name == "Ember" ? "F232151B" : "E01B2335",
        ["NexusInput"] = IsLight ? "F8FFFFFF" : Name == "Midnight" ? "EB0E182C" : Name == "Ember" ? "F21D101A" : Input,
        ["NexusSelection"] = Name == "Solstice" ? "29AA461F" : IsLight ? "253B62B7" : Name == "Midnight" ? "68435D98" : Name == "Ember" ? "424F2935" : "384F657F",
        ["NexusShell"] = Name == "Solstice" ? "E3FFF5EB" : IsLight ? "DFF0F0F8" : Name == "Midnight" ? "EA111A2D" : Name == "Ember" ? "F222111C" : "E01A2331", ["NexusIcon"] = Name == "Ember" ? "FF48202E" : "FF34465C", ["NexusHeroStart"] = HeroStart,
        ["NexusDesktopText"] = "FFF5F8FF", ["NexusDesktopMuted"] = "FFD2DDED",
        ["NexusHeroEnd"] = HeroEnd, ["NexusOverlay"] = "820E0B18",
        ["NexusDesktopLabel"] = Name == "Midnight" ? "B90C1529" : "CF241B2B", ["NexusHover"] = IsLight ? "FF251E24" : "FFFFFFFF",
        ["NexusHighlight"] = IsLight ? "90FFFFFF" : "26FFFFFF",
        ["NexusSelectedText"] = IsLight ? "FF302923" : Text,
        ["NexusSegment"] = IsLight ? "E6FFFFFF" : Name == "Midnight" ? "FF344461" : Name == "Ember" ? "FF492330" : "FF39465C",
        ["NexusTrack"] = IsLight ? "52463E39" : Name == "Midnight" ? "98576F97" : Name == "Ember" ? "685D2B3F" : "687D8A9E"
       };
       // Restyle the native templates as well as Nexus's buttons. Keep the
       // built-in keyboard, selection, automation and editing behavior.
       tokens["FlyoutPresenterBackground"] = tokens["MenuFlyoutPresenterBackground"] = Panel;
       tokens["FlyoutPresenterBorderBrush"] = tokens["MenuFlyoutPresenterBorderBrush"] = tokens["NexusBorder"];
       tokens["ComboBoxDropDownBackground"] = Panel;
       tokens["ComboBoxDropDownBorderBrush"] = tokens["NexusBorder"];
       tokens["ListViewItemBackgroundSelected"] = tokens["NexusSelection"];
       tokens["ListViewItemBackgroundSelectedPointerOver"] = tokens["NexusSelection"];
       tokens["ListViewItemForegroundSelected"] = tokens["NexusSelectedText"];
       tokens["TextControlPlaceholderForeground"] = Muted;
       tokens["TextControlPlaceholderForegroundFocused"] = Muted;
       tokens["TextControlPlaceholderForegroundPointerOver"] = Muted;
       tokens["TextControlPlaceholderForegroundDisabled"] = Muted;
       tokens["TextControlHeaderForeground"] = tokens["NexusText"];
       tokens["ComboBoxHeaderForeground"] = tokens["NexusText"];
       foreach (string state in new[] { "", "PointerOver", "Pressed", "Focused", "Disabled" })
       {
           tokens["TextControlForeground" + state] = state == "Disabled" ? Muted : tokens["NexusText"];
           tokens["TextControlBackground" + state] = tokens["NexusInput"];
           tokens["TextControlBorderBrush" + state] = state == "Focused" ? Accent : tokens["NexusBorder"];
           tokens["ComboBoxForeground" + state] = state == "Disabled" ? Muted : tokens["NexusText"];
           tokens["ComboBoxBackground" + state] = tokens["NexusInput"];
           tokens["ComboBoxBorderBrush" + state] = state == "Focused" ? Accent : tokens["NexusBorder"];
           tokens["MenuFlyoutItemForeground" + state] = state == "Disabled" ? Muted : tokens["NexusText"];
           tokens["MenuFlyoutItemBackground" + state] = state is "PointerOver" or "Pressed" ? tokens["NexusSelection"] : "00000000";
           tokens["SliderTrackValueFill" + state] = state == "Disabled" ? Muted : Accent;
           tokens["SliderTrackFill" + state] = tokens["NexusTrack"];
           tokens["SliderThumbBackground" + state] = IsLight ? "FFFFFFFF" : "FFF5F3F0";
           tokens["SliderThumbBorderBrush" + state] = tokens["NexusBorder"];
           tokens["CheckBoxForeground" + state] = state == "Disabled" ? Muted : tokens["NexusText"];
           tokens["CheckBoxCheckBackgroundFillChecked" + state] = Accent;
           tokens["CheckBoxCheckGlyphForegroundChecked" + state] = tokens["NexusAccentText"];
       }
       foreach (string state in new[] { "", "PointerOver", "Pressed" })
       {
           tokens["AccentButtonBackground" + state] = Accent;
           tokens["AccentButtonForeground" + state] = tokens["NexusAccentText"];
           tokens["AccentButtonBorderBrush" + state] = Accent;
           tokens["ToggleSwitchFillOn" + state] = Accent;
           tokens["ToggleSwitchStrokeOn" + state] = Accent;
           tokens["ToggleSwitchKnobFillOn" + state] = tokens["NexusAccentText"];
           tokens["ToggleSwitchFillOff" + state] = Card;
           tokens["ToggleSwitchStrokeOff" + state] = tokens["NexusBorder"];
           tokens["ToggleSwitchKnobFillOff" + state] = Muted;
           if (state.Length > 0)
           {
               tokens["ButtonBackground" + state] = tokens["NexusSelection"];
               tokens["ButtonForeground" + state] = tokens["NexusText"];
               tokens["ButtonBorderBrush" + state] = tokens["NexusBorder"];
           }
       }
       return tokens;
      }
    }
}
