using System.Text.Json.Serialization;

namespace HollowWake.Core.Definitions;

/// <summary>
/// How a thing looks: client-only content (docs/design/appearance.md). The simulation never reads any of it.
/// An entry is a built body (a family of parts with proportions, a palette, optional features and default
/// gear) or, with a slot, a piece of gear a body wears. Entities find their entry by look id first, then by the
/// pattern's tags against <c>match</c>; gear finds its entry by the item's class and tags.
/// </summary>
public sealed record AppearanceDef : Definition
{
    /// <summary>Tags all of which the pattern (or the item) must carry for this entry to apply; empty never matches by tags.</summary>
    public List<string> Match { get; init; } = new();

    /// <summary>Among entries whose match tags all apply, the highest priority wins; ties go to the id order.</summary>
    public int Priority { get; init; }

    /// <summary>The family that builds the body (a registered builder on each client): biped, quadruped, tree ...</summary>
    public string Body { get; init; } = "";

    /// <summary>For gear: the equipment slot the piece occupies; a body has none.</summary>
    public string? Slot { get; init; }

    /// <summary>Proportions and switches the family reads, by name; unknown names are ignored with a warning at validation.</summary>
    public Dictionary<string, double> Shape { get; init; } = new();

    /// <summary>Named colours the family paints with (skin, cloth, leather, metal, accent, glow ...), as #rrggbb.</summary>
    public Dictionary<string, string> Palette { get; init; } = new();

    /// <summary>Optional parts the family knows how to add: ears, horns, tail, hood, antlers ...</summary>
    public List<string> Features { get; init; } = new();

    /// <summary>Default gear per slot, as item classes (weapon.sword), worn when nothing on the wire says otherwise.</summary>
    public Dictionary<string, string> Gear { get; init; } = new();

    /// <summary>Seeded per-entity jitter of hue, value and size, so a pack is not identical twins.</summary>
    public AppearanceVariation Variation { get; init; } = new();

    /// <summary>Whether the isometric camera may draw this as a directional card: never, allow (the default) or prefer.</summary>
    public string Card { get; init; } = "allow";

    /// <summary>Scale over the entity's own height, for bodies that should stand taller or squatter than their collision.</summary>
    public double Scale { get; init; } = 1;

    /// <summary>A glow colour the body emits (fires, lumen, void), or null.</summary>
    public string? Glow { get; init; }
}

public sealed record AppearanceVariation
{
    public double Hue { get; init; } = 0.02;
    public double Value { get; init; } = 0.06;
    public double Scale { get; init; } = 0.05;
}

/// <summary>
/// Surface materials for the drawn world (docs/design/appearance.md): colours of ground cells by their surface
/// tags and cell flags, structure cells and layers by their tags, the skirt darkening, the water and the
/// cell-level value variation. Both clients read the one default entry.
/// </summary>
public sealed record SurfacesDef : Definition
{
    /// <summary>Ground rules, first match wins: a cell takes the first rule whose tags and flags it all has.</summary>
    public List<SurfaceRule> Ground { get; init; } = new();
    public string Fallback { get; init; } = "#738c59";

    /// <summary>Structure cells take the first matching structure rule and nothing else.</summary>
    public List<SurfaceRule> Structure { get; init; } = new();
    [JsonPropertyName("structure_fallback")]
    public string StructureFallback { get; init; } = "#99948a";

    /// <summary>Applied in order to every ground cell they match, after the ground rule.</summary>
    public List<SurfaceRule> Tints { get; init; } = new();

    /// <summary>Layered surfaces (bridges, floors) by their tags.</summary>
    public List<SurfaceRule> Layers { get; init; } = new();
    [JsonPropertyName("layer_fallback")]
    public string LayerFallback { get; init; } = "#99948c";

    [JsonPropertyName("skirt_darken")]
    public double SkirtDarken { get; init; } = 0.3;

    /// <summary>Seeded value jitter per cell (fraction of the colour) and the size of its patches in cells.</summary>
    public SurfaceVariation Variation { get; init; } = new();

    public WaterLook Water { get; init; } = new();
}

/// <summary>A rule matches a cell when the cell has every listed surface tag and every listed cell flag.</summary>
public sealed record SurfaceRule
{
    public List<string> Tags { get; init; } = new();
    public List<string> Flags { get; init; } = new();
    public string? Color { get; init; }
    public double Mix { get; init; } = 1;
    public double Darken { get; init; }
    /// <summary>For structure rules: the colour of the cell's vertical sides, when they differ from its top.</summary>
    public string? Side { get; init; }
    /// <summary>For structure rules: a pitched roof is drawn over the cell in this colour.</summary>
    public string? Roof { get; init; }
}

