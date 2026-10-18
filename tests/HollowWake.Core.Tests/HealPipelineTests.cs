using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Effects;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

public class HealPipelineTests
{
    [Theory]
    [InlineData("formula")]
    [InlineData("modifier")]
    [InlineData("potency")]
    [InlineData("weave")]
    public void AFinalNegativeHealCannotTakeLife(string path)
    {
        var arena = DeliveryArena.Create();
        var log = new ListSimLog();
        arena.Sim.Log = log;
        var victim = arena.Body(12, 10);
        var stats = arena.Sim.World.Get<Stats>(victim)!;
        stats.SetResource("life", stats.ResourceMax("life") - 20);
        var before = stats.Resource("life");
        var modifier = new Modifier { Attribute = "healing", Form = ModifierForm.Flat, Value = -10, Source = "test" };
        if (path == "modifier")
        {
            arena.Sim.World.Get<Stats>(arena.Caster)!.Attributes.Add(modifier);
        }
        var weave = arena.Weave("blast");
        if (path == "weave")
        {
            var content = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out var report);
            Assert.False(report.HasErrors, report.ToString());
            content.With(new GlyphDef { Id = "t_negative_healing", Modifiers = { modifier } });
            weave = new WeaveCompiler(content, EffectRegistry.Shared).Compile(new WeaveSpec
            {
                Form = "blast", Threads = { "ember" }, Glyphs = { GlyphRef.Parse("t_negative_healing") },
            });
        }
        var context = new EffectContext
        {
            Sim = arena.Sim, Source = arena.Caster, Target = victim,
            Weave = weave, Tags = weave.Tags, Rng = new Rng(77),
            Potency = path == "potency" ? -1 : 1,
        };
        var effect = arena.Effect(path == "formula"
            ? "{\"type\":\"heal\",\"amount\":\"-min(5,target.life)\"}"
            : "{\"type\":\"heal\",\"amount\":5}");
        effect.Execute(context);
        effect.Execute(context);
        Assert.Equal(before, stats.Resource("life"));
        Assert.Empty(arena.Of<DamageDealt>());
        Assert.Empty(arena.Of<Healed>());
        var warning = Assert.Single(log.Lines);
        Assert.Contains("the heal effect", warning);
        Assert.Contains("refused", warning);
        Assert.Contains(weave.Key, warning);
    }

    [Fact]
    public void ARefusedHealCanLaterHealAndOtherResourcesCanStillFall()
    {
        var arena = DeliveryArena.Create();
        var victim = arena.Body(12, 10);
        var stats = arena.Sim.World.Get<Stats>(victim)!;
        var at = arena.Sim.World.Get<Transform>(victim)!.Position;
        stats.SetResource("life", stats.ResourceMax("life") - 20);
        var before = stats.Resource("life");
        var caster = arena.Sim.World.Get<Stats>(arena.Caster)!;
        caster.Attributes.Add(new Modifier { Attribute = "healing", Form = ModifierForm.Flat, Value = -10, Source = "test" });
        var effect = arena.Effect("{\"type\":\"heal\",\"amount\":5}");
        var context = arena.Context(at, target: victim, weave: arena.Weave("blast"));
        effect.Execute(context);
        Assert.Equal(before, stats.Resource("life"));
        caster.Attributes.RemoveSource("test");
        effect.Execute(context);
        var healed = Assert.Single(arena.Of<Healed>());
        Assert.Equal(before + healed.Amount, stats.Resource("life"));
        Assert.True(healed.Amount > 0);

        caster.Attributes.Add(new Modifier { Attribute = "mana.max", Form = ModifierForm.Flat, Value = 100, Source = "test" });
        caster.SetResource("mana", 50);
        arena.Effect("{\"type\":\"heal\",\"resource\":\"mana\",\"amount\":-5,\"self\":true}").Execute(context);
        Assert.True(caster.Resource("mana") < 50);
        Assert.Empty(arena.Of<DamageDealt>());
    }
}
