using HollowWake.Core.Definitions;
using HollowWake.Core.Maths;
using HollowWake.Core.Tags;
using HollowWake.Core.World;

namespace HollowWake.Core.WorldGen;

/// <summary>
/// Zone-level planning: where points of interest go and which quests they seed. Placement is
/// constraint based (biome, region, elevation, slope, distance to water, spacing, budget) over a
/// jittered candidate grid, then pinned overrides are applied. Quests come from templates whose slots
/// are filled with real nearby places and creatures, so a generated town asks you to clear the actual
/// ruin on its actual hill.
/// </summary>
/// <remarks>
/// Planning runs once per zone, so most of its cost on a fresh server is compiling it; it is written
/// with plain loops and arrays rather than queries and closures, which each compile separately.
/// </remarks>
public sealed class ZonePlanner
{
    private readonly WorldGenerator _gen;
    private readonly WorldLayout _layout;
    private readonly GenParams _p;

    public ZonePlanner(WorldGenerator gen)
    {
        _gen = gen;
        _layout = gen.Layout;
        _p = gen.Params;
    }

    public IReadOnlyList<PoiInstance> PlacePois(ZoneCoord zc)
    {
        var region = _gen.Region(_layout.RegionOf(zc));
        var origin = _layout.ZoneOrigin(zc);
        var size = _layout.ZoneSize;
        var rng = _gen.RngFor($"pois:{zc.X}:{zc.Y}");
        var placed = new List<PoiInstance>();
        var ovr = _gen.ZoneOverride(zc);
        var defs = new List<PoiDef>(_gen.Content.Each<PoiDef>());
        defs.Sort(ById);

        // Pinned points of interest come first and are never displaced.
        if (ovr is not null)
        {
            foreach (var pin in ovr.Pois)
            {
                if (!_gen.Content.TryGet<PoiDef>(pin.Poi, out var def) || def is null)
                {
                    continue;
                }
                var x = origin.X + pin.X + 0.5;
                var y = origin.Y + pin.Y + 0.5;
                placed.Add(Make(def, zc, x, y, region, rng, pin.Name, pin.Landmark));
            }
        }

        if (defs.Count > 0)
        {
            var cell = _p.PoiCell;
            var margin = _p.PoiMargin;
            var columns = 0;
            for (var gx = margin; gx + margin <= size; gx += cell)
            {
                columns++;
            }
            var rows = 0;
            for (var gy = margin; gy + margin <= size; gy += cell)
            {
                rows++;
            }
            var xs = new double[rows * columns];
            var ys = new double[rows * columns];
            var at = 0;
            for (var gy = margin; gy + margin <= size; gy += cell)
            {
                for (var gx = margin; gx + margin <= size; gx += cell)
                {
                    var jx = rng.Range(0, Math.Min(cell, size - gx - margin));
                    var jy = rng.Range(0, Math.Min(cell, size - gy - margin));
                    xs[at] = origin.X + gx + jx;
                    ys[at] = origin.Y + gy + jy;
                    at++;
                }
            }
            // Shuffling the visiting order makes the same swaps a shuffle of the candidates would.
            var order = new int[xs.Length];
            for (var i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }
            rng.Shuffle(order);

            var budget = region.Archetype.PoiBudget;
            double target;
            if (budget.Count > 0)
            {
                target = 0;
                foreach (var kv in budget)
                {
                    target += kv.Value;
                }
            }
            else
            {
                target = _p.PoiTarget;
            }
            target += _gen.Overlays.Sum(_layout, "poi_bonus", origin.X + size / 2.0, origin.Y + size / 2.0);
            var want = (int)Math.Floor(target);
            if (rng.Chance(target - want))
            {
                want++;
            }

            var weights = new double[defs.Count];
            var options = new PoiDef[defs.Count];
            foreach (var k in order)
            {
                if (placed.Count >= want + (ovr?.Pois.Count ?? 0))
                {
                    break;
                }
                var x = xs[k];
                var y = ys[k];
                var sample = _gen.Sample(x, y);
                if (sample.Water)
                {
                    continue;
                }
                var biome = _gen.Biome(sample, region);
                var waterDistance = _gen.WaterDistance(x, y);
                var count = 0;
                foreach (var def in defs)
                {
                    if (!Fits(def, sample, biome, region, waterDistance) || TooClose(placed, def, x, y))
                    {
                        continue;
                    }
                    var w = def.Weight;
                    if (budget.Count > 0)
                    {
                        double bw = 0;
                        foreach (var kv in budget)
                        {
                            if (def.Id == kv.Key || def.Tags.Has(kv.Key))
                            {
                                bw += kv.Value;
                            }
                        }
                        if (bw <= 0)
                        {
                            continue;
                        }
                        var already = AlreadyPlaced(placed, def, budget);
                        w *= bw / (1 + already);
                    }
                    weights[count] = w;
                    options[count] = def;
                    count++;
                }
                if (count == 0)
                {
                    continue;
                }
                var exact = weights;
                if (count < weights.Length)
                {
                    exact = new double[count];
                    Array.Copy(weights, exact, count);
                }
                var idx = rng.PickWeighted(exact);
                if (idx < 0)
                {
                    continue;
                }
                placed.Add(Make(options[idx], zc, x, y, region, rng, null, null));
            }
        }
        return placed;
    }

