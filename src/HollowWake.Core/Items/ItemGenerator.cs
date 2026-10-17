using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Expressions;
using HollowWake.Core.Maths;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Items;

public sealed record LootResult(ItemInstance? Item, string? Currency, double Amount);

/// <summary>
/// Rolls items and loot deterministically from content: rarity, affix count, eligible affix families,
/// tiers biased by item level, roll fractions, names, uniques, and nested loot tables.
/// </summary>
public sealed class ItemGenerator
{
    private readonly ContentRegistry _content;
    private readonly ItemResolver _resolver;
    private readonly Dictionary<string, IPayloadRoller> _payloads = new(StringComparer.Ordinal);
    private long _nextUid;

    public ItemGenerator(ContentRegistry content, long firstUid = 1)
    {
        _content = content;
        _resolver = new ItemResolver(content);
        _nextUid = firstUid;
        RegisterPayload("glyph", new GlyphStonePayloads());
        RegisterPayload("codex", new CodexPayloads());
    }

    public ItemResolver Resolver => _resolver;

    public ContentRegistry Content => _content;

    /// <summary>
    /// What a weave item should hold: a glyph for a stone, a form or a preset weave for a codex, at a level. Left
    /// empty, the item draws one by drop weight, leaning toward <see cref="Lean"/>.
    /// </summary>
    public sealed record PayloadRequest
    {
        public string? Glyph { get; init; }
        public string? Form { get; init; }
        public string? Weave { get; init; }
        public int? Level { get; init; }
        /// <summary>Forms and glyphs the dropper weaves with: drawn more often, by rule <c>inventory.drop_lean</c>.</summary>
        public IReadOnlyCollection<string>? Lean { get; init; }
    }

    /// <summary>Registers what fills the payload of items whose class starts with <paramref name="itemClass"/>.</summary>
    public void RegisterPayload(string itemClass, IPayloadRoller roller) => _payloads[itemClass] = roller;

    public long NextUid() => Interlocked.Increment(ref _nextUid);

    public void SeedUid(long value) => _nextUid = Math.Max(_nextUid, value);

    public int TierFor(int itemLevel)
    {
        var breaks = _content.Rules.Strings("items.tier_breaks").Select(s => int.TryParse(s, out var v) ? v : 1).ToList();
        if (breaks.Count == 0)
        {
            breaks = new List<int> { 1, 8, 16, 26, 38, 52 };
        }
        var tier = 1;
        for (var i = 0; i < breaks.Count; i++)
        {
            if (itemLevel >= breaks[i])
            {
                tier = i + 1;
            }
        }
        return tier;
    }

    public string RollRarity(Rng rng, IReadOnlyDictionary<string, double>? weights = null)
    {
        var rarities = _content.Each<ItemRarityDef>().OrderBy(r => r.Rank).ToList();
        if (rarities.Count == 0)
        {
            return "common";
        }
        var w = rarities.Select(r => weights is not null && weights.TryGetValue(r.Id, out var ow) ? ow : r.Weight).ToList();
        var idx = rng.PickWeighted(w);
        return idx < 0 ? rarities[0].Id : rarities[idx].Id;
    }

    /// <summary>
    /// Rolls a base family at an item level and rarity. Null rarity rolls it from the rarity table. A base whose
    /// class has a payload roller (glyph stones, codices) also gets its payload, as <paramref name="payload"/> asks.
    /// </summary>
    public ItemInstance Roll(string baseId, int itemLevel, string? rarity, Rng rng, PayloadRequest? payload = null)
    {
        var item = RollBase(baseId, itemLevel, rarity, rng);
        var b = _content.Require<ItemBaseDef>(baseId);
        var root = b.Class.Contains('.') ? b.Class[..b.Class.IndexOf('.')] : b.Class;
        if (_payloads.TryGetValue(root, out var roller))
        {
            roller.Fill(this, item, b, payload, rng);
        }
        return item;
    }

    // The contracts added after the first mints (superior, overroll, sockets, unique choices) draw from a stream of
    // the item's own seed, so the shared stream draws exactly what it drew before and old seeds mint the same items.
    private static readonly ulong MintSalt = Rng.Hash64("mint");

    private static Rng MintStream(ulong seed) => new(seed ^ MintSalt);

