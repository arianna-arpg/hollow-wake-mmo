using Godot;
using HollowWake.Client.UI.Logic;

namespace HollowWake.Client.UI;

/// <summary>
/// The theme content turned into what Godot draws with: colours, fonts, a Godot theme for the stock controls,
/// glyph textures from the icon set, and surface materials for panes. Every view reads its look from here, so
/// a theme change is one new skin and a redraw.
/// </summary>
public sealed class Skin
{
    private readonly Dictionary<(string Icon, int Px), Texture2D> _icons = new();

    public UiContent Ui { get; }
    public ContentNames Names { get; }
    public UiTheme T => Ui.Theme;
    public UiLayout L => Ui.Layout;
    public UiIconSet IconSet => Ui.Icons;

    public Font Body { get; }
    public Font Title { get; }
    public Font Numerals { get; }
    public int BodySize { get; }
    public int TitleSize { get; }
    public int NumeralSize { get; }
    public Theme Theme { get; }

    public Skin(UiContent ui, ContentNames names)
    {
        Ui = ui;
        Names = names;
        var body = ui.Theme.Font("body");
        var title = ui.Theme.Font("title");
        var numerals = ui.Theme.Font("numerals");
        Body = MakeFont(body);
        Title = MakeFont(title);
        Numerals = MakeFont(numerals);
        BodySize = (int)Math.Round(body.Size);
        TitleSize = (int)Math.Round(title.Size);
        NumeralSize = (int)Math.Round(numerals.Size);
        Theme = BuildTheme();
    }

    public static Color C(Rgba c) => new(c.R, c.G, c.B, c.A);

    /// <summary>A named surface colour of the theme.</summary>
    public Color S(string name) => C(T.S(name));

    public Color S(string name, Color fallback) => T.Surface.TryGetValue(name, out var c) ? C(c) : fallback;

    public Color Fill(string resource) => C(T.Pool(resource).Fill);
    public Color Deep(string resource) => C(T.Pool(resource).Deep);
    public Color Rarity(string? rarity) => C(T.Rarity(rarity));
    public Color Thread(string thread) => C(T.Thread(thread));
    public Color Notice(string level) => C(T.Notice(level));
    public Color State(string name, Color fallback) => T.States.TryGetValue(name, out var c) ? C(c) : fallback;

    /// <summary>A material tunable of the theme (grain, bevel, corner ...).</summary>
    public float Mat(string name, float fallback) => (float)T.M(name, fallback);

    /// <summary>A layout metric (sizes in pixels at scale one, tunables).</summary>
    public float Metric(string name, float fallback) => (float)L.M(name, fallback);

    public bool Brass => T.Frame == "brass";

    private static Font MakeFont(FontSpec spec) => new SystemFont
    {
        FontNames = spec.Families.ToArray(),
        FontWeight = spec.Weight,
        Antialiasing = TextServer.FontAntialiasing.Gray,
        SubpixelPositioning = TextServer.SubpixelPositioning.Auto,
        AllowSystemFallback = true,
    };

    /// <summary>A glyph of the icon set as a white texture of the given pixel size, to be tinted by modulation.</summary>
    public Texture2D Icon(string icon, int px)
    {
        px = Math.Clamp(px, 8, 256);
        var id = IconSet.Resolve(icon);
        if (_icons.TryGetValue((id, px), out var cached))
        {
            return cached;
        }
        var image = new Image();
        var err = image.LoadSvgFromString(IconSet.Svg(id, px), 1f);
        Texture2D texture = err == Error.Ok ? ImageTexture.CreateFromImage(image) : Placeholder(px);
        _icons[(id, px)] = texture;
        return texture;
    }

    private static Texture2D Placeholder(int px)
    {
        var image = Image.CreateEmpty(px, px, false, Image.Format.Rgba8);
        image.Fill(new Color(1, 1, 1, 0.5f));
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>A surface material for a pane background of a given size.</summary>
    public ShaderMaterial SurfaceMaterial(Vector2 size, float seed, string baseName = "pane", string lightName = "pane_light", float opacity = 1f)
    {
        var m = new ShaderMaterial { Shader = Shaders.Surface };
        UpdateSurface(m, size, seed, baseName, lightName, opacity);
        return m;
    }

    public void UpdateSurface(ShaderMaterial m, Vector2 size, float seed, string baseName = "pane", string lightName = "pane_light", float opacity = 1f)
    {
        m.SetShaderParameter("base_color", S(baseName));
        m.SetShaderParameter("light_color", S(lightName, S(baseName).Lightened(0.08f)));
        m.SetShaderParameter("edge_color", S("edge_dark"));
        m.SetShaderParameter("grain", Mat("grain", 0.12f));
        m.SetShaderParameter("fibre", Mat("fibre", 0f));
        m.SetShaderParameter("vignette", Mat("vignette", 0.4f));
        m.SetShaderParameter("veins", Mat("veins", 0f));
        m.SetShaderParameter("size", size);
        m.SetShaderParameter("seed", seed);
        m.SetShaderParameter("opacity", opacity);
    }

    private StyleBoxFlat Flat(Color bg, Color border, int borderWidth = 1, int corner = -1, int margin = 6)
    {
        var c = corner < 0 ? (int)Mat("corner", 3) : corner;
        var sb = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            AntiAliasing = true,
        };
        sb.SetBorderWidthAll(borderWidth);
        sb.SetCornerRadiusAll(c);
        sb.SetContentMarginAll(margin);
        return sb;
    }

