using HollowWake.Tools;
using System.Globalization;
using System.Numerics;
using HollowWake.Core;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Expressions;
using HollowWake.Core.Items;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Territory;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

var command = args.Length > 0 ? args[0] : "help";
var rest = args.Skip(1).ToList();
string Opt(string name, string fallback)
{
    var i = rest.IndexOf("--" + name);
    return i >= 0 && i + 1 < rest.Count ? rest[i + 1] : fallback;
}
bool Flag(string name) => rest.Contains("--" + name);

var contentDirs = Opt("content", "content/core").Split(';', StringSplitOptions.RemoveEmptyEntries);

ContentRegistry LoadContent(bool print)
{
    var registry = ContentRegistry.Load(contentDirs, out var report);
    if (print || report.HasErrors)
    {
        foreach (var issue in report.Issues.Where(i => i.Severity != Severity.Info))
        {
            Console.WriteLine(issue);
        }
    }
    if (report.HasErrors)
    {
        Console.WriteLine($"content has {report.ErrorCount} error(s)");
    }
    return registry;
}

switch (command)
{
    case "validate":
    {
        var registry = ContentRegistry.Load(contentDirs, out var report);
        foreach (var issue in report.Issues)
        {
            Console.WriteLine(issue);
        }
        Console.WriteLine();
        foreach (var kind in registry.KindNames.OrderBy(k => k, StringComparer.Ordinal))
        {
            Console.WriteLine($"{kind,-20} {registry.CountOfKind(kind),5}");
        }
        Console.WriteLine();
        Console.WriteLine($"{report.ErrorCount} error(s), {report.WarningCount} warning(s)");
        return report.HasErrors ? 1 : 0;
    }
    case "expr":
    {
        var source = string.Join(" ", rest.TakeWhile(a => !a.StartsWith("--")));
        var scope = new DictScope(new Rng(1)).Set("level", 10).Set("might", 20).Set("grace", 15).Set("will", 25).Set("vigor", 18).Flag("stationary");
        var expr = Expr.Parse(source);
        Console.WriteLine($"identifiers: {string.Join(", ", expr.Identifiers)}");
        Console.WriteLine($"flags: {string.Join(", ", expr.Flags)}");
        Console.WriteLine($"value (level 10, might 20, grace 15, will 25, vigor 18): {expr.Evaluate(scope)}");
        return 0;
    }
    case "item":
    {
        var registry = LoadContent(false);
        var gen = new ItemGenerator(registry);
        var baseId = rest.FirstOrDefault(a => !a.StartsWith("--")) ?? "short_sword";
        var level = int.Parse(Opt("level", "10"));
        var rarity = Opt("rarity", "");
        var count = int.Parse(Opt("count", "3"));
        var rng = new Rng(Opt("seed", "forge"));
        for (var i = 0; i < count; i++)
        {
            var item = baseId == "random" ? gen.RollFromClass(Opt("class", "weapon"), level, rarity.Length == 0 ? null : rarity, rng)! : gen.Roll(baseId, level, rarity.Length == 0 ? null : rarity, rng);
            foreach (var line in gen.Resolver.Describe(item))
            {
                Console.WriteLine(line);
            }
            Console.WriteLine();
        }
        return 0;
    }
    case "loot":
    {
        var registry = LoadContent(false);
        var gen = new ItemGenerator(registry);
        var table = rest.FirstOrDefault(a => !a.StartsWith("--")) ?? "world_common";
        var level = int.Parse(Opt("level", "5"));
        var rolls = int.Parse(Opt("rolls", "20"));
        var rng = new Rng(Opt("seed", "loot"));
        var counts = new Dictionary<string, int>();
        for (var i = 0; i < rolls; i++)
        {
            foreach (var r in gen.RollLoot(table, level, rng))
            {
                var key = r.Item is not null ? $"{r.Item.Rarity} {r.Item.Base}" : $"{r.Currency} x{r.Amount}";
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        }
        foreach (var (k, v) in counts.OrderByDescending(kv => kv.Value))
        {
            Console.WriteLine($"{v,5}  {k}");
        }
        return 0;
    }
    case "region":
    case "zone":
    {
        var registry = LoadContent(false);
        var generator = new WorldGenerator(registry, Rng.Hash64(Opt("seed", registry.Rules.Text("world.seed", "aureth"))));
        var zx = int.Parse(Opt("x", "0"));
        var zy = int.Parse(Opt("y", "0"));
        if (command == "region")
        {
            var region = generator.Region(new RegionCoord(zx, zy));
            Console.WriteLine($"{region.Coord} {region.Name}: archetype {region.Archetype.Id}, faction {region.Faction ?? "none"}, level {region.Level}, distance {region.Distance}");
            foreach (var (t, v) in region.Threads)
            {
                Console.WriteLine($"  thread {t}: {v:+0.00;-0.00}");
            }
            return 0;
        }
        var zone = generator.Zone(new ZoneCoord(zx, zy));
        Console.WriteLine($"{zone.Coord} {zone.Name}: region {zone.Region.Name} ({zone.Region.Archetype.Id}), level {zone.Level}, safe {zone.Safe}, start {zone.PlayerStart}");
        foreach (var poi in zone.Pois)
        {
            Console.WriteLine($"  poi {poi.Def.Id,-10} {poi.Name,-24} at ({poi.X:0},{poi.Y:0}) h={poi.BaseHeight:0.0} landmark={poi.Landmark ?? "-"} faction={poi.Faction ?? "-"}");
        }
        foreach (var q in zone.Quests)
        {
            Console.WriteLine($"  quest {q.Id}: {q.Name} [L{q.Level}] {string.Join("; ", q.Objectives.Select(o => o.Text))}");
        }
        if (Flag("map"))
        {
            PrintMap(generator, new ZoneCoord(zx, zy));
        }
        return 0;
    }
    case "world":
    {
        var registry = LoadContent(false);
        var generator = new WorldGenerator(registry, Rng.Hash64(Opt("seed", registry.Rules.Text("world.seed", "aureth"))));
        var radius = int.Parse(Opt("radius", "3"));
        Console.WriteLine("regions around the origin (archetype, level, faction):");
        for (var y = radius; y >= -radius; y--)
        {
            var row = new List<string>();
            for (var x = -radius; x <= radius; x++)
            {
                var r = generator.Region(new RegionCoord(x, y));
                row.Add($"{r.Archetype.Id,-10}L{r.Level,-3}");
            }
            Console.WriteLine(string.Join(" | ", row));
        }
        return 0;
    }
    case "load":
    {
        var registry = LoadContent(true);
        var generator = new WorldGenerator(registry, Rng.Hash64(Opt("seed", registry.Rules.Text("world.seed", "aureth"))));
        var world = new WorldState(generator.Overlays);
        var zc = new ZoneCoord(int.Parse(Opt("x", "0")), int.Parse(Opt("y", "0")));
        var log = new ListSimLog();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var sim = ZoneBuilder.CreateZone(registry, generator, world, zc, log);
        var report = ZoneBuilder.LoadZone(sim);
        Console.WriteLine($"{report} in {sw.ElapsedMilliseconds} ms");
        var ticks = int.Parse(Opt("ticks", "200"));
        sw.Restart();
        sim.Run(ticks);
        Console.WriteLine($"ran {ticks} ticks in {sw.ElapsedMilliseconds} ms; entities {sim.World.Count}; monsters {sim.World.Query<Behavior>().Count()}");
        foreach (var line in log.Lines.Take(20))
        {
            Console.WriteLine(line);
        }
        return 0;
    }
    case "fight":
    {
        var registry = LoadContent(false);
        var sim = ZoneBuilder.CreateArena(registry, Rng.Hash64(Opt("seed", "fight")));
        var log = new ListSimLog();
        sim.Log = log;
        var factory = sim.Service<PatternFactory>();
        var a = rest.Count > 0 && !rest[0].StartsWith("--") ? rest[0] : "wolf";
        var b = rest.Count > 1 && !rest[1].StartsWith("--") ? rest[1] : "hob";
        var seconds = double.Parse(Opt("seconds", "30"));
        var ea = factory.Spawn(sim, a, new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
        var eb = factory.Spawn(sim, b, new Vector3(14, 10, 0), new SpawnOptions { Faction = "blue" });
        registry.With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } }).With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
        var events = new List<string>();
        sim.Events.Subscribe<DamageDealt>(d => events.Add($"t={sim.Clock.Time:0.00} {sim.World.Get<Identity>(d.Source)} hit {sim.World.Get<Identity>(d.Target)} for {d.Total:0.#}{(d.Crit ? " crit" : "")}{(d.Evaded ? " evaded" : "")}"));
        sim.Events.Subscribe<EntityDied>(d => events.Add($"t={sim.Clock.Time:0.00} {sim.World.Get<Identity>(d.Entity)} died"));
        sim.Events.Subscribe<StatusApplied>(s => events.Add($"t={sim.Clock.Time:0.00} status {s.Status} x{s.Stacks} on {sim.World.Get<Identity>(s.Target)} from {sim.World.Get<Identity>(s.Source)}"));
        sim.Events.Subscribe<WeaveCast>(w => events.Add($"t={sim.Clock.Time:0.00} {sim.World.Get<Identity>(w.Caster)} cast {w.Weave} at {sim.World.Get<Identity>(w.Target)}"));
        sim.Events.Subscribe<EntitySpawned>(s => events.Add($"t={sim.Clock.Time:0.00} spawned {s.Pattern} at {s.Position}"));
        sim.Events.Subscribe<EntityDespawned>(s => events.Add($"t={sim.Clock.Time:0.00} despawned {s.Pattern}"));
        sim.RunSeconds(seconds);
        foreach (var line in events.Take(int.Parse(Opt("lines", "40"))))
        {
            Console.WriteLine(line);
        }
        var sa = sim.World.Get<Stats>(ea);
        var sb = sim.World.Get<Stats>(eb);
        Console.WriteLine($"{a}: life {sa?.Resource("life"):0}/{sa?.ResourceMax("life"):0}   {b}: life {sb?.Resource("life"):0}/{sb?.ResourceMax("life"):0}");
        foreach (var line in log.Lines.Take(10))
        {
            Console.WriteLine(line);
        }
        return 0;
    }
    case "weave":
        return WeaveReport.Run(LoadContent(false), rest.FirstOrDefault(a => !a.StartsWith("--")), Opt);
    case "bench":
        return Bench.Run(LoadContent(false), Opt, Flag("json"));
    case "probe":
    {
        var host = Opt("host", "127.0.0.1");
        var port = int.Parse(Opt("port", "7777"));
        var account = Opt("account", "probe");
        var seconds = double.Parse(Opt("seconds", "6"));
        var features = Opt("features", "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (Flag("deltas"))
        {
            features.Add(HollowWake.Protocol.ProtocolInfo.EntityDeltaFeature);
        }
        if (Flag("interface"))
        {
            features.Add(HollowWake.Protocol.ProtocolInfo.InterfaceFeature);
        }
        return await Probe.RunAsync(host, port, account, seconds, features.Distinct().ToList());
    }
    case "protocol-ts":
    {
        // The web client's message types, generated from HollowWake.Protocol so they cannot drift.
        var path = Opt("out", HollowWake.Protocol.TypeScriptContract.ModulePath);
        var text = HollowWake.Protocol.TypeScriptContract.Generate();
        if (Flag("check"))
        {
            var current = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : "";
            Console.WriteLine(current == text ? $"{path} is current" : $"{path} is stale; run without --check to regenerate");
            return current == text ? 0 : 1;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, text);
        Console.WriteLine($"wrote {path}");
        return 0;
    }
    case "schema":
    {
        foreach (var kind in ContentKinds.All)
        {
            Console.WriteLine($"{kind.Name}: {kind.DefinitionType.Name}");
            foreach (var p in kind.DefinitionType.GetProperties())
            {
                var name = System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(p.Name);
                Console.WriteLine($"    {name}: {Pretty(p.PropertyType)}");
            }
        }
        return 0;
    }
    default:
        Console.WriteLine($"""
            hollowwake {CoreInfo.Name} tools

              validate [--content dir;dir]        load and validate content layers
              schema                              print every content kind and its fields
              expr <expression>                   evaluate an expression against a sample scope
              item <base|random> [--level n] [--rarity r] [--count n] [--class c] [--seed s]
              loot <table> [--level n] [--rolls n] [--seed s]
              region --x n --y n [--seed s]       describe a generated region
              zone --x n --y n [--map] [--seed s] describe a generated zone and its quests
              world [--radius n] [--seed s]       print the region grid around the origin
              load --x n --y n [--ticks n]        build a zone simulation and run it headless
              fight <pattern> <pattern> [--seconds n] [--seed s]   simulate a duel in an arena
              weave <preset|form> [--threads a,b] [--glyphs g,g@level] [--level n] [--calling c] [--caster-level n]
                                                  compile a weave: tags, mechanisms, dormant glyphs, graph, preview numbers
              protocol-ts [--out file] [--check]  write (or check) the web client's TypeScript message types
              probe [--host h] [--port n] [--account a] [--seconds s] [--deltas] [--interface] [--features a,b]
                                                  connect to a running server, enter, move and check what comes back
            """);
        return 0;
}

