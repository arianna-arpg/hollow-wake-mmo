using System.Text.Json;
using HollowWake.Core.Content;
using HollowWake.Core.Maths;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
var content = ContentRegistry.Load(new[] { Path.GetFullPath(args.Length > 1 ? args[1] : "content/core") }, out var report);
if (report.HasErrors) throw new InvalidOperationException(report.ToString());
var generator = new WorldGenerator(content, Rng.Hash64("aureth"));
var result = new List<object>();
foreach (var zc in new[] { new ZoneCoord(0, 0), new ZoneCoord(1, 0), new ZoneCoord(0, -1), new ZoneCoord(3, 2) })
{
    var zone = generator.Zone(zc);
    var spawns = generator.Layout.ChunksOf(zc).SelectMany(c => generator.GenerateChunk(c).Spawns).ToArray();
    result.Add(new {
        Zone = zc.Key, zone.Level,
        Sites = zone.Pois.Select(p => new { p.Id, p.Level, Population = spawns.Where(s => s.Group == p.Id).Sum(s => s.PackSize) }),
        Patterns = spawns.GroupBy(s => s.Pattern).OrderBy(g => g.Key).Select(g => new { Pattern = g.Key, Points = g.Count(), Bodies = g.Sum(s => s.PackSize) }),
        SpawnLevels = spawns.GroupBy(s => new { s.Pattern, s.Group, s.Level, EffectiveLevel = s.Level ?? content.Require<HollowWake.Core.Definitions.PatternDef>(s.Pattern).Level })
            .OrderBy(g => g.Key.Pattern).ThenBy(g => g.Key.Group).ThenBy(g => g.Key.EffectiveLevel)
            .Select(g => new { g.Key.Pattern, g.Key.Group, ExplicitLevel = g.Key.Level, g.Key.EffectiveLevel, Points = g.Count(), Bodies = g.Sum(s => s.PackSize) }),
        Quests = zone.Quests.Select(q => new { q.Id, q.Level, Targets = q.Objectives.Select(o => new { o.Type, o.Target, o.Poi }) })
    });
}
Directory.CreateDirectory(Path.GetDirectoryName(args[0])!);
File.WriteAllText(args[0], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine(args[0]);
