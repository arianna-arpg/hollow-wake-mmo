using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Characters;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Intents;
using HollowWake.Core.Items;
using HollowWake.Core.Sim;
using HollowWake.Core.Systems;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>The cast modes and the slot behaviors of the weave runtime.</summary>
public class CastModeTests
{
    private static (Simulation Sim, Entity Hero, Player Player) Hero(ContentRegistry? registry = null)
    {
        var sim = ZoneBuilder.CreateArena(registry ?? CoreContent.Registry, 5);
        var characters = sim.Service<CharacterFactory>();
        var sheet = characters.Create(sim, "c-cast", "a-cast", "Caster", "holdfast", sim.Service<ItemGenerator>());
        sheet.Position = new Vector3(5, 5, 0);
        var hero = characters.Materialize(sim, sheet);
        return (sim, hero, sim.World.Get<Player>(hero)!);
    }

    [Fact]
    public void PlayerSlotsKeepMisfitGlyphsDormantAcrossRecompiles()
    {
        // The test is about dormant recompiles, not ownership: build the hero with the ownership rule off.
        var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
        registry.With(new RulesDef { Id = "casting", Values = new JsonObject { ["require_ownership"] = false } });
        var (sim, hero, player) = Hero(registry);
        var rejected = new List<IntentRejected>();
        sim.Events.Subscribe<IntentRejected>(rejected.Add);
        // A strike cannot carry fork (it needs a projectile): the seat takes it anyway, dormant with its reason.
        player.Intents.Enqueue(new SetWeaveIntent(0, new WeaveSpec { Form = "strike", Glyphs = { "fork", "whetted" } }));
        sim.Tick();
        Assert.Empty(rejected);
        var book = sim.World.Get<WeaveBook>(hero)!;
        var slot = book.Slots[0];
        var dormant = Assert.Single(slot.Dormant);
        Assert.Equal("fork", dormant.Ref.Id);
        Assert.Contains("fork", dormant.Reason);

        // A gear change drops the compiled weave; the next resolve compiles it dormant again, so the seat still casts.
        book.InvalidateCompiled();
        var weave = sim.Service<WeaveRuntime>().Resolve(sim, slot, out var error);
        Assert.Null(error);
        Assert.NotNull(weave);
        Assert.Equal(new[] { "whetted" }, weave!.Glyphs.Select(g => g.Id));
        Assert.Equal("fork", Assert.Single(slot.Dormant).Ref.Id);
    }

    private sealed class Refusing : IWeaveOwnership
    {
        public bool Allows(Simulation sim, Entity hero, int slot, WeaveSpec spec, out string reason)
        {
            reason = "not_owned";
            return false;
        }
    }

    [Fact]
    public void OwnershipIsAskedOnlyWhileTheRuleHolds()
    {
        foreach (var required in new[] { false, true })
        {
            var registry = ContentRegistry.Load(new[] { CoreContent.FindContentDir() }, out _);
            registry.With(new RulesDef { Id = "casting", Values = new JsonObject { ["require_ownership"] = required } });
            var (sim, hero, player) = Hero(registry);
            sim.AddService<IWeaveOwnership>(new Refusing());
            var rejected = new List<IntentRejected>();
            sim.Events.Subscribe<IntentRejected>(rejected.Add);
            player.Intents.Enqueue(new SetWeaveIntent(1, new WeaveSpec { Preset = "ember_bolt" }));
            sim.Tick();
            var book = sim.World.Get<WeaveBook>(hero)!;
            if (required)
            {
                Assert.Equal("not_owned", Assert.Single(rejected).Reason);
                Assert.NotEqual("ember_bolt", book.Slots[1].Spec.Preset);
            }
            else
            {
                Assert.Empty(rejected);
                Assert.Equal("ember_bolt", book.Slots[1].Spec.Preset);
            }
        }
    }
}