static string Pretty(Type t)
{
    if (t.IsGenericType)
    {
        var name = t.Name[..t.Name.IndexOf('`')];
        return $"{name}<{string.Join(", ", t.GetGenericArguments().Select(Pretty))}>";
    }
    return t.Name;
}

static void PrintMap(WorldGenerator generator, ZoneCoord zc)
{
    var layout = generator.Layout;
    var origin = layout.ZoneOrigin(zc);
    var step = 4;
    var region = generator.Region(layout.RegionOf(zc));
    for (var y = layout.ZoneSize - step / 2; y >= 0; y -= step)
    {
        var line = new System.Text.StringBuilder();
        for (var x = step / 2; x < layout.ZoneSize; x += step)
        {
            var wx = origin.X + x;
            var wy = origin.Y + y;
            var s = generator.Sample(wx, wy);
            var biome = generator.Biome(s, region);
            char c;
            if (s.Water)
            {
                c = s.WaterDepth > 0.6 ? '~' : ',';
            }
            else
            {
                c = biome?.Id switch
                {
                    "forest" => 'T',
                    "deep_forest" => 'F',
                    "meadow" => '.',
                    "heath" => ':',
                    "scrub" => ';',
                    "marsh" => 'm',
                    "shore" => 's',
                    "cinderwaste" => 'c',
                    "hollowed" => 'h',
                    "highland" => '^',
                    "alpine" => 'A',
                    _ => '?',
                };
            }
            foreach (var poi in generator.PoisOf(zc))
            {
                if (Math.Abs(poi.X - wx) < step && Math.Abs(poi.Y - wy) < step)
                {
                    c = char.ToUpperInvariant(poi.Def.Id[0]);
                }
            }
            line.Append(c);
        }
        Console.WriteLine(line);
    }
}
