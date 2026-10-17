using System.Globalization;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;

namespace HollowWake.Core.Items;

/// <summary>Fills a freshly rolled item's payload: which glyph or weave it holds, at what level.</summary>
public interface IPayloadRoller
{
    void Fill(ItemGenerator generator, ItemInstance item, ItemBaseDef b, ItemGenerator.PayloadRequest? request, Rng rng);
}

/// <summary>
/// The items of the weave model (question Q1 of the parity plan): glyph stones, which hold a glyph at a level
/// with a rolled row per axis and socket into weave slots; and codices, which teach a form (composed with
/// Threads the hero chooses) or a whole preset weave at a level, and whose rarity sets the slot's sockets.
/// </summary>
public static class WeaveItems
{
    public const string StoneClass = "glyph";
    public const string CodexClass = "codex";

    public static bool IsStone(ItemInstance item) => item.Payload?.Glyph is not null;

    public static bool IsCodex(ItemInstance item) => item.Payload is { Glyph: null } p && (p.Form is not null || p.Weave is not null);

    /// <summary>The base items stones and codices are minted on, from rules <c>inventory.stone_base</c> and <c>inventory.codex_base</c>.</summary>
    public static string StoneBase(ContentRegistry content) => content.Rules.Text("inventory.stone_base", "glyph_stone");

    public static string CodexBase(ContentRegistry content) => content.Rules.Text("inventory.codex_base", "weave_codex");

    /// <summary>The form a codex teaches: its own, or its preset's.</summary>
    public static FormDef? FormOf(ContentRegistry content, ItemPayload payload)
    {
        if (payload.Weave is not null)
        {
            return content.Get<WeaveDef>(payload.Weave) is { } preset ? content.Get<FormDef>(preset.Form) : null;
        }
        return payload.Form is null ? null : content.Get<FormDef>(payload.Form);
    }

    /// <summary>
    /// The sockets a codex gives its weave slot: rule <c>inventory.codex_sockets.{rarity}</c>, capped by what the
    /// form can take beside the glyphs its preset already carries.
    /// </summary>
    public static int SocketsOf(ContentRegistry content, ItemInstance codex)
    {
        if (codex.Payload is not { } payload || FormOf(content, payload) is not { } form)
        {
            return 0;
        }
        var byRarity = (int)content.Rules.Number("inventory.codex_sockets." + codex.Rarity, 1);
        var builtIn = payload.Weave is not null && content.Get<WeaveDef>(payload.Weave) is { } preset ? preset.Glyphs.Count : 0;
        return Math.Clamp(byRarity, 0, Math.Max(0, form.MaxGlyphs - builtIn));
    }

    /// <summary>The name of what a codex teaches.</summary>
    public static string TeachingName(ContentRegistry content, ItemPayload payload)
    {
        if (payload.Weave is not null)
        {
            return content.Get<WeaveDef>(payload.Weave)?.DisplayName ?? Definition.Humanize(payload.Weave);
        }
        return payload.Form is null ? "" : content.Get<FormDef>(payload.Form)?.DisplayName ?? Definition.Humanize(payload.Form);
    }

    /// <summary>A name from a rule template such as <c>{name} Stone</c>.</summary>
    public static string Named(ContentRegistry content, string rule, string fallback, string name) =>
        content.Rules.Text(rule, fallback).Replace("{name}", name, StringComparison.Ordinal);

    /// <summary>A stone's glyph as a weave socket names it: id, level and rolled rows.</summary>
    public static GlyphRef RefOf(ItemInstance stone)
    {
        var payload = stone.Payload!;
        var rows = payload.Rows is { Count: > 0 } r ? new Dictionary<string, string>(r, StringComparer.Ordinal) : null;
        return rows is null
            ? new GlyphRef { Id = payload.Glyph!, Level = payload.Level }
            : new GlyphRef { Id = payload.Glyph!, Level = payload.Level, Rows = rows };
    }

    /// <summary>
    /// The weave a rack seat builds: the codex's preset or form at the codex's level, the seat's Threads (a form
    /// codex) and the socketed stones in socket order. Null for a seat without a codex.
    /// </summary>
    public static WeaveSpec? SpecOf(RackSeat seat)
    {
        if (seat.Codex?.Payload is not { } payload)
        {
            return null;
        }
        var glyphs = new List<GlyphRef>();
        foreach (var stone in seat.Stones)
        {
            if (stone is not null && IsStone(stone))
            {
                glyphs.Add(RefOf(stone));
            }
        }
        return payload.Weave is not null
            ? new WeaveSpec { Preset = payload.Weave, Threads = new List<string>(seat.Threads), Glyphs = glyphs, Level = Math.Max(1, payload.Level) }
            : new WeaveSpec { Form = payload.Form, Threads = new List<string>(seat.Threads), Glyphs = glyphs, Level = Math.Max(1, payload.Level) };
    }

    /// <summary>Requirements of what a weave item holds: the form's for a codex, the glyph's for a stone.</summary>
    public static IReadOnlyDictionary<string, double> RequirementsOf(ContentRegistry content, ItemInstance item)
    {
        if (item.Payload is not { } payload)
        {
            return Empty;
        }
        if (payload.Glyph is not null)
        {
            return content.Get<GlyphDef>(payload.Glyph)?.Requirements ?? Empty;
        }
        return FormOf(content, payload)?.Requirements ?? Empty;
    }

    private static readonly Dictionary<string, double> Empty = new(StringComparer.Ordinal);

