using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Sim;
using Xunit;

namespace HollowWake.Core.Tests;

// Historical review reproducer: both cases failed at b7709fd, before the healing guard.
// HealPipelineTests now covers both paths plus potency and glyph modifiers in the passing suite.
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
