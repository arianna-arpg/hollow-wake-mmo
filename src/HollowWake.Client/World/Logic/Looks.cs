using HollowWake.Client.UI.Logic;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Tags;

namespace HollowWake.Client.World.Logic;

/// <summary>
/// The appearance content as the clients use it (docs/design/appearance.md): looks by id and by tags, gear by
/// slot and item class, and the shared default entries of the world's surfaces, light and motion. Resolution
/// never reads a type: an entity's look id is tried first, then its pattern's tags against each entry's match
/// list, highest priority first, then the fallbacks by what it is.
/// </summary>
public sealed class AppearanceCatalog
{
    private readonly Dictionary<string, AppearanceDef> _byId = new(StringComparer.Ordinal);
    private readonly List<AppearanceDef> _bodiesByTags = new();
    private readonly List<AppearanceDef> _gear = new();
    private readonly ContentRegistry _content;
    private readonly Dictionary<string, TagSet> _patternTags = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _relations = new(StringComparer.Ordinal);

    public SurfacesDef Surfaces { get; }
    public WorldLookDef World { get; }
    public MotionLookDef Motion { get; }
    public MotionLook Timings { get; }

    public AppearanceCatalog(ContentRegistry content)
    {
        _content = content;
        foreach (var def in content.Each<AppearanceDef>().OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            _byId[def.Id] = def;
            if (def.Slot is not null)
            {
                _gear.Add(def);
            }
            else if (def.Match.Count > 0)
            {
                _bodiesByTags.Add(def);
            }
        }
        // Highest priority first; ties by id so two clients choose the same entry.
        _bodiesByTags.Sort(ByPriority);
        _gear.Sort(ByPriority);
        Surfaces = content.Get<SurfacesDef>("default") ?? content.Each<SurfacesDef>().FirstOrDefault() ?? new SurfacesDef { Id = "default" };
        World = content.Get<WorldLookDef>("default") ?? content.Each<WorldLookDef>().FirstOrDefault() ?? new WorldLookDef { Id = "default" };
        Motion = content.Get<MotionLookDef>("default") ?? content.Each<MotionLookDef>().FirstOrDefault() ?? new MotionLookDef { Id = "default" };
        Timings = new MotionLook(Motion);
        var players = content.Get<FactionDef>("players");
        foreach (var faction in content.Each<FactionDef>())
        {
            var stance = players is null ? Stance.Neutral
                : players.Relations.TryGetValue(faction.Id, out var r) ? r
                : faction.Relations.TryGetValue("players", out var back) ? back
                : faction.Default;
            _relations[faction.Id] = stance.ToString().ToLowerInvariant();
        }
    }

    private static int ByPriority(AppearanceDef a, AppearanceDef b)
    {
        var p = b.Priority.CompareTo(a.Priority);
        return p != 0 ? p : string.CompareOrdinal(a.Id, b.Id);
    }

    public AppearanceDef? ById(string id) => _byId.GetValueOrDefault(id);

    public IEnumerable<AppearanceDef> All => _byId.Values;

    /// <summary>The relation of a faction toward the players (hostile, friendly, allied, neutral), from the factions content.</summary>
    public string RelationOf(string? faction) => faction is not null && _relations.TryGetValue(faction, out var r) ? r : "neutral";

    /// <summary>A pattern's tags with those of everything it extends, as the factory merges them, plus its id as a tag.</summary>
    public TagSet PatternTags(string? patternId)
    {
        if (patternId is null)
        {
            return TagSet.Empty;
        }
        if (_patternTags.TryGetValue(patternId, out var cached))
        {
            return cached;
        }
        var tags = TagSet.Of(patternId);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var cur = _content.Get<PatternDef>(patternId);
        while (cur is not null && seen.Add(cur.Id))
        {
            tags = tags.Union(cur.Tags);
            cur = cur.Extends is null ? null : _content.Get<PatternDef>(cur.Extends);
        }
        _patternTags[patternId] = tags;
        return tags;
    }

