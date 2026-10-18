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

    // ------------------------------------------------------------ casting the host

    [Fact]
    public void CastOfTheHostNeedsNoPresetTakesNoLevelAndWantsARunningWeave()
    {
        var arena = Arena.Create();
        // No preset is named host and none is needed: the host is whichever weave runs the effect.
        var cast = arena.Effect("{\"type\": \"cast\", \"weave\": \"host\", \"at\": \"target\", \"damage_scale\": 0.5}");
        Assert.Throws<InvalidOperationException>(() => arena.Effect("{\"type\": \"cast\", \"weave\": \"host\", \"level\": 3}"));
        // A context that runs no weave (a status's or a proc's effects) has no host to cast.
        cast.Execute(arena.Context(new Vector3(15, 10, 0)));
        Assert.Empty(arena.Events);
    }

    [Fact]
    public void CastOfTheHostLandsItsHitAgainAtAScaleWithoutPayingTwice()
    {
        var arena = CastingArena.Create();
        var slot = arena.Slot("t_echo_bolt");
        var mana = arena.Stats.Resource("mana");
        var start = arena.Now;
        arena.Begin(slot);
        Assert.Equal(start + 2, arena.Book.ReadyAt[slot.Key], 6);
        arena.RunSeconds(1.5);

        // The bolt's on_hit casts the bolt's own graph again at the body it struck: a second flight from there
        // lands the hit again at half, one level deeper, and its own cast of the host is refused (the test
        // layer holds rule effects.host_depth at one).
        var hits = arena.Of<DamageDealt>().Where(d => d.Target == arena.Enemy).ToList();
        Assert.Equal(2, hits.Count);
        Assert.Equal(hits[0].Total * 0.5, hits[1].Total, 6);
        Assert.True(hits[1].Depth > hits[0].Depth);
        Assert.Equal(2, arena.Of<EntitySpawned>().Count(s => s.Pattern == "projectile"));
        // Still one cast: paid once, its cooldown started once at the press, published once.
        Assert.Equal(mana - 3, arena.Stats.Resource("mana"), 6);
        Assert.Equal(start + 2, arena.Book.ReadyAt[slot.Key], 6);
        Assert.Single(arena.Of<WeaveCast>(), c => c.Weave == slot.Key);
    }

    [Fact]
    public void AWeaveCastingItselfStopsAtTheRuleDepth()
    {
        var arena = CastingArena.Create();
        var log = new ListSimLog();
        arena.Sim.Log = log;
        arena.Begin(arena.Slot("t_recast"));
        arena.RunSeconds(0.2);
        // Every run casts the host again unconditionally. The first run and the one rerun the rule allows land
        // the hit; the rerun's own cast is refused, long before effects.max_depth would cut the chain.
        Assert.Equal(2, arena.Of<DamageDealt>().Count(d => d.Target == arena.Enemy));
        Assert.DoesNotContain(log.Lines, line => line.StartsWith("warn:", StringComparison.Ordinal));
    }

    [Fact]
    public void TheHostDepthCountsEachWeaveApart()
    {
        var arena = CastingArena.Create();
        arena.Begin(arena.Slot("t_relayer"));
        // The relayer casts itself once more, and each of its two runs casts the relay preset, which casts itself
        // once more in turn: host casts of one weave do not use up another weave's depth.
        Assert.Equal(2, arena.Custom("t_relayer").Count);
        Assert.Equal(4, arena.Custom("t_relay").Count);
    }

    [Fact]
    public void AGlyphHookCastsWhicheverWeaveItSitsOn()
    {
        var arena = CastingArena.Create();
        arena.Stats.Attributes.Add(new Attributes.Modifier { Attribute = "crit.chance", Form = Attributes.ModifierForm.Override, Value = 0, Source = "test" });
        var slot = arena.Slot(new WeaveSpec { Form = "blast", Threads = { "mind" }, Glyphs = { "t_echo" } });
        arena.Begin(slot);
        arena.RunSeconds(1.5);

        // The glyph's on_hit hook joined the blast's per-target list and found its host, the blast, when it ran:
        // the blast goes off again at the body it struck, at half, and the rerun's hook is refused. The weave's
        // probes read the hooked graph as before.
        var weave = slot.Compiled!;
        Assert.Empty(weave.Dormant);
        Assert.True(weave.HasMechanism("area"));
        var hits = arena.Of<DamageDealt>().Where(d => d.Target == arena.Enemy).ToList();
        Assert.Equal(2, hits.Count);
        Assert.Equal(hits[0].Total * 0.5, hits[1].Total, 6);
        Assert.All(hits, h => Assert.True(h.Tags.Has("form.blast")));
        Assert.Single(arena.Of<WeaveCast>(), c => c.Weave == slot.Key);
    }

    [Fact]
    public void ContentCastsTheHostAsDeepAsTheRuleSaysAndNamesNoPresetHost()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hollowwake-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "host.yaml"), string.Join("\n",
                "kind: rules",
                "entries:",
                "  - { id: effects, patch: true, values: { host_depth: 3 } }",
                "---",
                "kind: forms",
                "entries:",
                "  - id: t_host_form",
                "    targeting: enemy",
                "    cast_mode: instant",
                "    attributes: { range: 30 }",
                "    min_threads: 0",
                "    effects: [ { type: damage, amount: 10, threads: [mind], can_crit: false }, { type: cast, weave: host, at: target } ]",
                "---",
                "kind: weaves",
                "entries:",
                "  - { id: host, form: t_host_form }",
                "  - { id: t_host_level, form: t_host_form, effects: [ { type: cast, weave: host, level: 2 } ] }",
                "  - { id: t_host_fits, form: t_host_form }",
                ""));
            var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            var errors = report.Errors.Select(e => $"{e.Id}: {e.Message}").ToList();
            Assert.Contains(errors, e => e.StartsWith("host:", StringComparison.Ordinal) && e.Contains("cannot be named"));
            Assert.Contains(errors, e => e.StartsWith("t_host_level:", StringComparison.Ordinal) && e.Contains("takes no 'level'"));
            Assert.DoesNotContain(errors, e => e.StartsWith("t_host_fits", StringComparison.Ordinal));

            registry
                .With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } })
                .With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
            var arena = CastingArena.Create(registry: registry);
            arena.Begin(arena.Slot(new WeaveSpec { Form = "t_host_form" }));
            // With the rule at three, the first run and three reruns land the hit, each rerun one level deeper.
            var hits = arena.Of<DamageDealt>().Where(d => d.Target == arena.Enemy).ToList();
            Assert.Equal(new[] { 0, 1, 2, 3 }, hits.Select(h => h.Depth - hits[0].Depth));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
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