    private ItemInstance RollBase(string baseId, int itemLevel, string? rarity, Rng rng)
    {
        var b = _content.Require<ItemBaseDef>(baseId);
        rarity ??= RollRarity(rng);
        rarity = AtLeast(rarity, b.MinRarity);
        var rarityDef = _content.Get<ItemRarityDef>(rarity);
        var tier = TierFor(itemLevel);
        var window = rarityDef?.BaseRollWindow ?? 0.05;
        var baseRoll = Math.Clamp(0.5 + (rng.NextDouble() - 0.5) * 2 * Math.Max(window, 0.5), 0, 1);
        var seed = rng.NextUInt64();
        var mint = MintStream(seed);
        var superior = rarityDef?.Superior is { } sup && b.Stack <= 1 && mint.Chance(sup.Chance);
        if (superior)
        {
            baseRoll = 1 - Math.Clamp(rarityDef!.Superior!.Window, 0, 1) * mint.NextDouble();
        }
        var item = new ItemInstance
        {
            Uid = NextUid(),
            Base = baseId,
            Level = Math.Max(1, itemLevel),
            Tier = tier,
            Rarity = rarity,
            BaseRoll = baseRoll,
            Stack = b.Stack,
            Seed = seed,
            Superior = superior,
        };
        for (var i = 0; i < b.Implicits.Count; i++)
        {
            item.ImplicitRolls.Add(rng.NextDouble());
        }
        if (rarityDef is not null && b.Stack <= 1)
        {
            var overroll = rarityDef.Overroll is { } over && mint.Chance(over.Chance) ? mint.Range(1, Math.Max(1, over.MaxSteps)) : 0;
            RollAffixes(item, b, rarityDef, rng, overroll);
        }
        item.Name = MintName(item, b, rng);
        if (superior)
        {
            item.Name = _content.Rules.Text("items.superior_name", "Superior {name}").Replace("{name}", item.Name, StringComparison.Ordinal);
        }
        item.Sockets = MintSockets(b, rarity, mint);
        return item;
    }

    /// <summary>A rarity raised to a floor (a base's <c>min_rarity</c>), by rank.</summary>
    private string AtLeast(string rarity, string? floor)
    {
        if (floor is null || floor == rarity || _content.Get<ItemRarityDef>(floor) is not { } min)
        {
            return rarity;
        }
        return (_content.Get<ItemRarityDef>(rarity)?.Rank ?? int.MinValue) < min.Rank ? floor : rarity;
    }

    /// <summary>
    /// Gear sockets at mint (row I7): each socket up to the rarity's most rolls its chance (rules
    /// <c>inventory.sockets.rarity.{rarity}</c>), bounded by the cap of the item's class (<c>inventory.sockets.caps</c>,
    /// the longest class prefix that names one). Null when the item has none.
    /// </summary>
    private List<ItemInstance?>? MintSockets(ItemBaseDef b, string rarity, Rng mint)
    {
        if (b.Stack > 1 || b.Slot is null)
        {
            return null;
        }
        var cap = SocketCap(_content, b.Class);
        var most = Math.Min(cap, (int)_content.Rules.Number("inventory.sockets.rarity." + rarity + ".max", 0));
        if (most <= 0)
        {
            return null;
        }
        var chance = _content.Rules.Number("inventory.sockets.rarity." + rarity + ".chance", 0);
        var count = 0;
        while (count < most && mint.Chance(chance))
        {
            count++;
        }
        return count == 0 ? null : new List<ItemInstance?>(new ItemInstance?[count]);
    }

    /// <summary>The most sockets an item class holds: the cap of the longest class prefix in rules <c>inventory.sockets.caps</c>, zero when none names it.</summary>
    public static int SocketCap(ContentRegistry content, string itemClass)
    {
        var cls = itemClass;
        while (true)
        {
            var cap = content.Rules.Number("inventory.sockets.caps." + cls, -1);
            if (cap >= 0)
            {
                return (int)cap;
            }
            var dot = cls.LastIndexOf('.');
            if (dot < 0)
            {
                return 0;
            }
            cls = cls[..dot];
        }
    }

