using System.Numerics;
using System.Text.Json;
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
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>The <c>cast</c> and <c>delay</c> effects.</summary>
public class CastEffectTests
{
    private static readonly Lazy<ContentRegistry> Loaded = new(() =>
    {
        var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out var report);
        Assert.False(report.HasErrors, report.ToString());
        registry
            .With(new WeaveDef
            {
                Id = "t_probe_blast",
                Form = "blast",
                Threads = { "ember" },
                Effects = { Spec("{\"type\": \"event\", \"name\": \"t_probe\", \"data\": {\"level\": \"weave.level\", \"depth\": \"depth\"}}") },
            })
            .With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } })
            .With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
        return registry;
    });

    private static TypedSpec Spec(string json) => JsonSerializer.Deserialize<TypedSpec>(json, ContentJson.Options)!;

    private sealed class Arena
    {
        public required Simulation Sim;
        public required Entity Caster;
        public required Entity Enemy;
        public readonly List<GameEvent> Events = new();

        public static Arena Create(ulong seed = 21)
        {
            var sim = ZoneBuilder.CreateArena(Loaded.Value, seed);
            var factory = sim.Service<PatternFactory>();
            var caster = factory.Spawn(sim, "loosed_kindler", new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
            var enemy = factory.Spawn(sim, "hob", new Vector3(20, 10, 0), new SpawnOptions { Faction = "blue" });
            sim.World.Remove<Behavior>(caster);
            sim.World.Remove<Behavior>(enemy);
            var arena = new Arena { Sim = sim, Caster = caster, Enemy = enemy };
            sim.Events.SubscribeAll(arena.Events.Add);
            return arena;
        }

        public CompiledEffect Effect(string json)
        {
            var effects = Sim.Service<EffectRegistry>();
            return effects.Compile(Spec(json), new EffectCompileContext { Content = Sim.Content, Registry = effects, Owner = "test", Compiler = Sim.Service<WeaveCompiler>() });
        }

        public EffectContext Context(Vector3 point, CompiledWeave? host = null, int depth = 0, Vector2? direction = null, string tags = "") => new()
        {
            Sim = Sim,
            Source = Caster,
            Point = point,
            Direction = direction ?? Vector2.Zero,
            Weave = host,
            Tags = (host?.Tags ?? TagSet.Empty).Union(TagSet.Parse(tags)),
            Depth = depth,
            Rng = new Rng(77),
        };
    }

    [Fact]
    public void CastRunsAPresetAtAPointWithTheHostsLevelAndOneDeeper()
    {
        var arena = Arena.Create();
        var sim = arena.Sim;
        var host = sim.Service<WeaveCompiler>().Compile(new WeaveSpec { Preset = "ember_bolt", Level = 3 });
        var cast = arena.Effect("{\"type\": \"cast\", \"weave\": \"t_probe_blast\", \"at\": \"point\"}");
        var stats = sim.World.Get<Stats>(arena.Caster)!;
        var mana = stats.Resource("mana");
        var point = sim.World.Get<Transform>(arena.Enemy)!.Position;

        cast.Execute(arena.Context(point, host, depth: 2, tags: "t_host"));

        var probe = Assert.Single(arena.Events.OfType<CustomEvent>(), e => e.Name == "t_probe");
        Assert.Equal(3, probe.Data["level"]);
        Assert.Equal(3, probe.Data["depth"]);
        var hit = Assert.Single(arena.Events.OfType<DamageDealt>());
        Assert.Equal(arena.Caster, hit.Source);
        Assert.Equal(arena.Enemy, hit.Target);
        // The cast runs one deeper than its host; the blast's area deepens once more for each body it reaches.
        Assert.Equal(4, hit.Depth);
        Assert.True(hit.Tags.Has("t_host"));
        Assert.True(hit.Tags.Has("form.blast"));
        // Free of cost and cooldown, and not a cast by the caster.
        Assert.Equal(mana, stats.Resource("mana"));
        Assert.False(sim.World.Get<WeaveBook>(arena.Caster)!.ReadyAt.ContainsKey("t_probe_blast"));
        Assert.Empty(arena.Events.OfType<WeaveCast>());
    }

    [Fact]
    public void CastFiresDeliveriesFromThePointAndTurnsWhenRotating()
    {
        var arena = Arena.Create();
        var sim = arena.Sim;
        var cast = arena.Effect("{\"type\": \"cast\", \"weave\": \"ember_bolt\", \"at\": \"point\", \"aim\": \"rotating\", \"turn\": 90}");
        var point = new Vector3(15, 10, 0);
        var ctx = arena.Context(point, direction: Vector2.UnitX);
        cast.Execute(ctx);
        cast.Execute(ctx);
        var spawned = arena.Events.OfType<EntitySpawned>().Where(s => s.Pattern == "projectile").ToList();
        Assert.Equal(2, spawned.Count);
        Assert.All(spawned, s => Assert.Equal(new Vector3(15, 10, 1), s.Position));
        var yaws = spawned.Select(s => sim.World.Get<Transform>(s.Entity)!.Yaw).ToList();
        Assert.Equal(0, yaws[0], 4);
        Assert.Equal(MathF.PI / 2, yaws[1], 4);
    }

    [Fact]
    public void RandomAimIsTheSameForTheSameSeed()
    {
        List<float> Run()
        {
            var arena = Arena.Create(seed: 33);
            var cast = arena.Effect("{\"type\": \"cast\", \"weave\": \"ember_bolt\", \"at\": \"point\", \"aim\": \"random\"}");
            var ctx = arena.Context(new Vector3(15, 10, 0));
            for (var i = 0; i < 4; i++)
            {
                cast.Execute(ctx);
            }
            return arena.Events.OfType<EntitySpawned>().Select(s => arena.Sim.World.Get<Transform>(s.Entity)!.Yaw).ToList();
        }
        var first = Run();
        Assert.Equal(4, first.Count);
        Assert.Equal(first, Run());
        Assert.True(first.Distinct().Count() > 1);
    }

    [Fact]
    public void CastRefusesUnknownWeavesAndChoices()
    {
        var arena = Arena.Create();
        Assert.Throws<InvalidOperationException>(() => arena.Effect("{\"type\": \"cast\", \"weave\": \"no_such_weave\"}"));
        Assert.Throws<InvalidOperationException>(() => arena.Effect("{\"type\": \"cast\", \"weave\": \"ember_bolt\", \"at\": \"moon\"}"));
    }

    [Fact]
    public void DelayFiresLaterOneDeeperAndTheSameEveryRun()
    {
        List<(long Tick, double Depth)> Run()
        {
            var arena = Arena.Create();
            var delay = arena.Effect("{\"type\": \"delay\", \"seconds\": 0.5, \"effects\": [ {\"type\": \"event\", \"name\": \"t_later\", \"data\": {\"depth\": \"depth\"}} ]}");
            delay.Execute(arena.Context(new Vector3(12, 10, 0)));
            Assert.Empty(arena.Events.OfType<CustomEvent>());
            arena.Sim.Run(20);
            return arena.Events.OfType<CustomEvent>().Select(e => (e.Tick, e.Data["depth"])).ToList();
        }
        var fired = Assert.Single(Run());
        Assert.Equal(10, fired.Tick);
        Assert.Equal(1, fired.Depth);
        Assert.Equal(new[] { fired }, Run());
    }

    [Fact]
    public void DelayDoesNotFireOnceItsSourceIsGone()
    {
        var arena = Arena.Create();
        var delay = arena.Effect("{\"type\": \"delay\", \"seconds\": 0.5, \"effects\": [ {\"type\": \"event\", \"name\": \"t_later\"} ]}");
        delay.Execute(arena.Context(new Vector3(12, 10, 0)));
        arena.Sim.World.Destroy(arena.Caster);
        arena.Sim.Run(20);
        Assert.Empty(arena.Events.OfType<CustomEvent>());
    }
}
