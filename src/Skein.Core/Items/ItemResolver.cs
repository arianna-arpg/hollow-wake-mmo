using Skein.Core.Attributes;
using Skein.Core.Content;
using Skein.Core.Definitions;
using Skein.Core.Expressions;
using Skein.Core.Tags;

namespace Skein.Core.Items;

/// <summary>Reads what an item is worth: its tags, its lines as modifiers, its local attributes, its display.</summary>
public sealed class ItemResolver
{
    private readonly ContentRegistry _content;

    public ItemResolver(ContentRegistry content)
    {
        _content = content;
    }

    public ContentRegistry Content => _content;

    public ItemBaseDef BaseOf(ItemInstance item) => _content.Require<ItemBaseDef>(item.Base);

    public static TagSet ClassTags(string itemClass)
    {
        // weapon.sword -> [weapon weapon.sword]
        var parts = itemClass.Split('.');
        var tags = new List<string>();
        var acc = "";
        foreach (var p in parts)
        {
            acc = acc.Length == 0 ? p : acc + "." + p;
            tags.Add(acc);
        }
        return TagSet.From(tags);
    }

    public TagSet TagsOf(ItemInstance item)
    {
        var b = BaseOf(item);
        var tags = b.Tags.Union(ClassTags(b.Class)).With("item", "rarity." + item.Rarity);
        if (item.Unique is not null)
        {
            tags = tags.With("unique");
        }
        return tags;
    }

    /// <summary>Default per-tier growth for implicit and unique lines.</summary>
    private double TierScale(ModLine line, string ruleKey) => line.TierScale ?? _content.Rules.Number(ruleKey, 0.1);

    private double LineValue(ModLine line, double roll, int tier, double tierScale)
    {
        if (line.Range is { Length: >= 2 } r)
        {
            var v = r[0] + (r[1] - r[0]) * Math.Clamp(roll, 0, 1);
            return v * (1 + tierScale * (tier - 1));
        }
        if (line.Range is { Length: 1 } one)
        {
            return one[0] * (1 + tierScale * (tier - 1));
        }
        return line.Value;
    }

    /// <summary>Every modifier line with its rolled value resolved. Local lines are tagged so callers can split them.</summary>
    public IEnumerable<(ModLine Line, double Value, string Origin)> Lines(ItemInstance item)
    {
        var b = BaseOf(item);
        for (var i = 0; i < b.Implicits.Count; i++)
        {
            var line = b.Implicits[i];
            var roll = i < item.ImplicitRolls.Count ? item.ImplicitRolls[i] : 0.5;
            yield return (line, LineValue(line, roll, item.Tier, TierScale(line, "items.implicit_tier_scale")), "implicit");
        }
        foreach (var a in item.Affixes)
        {
            if (!_content.TryGet<AffixDef>(a.Affix, out var def) || def is null)
            {
                continue;
            }
            var tierIndex = Math.Clamp(a.Tier, 0, def.Tiers.Count - 1);
            var tier = def.Tiers.Count > 0 ? def.Tiers[tierIndex] : null;
            for (var i = 0; i < def.Lines.Count; i++)
            {
                var line = def.Lines[i];
                var roll = i < a.Rolls.Count ? a.Rolls[i] : 0.5;
                double value;
                if (tier is not null && i < tier.Ranges.Count && tier.Ranges[i].Length >= 2)
                {
                    value = tier.Ranges[i][0] + (tier.Ranges[i][1] - tier.Ranges[i][0]) * Math.Clamp(roll, 0, 1);
                }
                else if (tier is not null && i < tier.Ranges.Count && tier.Ranges[i].Length == 1)
                {
                    value = tier.Ranges[i][0];
                }
                else
                {
                    value = LineValue(line, roll, 1, 0);
                }
                yield return (line, value, "affix:" + a.Affix);
            }
        }
        if (item.Unique is not null && _content.TryGet<UniqueDef>(item.Unique, out var unique) && unique is not null)
        {
            for (var i = 0; i < unique.Lines.Count; i++)
            {
                var line = unique.Lines[i];
                var roll = i < item.UniqueRolls.Count ? item.UniqueRolls[i] : 0.5;
                yield return (line, LineValue(line, roll, item.Tier, TierScale(line, "items.unique_tier_scale")), "unique");
            }
        }
    }