    public ItemInstance RollUnique(string uniqueId, int itemLevel, Rng rng)
    {
        var u = _content.Require<UniqueDef>(uniqueId);
        var b = _content.Require<ItemBaseDef>(u.Base);
        var item = new ItemInstance
        {
            Uid = NextUid(),
            Base = u.Base,
            Level = Math.Max(itemLevel, u.MinLevel),
            Tier = TierFor(Math.Max(itemLevel, u.MinLevel)),
            Rarity = "unique",
            Unique = uniqueId,
            BaseRoll = rng.NextDouble(),
            Stack = 1,
            Seed = rng.NextUInt64(),
            Name = u.DisplayName,
        };
        for (var i = 0; i < b.Implicits.Count; i++)
        {
            item.ImplicitRolls.Add(rng.NextDouble());
        }
        for (var i = 0; i < u.Lines.Count; i++)
        {
            item.UniqueRolls.Add(rng.NextDouble());
        }
        if (u.Choices.Count > 0)
        {
            item.UniqueChoices = RollChoices(u, MintStream(item.Seed));
        }
        item.Sockets = MintSockets(b, "unique", MintStream(item.Seed ^ 1));
        return item;
    }

    /// <summary>A unique's picks from each choice family (row I5): by weight, without repeats, with a roll per line.</summary>
    private static List<UniqueChoiceRoll> RollChoices(UniqueDef u, Rng rng)
    {
        var picks = new List<UniqueChoiceRoll>();
        foreach (var choice in u.Choices)
        {
            var open = new List<UniqueOptionDef>(choice.Options);
            for (var n = 0; n < choice.Pick && open.Count > 0; n++)
            {
                var weights = new List<double>(open.Count);
                foreach (var option in open)
                {
                    weights.Add(option.Weight);
                }
                var i = Math.Max(0, rng.PickWeighted(weights));
                var pick = open[i];
                open.RemoveAt(i);
                var roll = new UniqueChoiceRoll { Choice = choice.Id, Option = pick.Id };
                for (var l = 0; l < pick.Lines.Count; l++)
                {
                    roll.Rolls.Add(rng.NextDouble());
                }
                picks.Add(roll);
            }
        }
        return picks;
    }

    /// <summary>Picks a base family matching a class filter such as weapon or armor.chest by drop weight.</summary>
    public ItemInstance? RollFromClass(string? classFilter, int itemLevel, string? rarity, Rng rng)
    {
        var filter = classFilter is null ? TagSet.Empty : TagSet.Of(classFilter);
        var options = _content.Each<ItemBaseDef>()
            .Where(b => b.DropWeight > 0 && b.MinLevel <= itemLevel && (filter.IsEmpty || ItemResolver.ClassTags(b.Class).HasAny(filter) || b.Tags.HasAny(filter)))
            .OrderBy(b => b.Id, StringComparer.Ordinal)
            .ToList();
        if (options.Count == 0)
        {
            return null;
        }
        var idx = rng.PickWeighted(options.Select(o => o.DropWeight).ToList());
        return Roll(options[Math.Max(0, idx)].Id, itemLevel, rarity, rng);
    }

    // ------------------------------------------------------------ affixes

    private void RollAffixes(ItemInstance item, ItemBaseDef b, ItemRarityDef rarity, Rng rng, int overroll = 0)
    {
        var maxPrefixes = rarity.Prefixes;
        var maxSuffixes = rarity.Suffixes;
        if (b.AffixCap.TryGetValue("prefix", out var pc))
        {
            maxPrefixes = Math.Min(maxPrefixes, pc);
        }
        if (b.AffixCap.TryGetValue("suffix", out var sc))
        {
            maxSuffixes = Math.Min(maxSuffixes, sc);
        }
        var total = maxPrefixes + maxSuffixes;
        if (rarity.CountWeights.Count > 0)
        {
            var idx = rng.PickWeighted(rarity.CountWeights.Select(c => c.Weight).ToList());
            if (idx >= 0)
            {
                total = Math.Min(total, rarity.CountWeights[idx].Count);
            }
        }
        var prefixes = 0;
        var suffixes = 0;
        for (var n = 0; n < total; n++)
        {
            AffixKind kind;
            if (prefixes >= maxPrefixes)
            {
                kind = AffixKind.Suffix;
            }
            else if (suffixes >= maxSuffixes)
            {
                kind = AffixKind.Prefix;
            }
            else
            {
                kind = rng.Chance(0.5) ? AffixKind.Prefix : AffixKind.Suffix;
            }
            if (!TryAddAffix(item, b, kind, rng, overroll: overroll))
            {
                break;
            }
            if (kind == AffixKind.Prefix)
            {
                prefixes++;
            }
            else
            {
                suffixes++;
            }
        }
    }