public sealed record SurfaceVariation
{
    public double Value { get; init; } = 0.05;
    public double Scale { get; init; } = 3;
}

public sealed record WaterLook
{
    public string Color { get; init; } = "#3373b3";
    public string Deep { get; init; } = "#1f3f5c";
    public string Light { get; init; } = "#9ad6d8";
    public double Opacity { get; init; } = 0.6;
    public double Ripple { get; init; } = 0.5;
    public double Speed { get; init; } = 0.4;
}

/// <summary>
/// The drawn world's light and air by time of day, the day's length and the decorative foliage
/// (docs/design/appearance.md). The clients turn the server clock into a day with it; the simulation does not read it.
/// </summary>
public sealed record WorldLookDef : Definition
{
    public DayLook Day { get; init; } = new();

    /// <summary>Keyframes over the day, by <c>at</c> from zero to one, blended in order and wrapped around midnight.</summary>
    public List<SkyKey> Sky { get; init; } = new();

    public SunLook Sun { get; init; } = new();
    public HazeLook Haze { get; init; } = new();
    public FoliageLook Foliage { get; init; } = new();
}

public sealed record DayLook
{
    [JsonPropertyName("length_seconds")]
    public double LengthSeconds { get; init; } = 1800;
    /// <summary>The fraction of the day at server time zero.</summary>
    public double Start { get; init; } = 0.3;
}

public sealed record SkyKey
{
    public double At { get; init; }
    public string Sky { get; init; } = "#8ca8cc";
    public string Horizon { get; init; } = "#c8d4e2";
    public string Sun { get; init; } = "#fff6e8";
    [JsonPropertyName("sun_energy")]
    public double SunEnergy { get; init; } = 1.2;
    public string Ambient { get; init; } = "#9a9eac";
    [JsonPropertyName("ambient_energy")]
    public double AmbientEnergy { get; init; } = 0.55;
    public string Fog { get; init; } = "#9eb3d1";
}

public sealed record SunLook
{
    /// <summary>The sun's height at noon, in degrees above the horizon.</summary>
    [JsonPropertyName("noon_pitch")]
    public double NoonPitch { get; init; } = 58;
    /// <summary>The sun's bearing at noon, degrees clockwise from north.</summary>
    [JsonPropertyName("noon_yaw")]
    public double NoonYaw { get; init; } = 35;
    /// <summary>Fraction of the day the sun is up, centred on noon.</summary>
    public double Daylight { get; init; } = 0.6;
}

public sealed record HazeLook
{
    public double Density { get; init; } = 0.0007;
    /// <summary>How much denser the haze is at night and at dawn, as a multiplier.</summary>
    public double Night { get; init; } = 1.6;
}

public sealed record FoliageLook
{
    /// <summary>Tufts per cell at density one, by surface tag.</summary>
    public List<FoliageRule> Tufts { get; init; } = new();
}

public sealed record FoliageRule
{
    public List<string> Tags { get; init; } = new();
    public double Density { get; init; } = 0.4;
    public string Color { get; init; } = "#6a9a40";
    public string Tip { get; init; } = "#9ac060";
    public double Height { get; init; } = 0.35;
}

/// <summary>
/// Timings and amounts of motion and feedback (docs/design/appearance.md): the walk, idle, swing, cast, hit,
/// death and camera feel. Cosmetic only; no cast, cooldown or hit window is read from here.
/// </summary>
public sealed record MotionLookDef : Definition
{
    public Dictionary<string, double> Walk { get; init; } = new();
    public Dictionary<string, double> Idle { get; init; } = new();
    public Dictionary<string, double> Swing { get; init; } = new();
    public Dictionary<string, double> Cast { get; init; } = new();
    public Dictionary<string, double> Hit { get; init; } = new();
    public Dictionary<string, double> Death { get; init; } = new();
    public Dictionary<string, double> Camera { get; init; } = new();
    [JsonPropertyName("combat_text")]
    public Dictionary<string, double> CombatText { get; init; } = new();
    public Dictionary<string, double> Labels { get; init; } = new();
    public ContactLook Contact { get; init; } = new();
}

/// <summary>The ground mark under a body: its opacity and the edge grammar per relation (colour-independent).</summary>
public sealed record ContactLook
{
    public double Opacity { get; init; } = 0.35;
    public string Hostile { get; init; } = "teeth";
    public string Friendly { get; init; } = "ticks";
    public string Neutral { get; init; } = "plain";
}
