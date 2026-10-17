using System.Diagnostics;
using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.Weaves;

namespace HollowWake.Tools;

/// <summary>
/// The deliveries' benchmark group: a crowd of still foes in an arena and a caster that keeps flights in
/// the air (fans of homing, forking bolts, orbs that shed shards), areas with lines of sight and fields with
/// domains. Measures what the projectile and field systems cost per tick and what they allocate.
/// </summary>
public static class BenchDeliveries
{
    public static IEnumerable<Bench.Result> Run(ContentRegistry content, int ticks = 400)
    {
        var sim = ZoneBuilder.CreateArena(content, Rng.Hash64("deliveries"));
        var factory = sim.Service<PatternFactory>();
        var caster = factory.Spawn(sim, "loosed_kindler", new Vector3(32, 32, 0), new SpawnOptions { RollRarity = false });
        sim.World.Remove<Behavior>(caster);
        var foes = new List<Entity>();
        for (var y = 0; y < 10; y++)
        {
            for (var x = 0; x < 12; x++)
            {
                var foe = factory.Spawn(sim, "boar", new Vector3(14 + x * 3.1f, 16 + y * 3.3f, 0), new SpawnOptions { RollRarity = false });
                sim.World.Remove<Behavior>(foe);
                var stats = sim.World.Get<Stats>(foe)!;
                stats.Attributes.Add(new Modifier { Attribute = "life.max", Value = 1_000_000, Source = "bench" });
                stats.SetResource("life", stats.ResourceMax("life"));
                foes.Add(foe);
            }
        }
        var compiler = sim.Service<WeaveCompiler>();
        var bolts = compiler.Compile(new WeaveSpec { Form = "bolt", Threads = { "storm" }, Glyphs = { GlyphRef.Parse("fork"), GlyphRef.Parse("forking"), GlyphRef.Parse("seeking") } });
        var orb = compiler.Compile(new WeaveSpec { Preset = "tide_orb" });
        var nova = compiler.Compile(new WeaveSpec { Form = "nova", Threads = { "ember" } });
        var field = compiler.Compile(new WeaveSpec { Preset = "tide_maelstrom" });
        var rng = new Rng(17);
        var flights = 0L;
        void Act()
        {
            var tick = sim.Clock.Tick;
            var target = foes[rng.Next(foes.Count)];
            if (tick % 2 == 0)
            {
                Cast(sim, caster, bolts, target, rng);
            }
            if (tick % 20 == 0)
            {
                Cast(sim, caster, orb, target, rng);
            }
            if (tick % 10 == 5)
            {
                Cast(sim, caster, nova, caster, rng);
            }
            if (tick % 40 == 0)
            {
                Cast(sim, caster, field, target, rng);
            }
        }
        for (var i = 0; i < 60; i++)
        {
            Act();
            sim.Tick();
        }
        sim.Profile = true;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetTotalAllocatedBytes(true);
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < ticks; i++)
        {
            Act();
            sim.Tick();
            flights += sim.World.CountOf<Projectile>();
        }
        var elapsed = sw.Elapsed.TotalMilliseconds;
        var allocated = GC.GetTotalAllocatedBytes(true) - before;
        var projectiles = sim.Costs.TryGetValue("projectiles", out var p) ? p : null;
        var fields = sim.Costs.TryGetValue("fields", out var f) ? f : null;
        var note = $"{flights / (double)ticks:0} flights in the air on average among {foes.Count} foes";
        yield return new Bench.Result("deliveries.tick", elapsed / ticks, "ms/tick", note + ", casts included");
        yield return new Bench.Result("deliveries.tick.alloc", allocated / 1024.0 / ticks, "KB/tick", "casts, hits and effects included");
        if (projectiles is not null)
        {
            yield return new Bench.Result("deliveries.projectiles", projectiles.TotalMilliseconds / projectiles.Ticks, "ms/tick", $"{projectiles.TotalAllocatedBytes / (double)projectiles.Ticks / 1024.0:0.0} KB/tick allocated, hits and their effects included");
        }
        if (fields is not null)
        {
            yield return new Bench.Result("deliveries.fields", fields.TotalMilliseconds / fields.Ticks, "ms/tick", $"{fields.TotalAllocatedBytes / (double)fields.Ticks / 1024.0:0.0} KB/tick allocated");
        }
    }

    /// <summary>Runs a compiled weave's graph from the caster at a target, free of cost and cast time.</summary>
    private static void Cast(Simulation sim, Entity caster, CompiledWeave weave, Entity target, Rng rng)
    {
        var ctx = new EffectContext
        {
            Sim = sim,
            Source = caster,
            Target = target,
            Point = sim.World.Get<Transform>(target)!.Position,
            Weave = weave,
            Tags = weave.Tags,
            Rng = new Rng(rng.NextUInt64()),
        };
        CompiledEffect.ExecuteAll(weave.Effects, ctx);
    }
}
