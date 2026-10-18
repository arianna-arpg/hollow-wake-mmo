using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using HollowWake.Core.Attributes;
using HollowWake.Core.Characters;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Items;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.Weaves;
using HollowWake.Core.Territory;
using HollowWake.Core.World;
using HollowWake.Core.WorldGen;
using HollowWake.Protocol;
using HollowWake.Server.World;

namespace HollowWake.Tools;

/// <summary>
/// The performance baseline. Every number here is a budget the optimization work is measured against:
/// attribute folds, expressions, chunk generation, a lively zone ticking, and snapshot building.
/// Run with --json to emit machine-readable results for baselines kept in the repository.
/// </summary>
public static class Bench
{
    public sealed record Result(string Name, double Value, string Unit, string Note);

    public static int Run(ContentRegistry content, Func<string, string, string> opt, bool json)
    {
        var results = new List<Result>();
        var only = opt("only", "");
        if (only is "" or "attributes")
        {
            results.AddRange(Attributes(content));
        }
        if (only is "" or "expressions")
        {
            results.AddRange(Expressions());
        }
        if (only is "" or "worldgen")
        {
            results.AddRange(WorldGen(content, opt("seed", "aureth")));
        }
        if (only is "" or "tick")
        {
            results.AddRange(Tick(content, opt("seed", "aureth"), int.Parse(opt("monsters", "300")), int.Parse(opt("players", "4")), int.Parse(opt("ticks", "600")), int.Parse(opt("minions", "0"))));
        }
        if (only is "" or "deliveries")
        {
            results.AddRange(BenchDeliveries.Run(content));
        }
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            Console.WriteLine($"{"benchmark",-34} {"value",12} {"unit",-12} note");
            foreach (var r in results)
            {
                Console.WriteLine($"{r.Name,-34} {r.Value,12:0.###} {r.Unit,-12} {r.Note}");
            }
        }
        return 0;
    }

    private static double Time(Action action, int iterations)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            action();
        }
        return sw.Elapsed.TotalMilliseconds;
    }

    /// <summary>Collects garbage before a timed loop, so a collection of startup data does not land inside it.</summary>
    private static void Settle()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    // ------------------------------------------------------------ attributes

    private static IEnumerable<Result> Attributes(ContentRegistry content)
    {
        var set = new AttributeSet(content.Attributes);
        var scope = new DictScope(new Rng(1)).Set("level", 20).Set("life_pct", 0.8).Set("vigor", 15).Set("might", 12).Set("grace", 10).Set("will", 11);
        var tags = new[] { TagSet.Of("ember", "spell"), TagSet.Of("attack", "melee", "stone"), TagSet.Of("projectile", "storm", "spell"), TagSet.Of("bloom", "heal") };
        var rng = new Rng(7);
        for (var i = 0; i < 40; i++)
        {
            var form = (ModifierForm)(i % 3);
            var scoped = i % 2 == 0 ? tags[i % tags.Length] : TagSet.Empty;
            set.Add(new Modifier { Attribute = "damage", Form = form, Value = 0.05 + rng.NextDouble() * 0.2, Scope = scoped, Source = "bench" + i, Condition = i % 7 == 0 ? Expr.Parse("life_pct > 0.5") : null });
        }
        for (var i = 0; i < 20; i++)
        {
            set.Add(new Modifier { Attribute = "life.max", Form = i % 2 == 0 ? ModifierForm.Flat : ModifierForm.Increased, Value = 10, Source = "life" + i });
        }
        var extra = new List<Modifier> { new() { Attribute = "damage", Form = ModifierForm.More, Value = 0.3 }, new() { Attribute = "damage", Form = ModifierForm.Increased, Value = 0.2, Scope = TagSet.Of("spell") } };

        // Warm.
        for (var i = 0; i < 1000; i++)
        {
            set.Get("damage", scope, tags[i % tags.Length]);
            set.Apply("damage", 100, scope, tags[i % tags.Length], extra);
        }
        Settle();
        const int n = 200_000;
        var cached = Time(() => set.Get("life.max", scope, tags[0]), n);
        yield return new Result("attribute.get.cached", cached / n * 1e6, "ns/op", "formula + 20 modifiers, cached");
        var scopedGet = Time(() =>
        {
            set.Touch();
            set.Get("damage", scope, tags[1]);
        }, n / 10);
        yield return new Result("attribute.get.uncached", scopedGet / (n / 10) * 1e6, "ns/op", "40 modifiers, 4 contexts, cache cleared");
        var apply = Time(() => set.Apply("damage", 100, scope, tags[0], extra), n);
        yield return new Result("attribute.apply.extra", apply / n * 1e6, "ns/op", "damage fold with weave extras (never cached)");
        Settle();
        var before = GC.GetTotalAllocatedBytes(true);
        for (var i = 0; i < 10_000; i++)
        {
            set.Apply("damage", 100, scope, tags[i % tags.Length], extra);
        }
        yield return new Result("attribute.apply.alloc", (GC.GetTotalAllocatedBytes(true) - before) / 10_000.0, "bytes/op", "allocation per damage fold");
    }

    // ------------------------------------------------------------ expressions

    private static IEnumerable<Result> Expressions()
    {
        var scope = new DictScope(new Rng(3)).Set("level", 12).Set("might", 18).Set("will", 22).Set("vigor", 15).Set("weapon.damage.min", 5).Set("weapon.damage.max", 9).Flag("stationary");
        var formulas = new[]
        {
            Expr.Parse("40 + level * 10 + vigor * 4"),
            Expr.Parse("lerp(weapon.damage.min, weapon.damage.max, rand()) * (1 + might * 0.02)"),
            Expr.Parse("has(\"stationary\") ? 1.35 : 1"),
            Expr.Parse("clamp(accuracy / max(1, accuracy + evasion * 0.25), 0.05, 1)"),
        };
        scope.Set("accuracy", 180).Set("evasion", 60);
        for (var i = 0; i < 20_000; i++)
        {
            foreach (var f in formulas)
            {
                f.Evaluate(scope);
            }
        }
        Settle();
        const int n = 1_000_000;
        var total = 0.0;
        var ms = Time(() =>
        {
            foreach (var f in formulas)
            {
                total += f.Evaluate(scope);
            }
        }, n / formulas.Length);
        yield return new Result("expression.evaluate", ms / n * 1e6, "ns/eval", $"four formulas, checksum {total:0}");
        Settle();
        var before = GC.GetTotalAllocatedBytes(true);
        for (var i = 0; i < 100_000; i++)
        {
            formulas[1].Evaluate(scope);
        }
        yield return new Result("expression.alloc", (GC.GetTotalAllocatedBytes(true) - before) / 100_000.0, "bytes/eval", "lerp with rand()");
        for (var i = 0; i < 2_000; i++)
        {
            Expr.Parse("40 + level * 10 + vigor * 4");
        }
        Settle();
        var parse = Time(() => Expr.Parse("40 + level * 10 + vigor * 4 + " + Guid.NewGuid().ToString("N")[..4].Length), 20_000);
        yield return new Result("expression.parse", parse / 20_000 * 1e6, "ns/parse", "uncached parse of a short formula");
    }

    // ------------------------------------------------------------ world generation

    private static IEnumerable<Result> WorldGen(ContentRegistry content, string seed)
    {
        var generator = new WorldGenerator(content, Rng.Hash64(seed));
        var zone = generator.StartZone();
        var sw = Stopwatch.StartNew();
        var info = generator.Zone(zone);
        yield return new Result("worldgen.zone.plan", sw.Elapsed.TotalMilliseconds, "ms", $"{info.Pois.Count} pois, {info.Quests.Count} quests");
        var chunks = generator.Layout.ChunksOf(zone).ToList();
        Settle();
        sw.Restart();
        var before = GC.GetTotalAllocatedBytes(true);
        long cells = 0;
        foreach (var c in chunks)
        {
            var chunk = generator.GenerateChunk(c);
            cells += chunk.Height.Length;
        }
        var ms = sw.Elapsed.TotalMilliseconds;
        yield return new Result("worldgen.chunk", ms / chunks.Count, "ms/chunk", $"{chunks.Count} chunks, {cells} cells");
        yield return new Result("worldgen.chunk.alloc", (GC.GetTotalAllocatedBytes(true) - before) / (double)chunks.Count / 1024, "KB/chunk", "allocation per generated chunk");
        sw.Restart();
        for (var i = 0; i < 20_000; i++)
        {
            generator.Sample(100 + i * 0.37, 50 + i * 0.11);
        }
        yield return new Result("worldgen.sample", sw.Elapsed.TotalMilliseconds / 20_000 * 1e6, "ns/sample", "climate sample");
    }

    // ------------------------------------------------------------ zone tick

    private static IEnumerable<Result> Tick(ContentRegistry content, string seed, int monsters, int players, int ticks, int minions)
    {
        var generator = new WorldGenerator(content, Rng.Hash64(seed));
        var world = new WorldState(generator.Overlays, content.Rules);
        var log = new ListSimLog();
        var sw = Stopwatch.StartNew();
        var sim = ZoneBuilder.CreateZone(content, generator, world, generator.StartZone(), log);
        var report = ZoneBuilder.LoadZone(sim);
        yield return new Result("zone.load", sw.Elapsed.TotalMilliseconds, "ms", report.ToString());

        var factory = sim.Service<PatternFactory>();
        var characters = sim.Service<CharacterFactory>();
        var items = sim.Service<ItemGenerator>();
        var (sx, sy) = generator.StartPoint(generator.StartZone());
        var rng = new Rng(99);
        var playerEntities = new List<Entity>();
        var callings = content.Each<HollowWake.Core.Definitions.CallingDef>().Select(c => c.Id).ToList();
        for (var i = 0; i < players; i++)
        {
            var sheet = characters.Create(sim, "bench" + i, "bench", "Bench" + i, callings[i % callings.Count], items);
            sheet.Position = new Vector3((float)(sx + rng.Range(-6.0, 6.0)), (float)(sy + rng.Range(-6.0, 6.0)), 0);
            playerEntities.Add(characters.Materialize(sim, sheet));
        }
        if (minions > 0)
        {
            // Each player raises a pooled summon, so minions follow, assist and fight beside it: a minion-heavy zone.
            var runtime = sim.Service<WeaveRuntime>();
            foreach (var p in playerEntities)
            {
                var book = sim.World.Get<WeaveBook>(p)!;
                var stats = sim.World.Get<Stats>(p)!;
                var position = sim.World.Get<Transform>(p)!.Position;
                var slot = book.Add(new HollowWake.Core.Definitions.WeaveSpec { Preset = "raise_barrow" });
                stats.Attributes.Add(new Modifier { Attribute = "summon.max", Form = ModifierForm.Flat, Value = minions, Source = "bench" });
                stats.Attributes.Add(new Modifier { Attribute = "summon.count", Form = ModifierForm.Flat, Value = minions, Source = "bench" });
                runtime.TryBegin(sim, p, slot, Entity.None, position, Vector2.Zero, out _);
            }
            sim.Run(30);
        }
        var patterns = new[] { "hob", "wolf", "boar", "barrow_wight", "loosed_brigand" };
        for (var i = 0; i < monsters; i++)
        {
            var ang = rng.NextDouble() * Math.PI * 2;
            var dist = rng.Range(12.0, 70.0);
            factory.Spawn(sim, patterns[i % patterns.Length], new Vector3((float)(sx + Math.Cos(ang) * dist), (float)(sy + Math.Sin(ang) * dist), 0), new SpawnOptions { RollRarity = true, Rng = rng.Fork((ulong)i) });
        }
        // Players walk in circles so movement, interest and combat all exercise.
        var walker = 0;
        sim.Scheduler.Every(1.0, () =>
        {
            foreach (var p in playerEntities)
            {
                if (sim.World.TryGet<Player>(p, out var player) && sim.World.TryGet<Transform>(p, out var t))
                {
                    var ang = walker * 0.7 + p.Index;
                    player.Intents.Enqueue(new HollowWake.Core.Intents.MoveDirIntent(new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang))));
                    if (sim.World.TryGet<WeaveBook>(p, out var book) && book.Slots.Count > 0)
                    {
                        var target = sim.Spatial.Within(t.Position, 10).Select(w => w.Entity).FirstOrDefault(e => e != p && sim.World.Has<Behavior>(e));
                        player.Intents.Enqueue(new HollowWake.Core.Intents.UseWeaveIntent(walker % book.Slots.Count, target, t.Position, Vector2.UnitX));
                    }
                }
            }
            walker++;
        });

        var entitiesStart = sim.World.Count;
        sim.Run(40);
        sim.Profile = true;
        Settle();
        var samples = new double[ticks];
        var allocBefore = GC.GetTotalAllocatedBytes(true);
        var gen0Before = GC.CollectionCount(0);
        for (var i = 0; i < ticks; i++)
        {
            var t0 = Stopwatch.GetTimestamp();
            sim.Tick();
            samples[i] = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
        }
        var allocPerTick = (GC.GetTotalAllocatedBytes(true) - allocBefore) / (double)ticks;
        Array.Sort(samples);
        yield return new Result("zone.tick.avg", samples.Average(), "ms/tick", $"{entitiesStart} entities at start, {sim.World.Count} at end, {monsters} monsters, {players} players");
        yield return new Result("zone.tick.p50", samples[samples.Length / 2], "ms/tick", "median");
        yield return new Result("zone.tick.p95", samples[(int)(samples.Length * 0.95)], "ms/tick", "95th percentile");
        yield return new Result("zone.tick.max", samples[^1], "ms/tick", "worst tick");
        yield return new Result("zone.tick.alloc", allocPerTick / 1024, "KB/tick", $"gen0 collections {GC.CollectionCount(0) - gen0Before}");
        yield return new Result("zone.events", sim.Events.Log.Count, "events", "raised during the last tick");
        foreach (var cost in sim.Costs.Values.OrderByDescending(c => c.TotalAllocatedBytes).Take(6))
        {
            yield return new Result("system." + cost.Name, cost.TotalMilliseconds / Math.Max(1, cost.Ticks), "ms/tick", $"{cost.TotalAllocatedBytes / Math.Max(1, cost.Ticks) / 1024.0:0.0} KB/tick allocated");
        }
        var errors = log.Lines.Count(l => l.StartsWith("error"));
        yield return new Result("zone.errors", errors, "count", errors == 0 ? "no system errors" : log.Lines.First(l => l.StartsWith("error")));

        // Snapshots, on the server path: one view per player persists across snapshots and the zone ticks
        // twice between snapshots (a 20 Hz simulation under 10 Hz snapshots). Entering costs one burst of
        // spawns; afterwards only changed entities are sent, as full states or, when a client opts in, deltas.
        var builder = new SnapshotBuilder(sim, (float)content.Rules.Number("server.interest_radius", 48));
        var views = new List<SnapshotView>();
        var output = new List<Message>();
        long enterBytes = 0;
        foreach (var p in playerEntities)
        {
            var view = new SnapshotView();
            views.Add(view);
            output.Clear();
            builder.Entities(view, p, output);
            if (builder.Self(view, p) is { } self)
            {
                output.Add(self);
            }
            enterBytes += Encoded(output);
        }
        yield return new Result("snapshot.enter", enterBytes / (double)players / 1024, "KB/player", "spawns and self state on entering the zone, sent once");
        const int snapshots = 50;
        var (fullMs, fullBytes, known) = Snapshot(sim, builder, views, playerEntities, output, snapshots);
        yield return new Result("snapshot.build", fullMs / (snapshots * players), "ms/player", $"{known / (double)(snapshots * players):0} known entities per player; build and encode");
        yield return new Result("snapshot.bytes", fullBytes / (double)(snapshots * players) / 1024, "KB/player", "changed entities as full states plus self state, steady state, JSON");
        foreach (var view in views)
        {
            view.SendDeltas = true;
        }
        var (deltaMs, deltaBytes, _) = Snapshot(sim, builder, views, playerEntities, output, snapshots);
        yield return new Result("snapshot.delta.build", deltaMs / (snapshots * players), "ms/player", "the same with deltas opted in");
        yield return new Result("snapshot.delta.bytes", deltaBytes / (double)(snapshots * players) / 1024, "KB/player", "changed entities as deltas plus self state, steady state, JSON");
        foreach (var view in views)
        {
            view.SendInterface = true;
        }
        var (panelMs, panelBytes, _) = Snapshot(sim, builder, views, playerEntities, output, snapshots);
        yield return new Result("snapshot.interface.build", panelMs / (snapshots * players), "ms/player", "deltas plus the interface states on change, build and encode");
        yield return new Result("snapshot.interface.bytes", panelBytes / (double)(snapshots * players) / 1024, "KB/player", "an interface client: deltas, self state with pools and casts, weave slots, inventory, passives and pages on change");
    }

    /// <summary>Runs a stretch of snapshots the way the zone does: two ticks, then every viewer; the self state every fifth snapshot.</summary>
    private static (double Milliseconds, long Bytes, long Known) Snapshot(Simulation sim, SnapshotBuilder builder, List<SnapshotView> views, List<Entity> viewers, List<Message> output, int snapshots)
    {
        double ms = 0;
        long bytes = 0;
        long known = 0;
        for (var round = 0; round < snapshots; round++)
        {
            sim.Tick();
            sim.Tick();
            for (var i = 0; i < viewers.Count; i++)
            {
                output.Clear();
                var t0 = Stopwatch.GetTimestamp();
                builder.Entities(views[i], viewers[i], output);
                if (round % 5 == 0 && builder.Self(views[i], viewers[i]) is { } self)
                {
                    output.Add(self);
                }
                if (views[i].SendInterface)
                {
                    builder.Panels(views[i], viewers[i], output);
                }
                bytes += Encoded(output);
                ms += Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
                known += views[i].Known.Count;
            }
        }
        return (ms, bytes, known);
    }

    private static long Encoded(List<Message> messages)
    {
        long bytes = 0;
        foreach (var m in messages)
        {
            bytes += MessageSerializer.Encode(m).Length;
        }
        return bytes;
    }
}