    private Theme BuildTheme()
    {
        var t = new Theme { DefaultFont = Body, DefaultFontSize = BodySize };
        var ink = S("ink");
        var muted = S("ink_muted");
        var well = S("inset");
        var edge = S("edge");
        var dark = S("edge_dark");
        var accent = S("accent");
        foreach (var type in new[] { "Label", "Button", "LineEdit", "RichTextLabel", "ItemList", "OptionButton", "CheckBox" })
        {
            t.SetColor("font_color", type, ink);
        }
        t.SetColor("default_color", "RichTextLabel", ink);
        t.SetColor("font_outline_color", "Label", S("shadow").Darkened(0.2f));
        t.SetColor("font_hover_color", "Button", ink.Lightened(0.1f));
        t.SetColor("font_pressed_color", "Button", accent);
        t.SetColor("font_disabled_color", "Button", muted.Darkened(0.2f));
        t.SetColor("font_placeholder_color", "LineEdit", muted);
        t.SetColor("caret_color", "LineEdit", accent);
        t.SetColor("selection_color", "LineEdit", accent with { A = 0.35f });
        var button = Flat(S("pane_light", well), edge, 1, -1, 5);
        button.BorderColor = dark;
        var hover = Flat(S("hover", well), accent, 1, -1, 5);
        var pressed = Flat(well, accent, 2, -1, 5);
        var disabled = Flat(well with { A = 0.6f }, dark with { A = 0.6f }, 1, -1, 5);
        t.SetStylebox("normal", "Button", button);
        t.SetStylebox("hover", "Button", hover);
        t.SetStylebox("pressed", "Button", pressed);
        t.SetStylebox("disabled", "Button", disabled);
        t.SetStylebox("focus", "Button", new StyleBoxEmpty());
        var line = Flat(well, dark, 1, -1, 5);
        t.SetStylebox("normal", "LineEdit", line);
        t.SetStylebox("focus", "LineEdit", Flat(well, accent, 1, -1, 5));
        t.SetStylebox("read_only", "LineEdit", line);
        t.SetStylebox("panel", "PanelContainer", new StyleBoxEmpty());
        t.SetStylebox("panel", "Panel", new StyleBoxEmpty());
        t.SetStylebox("normal", "RichTextLabel", new StyleBoxEmpty());
        t.SetStylebox("focus", "RichTextLabel", new StyleBoxEmpty());
        var tip = Flat(S("pane"), edge, 1, -1, 8);
        tip.ShadowColor = new Color(0, 0, 0, 0.45f);
        tip.ShadowSize = 6;
        t.SetStylebox("panel", "TooltipPanel", tip);
        t.SetColor("font_color", "TooltipLabel", ink);
        t.SetFont("font", "TooltipLabel", Body);
        t.SetFontSize("font_size", "TooltipLabel", BodySize);
        var grabber = Flat(edge with { A = 0.7f }, dark, 0, 2, 0);
        var scrollBg = Flat(well with { A = 0.5f }, dark, 0, 2, 0);
        foreach (var bar in new[] { "VScrollBar", "HScrollBar" })
        {
            t.SetStylebox("scroll", bar, scrollBg);
            t.SetStylebox("grabber", bar, grabber);
            t.SetStylebox("grabber_highlight", bar, Flat(accent with { A = 0.8f }, dark, 0, 2, 0));
            t.SetStylebox("grabber_pressed", bar, Flat(accent, dark, 0, 2, 0));
        }
        // Switches draw only their switch, in the theme's colours; the row around them is the frame.
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
        {
            t.SetStylebox(state, "CheckButton", new StyleBoxEmpty());
        }
        t.SetColor("font_color", "CheckButton", ink);
        t.SetColor("font_pressed_color", "CheckButton", ink);
        t.SetColor("font_hover_color", "CheckButton", ink);
        t.SetColor("font_hover_pressed_color", "CheckButton", ink);
        var light = S("pane_light", well);
        t.SetIcon("checked", "CheckButton", Svg(Switch(accent, light, dark, true)));
        t.SetIcon("unchecked", "CheckButton", Svg(Switch(well, muted, dark, false)));
        t.SetIcon("checked_disabled", "CheckButton", Svg(Switch(accent with { A = 0.5f }, light with { A = 0.6f }, dark, true)));
        t.SetIcon("unchecked_disabled", "CheckButton", Svg(Switch(well with { A = 0.5f }, muted with { A = 0.5f }, dark, false)));
        // Sliders: a sunken track filled with the accent up to a round grabber.
        var track = Flat(well, dark, 1, 4, 0);
        track.ContentMarginTop = track.ContentMarginBottom = 3;
        var filled = Flat(accent with { A = 0.75f }, dark, 1, 4, 0);
        filled.ContentMarginTop = filled.ContentMarginBottom = 3;
        t.SetStylebox("slider", "HSlider", track);
        t.SetStylebox("grabber_area", "HSlider", filled);
        t.SetStylebox("grabber_area_highlight", "HSlider", filled);
        t.SetIcon("grabber", "HSlider", Svg(Knob(light, dark)));
        t.SetIcon("grabber_highlight", "HSlider", Svg(Knob(light, accent)));
        t.SetIcon("grabber_disabled", "HSlider", Svg(Knob(well, dark)));
        // Drop-down lists of choices.
        var menu = Flat(S("pane"), edge, 1, -1, 6);
        menu.ShadowColor = new Color(0, 0, 0, 0.4f);
        menu.ShadowSize = 5;
        t.SetStylebox("panel", "PopupMenu", menu);
        t.SetStylebox("hover", "PopupMenu", Flat(S("hover", well), accent, 1, -1, 2));
        t.SetColor("font_color", "PopupMenu", ink);
        t.SetColor("font_hover_color", "PopupMenu", ink);
        t.SetColor("font_disabled_color", "PopupMenu", muted);
        t.SetColor("font_separator_color", "PopupMenu", muted);
        t.SetFont("font", "PopupMenu", Body);
        t.SetFontSize("font_size", "PopupMenu", BodySize);
        t.SetColor("font_color", "OptionButton", ink);
        t.SetColor("font_hover_color", "OptionButton", ink);
        t.SetColor("font_pressed_color", "OptionButton", ink);
        t.SetColor("font_focus_color", "OptionButton", ink);
        t.SetConstant("separation", "VBoxContainer", 4);
        t.SetConstant("separation", "HBoxContainer", 6);
        return t;
    }

