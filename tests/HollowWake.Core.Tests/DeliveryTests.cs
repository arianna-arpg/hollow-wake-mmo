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

/// <summary>A flat arena for delivery tests: a caster, still bodies to strike, compiled effects and contexts.</summary>
internal sealed class DeliveryArena
{
    private static readonly Lazy<ContentRegistry> Loaded = new(() =>
    {
        var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out var report);
        Assert.False(report.HasErrors, report.ToString());
        registry
            .With(new WeaveDef { Id = "t_spark", Form = "blast", Threads = { "ember" }, Effects = { Spec("{\"type\": \"event\", \"name\": \"t_spark\", \"data\": {\"depth\": \"depth\"}}") } })
            .With(new GlyphDef { Id = "t_wide", Modifiers = { new Attributes.Modifier { Attribute = "area.angle", Form = Attributes.ModifierForm.Increased, Value = 0.5 }, new Attributes.Modifier { Attribute = "melee.reach", Value = 1 } } })
            .With(new GlyphDef { Id = "t_echo", Modifiers = { new Attributes.Modifier { Attribute = "area.scatter", Value = 2 }, new Attributes.Modifier { Attribute = "area.pulses", Value = 1 } } })
            .With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } })
            .With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
        return registry;
    });

    public static ContentRegistry Registry => Loaded.Value;

    public required Simulation Sim;
    public required Entity Caster;
    public readonly List<GameEvent> Events = new();

    /// <summary>The radius each entity had when it was spawned.</summary>
    public readonly Dictionary<Entity, float> SpawnRadius = new();

    public static TypedSpec Spec(string json) => JsonSerializer.Deserialize<TypedSpec>(json, ContentJson.Options)!;

    public static DeliveryArena Create(ulong seed = 11, Vector3? caster = null)
    {
        var sim = ZoneBuilder.CreateArena(Registry, seed);
        var e = sim.Service<PatternFactory>().Spawn(sim, "loosed_kindler", caster ?? new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
        sim.World.Remove<Behavior>(e);
        var arena = new DeliveryArena { Sim = sim, Caster = e };
        sim.Events.SubscribeAll(arena.Events.Add);
        sim.Events.Subscribe<EntitySpawned>(s => arena.SpawnRadius[s.Entity] = sim.World.Get<Transform>(s.Entity)?.Radius ?? -1);
        return arena;
    }

    /// <summary>A still body: a boar of the other side, or of the caster's side.</summary>
    public Entity Body(float x, float y, string faction = "blue", string pattern = "boar")
    {
        var e = Sim.Service<PatternFactory>().Spawn(Sim, pattern, new Vector3(x, y, 0), new SpawnOptions { Faction = faction, RollRarity = false });
        Sim.World.Remove<Behavior>(e);
        return e;
    }

    public CompiledEffect Effect(string json)
    {
        var effects = Sim.Service<EffectRegistry>();
        return effects.Compile(Spec(json), new EffectCompileContext { Content = Sim.Content, Registry = effects, Owner = "test", Compiler = Sim.Service<WeaveCompiler>(), Threads = new[] { "ember" } });
    }

    public EffectContext Context(Vector3 point, Vector2? direction = null, Entity? target = null, CompiledWeave? weave = null, ulong rng = 77) => new()
    {
        Sim = Sim,
        Source = Caster,
        Target = target ?? Entity.None,
        Point = point,
        Direction = direction ?? Vector2.Zero,
        Weave = weave,
        Tags = weave?.Tags ?? TagSet.Empty,
        Rng = new Rng(rng),
    };

    public void Run(string json, Vector3 point, Vector2? direction = null, Entity? target = null, CompiledWeave? weave = null) =>
        Effect(json).Execute(Context(point, direction, target, weave));

    public CompiledWeave Weave(string form, string threads = "ember", params string[] glyphs)
    {
        var spec = new WeaveSpec { Form = form };
        if (threads.Length > 0)
        {
            spec.Threads.Add(threads);
        }
        foreach (var g in glyphs)
        {
            spec.Glyphs.Add(GlyphRef.Parse(g));
        }
        return Sim.Service<WeaveCompiler>().Compile(spec);
    }

    public List<Entity> Struck(string name = "t_hit") => Events.OfType<CustomEvent>().Where(e => e.Name == name).Select(e => e.Target).ToList();

    public List<T> Of<T>() where T : GameEvent => Events.OfType<T>().ToList();

    public List<EntitySpawned> Spawned(string pattern) => Events.OfType<EntitySpawned>().Where(s => s.Pattern == pattern).ToList();

    public void Tick(int n = 1) => Sim.Run(n);
}

/// <summary>Hit shapes: each holds what it draws, and its reach covers all of it.</summary>
public class ShapeTests
{
    private static bool In(string shape, float x, float y, float radius, float angle = float.NaN, float width = float.NaN, float inner = float.NaN, float pad = 0, float facing = 0)
    {
        var s = Shapes.Get(shape);
        var size = new ShapeSize(radius, float.IsNaN(angle) ? s.DefaultAngle : angle, float.IsNaN(width) ? s.DefaultWidth(radius, 2) : width, float.IsNaN(inner) ? s.DefaultInner : inner);
        return s.Contains(size, new Vector2(x, y), new Vector2(MathF.Cos(facing), MathF.Sin(facing)), pad);
    }