    /// <summary>Adds one affix of a kind if any family is eligible. Used by drops and by the crafting bench.</summary>
    /// <param name="overroll">Tiers above the item level's gate that may roll, each step losing weight by the rarity's step decay.</param>
    public bool TryAddAffix(ItemInstance item, ItemBaseDef b, AffixKind kind, Rng rng, bool crafted = false, int overroll = 0)
    {
        var tags = _resolver.TagsOf(item);
        var usedGroups = item.Affixes.Select(a => _content.Get<AffixDef>(a.Affix)?.Group ?? a.Affix).ToHashSet(StringComparer.Ordinal);
        var isMagic = item.Rarity == "magic";
        var options = new List<(AffixDef def, int tierIndex)>();
        var weights = new List<double>();
        var tierBiasLow = _content.Rules.Number("items.affix.tier_bias_low", 0.85);
        var tierBiasHigh = _content.Rules.Number("items.affix.tier_bias_high", 0.5);
        foreach (var def in _content.Each<AffixDef>().OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            if (def.Kind != kind || usedGroups.Contains(string.IsNullOrEmpty(def.Group) ? def.Id : def.Group))
            {
                continue;
            }
            if (b.AffixPool is { } pool)
            {
                // An explicit pool names what rolls; tags do not widen or narrow it.
                if (!pool.Contains(def.Id) && (string.IsNullOrEmpty(def.Group) || !pool.Contains(def.Group)))
                {
                    continue;
                }
            }
            else if ((!def.Requires.IsEmpty && !tags.HasAny(def.Requires)) || (!def.Excludes.IsEmpty && tags.HasAny(def.Excludes)))
            {
                continue;
            }
            var affinity = 1.0;
            foreach (var theme in def.Themes)
            {
                if (b.Affinity.TryGetValue(theme, out var mult))
                {
                    affinity *= mult;
                }
            }
            // Tiers are best first; eligible ones have level <= item level. Steps down the ladder lose weight.
            var eligible = new List<int>();
            for (var i = 0; i < def.Tiers.Count; i++)
            {
                var t = def.Tiers[i];
                if (t.Level <= item.Level && (!t.MagicOnly || isMagic))
                {
                    eligible.Add(i);
                }
            }
            if (eligible.Count == 0)
            {
                continue;
            }
            var depth = Math.Clamp((item.Level - def.Tiers[eligible[0]].Level) / 40.0, 0, 1);
            var bias = tierBiasLow + (tierBiasHigh - tierBiasLow) * depth;
            for (var step = 0; step < eligible.Count; step++)
            {
                var ti = eligible[step];
                var w = def.Weight * affinity * def.Tiers[ti].Weight * Math.Pow(bias, step);
                options.Add((def, ti));
                weights.Add(w);
            }
            if (overroll > 0 && eligible[0] > 0)
            {
                // Tiers are best first, so the tiers above the gate sit just before the best eligible one.
                var decay = _content.Get<ItemRarityDef>(item.Rarity)?.Overroll?.StepDecay ?? 0.5;
                for (var up = 1; up <= overroll && eligible[0] - up >= 0; up++)
                {
                    var ti = eligible[0] - up;
                    if (def.Tiers[ti].MagicOnly && !isMagic)
                    {
                        continue;
                    }
                    options.Add((def, ti));
                    weights.Add(def.Weight * affinity * def.Tiers[ti].Weight * Math.Pow(decay, up));
                }
            }
        }
        if (options.Count == 0)
        {
            return false;
        }
        var pick = rng.PickWeighted(weights);
        if (pick < 0)
        {
            return false;
        }
        var (chosen, tierIdx) = options[pick];
        var roll = new AffixRoll { Affix = chosen.Id, Tier = tierIdx, Crafted = crafted };
        for (var i = 0; i < chosen.Lines.Count; i++)
        {
            roll.Rolls.Add(rng.NextDouble());
        }
        item.Affixes.Add(roll);
        return true;
    }

