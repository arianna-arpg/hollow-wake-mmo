using System.Text.Json;
using HollowWake.Core.Content;
using HollowWake.Core.Maths;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;

// Compare the same seed against an archived base content tree and this worktree.
var before = ContentRegistry.Load(new[] { args[0] }, out var beforeReport);
var after = ContentRegistry.Load(new[] { "content/core" }, out var afterReport);
if (beforeReport.HasErrors || afterReport.HasErrors) throw new Exception("Content did not validate.");
var report = new List<object>();
foreach (var (x,y) in new[] { (0,0),(1,0),(0,-1),(3,2) })
{
    var a = new WorldGenerator(before,Rng.Hash64("aureth"));
    var b = new WorldGenerator(after,Rng.Hash64("aureth"));
    var zone = new ZoneCoord(x,y);
    string[] Describe(WorldGenerator g) => g.PoisAround(zone)
        .Select(p => $"{p.Id}: {p.Def.Id}, {p.Name}, {p.X},{p.Y}, height={p.BaseHeight:R}, level={p.Level}")
        .ToArray();
    var old = Describe(a); var current = Describe(b);
    report.Add(new { zone = zone.ToString(), removedNearby = old.Except(current).ToArray(),
        addedNearby = current.Except(old).ToArray(),
        questsBefore = a.Zone(zone).Quests.Select(q => new {q.Id,q.Name,q.Description,q.Objectives}),
        questsAfter = b.Zone(zone).Quests.Select(q => new {q.Id,q.Name,q.Description,q.Objectives}) });
}
File.WriteAllText("experiments/golems/evidence/world-neighbors.json",JsonSerializer.Serialize(report,new JsonSerializerOptions {WriteIndented=true})+"\n");
Console.WriteLine("Captured seed aureth: neighboring POIs and full quest definitions for every golden zone.");
