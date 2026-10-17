using System.Globalization;
using System.Numerics;
using System.Text.Json;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;
using HollowWake.Core.Tags;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>
/// Two bodies standing still in an arena, an effect compiler and a log of every event: the bench the status,
/// defense, proc and charge tests share.
/// </summary>
internal sealed class CombatBench
{
    public required Simulation Sim;
    public required Entity Attacker;
    public required Entity Victim;
    public readonly List<GameEvent> Events = new();

    public static CombatBench Create(ContentRegistry? registry = null, string attacker = "hob", string victim = "hob", float distance = 3, ulong seed = 11)
    {
        var sim = ZoneBuilder.CreateArena(registry ?? CoreContent.Registry, seed);
        var factory = sim.Service<PatternFactory>();
        var a = factory.Spawn(sim, attacker, new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
        var v = factory.Spawn(sim, victim, new Vector3(10 + distance, 10, 0), new SpawnOptions { Faction = "blue" });
        sim.World.Remove<Behavior>(a);
        sim.World.Remove<Behavior>(v);
        var bench = new CombatBench { Sim = sim, Attacker = a, Victim = v };
        sim.Events.SubscribeAll(bench.Events.Add);
        return bench;
    }

    public Stats AttackerStats => Sim.World.Get<Stats>(Attacker)!;
    public Stats VictimStats => Sim.World.Get<Stats>(Victim)!;
    public StatusService Statuses => Sim.Service<StatusService>();

    public static TypedSpec Spec(string json) => JsonSerializer.Deserialize<TypedSpec>(json, ContentJson.Options)!;

    public CompiledEffect Effect(string json)
    {
        var effects = Sim.Service<EffectRegistry>();
        return effects.Compile(Spec(json), new EffectCompileContext { Content = Sim.Content, Registry = effects, Owner = "test", Compiler = Sim.Service<WeaveCompiler>() });
    }

    public EffectContext Context(Entity source, Entity target, string tags = "", int depth = 0, ulong rng = 5) => new()
    {
        Sim = Sim,
        Source = source,
        Target = target,
        Tags = TagSet.Parse(tags),
        Depth = depth,
        Rng = new Rng(rng),
        Point = Sim.World.Get<Transform>(target)?.Position ?? default,
    };

    /// <summary>Runs an effect from the attacker at the victim (or the given bodies).</summary>
    public EffectContext Run(string json, Entity? source = null, Entity? target = null, string tags = "", int depth = 0)
    {
        var ctx = Context(source ?? Attacker, target ?? Victim, tags, depth);
        Effect(json).Execute(ctx);
        return ctx;
    }

    /// <summary>A damage effect of the attacker on the victim; returns the hit it published.</summary>
    public DamageDealt Hit(double amount = 10, string thread = "stone", string tags = "", bool canCrit = false, Entity? source = null, Entity? target = null)
    {
        var before = Events.OfType<DamageDealt>().Count();
        var json = $"{{\"type\": \"damage\", \"amount\": {amount.ToString(CultureInfo.InvariantCulture)}, \"threads\": [\"{thread}\"], \"can_crit\": {(canCrit ? "true" : "false")}}}";
        Run(json, source, target, tags);
        var hits = Events.OfType<DamageDealt>().ToList();
        Assert.Equal(before + 1, hits.Count);
        return hits[^1];
    }

    public StatusInstance? Apply(Entity target, string status, double magnitude = 1, double? duration = null, int stacks = 1, Entity? source = null) =>
        Statuses.Apply(Sim, target, status, source ?? Attacker, new StatusApplication { Magnitude = magnitude, Duration = duration, Stacks = stacks });

    public void Modify(Entity who, string attribute, double value, ModifierForm form = ModifierForm.Flat, string scope = "") =>
        Sim.World.Get<Stats>(who)!.Attributes.Add(new Modifier { Attribute = attribute, Form = form, Value = value, Scope = TagSet.Parse(scope), Source = "test" });

    public void Run(double seconds) => Sim.RunSeconds(seconds);
}

/// <summary>Statuses: forbidden tags, temporary modifiers, and the status mechanics of the parity rows.</summary>
public class StatusTests
{
    [Fact]
    public void ForbiddenTagsStopCasts()
    {
        var bench = CombatBench.Create(attacker: "loosed_kindler", victim: "hob", distance: 6);
        var sim = bench.Sim;
        var book = sim.World.Get<WeaveBook>(bench.Attacker)!;
        var bolt = book.Add(new WeaveSpec { Form = "bolt", Threads = { "ember" } });
        var strike = book.Add(new WeaveSpec { Form = "strike", Threads = { "stone" } });
        var runtime = sim.Service<WeaveRuntime>();
        var victimAt = sim.World.Get<Transform>(bench.Victim)!.Position;

        bench.Apply(bench.Attacker, "silenced", duration: 3);
        Assert.True(bench.AttackerStats.HasFlag("forbid:spell"));
        Assert.False(runtime.TryBegin(sim, bench.Attacker, bolt, bench.Victim, victimAt, Vector2.Zero, out var reason));
        Assert.Equal("forbidden:spell", reason);
        // Silence forbids spells only: an attack is refused for range, not for silence.
        runtime.TryBegin(sim, bench.Attacker, strike, bench.Victim, victimAt, Vector2.Zero, out reason);
        Assert.NotEqual("forbidden:attack", reason);
        Assert.DoesNotContain("forbidden", reason);

        bench.Statuses.Remove(sim, bench.Attacker, "silenced", TagSet.Empty);
        Assert.False(bench.AttackerStats.HasFlag("forbid:spell"));
        Assert.True(runtime.TryBegin(sim, bench.Attacker, bolt, bench.Victim, victimAt, Vector2.Zero, out reason), reason);
    }

    [Fact]
    public void ForbidFlagsEndWithTheStatus()
    {
        var bench = CombatBench.Create();
        bench.Apply(bench.Victim, "rooted", duration: 0.5);
        Assert.True(bench.VictimStats.HasFlag("forbid:movement"));
        bench.Run(1);
        Assert.False(bench.VictimStats.HasFlag("forbid:movement"));
        Assert.False(bench.VictimStats.HasFlag("rooted"));
    }

    [Fact]
    public void RefreshedTemporaryModifiersLastTheirFullTime()
    {
        var bench = CombatBench.Create();
        var stats = bench.VictimStats;
        var before = stats.Get("move.speed");
        const string modify = "{\"type\": \"modify\", \"attribute\": \"move.speed\", \"form\": \"flat\", \"value\": 1, \"duration\": 2, \"key\": \"t_haste\"}";
        bench.Run(modify);
        Assert.Equal(before + 1, stats.Get("move.speed"), 9);
        bench.Run(1.5);
        // Refreshed with half a second left on the first timer: the first timer must not end it.
        bench.Run(modify);
        bench.Run(1.0);
        Assert.Equal(before + 1, stats.Get("move.speed"), 9);
        bench.Run(1.2);
        Assert.Equal(before, stats.Get("move.speed"), 9);
    }
}