    /// <summary>An SVG paint: the colour and its opacity, written culture-free.</summary>
    private static string Paint(string attribute, Color c) =>
        $"{attribute}='#{c.ToHtml(false)}' {attribute}-opacity='{c.A.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}'";

    /// <summary>A switch glyph: a rounded track with its knob at the on or off end.</summary>
    private static string Switch(Color track, Color knob, Color edge, bool on) =>
        "<svg xmlns='http://www.w3.org/2000/svg' width='40' height='22'>" +
        $"<rect x='1' y='2' width='38' height='18' rx='9' {Paint("fill", track)} {Paint("stroke", edge)} stroke-width='1.5'/>" +
        $"<circle cx='{(on ? 29 : 11)}' cy='11' r='6.5' {Paint("fill", knob)} {Paint("stroke", edge)} stroke-width='1'/></svg>";

    /// <summary>A slider grabber: a round knob with a rim.</summary>
    private static string Knob(Color fill, Color rim) =>
        "<svg xmlns='http://www.w3.org/2000/svg' width='18' height='18'>" +
        $"<circle cx='9' cy='9' r='7' {Paint("fill", fill)} {Paint("stroke", rim)} stroke-width='2'/></svg>";

    private static Texture2D Svg(string svg)
    {
        var image = new Image();
        return image.LoadSvgFromString(svg, 1f) == Error.Ok ? ImageTexture.CreateFromImage(image) : Placeholder(16);
    }

    /// <summary>Ink that reads on a colour: the theme's light label on dark colours, its ink on light ones.</summary>
    public Color InkOn(Color background) => background.Luminance > 0.55f ? S("ink").Luminance < 0.5f ? S("ink") : new Color(0.08f, 0.06f, 0.05f) : S("label");
}
