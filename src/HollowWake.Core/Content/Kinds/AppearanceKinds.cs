using HollowWake.Core.Definitions;
using HollowWake.Core.World;

namespace HollowWake.Core.Content.Kinds;

/// <summary>
/// Content the clients draw the world with (docs/design/appearance.md): appearances (bodies and gear),
/// surfaces, the world's light and foliage, and the motion timings. Registered beside the interface kinds;
/// the simulation never reads any of it. The families, phases and relations below are the vocabulary both
/// clients implement, so a validator can say when content names something no client draws.
/// </summary>
internal static class AppearanceKinds
{
    /// <summary>Body families both clients build, each with the shape names it reads.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> BodyFamilies = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["biped"] = new[] { "head", "neck", "torso", "hips", "legs", "arms", "width", "hunch", "stance", "blocky", "belly", "shoulders" },
        ["quadruped"] = new[] { "head", "snout", "neck", "torso", "legs", "width", "tail", "rump", "mane" },
        ["crawler"] = new[] { "shell", "legs", "width", "claws", "eyes" },
        ["wisp"] = new[] { "head", "torso", "hover", "trail", "width", "arms" },
        ["totem"] = new[] { "width", "faces", "crown" },
        ["dome"] = new[] { "width", "ribs" },
        ["trap"] = new[] { "width", "teeth" },
        ["orb"] = new[] { "core", "halo" },
        ["tree"] = new[] { "trunk", "canopy", "layers", "spread", "bare", "lean", "taper" },
        ["bush"] = new[] { "lobes", "spread" },
        ["fern"] = new[] { "fronds", "spread" },
        ["reed"] = new[] { "stalks", "spread" },
        ["plant"] = new[] { "stem", "petals", "bud" },
        ["knot"] = new[] { "loops", "spread" },
        ["rock"] = new[] { "facets", "flat", "flecks" },
        ["stone"] = new[] { "taper", "lean", "cap" },
        ["log"] = new[] { "length", "stubs" },
        ["anvil"] = new[] { "horn", "block" },
        ["loom"] = new[] { "frame", "threads" },
        ["chest"] = new[] { "lid", "bands" },
        ["shrine"] = new[] { "steps", "niche" },
        ["marker"] = new[] { "height", "cap", "knotted" },
        ["fount"] = new[] { "basin", "rim", "spill" },
        ["drop"] = new[] { "ring" },
    };

    /// <summary>Gear families, each attached to a body's bones by its slot, with the shape names it reads.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> GearFamilies = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["blade"] = new[] { "length", "width", "guard", "curve" },
        ["axe"] = new[] { "length", "head", "double" },
        ["spear"] = new[] { "length", "head" },
        ["maul"] = new[] { "length", "head" },
        ["bow"] = new[] { "length", "curve" },
        ["wand"] = new[] { "length", "tip" },
        ["staff"] = new[] { "length", "head" },
        ["shield"] = new[] { "size", "round" },
        ["tome"] = new[] { "size" },
        ["helm"] = new[] { "brim", "crest", "visor" },
        ["hood"] = new[] { "depth" },
        ["cuirass"] = new[] { "plates", "skirt" },
        ["jerkin"] = new[] { "straps" },
        ["robe"] = new[] { "length", "sash" },
        ["gloves"] = new[] { "cuff" },
        ["boots"] = new[] { "height" },
        ["belt"] = new[] { "pouches" },
        ["sack"] = new[] { "size" },
        ["gem"] = new[] { "size" },
    };

    public static readonly IReadOnlyList<string> CardPolicies = new[] { "never", "allow", "prefer" };

    public static readonly IReadOnlyList<string> ContactStyles = new[] { "plain", "teeth", "ticks", "none" };

    public static readonly IReadOnlyList<string> MotionGroups = new[] { "walk", "idle", "swing", "cast", "hit", "death", "camera", "combat_text", "labels" };

    public static void Register()
    {
        ContentKinds.Register<AppearanceDef>("ui_appearances", Appearance);
        ContentKinds.Register<SurfacesDef>("ui_surfaces", Surfaces);
        ContentKinds.Register<WorldLookDef>("ui_world", WorldLook);
        ContentKinds.Register<MotionLookDef>("ui_motion", MotionLook);
    }

    private static void Appearance(AppearanceDef d, ValidationContext c)
    {
        var gear = d.Slot is not null;
        var families = gear ? GearFamilies : BodyFamilies;
        if (!families.TryGetValue(d.Body, out var shapes))
        {
            if ((gear ? BodyFamilies : GearFamilies).ContainsKey(d.Body))
            {
                c.Error(gear ? $"body '{d.Body}' is a body family, but the entry has a slot; gear families are {string.Join(", ", GearFamilies.Keys)}."
                    : $"body '{d.Body}' is a gear family and needs a slot; body families are {string.Join(", ", BodyFamilies.Keys)}.");
            }
            else
            {
                c.Error($"body '{d.Body}' is not a family the clients build ({string.Join(", ", families.Keys)}).");
            }
            shapes = Array.Empty<string>();
        }
        if (gear && !c.Registry.Has<SlotDef>(d.Slot!))
        {
            c.Warn($"slot '{d.Slot}' is not a declared equipment slot.");
        }
        foreach (var (name, value) in d.Shape)
        {
            if (!double.IsFinite(value))
            {
                c.Error($"shape.{name} is not a number.");
            }
            else if (shapes.Count > 0 && !shapes.Contains(name))
            {
                c.Warn($"shape.{name} is not read by the {d.Body} family ({string.Join(", ", shapes)}).");
            }
        }
        foreach (var (name, value) in d.Palette)
        {
            if (!InterfaceKinds.IsColor(value))
            {
                c.Error($"palette.{name} '{value}' is not a colour (#rgb, #rrggbb or #rrggbbaa).");
            }
        }
        if (d.Glow is not null && !InterfaceKinds.IsColor(d.Glow))
        {
            c.Error($"glow '{d.Glow}' is not a colour.");
        }
        if (d.Match.Any(string.IsNullOrWhiteSpace))
        {
            c.Error("match holds an empty tag.");
        }
        if (!CardPolicies.Contains(d.Card))
        {
            c.Error($"card '{d.Card}' is not one of {string.Join(", ", CardPolicies)}.");
        }
        if (!double.IsFinite(d.Scale) || d.Scale <= 0)
        {
            c.Error("scale must be positive.");
        }
        if (d.Variation.Hue < 0 || d.Variation.Value < 0 || d.Variation.Scale < 0 || d.Variation.Scale >= 1)
        {
            c.Error("variation amounts must be non-negative, and scale below one.");
        }
        foreach (var (slot, itemClass) in d.Gear)
        {
            if (!c.Registry.Has<SlotDef>(slot))
            {
                c.Warn($"gear.{slot} is not a declared equipment slot.");
            }
            if (string.IsNullOrWhiteSpace(itemClass))
            {
                c.Error($"gear.{slot} names no item class.");
            }
        }
    }

    private static void Surfaces(SurfacesDef d, ValidationContext c)
    {
        void Rules(IEnumerable<SurfaceRule> rules, string field)
        {
            var i = 0;
            foreach (var r in rules)
            {
                var at = $"{field}[{i++}]";
                if (r.Color is not null && !InterfaceKinds.IsColor(r.Color))
                {
                    c.Error($"{at}.color '{r.Color}' is not a colour.");
                }
                if (r.Side is not null && !InterfaceKinds.IsColor(r.Side))
                {
                    c.Error($"{at}.side '{r.Side}' is not a colour.");
                }
                if (r.Roof is not null && !InterfaceKinds.IsColor(r.Roof))
                {
                    c.Error($"{at}.roof '{r.Roof}' is not a colour.");
                }
                if (r.Mix is < 0 or > 1 || r.Darken is < 0 or > 1)
                {
                    c.Error($"{at}: mix and darken run from zero to one.");
                }
                foreach (var flag in r.Flags)
                {
                    if (!Enum.TryParse<CellFlags>(flag, ignoreCase: true, out var parsed) || parsed == CellFlags.None)
                    {
                        c.Error($"{at}.flags names '{flag}', which is not a cell flag ({string.Join(", ", Enum.GetNames<CellFlags>().Where(n => n != "None").Select(n => n.ToLowerInvariant()))}).");
                    }
                }
                if (r.Tags.Count == 0 && r.Flags.Count == 0 && field != "ground")
                {
                    c.Warn($"{at} matches every cell; put a catch-all last.");
                }
            }
        }
        Rules(d.Ground, "ground");
        Rules(d.Structure, "structure");
        Rules(d.Tints, "tints");
        Rules(d.Layers, "layers");
        foreach (var (name, color) in new[] { ("fallback", d.Fallback), ("structure_fallback", d.StructureFallback), ("layer_fallback", d.LayerFallback), ("water.color", d.Water.Color), ("water.deep", d.Water.Deep), ("water.light", d.Water.Light) })
        {
            if (!InterfaceKinds.IsColor(color))
            {
                c.Error($"{name} '{color}' is not a colour.");
            }
        }
        if (d.SkirtDarken is < 0 or > 1 || d.Water.Opacity is < 0 or > 1 || d.Variation.Value is < 0 or > 1 || d.Variation.Scale <= 0)
        {
            c.Error("skirt_darken, water.opacity and variation.value run from zero to one; variation.scale is positive.");
        }
        if (d.Id != "default" && !c.Registry.Has<SurfacesDef>("default"))
        {
            c.Warn("the clients read the entry called default; this one is unused.");
        }
    }

    private static void WorldLook(WorldLookDef d, ValidationContext c)
    {
        if (d.Day.LengthSeconds <= 0 || !double.IsFinite(d.Day.LengthSeconds))
        {
            c.Error("day.length_seconds must be positive.");
        }
        if (d.Day.Start is < 0 or >= 1)
        {
            c.Error("day.start is a fraction of the day, from zero up to one.");
        }
        if (d.Sky.Count == 0)
        {
            c.Error("sky needs at least one keyframe.");
        }
        var last = -1.0;
        foreach (var key in d.Sky)
        {
            if (key.At is < 0 or > 1 || key.At <= last)
            {
                c.Error($"sky keyframe at {key.At} is out of order or outside zero to one.");
            }
            last = key.At;
            foreach (var (name, color) in new[] { ("sky", key.Sky), ("horizon", key.Horizon), ("sun", key.Sun), ("ambient", key.Ambient), ("fog", key.Fog) })
            {
                if (!InterfaceKinds.IsColor(color))
                {
                    c.Error($"sky keyframe at {key.At}: {name} '{color}' is not a colour.");
                }
            }
            if (key.SunEnergy < 0 || key.AmbientEnergy < 0)
            {
                c.Error($"sky keyframe at {key.At}: energies must not be negative.");
            }
        }
        if (d.Sun.NoonPitch is <= 0 or > 90 || d.Sun.Daylight is <= 0 or > 1)
        {
            c.Error("sun.noon_pitch is degrees above the horizon; sun.daylight is a fraction of the day.");
        }
        if (d.Haze.Density < 0 || d.Haze.Night < 0)
        {
            c.Error("haze amounts must not be negative.");
        }
        var i = 0;
        foreach (var tuft in d.Foliage.Tufts)
        {
            if (!InterfaceKinds.IsColor(tuft.Color) || !InterfaceKinds.IsColor(tuft.Tip))
            {
                c.Error($"foliage.tufts[{i}] colours must be colours.");
            }
            if (tuft.Density < 0 || tuft.Height <= 0)
            {
                c.Error($"foliage.tufts[{i}]: density must not be negative and height must be positive.");
            }
            i++;
        }
        if (d.Id != "default" && !c.Registry.Has<WorldLookDef>("default"))
        {
            c.Warn("the clients read the entry called default; this one is unused.");
        }
    }

    private static void MotionLook(MotionLookDef d, ValidationContext c)
    {
        foreach (var (group, values) in new[] { ("walk", d.Walk), ("idle", d.Idle), ("swing", d.Swing), ("cast", d.Cast), ("hit", d.Hit), ("death", d.Death), ("camera", d.Camera), ("combat_text", d.CombatText), ("labels", d.Labels) })
        {
            foreach (var (name, value) in values)
            {
                if (!double.IsFinite(value) || value < 0)
                {
                    c.Error($"{group}.{name} must be a non-negative number.");
                }
            }
        }
        if (d.Contact.Opacity is < 0 or > 1)
        {
            c.Error("contact.opacity runs from zero to one.");
        }
        foreach (var (name, style) in new[] { ("hostile", d.Contact.Hostile), ("friendly", d.Contact.Friendly), ("neutral", d.Contact.Neutral) })
        {
            if (!ContactStyles.Contains(style))
            {
                c.Error($"contact.{name} '{style}' is not one of {string.Join(", ", ContactStyles)}.");
            }
        }
        if (d.Id != "default" && !c.Registry.Has<MotionLookDef>("default"))
        {
            c.Warn("the clients read the entry called default; this one is unused.");
        }
    }
}
