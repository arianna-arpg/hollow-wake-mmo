using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Maths;
using HollowWake.Core.Minions;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

public class UndeadResolutionTests
{
    private static Entity Corpse(MinionArena arena)
    {
        var corpse = arena.Foe("skeleton_warrior", new Vector3(22, 20, 0));
        arena.Sim.Service<DeathService>().Kill(arena.Sim, corpse, Entity.None);
        return corpse;
    }

    [Theory]
    [InlineData("echoing", 1)]
    [InlineData("unleash", 2)]
    public void RepeatedRaiseSpendsOneCorpseAndDoesNotMintFreeBodies(string glyph, int repeats)
    {
        var arena = MinionArena.Create();
        var slot = arena.Sim.World.Get<WeaveBook>(arena.Owner)!.Add(new WeaveSpec
        {
            Preset = "raise_zombie", Glyphs = { (GlyphRef)glyph },
        });
        var corpse = Corpse(arena);
        arena.Cast(slot, corpse, arena.At(corpse));
        for (var i = 0; i < 100; i++) arena.Sim.Tick();
        Assert.Single(arena.Minions());
        Assert.Single(arena.Events.OfType<CorpseRaised>());
        Assert.Equal(repeats, arena.Events.OfType<SummonFizzled>().Count(e => e.Reason == "no_corpse"));
    }

    [Fact]
    public void CorpseExpiringDuringCastPreventsBirth()
    {
        var arena = MinionArena.Create();
        var slot = arena.Learn("raise_zombie");
        var corpse = Corpse(arena);
        Assert.True(arena.Sim.Service<WeaveRuntime>().TryBegin(arena.Sim, arena.Owner, slot, corpse,
            arena.At(corpse), Vector2.Zero, out _));
        arena.Sim.World.Get<Dead>(corpse)!.DiedAt = -10000;
        for (var i = 0; i < 100; i++) arena.Sim.Tick();
        Assert.False(Corpses.Is(arena.Sim.World, corpse));
        Assert.Empty(arena.Minions());
        Assert.Empty(arena.Events.OfType<CorpseRaised>());
        Assert.Contains(arena.Events.OfType<SummonFizzled>(), e => e.Reason == "no_corpse");
    }

    [Fact]
    public void ConcurrentCastersCannotSpendTheSameCorpseTwice()
    {
        var arena = MinionArena.Create();
        var other = arena.Sim.Service<PatternFactory>().Spawn(arena.Sim, "loosed_kindler", new Vector3(20, 21, 0),
            new SpawnOptions { Faction = "red" });
        arena.Sim.World.Remove<Behavior>(other);
        var a = arena.Learn("raise_zombie");
        var b = arena.Sim.World.Get<WeaveBook>(other)!.Add(new WeaveSpec { Preset = "raise_zombie" });
        var corpse = Corpse(arena);
        var runtime = arena.Sim.Service<WeaveRuntime>();
        Assert.True(runtime.TryBegin(arena.Sim, arena.Owner, a, corpse, arena.At(corpse), Vector2.Zero, out _));
        Assert.True(runtime.TryBegin(arena.Sim, other, b, corpse, arena.At(corpse), Vector2.Zero, out _));
        for (var i = 0; i < 100; i++) arena.Sim.Tick();
        Assert.Single(arena.Sim.World.Query<Minion>());
        Assert.Single(arena.Events.OfType<CorpseRaised>());
        Assert.Single(arena.Events.OfType<SummonFizzled>());
    }

    [Fact]
    public void OwnedBodyAndItsDeliveryCarryOwnerAllegianceAndOnlyBodyClassifiers()
    {
        var arena = MinionArena.Create();
        arena.Cast(arena.Learn("summon_skeleton_archer"));
        var body = Assert.Single(arena.Minions());
        var identity = arena.Sim.World.Get<Identity>(body)!;
        var stats = arena.Sim.World.Get<Stats>(body)!;
        Assert.Equal("red", identity.Faction);
        Assert.True(stats.Tags.Has("faction.red"));
        Assert.False(stats.Tags.Has("faction.remnant"));
        foreach (var root in arena.Sim.Content.Rules.Strings("minions.encounter_tags"))
        {
            Assert.False(identity.Tags.Has(root));
            Assert.False(stats.Tags.Has(root));
        }
        Assert.True(identity.Tags.HasAll(HollowWake.Core.Tags.TagSet.Of("undead", "body.bone", "family.skeleton", "role.archer")));
        Assert.Null(arena.Sim.World.Get<Drops>(body));
        // Change the owner after birth: deliveries must resolve allegiance, not copy the stale birth stamp.
        arena.Sim.World.Get<Identity>(arena.Owner)!.Faction = "weftmarch_compact";
        Execute(arena, body, "{\"type\":\"projectile\",\"speed\":4,\"range\":8}");
        var flight = Assert.Single(arena.Sim.World.Query<Projectile>()).Entity;
        Assert.Equal("weftmarch_compact", arena.Sim.World.Get<Identity>(flight)!.Faction);
        Assert.Equal(Relations.FactionOf(arena.Sim, body), Relations.FactionOf(arena.Sim, flight));
    }

    [Fact]
    public void PlainSpawnHasWildFactionAndRewardsWithoutClaimingAnOwner()
    {
        var arena = MinionArena.Create();
        Execute(arena, arena.Owner, "{\"type\":\"spawn\",\"pattern\":\"skeleton_warrior\"}");
        var wild = Assert.Single(arena.Sim.World.Query<Identity>(), x => x.C1.Pattern == "skeleton_warrior");
        Assert.True(wild.C1.Owner.IsNone);
        Assert.Equal("remnant", wild.C1.Faction);
        Assert.NotNull(arena.Sim.World.Get<Drops>(wild.Entity));
        Assert.Null(arena.Sim.World.Get<Minion>(wild.Entity));
    }

    private static void Execute(MinionArena arena, Entity source, string json)
    {
        var registry = arena.Sim.Service<EffectRegistry>();
        var effect = registry.Compile(MinionArena.Spec(json), new EffectCompileContext
        {
            Content = arena.Sim.Content, Registry = registry, Owner = "undead-regression",
            Compiler = arena.Sim.Service<WeaveCompiler>(),
        });
        effect.Execute(new EffectContext { Sim = arena.Sim, Source = source, Point = arena.At(source),
            Direction = Vector2.UnitX, Rng = new Rng(17) });
    }
}
