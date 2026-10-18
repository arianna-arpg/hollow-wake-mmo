using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Maths;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
var zones = new[] { new ZoneCoord(0,0), new ZoneCoord(1,0), new ZoneCoord(0,-1), new ZoneCoord(3,2), new ZoneCoord(-15,-40), new ZoneCoord(-8,-40), new ZoneCoord(-25,-31), new ZoneCoord(-7,-23) };
var results = new List<object>();
foreach (var overlay in new[] { false, true })
{
 var content=ContentRegistry.Load(new[]{"content/core"},out var report);
 if(report.HasErrors)throw new InvalidOperationException(report.ToString());
 var o=new GenerationOverlays();
 if(overlay){
 o.Add(new GenerationOverlay { Id="season",Global=true,Params={ ["temperature_offset"]=-.08,["elevation_offset"]=.75 } });
 o.Add(new GenerationOverlay { Id="flood",Zones={new ZoneCoord(1,0)},Priority=1,Params={ ["water_level"]=2.5,["thread.void"]=.35 } });
 o.Add(new GenerationOverlay { Id="decree",Regions={new RegionCoord(0,-1)},Params={ ["poi_bonus"]=1.5,["thread.void"]=.5 } });
 o.Add(new GenerationOverlay { Id="fount",Circle=(140,120,70),Priority=2,Params={ ["thread.ember"]=.45,["moisture_offset"]=.2,["elevation_multiplier"]=1.15 } });
 }
 var g=new WorldGenerator(content,Rng.Hash64("aureth"),o);
 foreach(var zc in zones){
  var z=g.Zone(zc);
  using var geometry=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
  var spawns=new List<SpawnPoint>();
  foreach(var cc in g.Layout.ChunksOf(zc)){
   var c=g.GenerateChunk(cc); spawns.AddRange(c.Spawns);
   geometry.AppendData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{c.Coord,c.Height,c.Flags,c.SurfaceTag,c.BiomeIndex,c.Stamps,c.Flora})));
  }
  var populationWithoutLevels=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(spawns.Select(s=>new{s.Pattern,s.X,s.Y,s.PackSize,s.RespawnSeconds,s.Group})))));
  results.Add(new{overlay,zone=zc.Key,geometryHash=Convert.ToHexString(geometry.GetHashAndReset()),populationWithoutLevels,sites=z.Pois.Select(p=>new{p.Id,kind=p.Def.Id,p.Name,p.X,p.Y,siteLevel=p.Level,p.Landmark}),nearby=g.PoisAround(zc).Select(p=>p.Id),populations=spawns.GroupBy(s=>new{s.Pattern,s.Group,s.Level,EffectiveLevel=s.Level??content.Require<PatternDef>(s.Pattern).Level}).OrderBy(g=>g.Key.Group).ThenBy(g=>g.Key.Pattern).ThenBy(g=>g.Key.EffectiveLevel).Select(g=>new{g.Key.Pattern,site=g.Key.Group,explicitLevel=g.Key.Level,effectiveLevel=g.Key.EffectiveLevel,points=g.Count(),bodies=g.Sum(s=>s.PackSize)}),quests=z.Quests.Select(q=>new{q.Id,q.Name,q.Level,objectives=q.Objectives.Select(o=>new{o.Type,o.Target,o.Poi,o.Text})})});
 }
}
File.WriteAllText(args[0],JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true})+"\n");