    private static int ById(Definition a, Definition b) => string.CompareOrdinal(a.Id, b.Id);

    private static bool TooClose(List<PoiInstance> placed, PoiDef def, double x, double y)
    {
        foreach (var p in placed)
        {
            if (Distance(p.X, p.Y, x, y) < Math.Max(def.MinSpacing, p.Def.MinSpacing))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>How many placed points share this one's template or one of the budget tags with it.</summary>
    private static int AlreadyPlaced(List<PoiInstance> placed, PoiDef def, Dictionary<string, double> budget)
    {
        var already = 0;
        foreach (var p in placed)
        {
            if (p.Def.Id == def.Id)
            {
                already++;
                continue;
            }
            foreach (var kv in budget)
            {
                if (p.Def.Tags.Has(kv.Key) && def.Tags.Has(kv.Key))
                {
                    already++;
                    break;
                }
            }
        }
        return already;
    }

    private bool Fits(PoiDef def, in ClimateSample s, BiomeDef? biome, RegionInfo region, double waterDistance)
    {
        if (def.Elevation is { } e && !e.Contains(s.Elevation - _p.SeaLevel))
        {
            return false;
        }
        if (def.Slope is { } sl && !sl.Contains(s.Slope))
        {
            return false;
        }
        if (def.WaterDistance is { } wd && !wd.Contains(waterDistance))
        {
            return false;
        }
        if (!def.Biomes.IsEmpty)
        {
            if (biome is null)
            {
                return false;
            }
            if (!def.Biomes.Has(biome.Id) && !biome.Tags.HasAny(def.Biomes) && !AncestorMatches(biome, def.Biomes))
            {
                return false;
            }
        }
        if (!def.Regions.IsEmpty && !def.Regions.Has(region.Archetype.Id) && !region.Archetype.Tags.HasAny(def.Regions))
        {
            return false;
        }
        if (!def.Factions.IsEmpty && region.Faction is not null && !def.Factions.Has(region.Faction))
        {
            return false;
        }
        return true;
    }

    private bool AncestorMatches(BiomeDef biome, TagSet wanted)
    {
        var cur = biome;
        var guard = 0;
        while (cur?.Parent is not null && guard++ < 16)
        {
            if (wanted.Has(cur.Parent))
            {
                return true;
            }
            cur = _gen.Content.Get<BiomeDef>(cur.Parent);
        }
        return false;
    }

    private PoiInstance Make(PoiDef def, ZoneCoord zc, double x, double y, RegionInfo region, Rng rng, string? name, string? landmarkOverride)
    {
        var landmark = landmarkOverride;
        if (landmark is null && def.Landmarks.Count > 0)
        {
            var weights = new double[def.Landmarks.Count];
            for (var i = 0; i < weights.Length; i++)
            {
                weights[i] = _gen.Content.Get<LandmarkDef>(def.Landmarks[i])?.Weight ?? 1;
            }
            var idx = rng.PickWeighted(weights);
            landmark = def.Landmarks[Math.Max(0, idx)];
        }
        var id = $"poi.{zc.Key}.{def.Id}.{(int)x}.{(int)y}";
        name ??= def.Names.Count > 0 ? rng.Pick(def.Names) : def.DisplayName;
        var baseHeight = (float)Math.Max(_gen.WaterLevel(x, y) + 0.25, _gen.Elevation(x, y));
        var faction = !def.Factions.IsEmpty ? (region.Faction is not null && def.Factions.Has(region.Faction) ? region.Faction : rng.Pick(def.Factions.Items)) : region.Faction;
        return new PoiInstance(id, def, name, zc, Math.Floor(x) + 0.5, Math.Floor(y) + 0.5, baseHeight, landmark, faction, region.Level + def.Level);
    }

    private static double Distance(double ax, double ay, double bx, double by) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

    // ------------------------------------------------------------ quests

    public IReadOnlyList<QuestDef> GenerateQuests(ZoneCoord zc, IReadOnlyList<PoiInstance> pois)
    {
        var result = new List<QuestDef>();
        var around = _gen.PoisAroundCached(zc);
        var region = _gen.Region(_layout.RegionOf(zc));
        foreach (var giver in pois)
        {
            if (giver.Def.Quests.Count == 0)
            {
                continue;
            }
            var rng = _gen.RngFor($"quests:{giver.Id}");
            var n = 0;
            foreach (var templateId in giver.Def.Quests)
            {
                if (!_gen.Content.TryGet<QuestTemplateDef>(templateId, out var template) || template is null)
                {
                    continue;
                }
                var quest = Instantiate(template, giver, around, region, rng, n++);
                if (quest is not null)
                {
                    result.Add(quest);
                }
            }
        }
        return result;
    }

    private QuestDef? Instantiate(QuestTemplateDef template, PoiInstance giver, PoiInstance[] around, RegionInfo region, Rng rng, int index)
    {
        var fills = new Dictionary<string, (string Id, string Name)>(StringComparer.Ordinal);
        var usedPois = new HashSet<string> { giver.Id };
        foreach (var kv in template.Slots)
        {
            var slot = kv.Key;
            var req = kv.Value;
            if (!req.PoiTags.IsEmpty)
            {
                var pick = NearbyPoi(around, usedPois, req, giver, rng);
                if (pick is null)
                {
                    return null;
                }
                usedPois.Add(pick.Id);
                fills[slot] = (pick.Id, pick.Name);
                continue;
            }
            if (!req.PatternTags.IsEmpty)
            {
                var options = new List<PatternDef>();
                foreach (var p in _gen.Content.Each<PatternDef>())
                {
                    if (p.Tags.HasAll(req.PatternTags) && !p.Tags.Has("flora") && !p.Tags.Has("npc"))
                    {
                        options.Add(p);
                    }
                }
                if (options.Count == 0)
                {
                    return null;
                }
                options.Sort(ById);
                var pick = rng.Pick(options);
                fills[slot] = (pick.Id, pick.DisplayName);
                continue;
            }
            if (!req.ItemTags.IsEmpty)
            {
                var options = new List<ItemBaseDef>();
                foreach (var i in _gen.Content.Each<ItemBaseDef>())
                {
                    if (i.Tags.HasAll(req.ItemTags))
                    {
                        options.Add(i);
                    }
                }
                if (options.Count == 0)
                {
                    return null;
                }
                options.Sort(ById);
                var pick = rng.Pick(options);
                fills[slot] = (pick.Id, pick.DisplayName);
                continue;
            }
            return null;
        }

        var id = $"gen.{giver.Id}.{template.Id}.{index}";
        var objectives = new List<ObjectiveDef>(template.Objectives.Count);
        foreach (var o in template.Objectives)
        {
            objectives.Add(o with
            {
                Target = o.Target is null ? null : Sub(o.Target, false, fills, giver),
                Poi = o.Poi is null ? null : Sub(o.Poi, false, fills, giver),
                Text = Sub(o.Text, true, fills, giver),
            });
        }
        var text = new Dictionary<string, string>(template.Text.Count, StringComparer.Ordinal);
        foreach (var kv in template.Text)
        {
            text.Add(kv.Key, Sub(kv.Value, true, fills, giver));
        }
        return new QuestDef
        {
            Id = id,
            Name = Sub(string.IsNullOrEmpty(template.Name) ? template.DisplayName : template.Name, true, fills, giver),
            Description = Sub(template.Description, true, fills, giver),
            Tags = template.Tags.With("generated"),
            Level = Math.Max(1, giver.Level + template.LevelOffset),
            Giver = giver.Id,
            Zone = giver.Zone.Key,
            Objectives = objectives,
            Rewards = template.Rewards,
            Repeatable = template.Repeatable,
            Text = text,
            Faction = giver.Faction ?? region.Faction,
        };
    }

    /// <summary>
    /// One of the three nearest unused points of interest within reach that carries a wanted tag. Nearest
    /// first, ties in their original order.
    /// </summary>
    private static PoiInstance? NearbyPoi(PoiInstance[] around, HashSet<string> used, QuestSlot req, PoiInstance giver, Rng rng)
    {
        var options = new PoiInstance[around.Length];
        var keys = new double[around.Length];
        var count = 0;
        foreach (var p in around)
        {
            if (used.Contains(p.Id) || !p.Tags.HasAny(req.PoiTags))
            {
                continue;
            }
            var d = Distance(p.X, p.Y, giver.X, giver.Y);
            if (!(d <= req.Within))
            {
                continue;
            }
            // Stable insertion: everything farther moves up one place.
            var j = count - 1;
            while (j >= 0 && keys[j].CompareTo(d) > 0)
            {
                keys[j + 1] = keys[j];
                options[j + 1] = options[j];
                j--;
            }
            keys[j + 1] = d;
            options[j + 1] = p;
            count++;
        }
        if (count == 0)
        {
            return null;
        }
        return options[Math.Min(count - 1, rng.Next(Math.Min(3, count)))];
    }

    private static string Sub(string? text, bool names, Dictionary<string, (string Id, string Name)> fills, PoiInstance giver)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }
        var result = text;
        foreach (var kv in fills)
        {
            result = result.Replace("$" + kv.Key, names ? kv.Value.Name : kv.Value.Id, StringComparison.Ordinal);
        }
        return result.Replace("$giver", names ? giver.Name : giver.Id, StringComparison.Ordinal);
    }
}