    [Fact]
    public void EveryShapeHoldsWhatItDraws()
    {
        Assert.True(In("circle", 1.9f, 0, 2));
        Assert.False(In("circle", 2.1f, 0, 2));
        Assert.True(In("circle", 2.4f, 0, 2, pad: 0.5f));
        Assert.False(In("circle", 0.5f, 0, 2, inner: 0.5f));
        Assert.True(In("circle", 1.5f, 0, 2, inner: 0.5f));

        Assert.True(In("square", 0.9f, 0.9f, 1));
        Assert.False(In("square", 1.1f, 0, 1));
        Assert.True(In("square", 1.3f, 0, 1, facing: MathF.PI / 4));

        Assert.True(In("triangle", 1.9f, 0, 2));
        Assert.True(In("triangle", -0.9f, 0, 2));
        Assert.False(In("triangle", -1.1f, 0, 2));
        Assert.False(In("triangle", 0, 1.5f, 2));

        Assert.True(In("sector", 2, 1.5f, 3));
        Assert.False(In("sector", 1, 1.2f, 3));
        Assert.True(In("cone", 2, 1.5f, 3));
        Assert.True(In("sector", 0.05f, -0.05f, 3));
        Assert.True(In("sector", 1, 1.2f, 3, pad: 0.4f));
        Assert.False(In("sector", 3.2f, 0, 3));

        Assert.False(In("crescent", 1, 0, 4));
        Assert.True(In("crescent", 3, 0, 4));
        Assert.True(In("crescent", 0.5f, 3, 4));
        Assert.False(In("crescent", -1, 3, 4));

        Assert.True(In("line", 4.9f, 0.9f, 5));
        Assert.False(In("line", 5.1f, 0, 5));
        Assert.False(In("line", 2, 1.1f, 5));
        Assert.False(In("line", -0.1f, 0, 5));

        Assert.True(In("band", 2, 1.4f, 3));
        Assert.False(In("band", 2, 1.6f, 3));

        Assert.True(In("bar", 0.2f, 1.9f, 2));
        Assert.False(In("bar", 0.3f, 0, 2));
        Assert.False(In("bar", 0, 2.1f, 2));

        Assert.True(In("arc", 0, 0, 1));
        Assert.True(In("arc", -0.3f, 0.7f, 1));
        Assert.False(In("arc", 0.3f, 0, 1));
        Assert.False(In("arc", -1, 0.9f, 1));
        Assert.True(In("wave", 0, 0, 1));
        Assert.True(In("wave", -0.4f, 1.3f, 1));
        Assert.False(In("arc", -0.4f, 1.3f, 1));
    }

    [Fact]
    public void EveryShapeLiesWithinItsReach()
    {
        foreach (var name in Shapes.Names)
        {
            var shape = Shapes.Get(name);
            var size = new ShapeSize(2.5f, shape.DefaultAngle, shape.DefaultWidth(2.5f, 2), shape.DefaultInner);
            var reach = shape.Reach(size);
            for (var x = -8f; x <= 8f; x += 0.1f)
            {
                for (var y = -8f; y <= 8f; y += 0.1f)
                {
                    if (shape.Contains(size, new Vector2(x, y), new Vector2(0.6f, 0.8f), 0))
                    {
                        Assert.True(MathF.Sqrt(x * x + y * y) <= reach + 0.001f, $"{name} holds ({x}, {y}) beyond its reach {reach}");
                    }
                }
            }
        }
    }
}

/// <summary>Areas strike where their shapes are drawn, pick and require, and echo.</summary>
public class AreaDeliveryTests
{
    [Fact]
    public void AnchoredShapesExtendFromTheSourceAlongTheFacing()
    {
        var arena = DeliveryArena.Create();
        var ahead = arena.Body(13, 10);
        var wide = arena.Body(12, 12.2f);
        var behind = arena.Body(7, 10);
        arena.Run("{\"type\": \"area\", \"shape\": \"band\", \"radius\": 4, \"effects\": [{\"type\": \"event\", \"name\": \"t_hit\"}]}", new Vector3(13, 10, 0), Vector2.UnitX);
        Assert.Equal(new[] { ahead }, arena.Struck());
        arena.Events.Clear();
        arena.Run("{\"type\": \"area\", \"shape\": \"sector\", \"radius\": 4, \"angle\": 140, \"effects\": [{\"type\": \"event\", \"name\": \"t_hit\"}]}", new Vector3(13, 10, 0), Vector2.UnitX);
        Assert.Equal(new[] { ahead, wide }.OrderBy(e => e.Index), arena.Struck().OrderBy(e => e.Index));
        Assert.DoesNotContain(behind, arena.Struck());
    }

    [Fact]
    public void ReachFromTheEdgeAndFarEdgeOnly()
    {
        var arena = DeliveryArena.Create();
        var near = arena.Body(11.5f, 10);
        var far = arena.Body(14.2f, 10);
        var effect = "{\"type\": \"area\", \"shape\": \"sector\", \"radius\": 4, \"effects\": [{\"type\": \"event\", \"name\": \"t_hit\"}]";
        arena.Run(effect + "}", new Vector3(14, 10, 0), Vector2.UnitX);
        Assert.Equal(new[] { near }, arena.Struck());
        arena.Events.Clear();
        arena.Run(effect + ", \"reach\": \"edge\"}", new Vector3(14, 10, 0), Vector2.UnitX);
        Assert.Equal(new[] { near, far }, arena.Struck());
        arena.Events.Clear();
        arena.Run(effect + ", \"reach\": \"edge\", \"inner\": 0.6}", new Vector3(14, 10, 0), Vector2.UnitX);
        Assert.Equal(new[] { far }, arena.Struck());
    }

    [Fact]
    public void PicksChooseAmongManyAndRequireFilters()
    {
        var arena = DeliveryArena.Create();
        var a = arena.Body(11, 10);
        var b = arena.Body(13, 10);
        var c = arena.Body(10, 14);
        arena.Sim.World.Get<Stats>(b)!.SetResource("life", 10);
        var nova = "{\"type\": \"area\", \"radius\": 5, \"center\": \"source\", \"effects\": [{\"type\": \"event\", \"name\": \"t_hit\"}]";
        arena.Run(nova + ", \"max_targets\": 1, \"pick\": \"nearest\"}", new Vector3(10, 10, 0));
        Assert.Equal(new[] { a }, arena.Struck());
        arena.Events.Clear();
        arena.Run(nova + ", \"max_targets\": 1, \"pick\": \"farthest\"}", new Vector3(10, 10, 0));
        Assert.Equal(new[] { c }, arena.Struck());
        arena.Events.Clear();
        arena.Run(nova + ", \"max_targets\": 2, \"pick\": \"lowest_life\"}", new Vector3(10, 10, 0));
        Assert.Equal(b, arena.Struck()[0]);
        arena.Events.Clear();
        arena.Run(nova + ", \"require\": \"target.life_pct < 0.5\"}", new Vector3(10, 10, 0));
        Assert.Equal(new[] { b }, arena.Struck());
    }

