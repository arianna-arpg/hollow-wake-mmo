using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Sim;
using Xunit;

namespace HollowWake.Core.Tests;

// Review reproducer: copy into the core test project to demonstrate the open finding.
// Kept outside the passing suite until the runtime correction lands.
public class MorphReviewProbeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HealingCannotLowerLifeOutsideTheDamagePipeline(bool negativeModifier)
    {
        var arena = DeliveryArena.Create();
        var victim = arena.Body(12, 10);
        var stats = arena.Sim.World.Get<Stats>(victim)!;
        var at = arena.Sim.World.Get<Transform>(victim)!.Position;
        stats.SetResource("life", stats.ResourceMax("life") - 20);
        var before = stats.Resource("life");
        if (negativeModifier)
        {
            arena.Sim.World.Get<Stats>(arena.Caster)!.Attributes.Add(new Modifier
            {
                Attribute = "healing", Form = ModifierForm.Flat, Value = -10, Source = "review"
            });
        }
        var effect = arena.Effect(negativeModifier
            ? "{\"type\":\"heal\",\"amount\":5}"
            : "{\"type\":\"heal\",\"amount\":\"-min(5,target.life)\"}");
        effect.Execute(arena.Context(at, target: victim, weave: arena.Weave("blast")));
        Assert.Empty(arena.Of<DamageDealt>());
        Assert.Equal(before, stats.Resource("life"));
    }
}