    /// <summary>The body entry for an entity: by its look id, then by the tags of its pattern (or its place), then the fallbacks.</summary>
    public AppearanceDef? Resolve(string? look, string? patternId, TagSet? extraTags = null)
    {
        if (look is not null)
        {
            if (_byId.TryGetValue(look, out var byLook) && byLook.Slot is null)
            {
                return byLook;
            }
            if (look.StartsWith("poi:", StringComparison.Ordinal))
            {
                var poi = _content.Get<PoiDef>(look[4..]);
                var poiTags = poi?.Tags ?? TagSet.Empty;
                return ByTags(poiTags) ?? _byId.GetValueOrDefault("marker");
            }
            if (look.StartsWith("drop:", StringComparison.Ordinal))
            {
                return _byId.GetValueOrDefault("drop");
            }
            if (look == "projectile" || look == "field" || look == "fount")
            {
                return _byId.GetValueOrDefault(look);
            }
        }
        var tags = PatternTags(patternId);
        if (extraTags is not null)
        {
            tags = tags.Union(extraTags);
        }
        if (patternId is not null && _byId.TryGetValue(patternId, out var byPattern) && byPattern.Slot is null)
        {
            return byPattern;
        }
        return ByTags(tags);
    }

    private AppearanceDef? ByTags(TagSet tags)
    {
        foreach (var def in _bodiesByTags)
        {
            if (def.Match.All(tags.Has))
            {
                return def;
            }
        }
        return null;
    }

    /// <summary>The gear entry for an item worn in a slot, by its class (hierarchical) and tags; null draws nothing there.</summary>
    public AppearanceDef? GearFor(string slot, string? itemClass, IEnumerable<string>? itemTags)
    {
        if (itemClass is null && itemTags is null)
        {
            return null;
        }
        var tags = TagSet.Empty;
        if (itemClass is not null)
        {
            tags = tags.With(itemClass);
        }
        if (itemTags is not null)
        {
            foreach (var t in itemTags)
            {
                tags = tags.With(t);
            }
        }
        foreach (var def in _gear)
        {
            if (def.Slot == slot && def.Match.Count > 0 && def.Match.All(tags.Has))
            {
                return def;
            }
        }
        return null;
    }

    /// <summary>A gear entry for a dropped or carried item base, in whichever slot it belongs to.</summary>
    public AppearanceDef? GearForItem(string itemBase)
    {
        var item = _content.Get<ItemBaseDef>(itemBase);
        if (item is null)
        {
            return null;
        }
        var tags = item.Tags.With(item.Class);
        foreach (var def in _gear)
        {
            if (def.Match.Count > 0 && def.Match.All(tags.Has))
            {
                return def;
            }
        }
        // Anything else lies on the ground as a parcel.
        return _byId.GetValueOrDefault("gear_sack");
    }

    /// <summary>The item base's class and tags, for gear resolution of the self's equipment and monsters' grants.</summary>
    public (string? Class, IEnumerable<string> Tags)? ItemLook(string itemBase)
    {
        var item = _content.Get<ItemBaseDef>(itemBase);
        return item is null ? null : (item.Class, item.Tags.Items);
    }

    /// <summary>The default gear a body wears: the entry's gear map, else for a player look the Calling's starting items.</summary>
    public IReadOnlyDictionary<string, (string? Class, IEnumerable<string> Tags)> DefaultGear(AppearanceDef body, string? patternId)
    {
        var gear = new Dictionary<string, (string? Class, IEnumerable<string> Tags)>(StringComparer.Ordinal);
        foreach (var (slot, itemClass) in body.Gear)
        {
            gear[slot] = (itemClass, SplitClass(itemClass));
        }
        if (patternId is not null && _content.Get<PatternDef>(patternId) is { } pattern)
        {
            // Monsters wear what their pattern grants (the brigand's sword and jerkin), over the entry's defaults.
            foreach (var p in Chain(pattern))
            {
                if (p.Components.TryGetValue("equipment", out var equipment) && equipment["grants"] is System.Text.Json.Nodes.JsonArray grants)
                {
                    foreach (var grant in grants)
                    {
                        var itemId = Json.Str(grant?["item"]);
                        if (itemId.Length > 0 && _content.Get<ItemBaseDef>(itemId) is { Slot: { } slot } item)
                        {
                            gear[slot] = (item.Class, item.Tags.Items);
                        }
                    }
                }
            }
        }
        if (gear.Count == 0 && _content.Each<CallingDef>().FirstOrDefault(c => c.Look == body.Id || c.Id == body.Id) is { } calling)
        {
            foreach (var grant in calling.Items)
            {
                if (_content.Get<ItemBaseDef>(grant.Item) is { Slot: { } slot } item && !gear.ContainsKey(slot))
                {
                    gear[slot] = (item.Class, item.Tags.Items);
                }
            }
        }
        return gear;
    }

