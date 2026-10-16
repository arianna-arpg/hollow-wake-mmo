using Skein.Core.Definitions;
using Skein.Core.Maths;
using Skein.Core.Tags;
using Skein.Core.World;

namespace Skein.Core.WorldGen;

/// <summary>
/// Zone-level planning: where points of interest go and which quests they seed. Placement is
/// constraint based (biome, region, elevation, slope, distance to water, spacing, budget) over a
/// jittered candidate grid, then pinned overrides are applied. Quests come from templates whose slots
/// are filled with real nearby places and creatures, so a generated town asks you to clear the actual
/// ruin on its actual hill.
/// </summary>
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
        var defs = _gen.Content.Each<PoiDef>().OrderBy(d => d.Id, StringComparer.Ordinal).ToList();

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
            var candidates = new List<(double x, double y)>();
            for (var gy = margin; gy + margin <= size; gy += cell)
            {
                for (var gx = margin; gx + margin <= size; gx += cell)
                {
                    var jx = rng.Range(0, Math.Min(cell, size - gx - margin));
                    var jy = rng.Range(0, Math.Min(cell, size - gy - margin));
                    candidates.Add((origin.X + gx + jx, origin.Y + gy + jy));
                }
            }
            rng.Shuffle(candidates);

            var budget = region.Archetype.PoiBudget;
            var target = budget.Count > 0 ? budget.Values.Sum() : _p.PoiTarget;
            target += _gen.Overlays.Sum(_layout, "poi_bonus", origin.X + size / 2.0, origin.Y + size / 2.0);
            var want = (int)Math.Floor(target);
            if (rng.Chance(target - want))
            {
                want++;
            }

            foreach (var (x, y) in candidates)
            {
                if (placed.Count >= want + (ovr?.Pois.Count ?? 0))
                {
                    break;
                }
                var sample = _gen.Sample(x, y);
                if (sample.Water)
                {
                    continue;
                }
                var biome = _gen.Biome(sample, region);
                var waterDistance = _gen.WaterDistance(x, y);
                var weights = new List<double>();
                var options = new List<PoiDef>();
                foreach (var def in defs)
                {
                    if (!Fits(def, sample, biome, region, waterDistance))
                    {
                        continue;
                    }
                    if (placed.Any(p => Distance(p.X, p.Y, x, y) < Math.Max(def.MinSpacing, p.Def.MinSpacing)))
                    {
                        continue;
                    }
                    var w = def.Weight;
                    if (budget.Count > 0)
                    {
                        double bw = 0;
                        foreach (var (tag, amount) in budget)
                        {
                            if (def.Id == tag || def.Tags.Has(tag))
                            {
                                bw += amount;
                            }
                        }
                        if (bw <= 0)
                        {
                            continue;
                        }
                        var already = placed.Count(p => p.Def.Id == def.Id || budget.Keys.Any(t => p.Def.Tags.Has(t) && def.Tags.Has(t)));
                        w *= bw / (1 + already);
                    }
                    weights.Add(w);
                    options.Add(def);
                }
                if (options.Count == 0)
                {
                    continue;
                }
                var idx = rng.PickWeighted(weights);
                if (idx < 0)
                {
                    continue;
                }
                placed.Add(Make(options[idx], zc, x, y, region, rng, null, null));
            }
        }
        return placed;
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
            var weights = def.Landmarks.Select(id => _gen.Content.Get<LandmarkDef>(id)?.Weight ?? 1).ToList();
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
        var around = _gen.PoisAround(zc).ToList();
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

    private QuestDef? Instantiate(QuestTemplateDef template, PoiInstance giver, List<PoiInstance> around, RegionInfo region, Rng rng, int index)
    {
        var fills = new Dictionary<string, (string Id, string Name)>(StringComparer.Ordinal);
        var usedPois = new HashSet<string> { giver.Id };
        foreach (var (slot, req) in template.Slots)
        {
            if (!req.PoiTags.IsEmpty)
            {
                var options = around
                    .Where(p => !usedPois.Contains(p.Id) && p.Tags.HasAny(req.PoiTags) && Distance(p.X, p.Y, giver.X, giver.Y) <= req.Within)
                    .OrderBy(p => Distance(p.X, p.Y, giver.X, giver.Y))
                    .ToList();
                if (options.Count == 0)
                {
                    return null;
                }
                var pick = options[Math.Min(options.Count - 1, rng.Next(Math.Min(3, options.Count)))];
                usedPois.Add(pick.Id);
                fills[slot] = (pick.Id, pick.Name);
                continue;
            }
            if (!req.PatternTags.IsEmpty)
            {
                var options = _gen.Content.Each<PatternDef>()
                    .Where(p => p.Tags.HasAll(req.PatternTags) && !p.Tags.Has("flora") && !p.Tags.Has("npc"))
                    .OrderBy(p => p.Id, StringComparer.Ordinal)
                    .ToList();
                if (options.Count == 0)
                {
                    return null;
                }
                var pick = rng.Pick(options);
                fills[slot] = (pick.Id, pick.DisplayName);
                continue;
            }
            if (!req.ItemTags.IsEmpty)
            {
                var options = _gen.Content.Each<ItemBaseDef>()
                    .Where(i => i.Tags.HasAll(req.ItemTags))
                    .OrderBy(i => i.Id, StringComparer.Ordinal)
                    .ToList();
                if (options.Count == 0)
                {
                    return null;
                }
                var pick = rng.Pick(options);
                fills[slot] = (pick.Id, pick.DisplayName);
                continue;
            }
            return null;
        }

        string Sub(string? text, bool names)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }
            var result = text;
            foreach (var (slot, fill) in fills)
            {
                result = result.Replace("$" + slot, names ? fill.Name : fill.Id, StringComparison.Ordinal);
            }
            return result.Replace("$giver", names ? giver.Name : giver.Id, StringComparison.Ordinal);
        }

        var id = $"gen.{giver.Id}.{template.Id}.{index}";
        var objectives = template.Objectives.Select(o => o with
        {
            Target = o.Target is null ? null : Sub(o.Target, false),
            Poi = o.Poi is null ? null : Sub(o.Poi, false),
            Text = Sub(o.Text, true),
        }).ToList();
        var text = template.Text.ToDictionary(kv => kv.Key, kv => Sub(kv.Value, true), StringComparer.Ordinal);
        return new QuestDef
        {
            Id = id,
            Name = Sub(string.IsNullOrEmpty(template.Name) ? template.DisplayName : template.Name, true),
            Description = Sub(template.Description, true),
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
}