    /// <summary>Modifiers the wearer gains. Source is item:{uid} so unequipping removes them in one call.</summary>
    public List<Modifier> WearerModifiers(ItemInstance item)
    {
        var list = new List<Modifier>();
        foreach (var (line, value, _) in Lines(item))
        {
            if (line.Local)
            {
                continue;
            }
            list.Add(new Modifier
            {
                Attribute = line.Attribute,
                Form = line.Form,
                Value = value,
                ValueExpr = line.ValueExpr,
                From = line.From,
                Scope = line.Scope,
                Condition = line.Condition,
                Gauge = line.Gauge,
                GaugeAt = line.GaugeAt,
                Priority = line.Priority,
                Source = $"item:{item.Uid}",
            });
        }
        return list;
    }

    /// <summary>
    /// The item's own numbers: weapon damage, attack time, armor, evasion, ward. Bases come from the
    /// family's local ranges (rolled by the base roll) and the defense budget rule; local lines modify them.
    /// </summary>
    public AttributeSet LocalAttributes(ItemInstance item)
    {
        var b = BaseOf(item);
        var set = new AttributeSet(_content.Attributes);
        var qualityMult = 1 + _content.Rules.Number("items.quality_bonus", 0.2) * Math.Clamp(item.Quality, 0, 1);
        var tierMult = 1 + _content.Rules.Number("items.local_tier_scale", 0.12) * (item.Tier - 1);
        foreach (var (attr, range) in b.Local)
        {
            var v = range.Length >= 2 ? range[0] + (range[1] - range[0]) * item.BaseRoll : range[0];
            set.SetBase(attr, v * tierMult * (attr.Contains("time", StringComparison.Ordinal) ? 1 : qualityMult));
        }
        if (b.Defense.Count > 0 && b.Slot is not null)
        {
            var scope = new DictScope().Set("item_level", item.Level).Set("tier", item.Tier)
                .Set("slot_budget", _content.Rules.Number($"items.slot_budget.{b.Slot}", 1.0))
                .Set("base_roll", item.BaseRoll);
            var budget = _content.Rules.Eval("items.defense_budget", "(16 + 5 * item_level) * (1 + 0.3 * (tier - 1)) * slot_budget * (0.9 + 0.2 * base_roll)", scope);
            var total = b.Defense.Values.Sum();
            foreach (var (kind, weight) in b.Defense)
            {
                set.SetBase(kind, budget * (total <= 0 ? 0 : weight / total) * qualityMult);
            }
        }
        foreach (var (line, value, origin) in Lines(item))
        {
            if (!line.Local)
            {
                continue;
            }
            set.Add(new Modifier { Attribute = line.Attribute, Form = line.Form, Value = value, Source = origin });
        }
        return set;
    }

    public double Local(ItemInstance item, string attribute) => LocalValue(LocalAttributes(item), attribute);

    /// <summary>A local attribute the item actually defines; attributes the item never set read as zero.</summary>
    public static bool Defines(AttributeSet local, string attribute) => local.TryGetBaseOverride(attribute, out _) || local.HasModifiers(attribute);

    public static double LocalValue(AttributeSet local, string attribute) => Defines(local, attribute) ? local.Get(attribute, EmptyScope.Instance) : 0;

    public string TierName(ItemInstance item)
    {
        var b = BaseOf(item);
        if (b.NamesByTier.Count >= item.Tier && item.Tier >= 1)
        {
            return b.NamesByTier[item.Tier - 1];
        }
        var names = _content.Rules.Strings("items.tier_names");
        var prefix = item.Tier - 1 < names.Count && item.Tier >= 1 ? names[item.Tier - 1] : "";
        return string.IsNullOrEmpty(prefix) ? b.DisplayName : prefix + " " + b.DisplayName;
    }

    public IEnumerable<string> Describe(ItemInstance item)
    {
        var b = BaseOf(item);
        yield return $"{item.Name} [{item.Rarity}] {b.Class} L{item.Level} T{item.Tier}";
        var local = LocalAttributes(item);
        foreach (var attr in local.BaseOverrides.Select(kv => kv.Key).Concat(local.ModifiedAttributes).Distinct())
        {
            yield return $"  {attr}: {LocalValue(local, attr):0.##}";
        }
        foreach (var (line, value, origin) in Lines(item))
        {
            if (line.Local)
            {
                continue;
            }
            var m = new Modifier { Attribute = line.Attribute, Form = line.Form, Value = value, Scope = line.Scope };
            yield return $"  {m} ({origin})";
        }
    }
}