    private IEnumerable<PatternDef> Chain(PatternDef pattern)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var cur = pattern;
        while (cur is not null && seen.Add(cur.Id))
        {
            yield return cur;
            cur = cur.Extends is null ? null : _content.Get<PatternDef>(cur.Extends);
        }
    }

    /// <summary>An item class as tags: armor.chest.heavy carries armor, armor.chest and the trailing word as its own tag.</summary>
    private static IEnumerable<string> SplitClass(string itemClass)
    {
        yield return itemClass;
        var parts = itemClass.Split('.');
        if (parts.Length >= 3)
        {
            yield return parts[^1];
            yield return string.Join('.', parts[..2]);
        }
    }
}

/// <summary>Seeded jitter from an entity key, the same on every client: nothing here rolls a gameplay stream.</summary>
public static class Variation
{
    /// <summary>A stable hash of a key and a salt, as a fraction from zero to one.</summary>
    public static float Unit(long key, int salt)
    {
        unchecked
        {
            var h = (ulong)key * 0x9E3779B97F4A7C15UL + (ulong)salt * 0xBF58476D1CE4E5B9UL;
            h ^= h >> 31;
            h *= 0x94D049BB133111EBUL;
            h ^= h >> 29;
            return (h & 0xFFFFFFUL) / (float)0x1000000UL;
        }
    }

    /// <summary>A signed fraction from minus one to one.</summary>
    public static float Signed(long key, int salt) => Unit(key, salt) * 2 - 1;

    /// <summary>A palette colour shifted by the entry's hue and value jitter for this key.</summary>
    public static Rgba Jitter(Rgba color, AppearanceVariation variation, long key)
    {
        var hue = Signed(key, 1) * (float)variation.Hue;
        var value = 1 + Signed(key, 2) * (float)variation.Value;
        var (h, s, v) = ToHsv(color);
        h = (h + hue + 1) % 1;
        v = Math.Clamp(v * value, 0, 1);
        return FromHsv(h, s, v, color.A);
    }

    public static float ScaleOf(AppearanceVariation variation, long key) => 1 + Signed(key, 3) * (float)variation.Scale;

    public static (float H, float S, float V) ToHsv(Rgba c)
    {
        var max = MathF.Max(c.R, MathF.Max(c.G, c.B));
        var min = MathF.Min(c.R, MathF.Min(c.G, c.B));
        var d = max - min;
        var h = 0f;
        if (d > 1e-6f)
        {
            if (max == c.R)
            {
                h = ((c.G - c.B) / d + 6) % 6;
            }
            else if (max == c.G)
            {
                h = (c.B - c.R) / d + 2;
            }
            else
            {
                h = (c.R - c.G) / d + 4;
            }
            h /= 6;
        }
        return (h, max <= 1e-6f ? 0 : d / max, max);
    }

    public static Rgba FromHsv(float h, float s, float v, float a = 1)
    {
        var i = (int)MathF.Floor(h * 6) % 6;
        var f = h * 6 - MathF.Floor(h * 6);
        var p = v * (1 - s);
        var q = v * (1 - f * s);
        var t = v * (1 - (1 - f) * s);
        return i switch
        {
            0 => new Rgba(v, t, p, a),
            1 => new Rgba(q, v, p, a),
            2 => new Rgba(p, v, t, a),
            3 => new Rgba(p, q, v, a),
            4 => new Rgba(t, p, v, a),
            _ => new Rgba(v, p, q, a),
        };
    }
}

/// <summary>The motion timings with their defaults, read once from the motion content.</summary>
public sealed class MotionLook
{
    private readonly MotionLookDef _def;

    public MotionLook(MotionLookDef def)
    {
        _def = def;
    }

    private static float Get(Dictionary<string, double> group, string name, float fallback) => group.TryGetValue(name, out var v) ? (float)v : fallback;

    public float Walk(string name, float fallback) => Get(_def.Walk, name, fallback);
    public float Idle(string name, float fallback) => Get(_def.Idle, name, fallback);
    public float Swing(string name, float fallback) => Get(_def.Swing, name, fallback);
    public float Cast(string name, float fallback) => Get(_def.Cast, name, fallback);
    public float Hit(string name, float fallback) => Get(_def.Hit, name, fallback);
    public float Death(string name, float fallback) => Get(_def.Death, name, fallback);
    public float Camera(string name, float fallback) => Get(_def.Camera, name, fallback);
    public float CombatText(string name, float fallback) => Get(_def.CombatText, name, fallback);
    public float Labels(string name, float fallback) => Get(_def.Labels, name, fallback);
    public ContactLook Contact => _def.Contact;
}
