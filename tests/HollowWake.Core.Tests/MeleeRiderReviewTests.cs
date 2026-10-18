using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

public class MeleeRiderReviewTests
{
    [Theory]
    [InlineData("rending", false)]
    [InlineData("rending", true)]
    [InlineData("felling", false)]
    [InlineData("felling", true)]
    [InlineData("furious", false)]
    [InlineData("furious", true)]
    public void RidersFollowTheSelectedBodyAndNeverEscapeAWhiff(string glyph, bool bystander)
    {
        var arena = DeliveryArena.Create();
        var aimed = arena.Body(11, 10);
        var selected = bystander ? arena.Body(11.5f, 10) : Entity.None;
        Prepare(arena, aimed);
        if (bystander) Prepare(arena, selected);
        var weave = arena.Weave("strike", "stone", glyph);
        Assert.Empty(weave.Dormant);
        Assert.Single(weave.Effects); // The delivery remains observable by the shared AI/forecast path.
        var context = arena.Context(new Vector3(11, 10, 0), Vector2.UnitX, aimed, weave);
        arena.Sim.World.Get<Transform>(aimed)!.Position = new Vector3(8, 10, 0);
        arena.Sim.Spatial.Update(aimed, new Vector3(8, 10, 0));
        arena.Events.Clear();
        foreach (var effect in weave.Effects) effect.Execute(context);
        var hits = arena.Of<DamageDealt>();
        Assert.DoesNotContain(hits, h => h.Target == aimed);
        Assert.Equal(bystander ? 0 : 1, arena.Of<WeaveMissed>().Count);
        if (!bystander)
        {
            Assert.Empty(hits);
            Assert.Empty(arena.Of<StatusRuptured>());
            Assert.Empty(arena.Of<ChargeGained>());
            return;
        }
        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.Equal(selected, h.Target));
        Assert.Contains(hits, h => h.Depth == 0); // The selector adds no proc depth.
        if (glyph == "rending") Assert.Equal(selected, Assert.Single(arena.Of<StatusRuptured>()).Target);
        if (glyph == "felling") Assert.Equal(2, hits.Count);
        if (glyph == "furious") Assert.Equal(arena.Caster, Assert.Single(arena.Of<ChargeGained>()).Holder);
    }

    private static void Prepare(DeliveryArena arena, Entity target)
    {
        CastingArena.Sturdy(arena.Sim, target);
        var stats = arena.Sim.World.Get<Stats>(target)!;
        stats.Flags.Add("pool.poise.broken");
        arena.Sim.Service<StatusService>().Apply(arena.Sim, target, "bleeding", arena.Caster,
            new StatusApplication { Magnitude = 2, Duration = 20 });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RendingCannotRuptureTheAimAfterItLeavesDuringWindup(bool bystander)
    {
        var arena = CastingArena.Create(distance: 1);
        var slot = arena.Slot(new WeaveSpec { Form = "strike", Threads = { "stone" }, Glyphs = { "rending" } });
        var selected = bystander ? arena.Spawn("hob", new Vector3(11.5f, 10, 0)) : Entity.None;
        foreach (var target in bystander ? new[] { arena.Enemy, selected } : new[] { arena.Enemy })
            arena.Sim.Service<StatusService>().Apply(arena.Sim, target, "bleeding", arena.Caster,
                new StatusApplication { Magnitude = 2, Duration = 20 });
        arena.Begin(slot);
        Assert.NotNull(arena.Book.Casting);
        var moved = new Vector3(8, 10, 0);
        arena.Sim.World.Get<Transform>(arena.Enemy)!.Position = moved;
        arena.Sim.Spatial.Update(arena.Enemy, moved);
        for (var tick = 0; tick < 100 && arena.Book.Casting is not null; tick++) arena.Sim.Tick();
        Assert.Null(arena.Book.Casting);
        var ruptures = arena.Of<StatusRuptured>().ToList();
        if (bystander) Assert.Equal(selected, Assert.Single(ruptures).Target);
        else Assert.Empty(ruptures);
        Assert.Equal(bystander ? 0 : 1, arena.Of<WeaveMissed>().Count());
    }

    [Fact]
    public void ReapingKeepsItsKillHookOnTheSelectedMeleeVictim()
    {
        var arena = DeliveryArena.Create();
        var aimed = arena.Body(8, 10);
        var selected = arena.Body(11, 10);
        arena.Sim.World.Get<Stats>(aimed)!.SetResource("life", 1);
        arena.Sim.World.Get<Stats>(selected)!.SetResource("life", 1);
        var weave = arena.Weave("strike", "stone", "reaping");
        Assert.Single(weave.Effects);
        foreach (var effect in weave.Effects)
            effect.Execute(arena.Context(new Vector3(11, 10, 0), Vector2.UnitX, aimed, weave));
        Assert.False(arena.Sim.World.Has<Dead>(aimed));
        Assert.True(arena.Sim.World.Has<Dead>(selected));
        Assert.Single(arena.Sim.World.Query<Minion>(), m => m.C1.Owner == arena.Caster && m.C1.Group == "reaped");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SecondaryPlacementsDoNotReplayThePrimaryMiss(bool cascade, bool initialHit)
    {
        var arena = DeliveryArena.Create();
        arena.Sim.EmitCastPresentation = true;
        var target = initialHit ? arena.Body(15, 10) : Entity.None;
        var weave = arena.Weave("blast", "ember", "t_echo");
        const string area = """{"type":"area","radius":1,"center":"point","report_miss":true,"payload_depth":"same","effects":[{"type":"event","name":"t_hit","data":{"depth":"depth"}}]}""";
        var graph = cascade ? "{\"type\":\"cascade\",\"count\":2,\"step\":3,\"effects\":[" + area + "]}" : area;
        var context = arena.Context(new Vector3(15, 10, 0), Vector2.UnitX, weave: weave);
        arena.Effect(graph).Execute(context);
        if (initialHit)
        {
            Assert.Single(arena.Struck());
            Assert.Equal(0, Assert.Single(arena.Of<CustomEvent>()).Data["depth"]);
            arena.Sim.World.Get<Transform>(target)!.Position = new Vector3(50, 50, 0);
            arena.Sim.Spatial.Update(target, new Vector3(50, 50, 0));
        }
        arena.Tick(50);
        Assert.Equal(initialHit ? 0 : 1, arena.Of<WeaveMissed>().Count);
        Assert.Equal(initialHit ? 0 : 1, arena.Of<CastPresentationEvent>().Count(e => e.Phase == "miss"));
    }

    [Theory]
    [InlineData("felling")]
    [InlineData("furious")]
    public void DirectPresetDamageKeepsItsRider(string glyph)
    {
        var arena = DeliveryArena.Create();
        var target = arena.Body(11, 10);
        Prepare(arena, target);
        var weave = arena.Sim.Service<WeaveCompiler>().Compile(new WeaveSpec { Preset = "eviscerate", Glyphs = { glyph } });
        arena.Events.Clear();
        foreach (var effect in weave.Effects)
            effect.Execute(arena.Context(new Vector3(11, 10, 0), Vector2.UnitX, target, weave));
        Assert.Empty(weave.Dormant);
        if (glyph == "furious") Assert.Single(arena.Of<ChargeGained>());
        else Assert.Equal(3, arena.Of<DamageDealt>().Count); // rupture, strike, felling
    }

    [Fact]
    public void DamageHookRunsOncePerPositivePacketAtPayloadDepthAfterKillHook()
    {
        var arena = DeliveryArena.Create();
        var target = arena.Body(11, 10);
        arena.Sim.World.Get<Stats>(target)!.SetResource("life", 1);
        var effect = arena.Effect("""
            {"type":"damage","amount":1000,"threads":["stone"],"can_crit":false,
             "after_damage":[{"type":"event","name":"after","data":{"depth":"depth","amount":"last_damage"}}],
             "on_kill":[{"type":"event","name":"kill","data":{"depth":"depth"}}]}
            """);
        var context = arena.Context(Vector3.Zero, target: target).Deeper();
        effect.Execute(context);
        var events = arena.Of<CustomEvent>();
        Assert.Equal(new[] { "kill", "after" }, events.Select(e => e.Name));
        Assert.Equal(2, events[0].Data["depth"]);
        Assert.Equal(1, events[1].Data["depth"]);
        Assert.True(events[1].Data["amount"] > 0);
        effect.Execute(context); // A dead target cannot fire the hook.
        effect.Execute(context.Targetless());
        Assert.Equal(2, arena.Of<CustomEvent>().Count);
    }

    [Fact]
    public void DamageHooksArePerHitAndHookPayloadsAreNotRecursivelyHooked()
    {
        var arena = DeliveryArena.Create();
        var target = arena.Body(11, 10);
        Prepare(arena, target);
        var weave = arena.Weave("strike", "stone", "felling", "furious");
        var context = arena.Context(new Vector3(11, 10, 0), Vector2.UnitX, target, weave);
        arena.Events.Clear();
        foreach (var effect in weave.Effects) effect.Execute(context);
        Assert.Equal(2, arena.Of<DamageDealt>().Count);
        Assert.Single(arena.Of<ChargeGained>()); // Added felling damage does not itself gain fury.
        Assert.All(arena.Of<DamageDealt>(), h => Assert.Equal(0, h.Depth));
        foreach (var effect in weave.Effects) effect.Execute(context);
        Assert.Equal(2, arena.Of<ChargeGained>().Count);
    }

    [Fact]
    public void ZeroDamageCannotRunAfterDamage()
    {
        var arena = DeliveryArena.Create();
        var target = arena.Body(11, 10);
        arena.Run("""{"type":"damage","amount":0,"effectiveness":0,"after_damage":[{"type":"event","name":"unexpected"}]}""",
            Vector3.Zero, target: target);
        Assert.Empty(arena.Of<CustomEvent>());
    }
}
