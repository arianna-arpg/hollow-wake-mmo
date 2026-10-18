using HollowWake.Client.UI.Logic;
using HollowWake.Core.Definitions;
using HollowWake.Core.Tags;
using HollowWake.Core.World;

namespace HollowWake.Client.World.Logic;

/// <summary>
/// The colour of a ground cell from the surfaces content (docs/design/appearance.md), the same on both
/// clients: structure cells take the first matching structure rule (with a side colour for their skirts and
/// a roof colour that raises a pitched cap); other cells take the first matching ground rule, then every
/// matching tint in order, then a seeded value jitter in patches so a field is not one flat colour. Mixing
/// happens in sRGB, as the interface's colours do.
/// </summary>
public sealed class SurfaceLook
{
    private sealed record Rule(IReadOnlyList<string> Tags, CellFlags Flags, Rgba? Color, float Mix, float Darken, Rgba? Side, Rgba? Roof)
    {
        public bool Matches(TagSet tags, CellFlags flags) => (flags & Flags) == Flags && Tags.All(tags.Has);
    }

    private readonly List<Rule> _ground = new();
    private readonly List<Rule> _structure = new();
    private readonly List<Rule> _tints = new();
    private readonly List<Rule> _layers = new();
    private readonly Rgba _fallback;
    private readonly Rgba _structureFallback;
    private readonly Rgba _layerFallback;

    public float SkirtDarken { get; }
    public float VariationValue { get; }
    public float VariationScale { get; }
    public Rgba Water { get; }
    public Rgba WaterDeep { get; }
    public Rgba WaterLight { get; }
    public float WaterOpacity { get; }
    public float WaterRipple { get; }
    public float WaterSpeed { get; }

    public SurfaceLook(SurfacesDef def)
    {
        static Rule Compile(SurfaceRule r)
        {
            CellFlags flags = CellFlags.None;
            foreach (var name in r.Flags)
            {
                if (Enum.TryParse<CellFlags>(name, ignoreCase: true, out var f))
                {
                    flags |= f;
                }
            }
            return new Rule(r.Tags, flags, Parse(r.Color), (float)r.Mix, (float)r.Darken, Parse(r.Side), Parse(r.Roof));
        }
        _ground.AddRange(def.Ground.Select(Compile));
        _structure.AddRange(def.Structure.Select(Compile));
        _tints.AddRange(def.Tints.Select(Compile));
        _layers.AddRange(def.Layers.Select(Compile));
        _fallback = Rgba.Parse(def.Fallback, new Rgba(0.45f, 0.55f, 0.35f));
        _structureFallback = Rgba.Parse(def.StructureFallback, new Rgba(0.6f, 0.58f, 0.54f));
        _layerFallback = Rgba.Parse(def.LayerFallback, new Rgba(0.6f, 0.58f, 0.55f));
        SkirtDarken = (float)def.SkirtDarken;
        VariationValue = (float)def.Variation.Value;
        VariationScale = MathF.Max(0.5f, (float)def.Variation.Scale);
        Water = Rgba.Parse(def.Water.Color, new Rgba(0.2f, 0.45f, 0.7f));
        WaterDeep = Rgba.Parse(def.Water.Deep, new Rgba(0.12f, 0.25f, 0.36f));
        WaterLight = Rgba.Parse(def.Water.Light, new Rgba(0.6f, 0.84f, 0.85f));
        WaterOpacity = (float)def.Water.Opacity;
        WaterRipple = (float)def.Water.Ripple;
        WaterSpeed = (float)def.Water.Speed;
    }

    private static Rgba? Parse(string? text) => text is not null && Rgba.TryParse(text, out var c) ? c : null;

    /// <summary>A structure cell's top colour, side colour and roof colour, or null for a cell that is not a structure.</summary>
    public (Rgba Top, Rgba Side, Rgba? Roof)? Structure(TagSet tags, CellFlags flags)
    {
        if ((flags & CellFlags.Structure) == 0 && !tags.Has("structure"))
        {
            return null;
        }
        var rule = _structure.FirstOrDefault(r => r.Matches(tags, flags));
        var top = rule?.Color ?? _structureFallback;
        return (top, rule?.Side ?? top.Darkened(SkirtDarken), rule?.Roof);
    }

    /// <summary>The colour of a cell before its jitter.</summary>
    public Rgba Cell(TagSet tags, CellFlags flags)
    {
        if (Structure(tags, flags) is { } s)
        {
            return s.Top;
        }
        var color = _ground.FirstOrDefault(r => r.Color is not null && r.Matches(tags, flags))?.Color ?? _fallback;
        foreach (var tint in _tints)
        {
            if (!tint.Matches(tags, flags))
            {
                continue;
            }
            if (tint.Color is { } c)
            {
                color = color.Lerp(c, tint.Mix);
            }
            if (tint.Darken > 0)
            {
                color = color.Darkened(tint.Darken);
            }
        }
        return color;
    }

    public Rgba Layer(TagSet tags) => _layers.FirstOrDefault(r => r.Matches(tags, CellFlags.None))?.Color ?? _layerFallback;

    public Rgba Skirt(Rgba color) => color.Darkened(SkirtDarken);

    /// <summary>
    /// The seeded value jitter of a cell: smooth patches a few cells wide from a hash of the cell's world
    /// position, so neighbouring chunks and both clients agree. Structures and roads stay even.
    /// </summary>
    public float Jitter(int cellX, int cellY, CellFlags flags)
    {
        if (VariationValue <= 0 || (flags & (CellFlags.Structure | CellFlags.Road)) != 0)
        {
            return 1;
        }
        var fx = cellX / VariationScale;
        var fy = cellY / VariationScale;
        var x0 = (int)MathF.Floor(fx);
        var y0 = (int)MathF.Floor(fy);
        var tx = fx - x0;
        var ty = fy - y0;
        static float Smooth(float t) => t * t * (3 - 2 * t);
        var sx = Smooth(tx);
        var sy = Smooth(ty);
        var a = Hash(x0, y0);
        var b = Hash(x0 + 1, y0);
        var c = Hash(x0, y0 + 1);
        var d = Hash(x0 + 1, y0 + 1);
        var v = (a + (b - a) * sx) + ((c + (d - c) * sx) - (a + (b - a) * sx)) * sy;
        var fine = Hash(cellX * 7 + 3, cellY * 11 + 5) * 0.3f;
        return 1 + ((v - 0.5f) * 2 * 0.7f + (fine - 0.15f) * 2) * VariationValue;
    }

    /// <summary>A hash of two integers to a fraction from zero to one, stable across clients.</summary>
    public static float Hash(int x, int y)
    {
        unchecked
        {
            var h = (uint)x * 374761393u + (uint)y * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / (float)0x1000000u;
        }
    }
}