    [Fact]
    public void AreaAngleAndMeleeReachScaleSweeps()
    {
        var arena = DeliveryArena.Create();
        var side = arena.Body(11.5f, 12.4f);
        var reach = arena.Body(13.6f, 10);
        var plain = arena.Weave("cleave", "");
        var wide = arena.Sim.Service<WeaveCompiler>().Compile(new WeaveSpec { Form = "cleave", Glyphs = { GlyphRef.Parse("t_wide") } });
        foreach (var e in plain.Effects)
        {
            e.Execute(arena.Context(new Vector3(13, 10, 0), Vector2.UnitX, weave: plain));
        }
        Assert.DoesNotContain(arena.Of<DamageDealt>(), d => d.Target == side || d.Target == reach);
        arena.Events.Clear();
        foreach (var e in wide.Effects)
        {
            e.Execute(arena.Context(new Vector3(13, 10, 0), Vector2.UnitX, weave: wide));
        }
        Assert.Contains(arena.Of<DamageDealt>(), d => d.Target == side);
        Assert.Contains(arena.Of<DamageDealt>(), d => d.Target == reach);
    }

    [Fact]
    public void AftershocksAndPulsesEchoTheArea()
    {
        var arena = DeliveryArena.Create();
        arena.Body(15, 10);
        var weave = arena.Sim.Service<WeaveCompiler>().Compile(new WeaveSpec { Form = "blast", Threads = { "ember" }, Glyphs = { GlyphRef.Parse("t_echo") } });
        var area = arena.Effect("{\"type\": \"area\", \"radius\": 2, \"center\": \"point\", \"effects\": [{\"type\": \"event\", \"name\": \"t_hit\", \"data\": {\"depth\": \"depth\"}}]}");
        var pending = arena.Sim.Scheduler.Pending;
        area.Execute(arena.Context(new Vector3(15, 10, 0), weave: weave));
        // Struck now; two aftershocks and one pulse wait their turn.
        Assert.Single(arena.Struck());
        Assert.Equal(3, arena.Sim.Scheduler.Pending - pending);
        arena.Tick(20);
        var hits = arena.Events.OfType<CustomEvent>().Where(e => e.Name == "t_hit").ToList();
        Assert.Equal(1, hits[0].Data["depth"]);
        // The pulse strikes where the area stood, one level deeper; echoes never echo again.
        Assert.Contains(hits.Skip(1), h => h.Data["depth"] == 2);
        Assert.Equal(pending, arena.Sim.Scheduler.Pending);
    }
}

/// <summary>Projectiles: firing styles, trajectories, forks, returns, rehits, chains, emitters and sequels.</summary>
public class ProjectileDeliveryTests
{
    private const string Hit = "\"on_hit\": [{\"type\": \"event\", \"name\": \"t_hit\"}]";

    private static List<float> Yaws(DeliveryArena arena) => arena.Spawned("projectile").Select(s => arena.Sim.World.Get<Transform>(s.Entity)!.Yaw).ToList();

    [Fact]
    public void FansVolleysAndRingsLeaveAsDrawn()
    {
        var arena = DeliveryArena.Create();
        arena.Run("{\"type\": \"projectile\", \"count\": 3, \"spread\": 30, \"speed\": 10, \"range\": 8, \"pierce\": 0}", new Vector3(20, 10, 0), Vector2.UnitX);
        var fan = Yaws(arena);
        Assert.Equal(new[] { -15f, 0f, 15f }, fan.Select(y => MathF.Round(y * 180 / MathF.PI, 3)));
        arena.Events.Clear();
        arena.Run("{\"type\": \"projectile\", \"fire\": \"volley\", \"count\": 3, \"volley_spacing\": 1, \"speed\": 10, \"range\": 8, \"pierce\": 0}", new Vector3(20, 10, 0), Vector2.UnitX);
        var volley = arena.Spawned("projectile");
        Assert.Equal(new[] { 9f, 10f, 11f }, volley.Select(s => MathF.Round(s.Position.Y, 4)));
        Assert.All(volley, s => Assert.Equal(0f, arena.Sim.World.Get<Transform>(s.Entity)!.Yaw));
        arena.Events.Clear();
        arena.Run("{\"type\": \"projectile\", \"fire\": \"ring\", \"count\": 4, \"speed\": 10, \"range\": 8, \"pierce\": 0}", new Vector3(20, 10, 0), Vector2.UnitY);
        Assert.Equal(new[] { 90f, 180f, 270f, 360f }, Yaws(arena).Select(y => MathF.Round(y * 180 / MathF.PI, 3)));
    }

    [Fact]
    public void SalvosFireOneShotABeatAtTheTargetWhereItNowStands()
    {
        var arena = DeliveryArena.Create();
        var target = arena.Body(18, 10);
        arena.Run("{\"type\": \"projectile\", \"fire\": \"salvo\", \"count\": 3, \"salvo_interval\": 0.2, \"speed\": 30, \"range\": 12, \"pierce\": 0, " + Hit + "}", new Vector3(18, 10, 0), target: target);
        Assert.Single(arena.Spawned("projectile"));
        arena.Tick(2);
        var t = arena.Sim.World.Get<Transform>(target)!;
        t.Position = new Vector3(14, 16, 0);
        arena.Sim.Spatial.Update(target, t.Position);
        arena.Tick(6);
        var shots = arena.Spawned("projectile");
        Assert.Equal(3, shots.Count);
        Assert.Equal(new long[] { 0, 4, 8 }, shots.Select(s => s.Tick - shots[0].Tick));
        // The last shot left this tick, aimed at where the target now stands.
        Assert.Equal(MathF.Atan2(16 - 10, 14 - 10), arena.Sim.World.Get<Transform>(shots[2].Entity)!.Yaw, 3);
        arena.Tick(10);
        Assert.Equal(3, arena.Spawned("projectile").Count);
    }

