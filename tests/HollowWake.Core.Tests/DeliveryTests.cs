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

    public EffectContext Context(Vector3 point, Vector2? direction = null, Entity? target = null, CompiledWeave? weave = null, ulong rng = 77, Entity? source = null) => new()
    {
        Sim = Sim,
        Source = source ?? Caster,
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
        // The unmodified sweep now touches body edges. Put this body beyond that graze,
        // but inside the wider glyph's cone, so the test still proves angle scaling.
        var side = arena.Body(11.2f, 12.9f);
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
    public void SalvosAndGuidedFlightsFollowTheLiveAim()
    {
        var arena = DeliveryArena.Create();
        var book = arena.Sim.World.GetOrAdd<WeaveBook>(arena.Caster);
        // An aim sent before the cast is already in it, so the salvo keeps the aim it was made with.
        book.AimPoint = new Vector3(10, 30, 0);
        book.AimDirection = Vector2.UnitY;
        book.AimAt = arena.Sim.Clock.Time - 1;
        arena.Run("{\"type\": \"projectile\", \"fire\": \"salvo\", \"count\": 3, \"salvo_interval\": 0.2, \"speed\": 10, \"range\": 30, \"pierce\": 0}", new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Tick(6);
        // An aim sent while it fires turns the shots that follow toward it.
        book.AimAt = arena.Sim.Clock.Time;
        arena.Tick(4);
        Assert.Equal(new[] { 0f, 0f, 90f }, Yaws(arena).Select(y => MathF.Round(y * 180 / MathF.PI, 3)));

        var guided = DeliveryArena.Create();
        var guidedBook = guided.Sim.World.GetOrAdd<WeaveBook>(guided.Caster);
        guided.Run("{\"type\": \"projectile\", \"speed\": 10, \"range\": 30, \"pierce\": 0, \"guide\": 720}", new Vector3(20, 10, 0), Vector2.UnitX);
        var flight = guided.Spawned("projectile").Single().Entity;
        guided.Tick(4);
        var ft = guided.Sim.World.Get<Transform>(flight)!;
        Assert.Equal(10, ft.Position.Y, 3);
        guidedBook.AimPoint = new Vector3(ft.Position.X, 30, 0);
        guidedBook.AimAt = guided.Sim.Clock.Time;
        guided.Tick(6);
        Assert.True(ft.Position.Y > 12, $"a guided flight turns toward the live aim, but stands at {ft.Position}");
    }

    [Fact]
    public void WhatAFlightRunsKeepsTheSlotItWasCastFrom()
    {
        var arena = DeliveryArena.Create();
        var book = arena.Sim.World.GetOrAdd<WeaveBook>(arena.Caster);
        book.Slots.Clear();
        book.ReadyAt.Clear();
        var bolt = arena.Weave("bolt");
        var nova = arena.Weave("nova");
        var host = book.Add(bolt.Spec);
        host.Compiled = bolt;
        var other = book.Add(nova.Spec);
        other.Compiled = nova;
        var now = arena.Sim.Clock.Time;
        book.ReadyAt[host.Key] = now + 10;
        book.ReadyAt[other.Key] = now + 10;
        var flight = arena.Effect("{\"type\": \"projectile\", \"speed\": 10, \"range\": 2, \"pierce\": 0, \"on_end\": [{\"type\": \"cooldown\", \"seconds\": 4}]}");
        flight.Execute(new EffectContext { Sim = arena.Sim, Source = arena.Caster, Point = new Vector3(20, 10, 0), Direction = Vector2.UnitX, Rng = new Rng(3), Slot = host, Host = bolt });
        arena.Tick(10);
        // The flight's end shaves every cooldown but its own weave's, which it still knows as its host.
        Assert.Equal(now + 10, book.ReadyAt[host.Key], 6);
        Assert.Equal(now + 6, book.ReadyAt[other.Key], 6);
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
    public void FlightsAndFieldsAllocateNothingPerTickInSteadyState()
    {
        var arena = DeliveryArena.Create();
        arena.Body(30, 40);
        arena.Run("{\"type\": \"projectile\", \"fire\": \"ring\", \"count\": 12, \"speed\": 2, \"range\": 30, \"pierce\": 0, \"homing\": 30, \"wave\": 1, \"spin\": 90}", new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Run("{\"type\": \"field\", \"radius\": 3, \"duration\": 30, \"interval\": 0.2, \"pulse\": [{\"type\": \"event\", \"name\": \"t_hit\"}]}", new Vector3(40, 40, 0));
        arena.Sim.Profile = true;
        arena.Tick(10);
        var projectiles = arena.Sim.Costs["projectiles"];
        var fields = arena.Sim.Costs["fields"];
        var (p0, f0, t0) = (projectiles.TotalAllocatedBytes, fields.TotalAllocatedBytes, projectiles.Ticks);
        arena.Tick(40);
        // Only the component query's own enumerator is allocated each tick.
        Assert.True((projectiles.TotalAllocatedBytes - p0) / (double)(projectiles.Ticks - t0) < 200, $"{(projectiles.TotalAllocatedBytes - p0) / (projectiles.Ticks - t0)} bytes a tick for flights");
        Assert.True((fields.TotalAllocatedBytes - f0) / (double)(fields.Ticks - t0) < 200, $"{(fields.TotalAllocatedBytes - f0) / (fields.Ticks - t0)} bytes a tick for fields");
        Assert.Equal(12, arena.Sim.World.CountOf<Projectile>());
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

/// <summary>Walls stop deliveries unless they phase.</summary>
public class OcclusionTests
{
    private const string Hit = "\"on_hit\": [{\"type\": \"event\", \"name\": \"t_hit\"}]";

    /// <summary>A wall of structure cells that cannot be walked, across x from y0 to y1.</summary>
    private static void Wall(Simulation sim, int x, int y0, int y1)
    {
        for (var y = y0; y <= y1; y++)
        {
            var (chunk, i) = sim.Terrain.Locate(new World.CellCoord(x, y));
            chunk.Flags[i] = World.CellFlags.Structure | World.CellFlags.Blocked;
        }
    }

    [Fact]
    public void LinesOfSightStopAtWallsAndRisingGround()
    {
        var arena = DeliveryArena.Create();
        Wall(arena.Sim, 13, 8, 12);
        var terrain = arena.Sim.Terrain;
        Assert.True(World.LineOfSight.Clear(terrain, new Vector3(10, 10, 1), new Vector3(12.5f, 10, 1)));
        Assert.False(World.LineOfSight.Clear(terrain, new Vector3(10, 10, 1), new Vector3(16, 10, 1)));
        Assert.True(World.LineOfSight.Clear(terrain, new Vector3(10, 14, 1), new Vector3(16, 14, 1)));
        Assert.Equal(new World.CellCoord(13, 10), World.LineOfSight.Blocker(terrain, new Vector3(10, 10, 1), new Vector3(16, 11, 1), out var t));
        Assert.InRange(t, 0.4f, 0.6f);
        // A hill between the ends blocks too.
        var (chunk, i) = terrain.Locate(new World.CellCoord(20, 20));
        chunk.Height[i] = 5;
        Assert.False(World.LineOfSight.Clear(terrain, new Vector3(18.5f, 20.5f, 1), new Vector3(22.5f, 20.5f, 1)));
    }

    [Fact]
    public void WallsStopFlightsAndAreasUnlessTheyPhase()
    {
        var arena = DeliveryArena.Create();
        Wall(arena.Sim, 13, 6, 14);
        var behind = arena.Body(15, 10);
        arena.Run("{\"type\": \"projectile\", \"speed\": 12, \"range\": 10, \"pierce\": 0, " + Hit + ", \"on_end\": [{\"type\": \"event\", \"name\": \"t_end\"}]}", new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Tick(20);
        Assert.Empty(arena.Struck());
        Assert.Single(arena.Struck("t_end"));
        arena.Events.Clear();
        arena.Run("{\"type\": \"projectile\", \"speed\": 12, \"range\": 10, \"pierce\": 0, \"occlusion\": \"free\", " + Hit + "}", new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Tick(20);
        Assert.Equal(new[] { behind }, arena.Struck());
        arena.Events.Clear();
        var nova = "{\"type\": \"area\", \"radius\": 7, \"center\": \"source\", \"effects\": [{\"type\": \"event\", \"name\": \"t_hit\"}]";
        arena.Run(nova + "}", new Vector3(10, 10, 0));
        Assert.Empty(arena.Struck());
        arena.Run(nova + ", \"occlusion\": \"free\"}", new Vector3(10, 10, 0));
        Assert.Equal(new[] { behind }, arena.Struck());
    }

    [Fact]
    public void ABodyPressedAgainstAWallIsNotStruckThroughIt()
    {
        var arena = DeliveryArena.Create();
        Wall(arena.Sim, 13, 6, 14);
        arena.Body(14.2f, 10);
        // Its fifth step lands at 13.4, inside the wall and within reach of the body behind it.
        arena.Run("{\"type\": \"projectile\", \"speed\": 13.6, \"range\": 10, \"pierce\": 0, " + Hit + "}", new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Tick(20);
        Assert.Empty(arena.Struck());
    }

    [Fact]
    public void BouncingFlightsGlanceOffWalls()
    {
        var arena = DeliveryArena.Create();
        Wall(arena.Sim, 13, 6, 14);
        arena.Run("{\"type\": \"projectile\", \"speed\": 10, \"range\": 10, \"pierce\": 0, \"bounce\": 1}", new Vector3(20, 10, 0), Vector2.UnitX);
        var e = arena.Spawned("projectile").Single().Entity;
        arena.Tick(8);
        Assert.Single(arena.Of<ProjectileBounced>());
        var flight = arena.Sim.World.Get<Projectile>(e)!;
        Assert.True(MathF.Cos(flight.Heading) < -0.9f, $"heading {flight.Heading} after the bounce");
    }
}

/// <summary>Auras, tethers, aim transforms and detonations.</summary>
public class AuraTetherAimDetonateTests
{
    [Fact]
    public void AurasFollowTheirSourceLendTheirDomainAndToggleOff()
    {
        var arena = DeliveryArena.Create();
        var ally = arena.Body(11.5f, 10, faction: "red");
        var foe = arena.Body(12, 11);
        var allyStats = arena.Sim.World.Get<Stats>(ally)!;
        var before = allyStats.Get("move.speed");
        var aura = "{\"type\": \"aura\", \"radius\": 3, \"toggle\": true, \"interval\": 0.25, \"domain\": [{\"attribute\": \"move.speed\", \"form\": \"more\", \"value\": 0.5}], \"pulse\": [{\"type\": \"event\", \"name\": \"t_hit\"}]}";
        arena.Run(aura, new Vector3(10, 10, 0));
        arena.Tick(2);
        Assert.Equal(before * 1.5, allyStats.Get("move.speed"), 6);
        Assert.Contains(foe, arena.Struck());
        Assert.DoesNotContain(ally, arena.Struck());
        // It goes where its source goes.
        var ct = arena.Sim.World.Get<Transform>(arena.Caster)!;
        ct.Position = new Vector3(30, 10, 0);
        arena.Sim.Spatial.Update(arena.Caster, ct.Position);
        arena.Tick(2);
        Assert.Equal(before, allyStats.Get("move.speed"), 6);
        Assert.Equal(30, arena.Sim.World.Query<Transform, Field>().Single().C1.Position.X, 3);
        // Casting it again turns it off.
        arena.Run(aura, new Vector3(30, 10, 0));
        arena.Tick();
        Assert.Empty(arena.Sim.World.Query<Field>());
    }

    [Fact]
    public void AnAuraEndsWhenItsBearerFalls()
    {
        var arena = DeliveryArena.Create();
        var ally = arena.Body(11.5f, 10, faction: "red");
        var allyStats = arena.Sim.World.Get<Stats>(ally)!;
        var before = allyStats.Get("move.speed");
        arena.Run("{\"type\": \"aura\", \"radius\": 3, \"interval\": 0.25, \"domain\": [{\"attribute\": \"move.speed\", \"form\": \"more\", \"value\": 0.5}], \"on_end\": [{\"type\": \"event\", \"name\": \"t_end\"}]}", new Vector3(10, 10, 0));
        arena.Tick(2);
        Assert.Equal(before * 1.5, allyStats.Get("move.speed"), 6);
        arena.Sim.World.Add(arena.Caster, new Dead { DiedAt = arena.Sim.Clock.Time });
        arena.Tick(2);
        Assert.Equal(before, allyStats.Get("move.speed"), 6);
        Assert.Single(arena.Struck("t_end"));
        Assert.Empty(arena.Sim.World.Query<Field>());
    }

    [Fact]
    public void TethersStrikeWhatCrossesThemAndSnapWhenStretched()
    {
        var arena = DeliveryArena.Create();
        var anchor = arena.Body(18, 10);
        var crossing = arena.Body(14, 10.4f);
        var clear = arena.Body(14, 12.5f);
        arena.Run("{\"type\": \"tether\", \"link\": \"target\", \"width\": 1, \"interval\": 0.5, \"duration\": 3, \"max_length\": 12, \"pulse\": [{\"type\": \"event\", \"name\": \"t_hit\"}], \"on_end\": [{\"type\": \"event\", \"name\": \"t_snap\"}]}", new Vector3(18, 10, 0), target: anchor);
        arena.Tick(2);
        Assert.Equal(new[] { crossing }, arena.Struck());
        Assert.Single(arena.Spawned("tether"));
        arena.Tick(10);
        Assert.Equal(2, arena.Struck().Count);
        var at = arena.Sim.World.Get<Transform>(anchor)!;
        at.Position = new Vector3(30, 10, 0);
        arena.Sim.Spatial.Update(anchor, at.Position);
        arena.Tick(2);
        Assert.Single(arena.Struck("t_snap"));
        Assert.Empty(arena.Sim.World.Query<Tether>());
        _ = clear;
    }

    [Fact]
    public void AimTurnsTheBearingBySequenceAndBySpreadForASeed()
    {
        var arena = DeliveryArena.Create();
        var aim = arena.Effect("{\"type\": \"aim\", \"sequence\": [-30, 0, 30], \"effects\": [{\"type\": \"projectile\", \"speed\": 10, \"range\": 8, \"pierce\": 0}]}");
        for (var i = 0; i < 4; i++)
        {
            aim.Execute(arena.Context(new Vector3(20, 10, 0), Vector2.UnitX));
        }
        var yaws = arena.Spawned("projectile").Select(s => MathF.Round(arena.Sim.World.Get<Transform>(s.Entity)!.Yaw * 180 / MathF.PI)).ToList();
        Assert.Equal(new[] { -30f, 0f, 30f, -30f }, yaws);
        List<float> Spread(ulong seed)
        {
            var other = DeliveryArena.Create();
            var effect = other.Effect("{\"type\": \"aim\", \"spread\": 40, \"effects\": [{\"type\": \"projectile\", \"speed\": 10, \"range\": 8, \"pierce\": 0}]}");
            for (var i = 0; i < 5; i++)
            {
                effect.Execute(other.Context(new Vector3(20, 10, 0), Vector2.UnitX, rng: seed + (ulong)i));
            }
            return other.Spawned("projectile").Select(s => other.Sim.World.Get<Transform>(s.Entity)!.Yaw).ToList();
        }
        var spread = Spread(9);
        Assert.Equal(spread, Spread(9));
        Assert.All(spread, y => Assert.InRange(y * 180 / MathF.PI, -20.01f, 20.01f));
        Assert.True(spread.Distinct().Count() > 1);
    }

    [Fact]
    public void EachBodyKeepsItsOwnPlaceInAnAimSequence()
    {
        var arena = DeliveryArena.Create();
        var other = arena.Body(10, 14, faction: "red");
        var aim = arena.Effect("{\"type\": \"aim\", \"sequence\": [-30, 0, 30], \"effects\": [{\"type\": \"projectile\", \"speed\": 10, \"range\": 8, \"pierce\": 0}]}");
        var yaws = new Dictionary<Entity, List<float>> { [arena.Caster] = new(), [other] = new() };
        foreach (var source in new[] { arena.Caster, other, arena.Caster, other, other })
        {
            var before = arena.Spawned("projectile").Count;
            aim.Execute(arena.Context(new Vector3(20, 12, 0), Vector2.UnitX, source: source));
            var shot = arena.Spawned("projectile")[before].Entity;
            yaws[source].Add(MathF.Round(arena.Sim.World.Get<Transform>(shot)!.Yaw * 180 / MathF.PI));
        }
        Assert.Equal(new[] { -30f, 0f }, yaws[arena.Caster]);
        Assert.Equal(new[] { -30f, 0f, 30f }, yaws[other]);
        // The place is kept on the body, so it goes when the body does.
        arena.Sim.World.Destroy(other);
        Assert.Single(arena.Sim.World.Query<AimCursors>());
    }

    [Fact]
    public void DetonationsSetOffWhatTheSourceOwns()
    {
        var arena = DeliveryArena.Create();
        arena.Run("{\"type\": \"projectile\", \"speed\": 2, \"range\": 20, \"pierce\": 0, \"on_end\": [{\"type\": \"event\", \"name\": \"t_end\"}]}", new Vector3(20, 10, 0), Vector2.UnitX);
        arena.Run("{\"type\": \"projectile\", \"speed\": 2, \"range\": 20, \"pierce\": 0, \"on_end\": [{\"type\": \"event\", \"name\": \"t_end\"}]}", new Vector3(10, 20, 0), Vector2.UnitY);
        arena.Run("{\"type\": \"field\", \"radius\": 1, \"duration\": 10, \"interval\": 1, \"on_end\": [{\"type\": \"event\", \"name\": \"t_field_end\"}]}", new Vector3(14, 14, 0));
        arena.Tick(10);
        // The nearest projectile to the point first.
        arena.Run("{\"type\": \"detonate\", \"tags\": [\"projectile\"], \"pick\": \"nearest\", \"effects\": [{\"type\": \"event\", \"name\": \"t_boom\"}]}", new Vector3(12, 10, 0));
        Assert.Single(arena.Struck("t_end"));
        Assert.Single(arena.Struck("t_boom"));
        arena.Run("{\"type\": \"detonate\", \"effects\": [{\"type\": \"event\", \"name\": \"t_boom\"}]}", new Vector3(12, 10, 0));
        arena.Tick();
        Assert.Equal(2, arena.Struck("t_end").Count);
        Assert.Single(arena.Struck("t_field_end"));
        Assert.Equal(3, arena.Struck("t_boom").Count);
        Assert.Empty(arena.Sim.World.Query<Projectile>());
        Assert.Empty(arena.Sim.World.Query<Field>());
    }
}

/// <summary>The delivery forms and glyphs in content, cast through the runtime.</summary>
public class DeliveryContentTests
{
    private static bool Cast(DeliveryArena arena, string preset, Entity target, Vector3 point, Vector2? direction = null)
    {
        var book = arena.Sim.World.Get<WeaveBook>(arena.Caster)!;
        var slot = book.Slots.FirstOrDefault(s => s.Spec.Preset == preset) ?? book.Add(new WeaveSpec { Preset = preset });
        book.ReadyAt.Clear();
        return arena.Sim.Service<WeaveRuntime>().TryBegin(arena.Sim, arena.Caster, slot, target, point, direction ?? Vector2.Zero, out _);
    }

    private static void Full(DeliveryArena arena)
    {
        var stats = arena.Sim.World.Get<Stats>(arena.Caster)!;
        foreach (var r in new[] { "mana", "stamina" })
        {
            stats.SetResource(r, stats.ResourceMax(r));
        }
    }

    [Fact]
    public void LeapSlamsLandAndThrowFoesBack()
    {
        var arena = DeliveryArena.Create();
        var foe = arena.Body(16.5f, 10);
        Assert.True(Cast(arena, "stone_leap", Entity.None, new Vector3(16, 10, 0)));
        arena.Tick(30);
        Assert.Equal(16, arena.Sim.World.Get<Transform>(arena.Caster)!.Position.X, 1);
        Assert.Contains(arena.Of<DamageDealt>(), d => d.Target == foe);
        Assert.True(arena.Sim.World.Get<Transform>(foe)!.Position.X > 17, "the landing did not throw the foe back");
        Assert.Single(arena.Spawned("telegraph"));
    }

    [Fact]
    public void ShadowstepArrivesBehindItsTarget()
    {
        var arena = DeliveryArena.Create();
        var foe = arena.Body(15, 10);
        arena.Sim.World.Get<Transform>(foe)!.Yaw = MathF.PI;
        Assert.True(Cast(arena, "umbra_step", foe, new Vector3(15, 10, 0)));
        Assert.True(arena.Sim.World.Get<Transform>(arena.Caster)!.Position.X > 15.5f);
    }

    [Fact]
    public void PresenceTogglesOnAndOff()
    {
        var arena = DeliveryArena.Create();
        var foe = arena.Body(12, 10);
        Assert.True(Cast(arena, "lumen_presence", arena.Caster, new Vector3(10, 10, 0)));
        arena.Tick(25);
        Assert.Single(arena.Sim.World.Query<Field>());
        Assert.Contains(arena.Of<DamageDealt>(), d => d.Target == foe);
        Full(arena);
        Assert.True(Cast(arena, "lumen_presence", arena.Caster, new Vector3(10, 10, 0)));
        arena.Tick();
        Assert.Empty(arena.Sim.World.Query<Field>());
    }

    [Fact]
    public void BindingBurnsWhatCrossesItsLine()
    {
        var arena = DeliveryArena.Create();
        var foe = arena.Body(18, 10);
        var crossing = arena.Body(14, 10.3f);
        Assert.True(Cast(arena, "void_binding", foe, new Vector3(18, 10, 0)));
        arena.Tick(40);
        Assert.Contains(arena.Of<DamageDealt>(), d => d.Target == crossing);
        Assert.DoesNotContain(arena.Of<DamageDealt>(), d => d.Target == foe);
    }

    [Fact]
    public void GlyphsGraftAimsAndPhasing()
    {
        var compiler = new WeaveCompiler(DeliveryArena.Registry, EffectRegistry.Shared);
        var scatter = compiler.Compile(new WeaveSpec { Form = "bolt", Threads = { "ember" }, Glyphs = { GlyphRef.Parse("scattershot") } });
        Assert.Equal("aim", scatter.Specs[0].Type);
        Assert.True(compiler.Compile(new WeaveSpec { Preset = "stone_leap" }).Mechanisms.Has("leap"));
        Assert.True(compiler.Compile(new WeaveSpec { Preset = "void_binding" }).Mechanisms.Has("tether"));
        Assert.True(compiler.Compile(new WeaveSpec { Preset = "quick_dash" }).Mechanisms.Has("dash"));
        var arena = DeliveryArena.Create();
        var (chunk, i) = arena.Sim.Terrain.Locate(new World.CellCoord(13, 10));
        chunk.Flags[i] = World.CellFlags.Structure | World.CellFlags.Blocked;
        var behind = arena.Body(15, 10);
        var ethereal = arena.Sim.Service<WeaveCompiler>().Compile(new WeaveSpec { Form = "bolt", Threads = { "ember" }, Glyphs = { GlyphRef.Parse("ethereal") } });
        foreach (var effect in ethereal.Effects)
        {
            effect.Execute(arena.Context(new Vector3(15, 10, 0), target: behind, weave: ethereal));
        }
        arena.Tick(20);
        Assert.Contains(arena.Of<DamageDealt>(), d => d.Target == behind);
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