    /// <summary>The first requirement the hero does not meet, as attribute and value, or null when all are met.</summary>
    public static (string Attribute, double Value)? Unmet(IReadOnlyDictionary<string, double> requirements, Stats stats)
    {
        foreach (var (attr, required) in requirements)
        {
            var have = attr == "level" ? stats.Level : stats.Get(attr);
            if (have < required)
            {
                return (attr, required);
            }
        }
        return null;
    }

    /// <summary>A requirement as a player reads it: the attribute's name and the value.</summary>
    public static string RequirementText(ContentRegistry content, string attribute, double value)
    {
        var name = attribute == "level" ? "Level" : content.Attributes.Get(attribute)?.DisplayName ?? Definition.Humanize(attribute);
        return $"{name} {value.ToString("0.##", CultureInfo.InvariantCulture)}";
    }

    /// <summary>A weighted draw over candidates in id order, with a lean toward ids the dropper uses.</summary>
    internal static T? Draw<T>(List<T> candidates, Func<T, double> weight, Func<T, string> id, IReadOnlyCollection<string>? lean, double leanFactor, Rng rng) where T : class
    {
        if (candidates.Count == 0)
        {
            return null;
        }
        candidates.Sort((a, b) => string.CompareOrdinal(id(a), id(b)));
        var weights = new List<double>(candidates.Count);
        foreach (var c in candidates)
        {
            var w = Math.Max(0, weight(c));
            if (lean is not null && lean.Contains(id(c)))
            {
                w *= leanFactor;
            }
            weights.Add(w);
        }
        var index = rng.PickWeighted(weights);
        return index < 0 ? candidates[0] : candidates[index];
    }

    internal static int LevelFrom(ContentRegistry content, string rule, int itemLevel, int? requested, int max)
    {
        var level = requested ?? (int)Math.Round(content.Rules.Eval(rule, "1", new DictScope().Set("item_level", itemLevel)));
        return Math.Clamp(level, 1, Math.Max(1, max));
    }
}

/// <summary>Fills a glyph stone: the glyph (asked for, or drawn by drop weight), its level, and a row per axis.</summary>
internal sealed class GlyphStonePayloads : IPayloadRoller
{
    public void Fill(ItemGenerator generator, ItemInstance item, ItemBaseDef b, ItemGenerator.PayloadRequest? request, Rng rng)
    {
        var content = generator.Content;
        GlyphDef? glyph = request?.Glyph is { } wanted ? content.Get<GlyphDef>(wanted) : null;
        if (glyph is null)
        {
            var candidates = new List<GlyphDef>();
            foreach (var g in content.Each<GlyphDef>())
            {
                if (g.DropWeight > 0 && g.MinLevel <= item.Level)
                {
                    candidates.Add(g);
                }
            }
            glyph = WeaveItems.Draw(candidates, g => g.DropWeight, g => g.Id, request?.Lean, content.Rules.Number("inventory.drop_lean", 4), rng);
        }
        if (glyph is null)
        {
            return;
        }
        var maxLevel = (int)content.Rules.Number("glyphs.max_level", 20);
        var payload = new ItemPayload { Glyph = glyph.Id, Level = WeaveItems.LevelFrom(content, "inventory.stone_level", item.Level, request?.Level, maxLevel) };
        foreach (var axis in glyph.Axes)
        {
            if (axis.Rows.Count == 0)
            {
                continue;
            }
            var weights = new List<double>(axis.Rows.Count);
            foreach (var row in axis.Rows)
            {
                weights.Add(Math.Max(0, row.Weight));
            }
            var index = rng.PickWeighted(weights);
            (payload.Rows ??= new Dictionary<string, string>(StringComparer.Ordinal))[axis.Id] = axis.Rows[Math.Max(0, index)].Id;
        }
        item.Payload = payload;
        item.Name = WeaveItems.Named(content, "inventory.names.stone", "{name} Stone", glyph.DisplayName);
    }
}

/// <summary>Fills a codex: the preset or form it teaches (asked for, or a form drawn by drop weight) and its level.</summary>
internal sealed class CodexPayloads : IPayloadRoller
{
    public void Fill(ItemGenerator generator, ItemInstance item, ItemBaseDef b, ItemGenerator.PayloadRequest? request, Rng rng)
    {
        var content = generator.Content;
        var payload = new ItemPayload();
        if (request?.Weave is { } weave && content.Has<WeaveDef>(weave))
        {
            payload.Weave = weave;
        }
        else
        {
            FormDef? form = request?.Form is { } wanted ? content.Get<FormDef>(wanted) : null;
            if (form is null)
            {
                var candidates = new List<FormDef>();
                foreach (var f in content.Each<FormDef>())
                {
                    if (f.DropWeight > 0 && f.MinLevel <= item.Level)
                    {
                        candidates.Add(f);
                    }
                }
                form = WeaveItems.Draw(candidates, f => f.DropWeight, f => f.Id, request?.Lean, content.Rules.Number("inventory.drop_lean", 4), rng);
            }
            if (form is null)
            {
                return;
            }
            payload.Form = form.Id;
        }
        payload.Level = WeaveItems.LevelFrom(content, "inventory.codex_level", item.Level, request?.Level, (int)content.Rules.Number("inventory.codex_max_level", 20));
        item.Payload = payload;
        item.Name = WeaveItems.Named(content, "inventory.names.codex", "{name} Codex", WeaveItems.TeachingName(content, payload));
    }
}