    [Fact]
    public void HomingFlightsTurnOntoTheirQuarry()
    {
        var arena = DeliveryArena.Create();
        var quarry = arena.Body(16, 14);
        var straight = "{\"type\": \"projectile\", \"speed\": 10, \"range\": 14, \"pierce\": 0, " + Hit + "}";
        arena.Run(straight, new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Tick(30);
        Assert.Empty(arena.Struck());
        arena.Run("{\"type\": \"projectile\", \"speed\": 10, \"range\": 14, \"pierce\": 0, \"homing\": 270, " + Hit + "}", new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Tick(30);
        Assert.Equal(new[] { quarry }, arena.Struck());
    }

    [Fact]
    public void ErraticFlightsRepeatForASeedAndDifferAcrossSeeds()
    {
        List<Vector3> Path(ulong seed)
        {
            var arena = DeliveryArena.Create();
            arena.Effect("{\"type\": \"projectile\", \"speed\": 8, \"range\": 30, \"pierce\": 0, \"erratic\": 720}").Execute(arena.Context(new Vector3(40, 10, 0), Vector2.UnitX, rng: seed));
            var e = arena.Spawned("projectile").Single().Entity;
            var path = new List<Vector3>();
            for (var i = 0; i < 30; i++)
            {
                arena.Tick();
                path.Add(arena.Sim.World.Get<Transform>(e)!.Position);
            }
            return path;
        }
        var first = Path(5);
        Assert.Equal(first, Path(5));
        Assert.NotEqual(first, Path(6));
        Assert.Contains(first, p => MathF.Abs(p.Y - 10) > 0.3f);
    }

    [Fact]
    public void TrajectoryAxesBendTheFlight()
    {
        Vector3 After(string axes, int ticks = 20)
        {
            var arena = DeliveryArena.Create();
            arena.Run("{\"type\": \"projectile\", \"speed\": 8, \"range\": 40, \"pierce\": 0" + axes + "}", new Vector3(40, 10, 0), Vector2.UnitX);
            var e = arena.Spawned("projectile").Single().Entity;
            arena.Tick(ticks);
            return arena.Sim.World.Get<Transform>(e)!.Position;
        }
        var straight = After("");
        Assert.Equal(10, straight.Y, 3);
        Assert.True(After(", \"recurve\": 90").Y > 10.5f);
        Assert.True(After(", \"accel\": 10").X > straight.X + 1);
        var weaving = After(", \"wave\": 1.25, \"amplitude\": 0.8", 4);
        Assert.True(MathF.Abs(weaving.Y - 10) > 0.5f);
        // A spiral wheels round where it left; an orbit circles its caster.
        var spiral = After(", \"spiral\": 180", 20);
        Assert.True(spiral.X < straight.X - 2);
        var orbit = After(", \"orbit\": 180, \"orbit_radius\": 2, \"duration\": 3", 10);
        Assert.Equal(2, Vector2.Distance(new Vector2(orbit.X, orbit.Y), new Vector2(10, 10)), 2);
    }

    [Fact]
    public void ForkChildrenInheritTheFlightAndSpareTheBodyThatSplitThem()
    {
        var arena = DeliveryArena.Create();
        var first = arena.Body(14, 10);
        arena.Run("{\"type\": \"projectile\", \"speed\": 10, \"range\": 14, \"pierce\": 0, \"forks\": 1, \"spin\": 90, \"wave\": 0.5, \"amplitude\": 0.01, " + Hit + "}", new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Tick(8);
        var fork = Assert.Single(arena.Of<ProjectileForked>());
        Assert.Equal(2, fork.Children);
        var children = arena.Spawned("projectile").Skip(1).Select(s => arena.Sim.World.Get<Projectile>(s.Entity)!).ToList();
        Assert.Equal(2, children.Count);
        Assert.Equal(new[] { -30f, 30f }, children.Select(c => MathF.Round(c.Heading * 180 / MathF.PI)).OrderBy(a => a));
        Assert.All(children, c =>
        {
            Assert.Equal(10, c.Speed);
            Assert.Equal(0, c.Forks);
            Assert.True(c.Spin > 0 && c.Wave > 0);
            Assert.True(c.FirstHitDone);
            Assert.True(c.Hits.Contains(first));
        });
        arena.Tick(20);
        Assert.Equal(new[] { first }, arena.Struck());
    }

    [Fact]
    public void ReturningFlightsStrikeAgainOnTheWayBack()
    {
        var arena = DeliveryArena.Create();
        var body = arena.Body(14, 10);
        arena.Run("{\"type\": \"projectile\", \"speed\": 12, \"range\": 8, \"pierce\": 3, \"return\": \"caster\", " + Hit + ", \"on_return\": [{\"type\": \"event\", \"name\": \"t_home\"}]}", new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Tick(40);
        Assert.Equal(new[] { body, body }, arena.Struck());
        Assert.Single(arena.Of<ProjectileTurned>());
        Assert.Single(arena.Of<ProjectileReturned>());
        Assert.Single(arena.Struck("t_home"));
        Assert.Single(arena.Of<EntityDespawned>(), d => d.Pattern == "projectile");
    }

    [Fact]
    public void RehitFlightsWaitOutTheirLockout()
    {
        var arena = DeliveryArena.Create();
        var body = arena.Body(14, 10);
        arena.Run("{\"type\": \"projectile\", \"speed\": 1, \"duration\": 3, \"radius\": 1.5, \"rehit\": 0.5, " + Hit + "}", new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Tick(60);
        var ticks = arena.Events.OfType<CustomEvent>().Where(e => e.Name == "t_hit").Select(e => e.Tick).ToList();
        Assert.True(ticks.Count >= 3, $"struck {ticks.Count} times");
        for (var i = 1; i < ticks.Count; i++)
        {
            Assert.True(ticks[i] - ticks[i - 1] >= 10, $"struck again after {ticks[i] - ticks[i - 1]} ticks");
        }
        Assert.All(arena.Struck(), e => Assert.Equal(body, e));
    }

    [Fact]
    public void SpentFlightsChainToTheNextFoe()
    {
        var arena = DeliveryArena.Create();
        var a = arena.Body(14, 10);
        var b = arena.Body(17, 14);
        arena.Run("{\"type\": \"projectile\", \"speed\": 12, \"range\": 12, \"pierce\": 0, \"chains\": 1, " + Hit + "}", new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Tick(30);
        Assert.Equal(new[] { a, b }, arena.Struck());
        var chained = Assert.Single(arena.Of<ProjectileChained>());
        Assert.Equal(a, chained.From);
        Assert.Equal(b, chained.To);
    }

    [Fact]
    public void PierceThenForkThenChainThenEnd()
    {
        var arena = DeliveryArena.Create();
        var a = arena.Body(13, 10);
        var b = arena.Body(16, 10);
        arena.Run("{\"type\": \"projectile\", \"speed\": 12, \"range\": 12, \"pierce\": 1, \"forks\": 1, " + Hit + "}", new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Tick(30);
        // Pierces the first body, forks on the second.
        Assert.Equal(new[] { a, b }, arena.Struck());
        Assert.InRange(Assert.Single(arena.Of<ProjectileForked>()).Position.X, 14.9f, 16f);
    }

    [Fact]
    public void EmittersAndSequelsCastWhereTheFlightIs()
    {
        var arena = DeliveryArena.Create();
        arena.Run("{\"type\": \"projectile\", \"speed\": 10, \"range\": 6, \"pierce\": 0, \"tick\": 0.2, \"on_tick\": [{\"type\": \"cast\", \"weave\": \"t_spark\", \"at\": \"point\"}], \"on_end\": [{\"type\": \"event\", \"name\": \"t_end\"}, {\"type\": \"cast\", \"weave\": \"t_spark\", \"at\": \"point\"}]}", new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Tick(20);
        var sparks = arena.Events.OfType<CustomEvent>().Where(e => e.Name == "t_spark").ToList();
        // Beats at 0.2 and 0.4 seconds; the flight runs out of range at 0.6 before its third beat, and the
        // sequel casts where it ended.
        Assert.Equal(2 + 1, sparks.Count);
        Assert.Single(arena.Struck("t_end"));
        // The flight runs one deeper than the cast, and the cast weave one deeper again.
        Assert.All(sparks, s => Assert.Equal(2, s.Data["depth"]));
    }

    [Fact]
    public void FirstHitRidersRunOnceAndChildrenDoNotRepeatThem()
    {
        var arena = DeliveryArena.Create();
        arena.Body(14, 10);
        arena.Body(16, 8.8f);
        arena.Body(16, 11.2f);
        arena.Run("{\"type\": \"projectile\", \"speed\": 12, \"range\": 12, \"pierce\": 0, \"forks\": 1, " + Hit + ", \"on_first_hit\": [{\"type\": \"event\", \"name\": \"t_first\"}]}", new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Tick(30);
        Assert.Equal(3, arena.Struck().Count);
        Assert.Single(arena.Struck("t_first"));
    }

    [Fact]
    public void LobbedFlightsLandAtTheirAimAndBounce()
    {
        var arena = DeliveryArena.Create();
        arena.Run("{\"type\": \"projectile\", \"speed\": 8, \"range\": 12, \"pierce\": 0, \"gravity\": true, \"on_land\": [{\"type\": \"field\", \"radius\": 0.5, \"duration\": 0.2, \"interval\": 1}]}", new Vector3(16, 10, 0), Vector2.UnitX);
        arena.Tick(40);
        var landed = Assert.Single(arena.Spawned("field"));
        Assert.Equal(16, landed.Position.X, 0);
        arena.Events.Clear();
        arena.Run("{\"type\": \"projectile\", \"speed\": 8, \"range\": 12, \"pierce\": 0, \"gravity\": true, \"bounce\": 1, \"on_land\": [{\"type\": \"field\", \"radius\": 0.5, \"duration\": 0.2, \"interval\": 1}]}", new Vector3(16, 10, 0), Vector2.UnitX);
        arena.Tick(60);
        Assert.Single(arena.Of<ProjectileBounced>());
        Assert.True(Assert.Single(arena.Spawned("field")).Position.X > 17);
    }

    [Fact]
    public void ShapedFlightsStrikeWhereTheirShapeIs()
    {
        var arena = DeliveryArena.Create();
        var wideLeft = arena.Body(14, 11.6f);
        var wideRight = arena.Body(14, 8.4f);
        arena.Run("{\"type\": \"projectile\", \"speed\": 10, \"range\": 8, \"pierce\": 5, \"shape\": \"bar\", \"shape_size\": 2, " + Hit + "}", new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Tick(20);
        Assert.Equal(2, arena.Struck().Count);
        arena.Events.Clear();
        arena.Run("{\"type\": \"projectile\", \"speed\": 10, \"range\": 8, \"pierce\": 5, " + Hit + "}", new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Tick(20);
        Assert.Empty(arena.Struck());
        _ = wideLeft;
        _ = wideRight;
    }

    [Fact]
    public void ASpentFlightDespawnsOnceAndLeavesTheGrid()
    {
        var arena = DeliveryArena.Create();
        arena.Body(10.9f, 10);
        arena.Run("{\"type\": \"projectile\", \"speed\": 20, \"range\": 1, \"pierce\": 0, " + Hit + "}", new Vector3(20, 10, 0), Vector2.UnitX);
        var e = arena.Spawned("projectile").Single().Entity;
        arena.Tick(3);
        Assert.Single(arena.Struck());
        Assert.Single(arena.Of<EntityDespawned>(), d => d.Entity == e);
        Assert.False(arena.Sim.Spatial.TryGetPosition(e, out _));
    }

    [Fact]
    public void AFlightWhoseCasterIsGoneEndsQuietly()
    {
        var arena = DeliveryArena.Create();
        var log = new ListSimLog();
        arena.Sim.Log = log;
        var target = arena.Body(18, 10);
        var bolt = arena.Sim.Service<WeaveCompiler>().Compile(new WeaveSpec { Preset = "storm_bolt" });
        foreach (var effect in bolt.Effects)
        {
            effect.Execute(arena.Context(new Vector3(18, 10, 0), target: target, weave: bolt));
        }
        arena.Tick(2);
        arena.Sim.World.Destroy(arena.Caster);
        arena.Tick(20);
        Assert.Empty(arena.Of<DamageDealt>());
        Assert.DoesNotContain(log.Lines, l => l.StartsWith("error"));
        Assert.Single(arena.Of<EntityDespawned>(), d => d.Pattern == "projectile");
        Assert.Empty(arena.Sim.World.Query<Projectile>());
    }
}

/// <summary>Storms, cascades and fields: where they land, when, and what they do to the bodies inside.</summary>
public class PlacementDeliveryTests
{
    private const string Mark = "{\"type\": \"field\", \"radius\": 0.4, \"duration\": 0.1, \"interval\": 1}";

    [Fact]
    public void StormsPlaceTheSameStrikesForASeedFoesFirst()
    {
        List<Vector3> Strikes(ulong seed, bool enemies)
        {
            var arena = DeliveryArena.Create();
            arena.Body(20, 12);
            arena.Body(22, 8);
            arena.Effect("{\"type\": \"scatter\", \"count\": 5, \"radius\": 4, \"interval\": 0.1, \"at\": \"" + (enemies ? "enemies" : "random") + "\", \"effects\": [" + Mark + "]}")
                .Execute(arena.Context(new Vector3(20, 10, 0), rng: seed));
            arena.Tick(20);
            return arena.Spawned("field").Select(s => s.Position).ToList();
        }
        var random = Strikes(3, false);
        Assert.Equal(5, random.Count);
        Assert.Equal(random, Strikes(3, false));
        Assert.NotEqual(random, Strikes(4, false));
        Assert.All(random, p => Assert.True(Vector2.Distance(new Vector2(p.X, p.Y), new Vector2(20, 10)) <= 4.001f));
        var foes = Strikes(3, true);
        Assert.Equal(new Vector2(20, 12), new Vector2(foes[0].X, foes[0].Y));
        Assert.Equal(new Vector2(22, 8), new Vector2(foes[1].X, foes[1].Y));
    }

    [Fact]
    public void TelegraphedStrikesAreMarkedThenLandLater()
    {
        var arena = DeliveryArena.Create();
        arena.Run("{\"type\": \"scatter\", \"count\": 2, \"radius\": 3, \"interval\": 0, \"telegraph\": 0.5, \"effects\": [" + Mark + "]}", new Vector3(20, 10, 0));
        Assert.Equal(2, arena.Spawned("telegraph").Count);
        Assert.Equal(2, arena.Of<PlacementTelegraphed>().Count);
        Assert.Empty(arena.Spawned("field"));
        arena.Tick(12);
        Assert.Equal(2, arena.Spawned("field").Count);
        Assert.Equal(2, arena.Of<EntityDespawned>().Count(d => d.Pattern is null && arena.Spawned("telegraph").Any(t => t.Entity == d.Entity)));
    }

    [Fact]
    public void CascadesStepOutwardLargerAndLater()
    {
        var arena = DeliveryArena.Create();
        arena.Run("{\"type\": \"cascade\", \"count\": 3, \"dir\": \"forward\", \"step\": 2, \"scale_step\": 0.5, \"interval\": 0.1, \"effects\": [{\"type\": \"field\", \"radius\": 1, \"duration\": 0.2, \"interval\": 1}]}", new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Tick(10);
        var fields = arena.Spawned("field");
        Assert.Equal(new[] { 20f, 22f, 24f, 26f }, fields.Select(f => MathF.Round(f.Position.X, 3)));
        Assert.Equal(new long[] { 0, 2, 4, 6 }, fields.Select(f => f.Tick - fields[0].Tick));
        Assert.Equal(new[] { 1f, 1.5f, 2f, 2.5f }, fields.Select(f => arena.SpawnRadius[f.Entity]));
    }

    [Fact]
    public void AxisCascadesAlternateSides()
    {
        var arena = DeliveryArena.Create();
        arena.Run("{\"type\": \"cascade\", \"count\": 4, \"dir\": \"axis\", \"step\": 2, \"interval\": 0, \"effects\": [" + Mark + "]}", new Vector3(20, 10, 0), Vector2.UnitX);
        Assert.Equal(new[] { 20f, 22f, 18f, 24f, 16f }, arena.Spawned("field").Select(f => MathF.Round(f.Position.X, 3)));
    }

    [Fact]
    public void DelayedFieldsWaitThenStrikeOnceUntilReArmed()
    {
        var arena = DeliveryArena.Create();
        var body = arena.Body(20, 10);
        arena.Run("{\"type\": \"field\", \"radius\": 2, \"duration\": 4, \"interval\": 0.2, \"delay\": 0.5, \"hit_once\": true, \"rearm_on_exit\": true, \"pulse\": [{\"type\": \"event\", \"name\": \"t_hit\"}]}", new Vector3(20, 10, 0));
        Assert.Single(arena.Of<PlacementTelegraphed>());
        arena.Tick(9);
        Assert.Empty(arena.Struck());
        arena.Tick(2);
        Assert.Single(arena.Of<PlacementLanded>());
        Assert.Single(arena.Struck());
        arena.Tick(20);
        Assert.Single(arena.Struck());
        // Out and back in: struck again.
        var t = arena.Sim.World.Get<Transform>(body)!;
        t.Position = new Vector3(26, 10, 0);
        arena.Sim.Spatial.Update(body, t.Position);
        arena.Tick(2);
        t.Position = new Vector3(20, 10, 0);
        arena.Sim.Spatial.Update(body, t.Position);
        arena.Tick(6);
        Assert.Equal(2, arena.Struck().Count);
    }

    [Fact]
    public void ExposureHoldsPulsesBackUntilABodyHasStoodInside()
    {
        var arena = DeliveryArena.Create();
        arena.Body(20, 10);
        arena.Run("{\"type\": \"field\", \"radius\": 2, \"duration\": 3, \"interval\": 0.1, \"exposure\": 1, \"pulse\": [{\"type\": \"event\", \"name\": \"t_hit\"}]}", new Vector3(20, 10, 0));
        arena.Tick(18);
        Assert.Empty(arena.Struck());
        arena.Tick(6);
        Assert.NotEmpty(arena.Struck());
    }

    [Fact]
    public void DomainsHoldWhileInsideAndLeaveAfter()
    {
        var arena = DeliveryArena.Create();
        var body = arena.Body(20, 10);
        var stats = arena.Sim.World.Get<Stats>(body)!;
        var before = stats.Get("move.speed");
        arena.Run("{\"type\": \"field\", \"radius\": 2, \"duration\": 1, \"interval\": 0.5, \"domain\": [{\"attribute\": \"move.speed\", \"form\": \"more\", \"value\": -0.5}]}", new Vector3(20, 10, 0));
        arena.Tick(2);
        Assert.Equal(before * 0.5, stats.Get("move.speed"), 6);
        var t = arena.Sim.World.Get<Transform>(body)!;
        t.Position = new Vector3(26, 10, 0);
        arena.Sim.Spatial.Update(body, t.Position);
        arena.Tick(2);
        Assert.Equal(before, stats.Get("move.speed"), 6);
        t.Position = new Vector3(20, 10, 0);
        arena.Sim.Spatial.Update(body, t.Position);
        arena.Tick(2);
        Assert.Equal(before * 0.5, stats.Get("move.speed"), 6);
        arena.Tick(20);
        Assert.Equal(before, stats.Get("move.speed"), 6);
    }

    [Fact]
    public void ExclusiveFieldsReplaceTheCastersOlderOne()
    {
        var arena = DeliveryArena.Create();
        var field = "{\"type\": \"field\", \"radius\": 2, \"duration\": 10, \"interval\": 1, \"exclusive\": \"t_wall\"}";
        arena.Run(field, new Vector3(20, 10, 0));
        arena.Tick();
        arena.Run(field, new Vector3(24, 10, 0));
        arena.Tick();
        Assert.Single(arena.Sim.World.Query<Field>());
        Assert.Single(arena.Of<EntityDespawned>(), d => d.Pattern == "field");
    }

    [Fact]
    public void SegmentedFieldsLieAcrossTheWay()
    {
        var arena = DeliveryArena.Create();
        arena.Run("{\"type\": \"field\", \"radius\": 1, \"duration\": 1, \"interval\": 1, \"segments\": {\"count\": 3, \"spacing\": 2, \"axis\": \"across\"}}", new Vector3(20, 10, 0), Vector2.UnitX);
        Assert.Equal(new[] { 8f, 10f, 12f }, arena.Spawned("field").Select(f => MathF.Round(f.Position.Y, 3)));
        Assert.All(arena.Spawned("field"), f => Assert.Equal(20, f.Position.X, 3));
    }

    [Fact]
    public void PullingFieldsDrawBodiesInAndGrowAlongTheirCurve()
    {
        var arena = DeliveryArena.Create();
        var body = arena.Body(22.5f, 10);
        arena.Run("{\"type\": \"field\", \"radius\": 4, \"duration\": 2, \"interval\": 1, \"pull\": 3, \"radius_curve\": {\"from\": 0.5, \"to\": 1, \"curve\": \"linear\"}}", new Vector3(20, 10, 0));
        var field = arena.Spawned("field").Single().Entity;
        arena.Tick(20);
        Assert.True(arena.Sim.World.Get<Transform>(body)!.Position.X < 22, "the body was not drawn in");
        Assert.True(arena.Sim.World.Get<Transform>(field)!.Radius > 2.9f);
    }

    [Fact]
    public void FieldsAndPlacementsWhoseCasterIsGoneEndQuietly()
    {
        var arena = DeliveryArena.Create();
        var log = new ListSimLog();
        arena.Sim.Log = log;
        var body = arena.Body(20, 10);
        var stats = arena.Sim.World.Get<Stats>(body)!;
        var speed = stats.Get("move.speed");
        var field = arena.Sim.Service<WeaveCompiler>().Compile(new WeaveSpec { Preset = "umbra_field" });
        arena.Effect("{\"type\": \"field\", \"radius\": 2, \"duration\": 5, \"interval\": 0.2, \"domain\": [{\"attribute\": \"move.speed\", \"form\": \"more\", \"value\": -0.5}], \"pulse\": [{\"type\": \"damage\", \"amount\": \"weave.base_damage\"}], \"on_end\": [{\"type\": \"event\", \"name\": \"t_end\"}]}")
            .Execute(arena.Context(new Vector3(20, 10, 0), weave: field));
        arena.Effect("{\"type\": \"scatter\", \"count\": 3, \"radius\": 2, \"interval\": 0.3, \"telegraph\": 0.2, \"effects\": [{\"type\": \"area\", \"radius\": 1, \"center\": \"point\", \"effects\": [{\"type\": \"damage\", \"amount\": \"weave.base_damage\"}]}]}")
            .Execute(arena.Context(new Vector3(20, 10, 0), weave: field));
        arena.Tick(2);
        Assert.Equal(speed * 0.5, stats.Get("move.speed"), 6);
        var hits = arena.Of<DamageDealt>().Count;
        arena.Sim.World.Destroy(arena.Caster);
        arena.Tick(40);
        Assert.Equal(hits, arena.Of<DamageDealt>().Count);
        Assert.Empty(arena.Struck("t_end"));
        Assert.Equal(speed, stats.Get("move.speed"), 6);
        Assert.Empty(arena.Sim.World.Query<Field>());
        Assert.DoesNotContain(log.Lines, l => l.StartsWith("error"));
    }
}

/// <summary>Content naming shapes, styles and choices that do not exist fails validation.</summary>
public class DeliveryValidationTests
{
    [Fact]
    public void ValidationRejectsUnknownShapesStylesAndChoices()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hollowwake-wp3-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "forms.yaml"), string.Join("\n",
                "kind: forms",
                "entries:",
                "  - id: t_bad",
                "    mechanisms: [projectile]",
                "    effects:",
                "      - type: projectile",
                "        fire: spray",
                "        return: home",
                "        on_hit: [ { type: area, shape: blob }, { type: chain, pick: best } ]",
                "      - { type: field, radius_curve: { curve: wobble }, domain: [ { attribute: move.speed, value: 1 } ] }",
                "      - { type: move, kind: yeet }",
                "      - { type: cascade, dir: sideways }",
                ""));
            File.WriteAllText(Path.Combine(dir, "glyphs.yaml"), string.Join("\n",
                "kind: glyphs",
                "entries:",
                "  - id: t_bad_patch",
                "    patches: [ { type: area, set: { shape: blob } } ]",
                ""));
            ContentRegistry.Load(new[] { CoreContent.FindContentDir(), dir }, out var report);
            var errors = report.Errors.Select(e => $"{e.Id}: {e.Message}").ToList();
            Assert.Contains(errors, e => e.StartsWith("t_bad") && e.Contains("spray"));
            Assert.Contains(errors, e => e.StartsWith("t_bad") && e.Contains("home"));
            Assert.Contains(errors, e => e.StartsWith("t_bad") && e.Contains("blob"));
            Assert.Contains(errors, e => e.StartsWith("t_bad") && e.Contains("best"));
            Assert.Contains(errors, e => e.StartsWith("t_bad") && e.Contains("wobble"));
            Assert.Contains(errors, e => e.StartsWith("t_bad") && e.Contains("yeet"));
            Assert.Contains(errors, e => e.StartsWith("t_bad") && e.Contains("sideways"));
            Assert.Contains(errors, e => e.StartsWith("t_bad_patch") && e.Contains("blob"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void DeliveryProbesReadTheCompiledGraph()
    {
        var compiler = new WeaveCompiler(DeliveryArena.Registry, EffectRegistry.Shared);
        var glaive = compiler.Compile(new WeaveSpec { Preset = "stone_glaive" });
        Assert.True(glaive.Mechanisms.Has("returning"));
        Assert.False(glaive.Mechanisms.Has("homing"));
        var seeking = compiler.Compile(new WeaveSpec { Preset = "seeking_bolt" });
        Assert.True(seeking.Mechanisms.Has("homing") && seeking.Mechanisms.Has("trajectory"));
        Assert.True(compiler.Compile(new WeaveSpec { Preset = "storm_call" }).Mechanisms.Has("storm"));
        Assert.True(compiler.Compile(new WeaveSpec { Preset = "storm_call" }).Mechanisms.Has("telegraph"));
        Assert.True(compiler.Compile(new WeaveSpec { Preset = "stone_ripple" }).Mechanisms.Has("cascade"));
        Assert.True(compiler.Compile(new WeaveSpec { Preset = "shield_bash" }).Mechanisms.Has("shove"));
        Assert.True(compiler.Compile(new WeaveSpec { Preset = "ember_volley" }).HasMechanism("fire:volley"));
        Assert.True(compiler.Compile(new WeaveSpec { Form = "cleave" }).HasMechanism("shape:sector"));
        // A glyph that needs a sector lifts on a cleave and stays out of a nova.
        Assert.True(compiler.Compile(new WeaveSpec { Form = "cleave", Glyphs = { GlyphRef.Parse("sweeping") } }).Fits);
        Assert.False(compiler.Compile(new WeaveSpec { Form = "nova", Threads = { "storm" }, Glyphs = { GlyphRef.Parse("sweeping") } }, GlyphFit.Dormant).Fits);
        // The returning glyph turns a bolt's flight around; a cascading glyph wraps a nova in a cascade.
        var returning = compiler.Compile(new WeaveSpec { Form = "bolt", Threads = { "ember" }, Glyphs = { GlyphRef.Parse("returning") } });
        Assert.Equal("origin", JsonNum.StringOf(returning.Specs[0].Params["return"]));
        var cascading = compiler.Compile(new WeaveSpec { Form = "nova", Threads = { "storm" }, Glyphs = { GlyphRef.Parse("cascading") } });
        Assert.Equal("cascade", cascading.Specs[0].Type);
    }
}

/// <summary>The package's acceptance: a seeded fight with forking, returning and homing weaves splits, returns and repeats.</summary>
public class DeliveryAcceptanceTests
{
    private static List<string> Fight(string seed)
    {
        var sim = ZoneBuilder.CreateArena(DeliveryArena.Registry, Rng.Hash64(seed));
        var factory = sim.Service<PatternFactory>();
        factory.Spawn(sim, "loosed_skirmisher", new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
        factory.Spawn(sim, "boar", new Vector3(14, 10, 0), new SpawnOptions { Faction = "blue" });
        var trace = new List<string>();
        sim.Events.SubscribeAll(e => trace.Add(e switch
        {
            DamageDealt d => $"{d.Tick} hit {d.Source}->{d.Target} {d.Total:0.###} depth {d.Depth}",
            _ => e.ToString(),
        }));
        sim.RunSeconds(12);
        return trace;
    }

    [Fact]
    public void ASeededFightSplitsReturnsAndRepeatsExactly()
    {
        var first = Fight("delivery");
        Assert.Contains(first, l => l.StartsWith("ProjectileForked"));
        Assert.Contains(first, l => l.StartsWith("ProjectileTurned"));
        Assert.Contains(first, l => l.StartsWith("ProjectileReturned"));
        // A hit lands after a glaive turns and before it is home: a return hit.
        var turned = first.FindIndex(l => l.StartsWith("ProjectileTurned"));
        var home = first.FindIndex(turned, l => l.StartsWith("ProjectileReturned"));
        Assert.Contains(first.Skip(turned).Take(home - turned), l => l.Contains(" hit "));
        Assert.Equal(first, Fight("delivery"));
    }
}
