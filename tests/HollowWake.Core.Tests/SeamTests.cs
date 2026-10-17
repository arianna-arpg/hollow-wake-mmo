using System.Numerics;
using HollowWake.Core.Characters;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Expressions;
using HollowWake.Core.Intents;
using HollowWake.Core.Items;
using HollowWake.Core.Maths;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Systems;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>The registries the work packages extend: triggers, intents, content kinds, attribute families, charges, curves, systems.</summary>
public class SeamTests
{
    private static (Simulation Sim, Entity A, Entity B) Duel(string a = "wolf", string b = "hob", ulong seed = 7)
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, seed);
        var factory = sim.Service<PatternFactory>();
        var ea = factory.Spawn(sim, a, new Vector3(10, 10, 0), new SpawnOptions { Faction = "red" });
        var eb = factory.Spawn(sim, b, new Vector3(12, 10, 0), new SpawnOptions { Faction = "blue" });
        return (sim, ea, eb);
    }

    [Fact]
    public void TriggersReachTheirOwnersFromTheEventBus()
    {
        var (sim, wolf, hob) = Duel();
        var hub = sim.Service<TriggerHub>();
        var wolfHits = new List<TriggerEvent>();
        var hobHurts = 0;
        var anyHits = 0;
        var kills = new List<Entity>();
        hub.Subscribe("hit", wolf, (in TriggerEvent e) => wolfHits.Add(e));
        hub.Subscribe(TriggerKinds.Hurt, hob, (in TriggerEvent e) => hobHurts++);
        hub.Subscribe(TriggerKinds.Hit, Entity.None, (in TriggerEvent e) => anyHits++);
        hub.Subscribe(TriggerKinds.Kill, Entity.None, (in TriggerEvent e) => kills.Add(e.Owner));
        var damage = new List<DamageDealt>();
        sim.Events.Subscribe<DamageDealt>(damage.Add);
        sim.RunSeconds(40);

        var wolfLanded = damage.Count(d => d.Source == wolf && !d.Evaded && d.Total > 0);
        Assert.True(wolfLanded > 0);
        Assert.Equal(wolfLanded, wolfHits.Count);
        Assert.All(wolfHits, e => Assert.Equal(wolf, e.Owner));
        Assert.All(wolfHits, e => Assert.Equal(TriggerKinds.Hit, e.Kind));
        Assert.Equal(damage.Count(d => d.Target == hob && !d.Evaded && d.Total > 0), hobHurts);
        Assert.Equal(damage.Count(d => !d.Evaded && d.Total > 0), anyHits);
        Assert.Single(kills);
    }

    [Fact]
    public void TriggerHandlersMayUnsubscribeWhileBeingCalled()
    {
        var (sim, wolf, _) = Duel();
        var hub = sim.Service<TriggerHub>();
        var calls = 0;
        var handle = 0;
        handle = hub.Subscribe(TriggerKinds.Hit, wolf, (in TriggerEvent e) =>
        {
            calls++;
            hub.Unsubscribe(handle);
        });
        sim.RunSeconds(40);
        Assert.Equal(1, calls);
        Assert.False(hub.HasSubscribers(TriggerKinds.Hit));
        Assert.True(TriggerKinds.TryGet("death_near", out _));
        Assert.True(TriggerKinds.TryGet("interval", out _));
    }

    [Fact]
    public void IntentHandlersDispatchByTypeAndRefuseWhatNoneTakes()
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 1);
        var characters = sim.Service<CharacterFactory>();
        var sheet = characters.Create(sim, "c-seam", "a-seam", "Seam", "holdfast", sim.Service<ItemGenerator>());
        sheet.Position = new Vector3(5, 5, 0);
        var e = characters.Materialize(sim, sheet);
        var player = sim.World.Get<Player>(e)!;
        var rejected = new List<IntentRejected>();
        sim.Events.Subscribe<IntentRejected>(rejected.Add);
        var handlers = sim.Service<IntentHandlers>();
        Assert.True(handlers.Has<UseWeaveIntent>());
        Assert.True(handlers.Has<ReleaseWeaveIntent>());
        Assert.True(handlers.Has<PressWeaveIntent>());
        Assert.True(handlers.Has<AimIntent>());

        var seen = 0;
        handlers.Register<TestIntent>((in IntentContext ctx, TestIntent t) => seen += t.Amount);
        player.Intents.Enqueue(new TestIntent(3));
        player.Intents.Enqueue(new ReleaseWeaveIntent(0));
        player.Intents.Enqueue(new PressWeaveIntent(0));
        player.Intents.Enqueue(new AimIntent(new Vector3(6, 6, 0), Vector2.UnitX));
        player.Intents.Enqueue(new UnknownIntent());
        sim.Tick();
        Assert.Equal(3, seen);
        var only = Assert.Single(rejected);
        Assert.Equal("Unknown", only.Intent);
        Assert.Equal("unknown_intent", only.Reason);
    }

    private sealed record TestIntent(int Amount) : IIntent;

    private sealed record UnknownIntent : IIntent;

    [Fact]
    public void AttributeFamiliesExpandIntoDeclaredAttributes()
    {
        var registry = CoreContent.Registry;
        var burning = registry.Get<AttributeDef>("status.burning.chance");
        Assert.NotNull(burning);
        Assert.Equal("status_chance", burning!.Family);
        Assert.Equal("Chance to inflict Burning", burning.Name);
        Assert.Equal(1, burning.Max);
        Assert.True(registry.Attributes.Contains("added.ember"));
        Assert.Equal(registry.Count<StatusDef>(), registry.Each<AttributeDef>().Count(a => a.Family == "status_chance"));
        // A family over a list, expanded in code, joins the catalog too.
        var copy = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        copy.With(new AttributeFamilyDef { Id = "t_seats", Pattern = "glyph.slot{n}.power", Over = new System.Text.Json.Nodes.JsonArray(1, 2, 3), Name = "Seat {n} power" });
        Assert.True(copy.Attributes.Contains("glyph.slot2.power"));
        Assert.Equal("Seat 3 power", copy.Get<AttributeDef>("glyph.slot3.power")!.Name);
    }

    [Fact]
    public void KindsTakeSeveralValidators()
    {
        Assert.True(ContentKinds.TryGet("forms", out var forms));
        Assert.True(forms!.Validators.Count >= 2);
        Assert.True(ContentKinds.TryGet("glyphs", out var glyphs));
        Assert.True(glyphs!.Validators.Count >= 3);
        Assert.True(ContentKinds.TryGet("attribute_families", out _));
    }

    [Fact]
    public void ChargesCountGainAndSpendThroughGauges()
    {
        var (sim, wolf, _) = Duel();
        var charges = sim.Service<ChargeService>();
        Assert.Equal(0, charges.Count(sim, wolf, "fury"));
        Assert.Equal(3, charges.Gain(sim, wolf, "fury", 3, max: 5));
        Assert.Equal(2, charges.Gain(sim, wolf, "fury", 4, max: 5));
        Assert.Equal(5, sim.World.Get<Stats>(wolf)!.Gauge("charge.fury"));
        Assert.False(charges.Spend(sim, wolf, "fury", 6));
        Assert.True(charges.Spend(sim, wolf, "fury", 2));
        Assert.Equal(3, Expr.Parse("charge.fury").Evaluate(sim.World.Get<Stats>(wolf)!));
        Assert.Equal(3, charges.SpendAll(sim, wolf, "fury"));
        Assert.Equal(0, sim.World.Get<Stats>(wolf)!.Gauge("charge.fury"));
    }

    [Fact]
    public void CurvesAreNamed()
    {
        Assert.Equal(0.25, Curves.Evaluate("quadratic", 0.5), 9);
        Assert.Equal(0.75, Curves.Evaluate("quad_out", 0.5), 9);
        Assert.Equal(1, Curves.Evaluate("breath", 0.5), 9);
        Assert.Equal(0.3, Curves.Evaluate("no_such_curve", 0.3), 9);
        Assert.True(Curves.Has("linear"));
    }

    [Fact]
    public void ShellSystemsAndServicesAreWiredInPhaseOrder()
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 1);
        var names = sim.Systems.Select(s => s.Name).ToList();
        foreach (var name in new[] { "minions", "displacement", "tethers", "upkeep" })
        {
            Assert.Contains(name, names);
        }
        Assert.True(names.IndexOf("spawners") < names.IndexOf("minions") && names.IndexOf("minions") < names.IndexOf("behavior"));
        Assert.True(names.IndexOf("movement") < names.IndexOf("displacement") && names.IndexOf("displacement") < names.IndexOf("projectiles"));
        Assert.True(names.IndexOf("fields") < names.IndexOf("tethers"));
        Assert.True(names.IndexOf("statuses") < names.IndexOf("upkeep"));
        Assert.Equal(sim.Systems.Count, sim.Systems.Select(s => (s.Phase, s.Order)).Distinct().Count());
        Assert.True(sim.TryService<TriggerHub>(out _));
        Assert.True(sim.TryService<ChargeService>(out _));
        Assert.True(sim.TryService<IntentHandlers>(out _));
        Assert.True(MechanismRegistry.TryGet("affliction", out _));
        Assert.True(EffectRegistry.Shared.Has("cast") && EffectRegistry.Shared.Has("delay") && EffectRegistry.Shared.Has("taunt"));
    }
}