    private string MintName(ItemInstance item, ItemBaseDef b, Rng rng)
    {
        var baseName = _resolver.TierName(item);
        switch (item.Rarity)
        {
            case "magic":
            {
                var prefix = item.Affixes.Select(a => _content.Get<AffixDef>(a.Affix)).FirstOrDefault(d => d?.Kind == AffixKind.Prefix);
                var suffix = item.Affixes.Select(a => _content.Get<AffixDef>(a.Affix)).FirstOrDefault(d => d?.Kind == AffixKind.Suffix);
                var name = baseName;
                if (prefix is not null && prefix.Names.Count > 0)
                {
                    var tier = item.Affixes.First(a => a.Affix == prefix.Id).Tier;
                    name = prefix.Names[Math.Min(tier, prefix.Names.Count - 1)] + " " + name;
                }
                if (suffix is not null && suffix.Names.Count > 0)
                {
                    var tier = item.Affixes.First(a => a.Affix == suffix.Id).Tier;
                    name = name + " " + suffix.Names[Math.Min(tier, suffix.Names.Count - 1)];
                }
                return name;
            }
            case "rare":
            {
                var first = _content.Rules.Strings("items.rare_names.first");
                var second = _content.Rules.Strings("items.rare_names.second");
                if (first.Count > 0 && second.Count > 0)
                {
                    return rng.Pick(first) + rng.Pick(second) + ", " + baseName;
                }
                return "Rare " + baseName;
            }
            default:
                return baseName;
        }
    }

    // ------------------------------------------------------------ loot

    /// <param name="lean">Forms and glyphs the dropper weaves with; stones and codices without a fixed payload lean toward them.</param>
    public List<LootResult> RollLoot(string tableId, int itemLevel, Rng rng, IExprScope? scope = null, int depth = 0, IReadOnlyCollection<string>? lean = null)
    {
        var results = new List<LootResult>();
        var maxDepth = (int)_content.Rules.Number("items.loot.max_depth", 8);
        if (depth > maxDepth || !_content.TryGet<LootTableDef>(tableId, out var table) || table is null)
        {
            return results;
        }
        scope ??= new DictScope().Set("item_level", itemLevel);
        foreach (var roll in table.Rolls)
        {
            var count = roll.Count is null ? 1 : (int)Math.Round(roll.Count.Evaluate(scope));
            for (var n = 0; n < count; n++)
            {
                var entries = roll.Entries.Where(e => e.Condition is null || e.Condition.IsTrue(scope)).ToList();
                if (entries.Count == 0)
                {
                    continue;
                }
                var idx = rng.PickWeighted(entries.Select(e => e.Weight).ToList());
                if (idx < 0)
                {
                    continue;
                }
                var e = entries[idx];
                var level = itemLevel + e.LevelBonus;
                switch (e.Kind)
                {
                    case "nothing":
                        break;
                    case "item":
                    {
                        var rarity = e.Rarity ?? (e.RarityWeights.Count > 0 ? RollRarity(rng, e.RarityWeights) : null);
                        var payload = e.Glyph is null && e.Form is null && e.Weave is null && lean is null
                            ? null
                            : new PayloadRequest { Glyph = e.Glyph, Form = e.Form, Weave = e.Weave, Lean = lean };
                        var item = e.Item is not null ? Roll(e.Item, level, rarity, rng, payload) : RollFromClass(e.Class, level, rarity, rng);
                        if (item is not null)
                        {
                            if (e.Amount is { Length: > 0 } amt && item.Stack > 1)
                            {
                                item.Count = Math.Clamp(rng.Range((int)amt[0], (int)amt[Math.Min(1, amt.Length - 1)]), 1, item.Stack);
                            }
                            results.Add(new LootResult(item, null, item.Count));
                        }
                        break;
                    }
                    case "unique":
                    {
                        var uniques = e.Unique is not null
                            ? new List<UniqueDef> { _content.Require<UniqueDef>(e.Unique) }
                            : _content.Each<UniqueDef>().Where(u => u.MinLevel <= level && (e.Class is null || ItemResolver.ClassTags(_content.Require<ItemBaseDef>(u.Base).Class).Has(e.Class))).OrderBy(u => u.Id, StringComparer.Ordinal).ToList();
                        if (uniques.Count > 0)
                        {
                            var ui = rng.PickWeighted(uniques.Select(u => u.Weight).ToList());
                            results.Add(new LootResult(RollUnique(uniques[Math.Max(0, ui)].Id, level, rng), null, 1));
                        }
                        break;
                    }
                    case "table":
                        if (e.Table is not null)
                        {
                            results.AddRange(RollLoot(e.Table, level, rng, scope, depth + 1, lean));
                        }
                        break;
                    case "currency":
                        if (e.Currency is not null)
                        {
                            var amount = e.Amount is { Length: > 0 } a ? rng.Range(a[0], a[Math.Min(1, a.Length - 1)]) : 1;
                            results.Add(new LootResult(null, e.Currency, Math.Round(amount)));
                        }
                        break;
                }
            }
        }
        return results;
    }
}
