using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using Xunit;

namespace HollowWake.Core.Tests;

public class ConstructUseRangeTests
{
    private static (CastingArena Arena, Entity Owner, WeaveSlot Slot) Setup(bool requireMana = false)
    {
        var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        content.With(new FactionDef { Id = "red", Relations = { ["blue"] = Stance.Hostile } });
        content.With(new FactionDef { Id = "blue", Relations = { ["red"] = Stance.Hostile } });
        content.With(CastForecastContent.Registry.Get<FormDef>("forecast_direct")! with
        {
            Id = "device_range_probe", CastTime = Expr.Constant(0.25),
            ReleaseCheck = new() { Policy = "body" },
            Target = requireMana ? new() { Kind = "enemy", Require = Expr.Parse("mana >= 50") } : null,
            Effects = new() { CastForecastContent.Spec("""{"type":"event","name":"device_payload","data":{"range":"source.range"}}""") },
        });
        var arena = CastingArena.Create(registry: content, distance: 1);
        var owner = arena.Spawn("loosed_kindler", new Vector3(40, 10, 0), "red");
        arena.Sim.World.Add(arena.Caster, new Minion { Owner = owner, Construct = true, InheritCaster = true });
        var slot = arena.Slot(new WeaveSpec { Form = "device_range_probe" });
        return (arena, owner, slot);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnInheritingDevicesUseKeepsItsOwnReleaseRange(bool deviceHasReach)
    {
        var (arena, owner, slot) = Setup();
        arena.Stats.Attributes.Add(new Modifier { Attribute = "range", Form = ModifierForm.Override, Value = deviceHasReach ? 12 : 2, Source = "test" });
        arena.Sim.World.Get<Stats>(owner)!.Attributes.Add(new Modifier { Attribute = "range", Form = ModifierForm.Override, Value = deviceHasReach ? 2 : 12, Source = "test" });
        arena.Begin(slot);
        Assert.NotNull(arena.Book.Casting);
        CastForecastContent.Move(arena, arena.Enemy, new Vector3(18, 10, 0));
        arena.RunSeconds(0.5);
        Assert.Equal(deviceHasReach ? 1 : 0, arena.Custom("device_payload").Count);
        if (deviceHasReach)
        {
            var payload = Assert.Single(arena.Custom("device_payload"));
            Assert.Equal(owner, payload.Source);
            Assert.Equal(2, payload.Data["range"]);
            Assert.Contains(arena.Of<WeaveCast>(), e => e.Caster == arena.Caster);
        }
        else
        {
            Assert.Contains(arena.Of<WeaveMissed>(), e => e.Caster == arena.Caster && e.Reason == "out_of_reach");
            Assert.DoesNotContain(arena.Of<WeaveCast>(), e => e.Caster == arena.Caster);
        }
    }

    [Fact]
    public void ADevicesBodyRadiusStillCountsAtRelease()
    {
        var (arena, owner, slot) = Setup();
        arena.Stats.Attributes.Add(new Modifier { Attribute = "range", Form = ModifierForm.Override, Value = 2, Source = "test" });
        arena.Sim.World.Get<Stats>(owner)!.Attributes.Add(new Modifier { Attribute = "range", Form = ModifierForm.Override, Value = 2, Source = "test" });
        arena.Sim.World.Get<Transform>(arena.Caster)!.Radius = 2;
        arena.Sim.World.Get<Transform>(arena.Enemy)!.Radius = 0.25f;
        arena.Begin(slot);
        CastForecastContent.Move(arena, arena.Enemy, new Vector3(13.5f, 10, 0));
        arena.RunSeconds(0.5);
        Assert.Equal(owner, Assert.Single(arena.Custom("device_payload")).Source);
        Assert.Empty(arena.Of<WeaveMissed>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ADevicesOwnTargetConditionIsRecheckedAtRelease(bool deviceStillQualifies)
    {
        var (arena, owner, slot) = Setup(requireMana: true);
        arena.Stats.SetResource("mana", 100);
        arena.Begin(slot);
        Assert.NotNull(arena.Book.Casting);
        arena.Stats.SetResource("mana", deviceStillQualifies ? 100 : 0);
        arena.Sim.World.Get<Stats>(owner)!.SetResource("mana", deviceStillQualifies ? 0 : 100);
        arena.RunSeconds(0.5);
        Assert.Equal(deviceStillQualifies ? 1 : 0, arena.Custom("device_payload").Count);
        if (deviceStillQualifies) Assert.Equal(owner, Assert.Single(arena.Custom("device_payload")).Source);
        else Assert.Contains(arena.Of<WeaveMissed>(), e => e.Caster == arena.Caster && e.Reason == "target_condition");
    }
}
