using System.Text.Json;
using HollowWake.Core.Content;
using HollowWake.Core.Maths;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
var content = ContentRegistry.Load(new[] { Path.GetFullPath("content/core") }, out var report);
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
        Quests = zone.Quests.Select(q => new { q.Id, q.Level, Targets = q.Objectives.Select(o => new { o.Type, o.Target, o.Poi }) })
    });
}
Directory.CreateDirectory(Path.GetDirectoryName(args[0])!);
File.WriteAllText(args[0], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine(args[0]);
