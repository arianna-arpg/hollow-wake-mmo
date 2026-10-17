using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>Constructs: deploy caps and lifetimes, casting with the owner's numbers and credit, traps, domes, echoes and forwarding.</summary>
public class ConstructTests
{
    private static MinionArena Arena(ulong seed = 9) => MinionArena.Create(seed);

    [Fact]
    public void DeploysAreCappedAndExpire()
    {
        var arena = Arena();
        var slot = arena.Learn("plant_ember_totem");
        var point = arena.OwnerAt.Position + new Vector3(3, 0, 0);
        arena.Cast(slot, point: point);
        arena.Cast(slot, point: point);
        var first = arena.Minions("ember_totems");
        Assert.Equal(2, first.Count);
        Assert.All(first, t => Assert.True(arena.Sim.World.Get<Minion>(t)!.Construct));
        Assert.All(first, t => Assert.True(arena.Sim.World.Get<Identity>(t)!.Tags.Has("construct")));
        arena.Cast(slot, point: point);
        var now = arena.Minions("ember_totems");
        Assert.Equal(2, now.Count);
        Assert.DoesNotContain(first[0], now);
        // Totems last the weave's duration.
        arena.Sim.RunSeconds(13);
        Assert.Empty(arena.Minions("ember_totems"));
    }

    [Fact]
    public void ATotemThatInheritsItsCasterHitsWithTheOwnersNumbersAndCreditsTheOwner()
    {
        var arena = Arena();
        arena.OwnerStats.Attributes.Add(new Modifier { Attribute = "damage", Form = ModifierForm.More, Value = 1, Source = "test" });
        var foe = arena.Foe("tangle", arena.OwnerAt.Position + new Vector3(9, 0, 0));
        arena.Cast(arena.Learn("plant_ember_totem"), point: arena.OwnerAt.Position + new Vector3(2, 2, 0));
        var inheriting = arena.Minions("ember_totems").Single();
        arena.Cast(arena.Learn("t_totem_plain"), point: arena.OwnerAt.Position + new Vector3(2, -2, 0));
        var plain = arena.Minions("t_plain_totems").Single();
        arena.Sim.RunSeconds(4);
        Assert.Contains(arena.Events.OfType<WeaveCast>(), w => w.Caster == inheriting);
        Assert.Contains(arena.Events.OfType<WeaveCast>(), w => w.Caster == plain);
        // The inheriting totem's bolts start where it stands but hit as the owner; the plain one hits as itself.
        var asOwner = arena.Events.OfType<DamageDealt>().Where(d => d.Source == arena.Owner && d.Target == foe && !d.Crit && d.Total > 0 && !d.Tags.Has("dot")).ToList();
        var asItself = arena.Events.OfType<DamageDealt>().Where(d => d.Source == plain && d.Target == foe && !d.Crit && d.Total > 0 && !d.Tags.Has("dot")).ToList();
        Assert.NotEmpty(asOwner);
        Assert.NotEmpty(asItself);
        Assert.True(asOwner.Average(d => d.Total) > asItself.Average(d => d.Total) * 1.5, $"{asOwner.Average(d => d.Total)} vs {asItself.Average(d => d.Total)}");
        var spawns = arena.Events.OfType<EntitySpawned>().Where(s => s.Pattern == "projectile").ToList();
        Assert.Contains(spawns, s => Vector2.Distance(new Vector2(s.Position.X, s.Position.Y), new Vector2(arena.At(inheriting).X, arena.At(inheriting).Y)) < 0.01f);
        // A kill by the totem is the owner's.
        arena.Sim.World.Get<Stats>(foe)!.SetResource("life", 1);
        arena.Sim.RunSeconds(3);
        var death = Assert.Single(arena.Events.OfType<EntityDied>(), d => d.Entity == foe);
        Assert.True(death.Killer == arena.Owner || arena.Sim.World.Get<Minion>(death.Killer)?.Owner == arena.Owner, $"credited {death.Killer}");
    }

    [Fact]
    public void TrapsSpringWhenAFoeComesNearAndAreSpent()
    {
        var arena = Arena();
        var point = arena.OwnerAt.Position + new Vector3(5, 0, 0);
        arena.Cast(arena.Learn("lay_snare"), point: point);
        var trap = arena.Minions("snares").Single();
        var foe = arena.Foe("hob", point + new Vector3(4, 0, 0));
        arena.Sim.RunSeconds(1);
        Assert.False(arena.Hit(arena.Owner, foe));
        Assert.True(arena.Sim.World.IsAlive(trap));
        arena.Move(foe, point + new Vector3(1, 0, 0));
        arena.Sim.RunSeconds(0.5);
        Assert.True(arena.Hit(arena.Owner, foe), "the snare never sprang");
        Assert.Empty(arena.Minions("snares"));
        var hits = arena.Events.OfType<DamageDealt>().Count(d => d.Target == foe && d.Total > 0 && !d.Tags.Has("dot"));
        arena.Sim.RunSeconds(2);
        Assert.Equal(hits, arena.Events.OfType<DamageDealt>().Count(d => d.Target == foe && d.Total > 0 && !d.Tags.Has("dot")));
    }

    [Fact]
    public void DomesDissolveHostileProjectilesBeforeTheyLand()
    {
        var arena = Arena();
        arena.Cast(arena.Learn("lumen_dome"));
        var dome = arena.Minions("domes").Single();
        Assert.NotNull(arena.Sim.World.Get<Intercept>(dome));
        var foe = arena.Foe("loosed_kindler", arena.OwnerAt.Position + new Vector3(12, 0, 0));
        var runtime = arena.Sim.Service<WeaveRuntime>();
        var spit = arena.Sim.World.Get<WeaveBook>(foe)!.Find("cinder_spit")!;
        Assert.True(runtime.TryBegin(arena.Sim, foe, spit, arena.Owner, arena.OwnerAt.Position, Vector2.Zero, out var reason), reason);
        arena.Sim.RunSeconds(3);
        Assert.Contains(arena.Events.OfType<EntitySpawned>(), s => s.Pattern == "projectile");
        Assert.Contains(arena.Events.OfType<ProjectileIntercepted>(), p => p.Dome == dome && p.Mode == "dissolve");
        Assert.False(arena.Hit(foe, arena.Owner), "the bolt passed the dome");
    }

    [Fact]
    public void ADeflectingDomeSendsProjectilesBackAtTheirOwnSide()
    {
        var arena = Arena();
        arena.Cast(arena.Learn("t_dome_deflect"));
        var foe = arena.Foe("loosed_kindler", arena.OwnerAt.Position + new Vector3(12, 0, 0));
        var runtime = arena.Sim.Service<WeaveRuntime>();
        var spit = arena.Sim.World.Get<WeaveBook>(foe)!.Find("cinder_spit")!;
        Assert.True(runtime.TryBegin(arena.Sim, foe, spit, arena.Owner, arena.OwnerAt.Position, Vector2.Zero, out var reason), reason);
        arena.Sim.RunSeconds(3);
        Assert.Contains(arena.Events.OfType<ProjectileIntercepted>(), p => p.Mode == "deflect");
        Assert.False(arena.Hit(foe, arena.Owner));
        Assert.True(arena.Hit(arena.Owner, foe), "the deflected bolt did not strike its caster's side");
    }

    [Fact]
    public void AnEchoReplaysItsOwnersCastsAtHalfStrength()
    {
        var arena = Arena();
        var foe = arena.Foe("tangle", arena.OwnerAt.Position + new Vector3(9, 0, 0));
        arena.Cast(arena.Learn("shade_echo"), point: arena.OwnerAt.Position);
        var echo = arena.Minions("echoes").Single();
        arena.Sim.RunSeconds(0.5);
        var spit = arena.Sim.World.Get<WeaveBook>(arena.Owner)!.Find("cinder_spit")!;
        arena.Cast(spit, foe, arena.At(foe));
        arena.Sim.RunSeconds(2);
        Assert.Contains(arena.Events.OfType<WeaveCast>(), w => w.Caster == echo && w.Weave == spit.Key);
        var hits = arena.Events.OfType<DamageDealt>().Where(d => d.Source == arena.Owner && d.Target == foe && !d.Crit && !d.Tags.Has("dot") && d.Total > 0).Select(d => d.Total).ToList();
        Assert.Equal(2, hits.Count);
        Assert.Equal(0.5, hits.Min() / hits.Max(), 2);
    }

    [Fact]
    public void ConstructsBoardTheirWeavesGlyphsAtAPrice()
    {
        var arena = Arena();
        arena.Cast(arena.Learn("t_totem_swift"), point: arena.OwnerAt.Position + new Vector3(3, 0, 0));
        var totem = arena.Minions("t_swift_totems").Single();
        var slot = Assert.Single(arena.Sim.World.Get<WeaveBook>(totem)!.Slots);
        Assert.Contains(slot.Compiled!.GlyphRefs, g => g.Id == "swift");
        var scale = arena.Sim.Content.Rules.Number("minions.construct_forward_scale", 0.7);
        var castSpeed = Assert.Single(slot.Compiled.Modifiers, m => m.Source == "glyph:swift" && m.Attribute == "cast.speed");
        Assert.Equal(0.25 * scale, castSpeed.Value, 9);
    }

    [Fact]
    public void ShatteringConstructsBurstsThemForTheirLife()
    {
        var arena = Arena();
        var point = arena.OwnerAt.Position + new Vector3(4, 0, 0);
        var foe = arena.Foe("tangle", point + new Vector3(1, 1, 0));
        arena.Sim.World.Remove<WeaveBook>(foe);
        arena.Cast(arena.Learn("lay_snare"), point: point + new Vector3(0, -6, 0));
        arena.Cast(arena.Learn("plant_ember_totem"), point: point);
        arena.Sim.World.Remove<Behavior>(arena.Minions("ember_totems").Single());
        arena.Events.Clear();
        arena.Cast(arena.Learn("t_shatter"), point: point);
        Assert.Equal(2, arena.Events.OfType<EntityDied>().Count());
        Assert.Empty(arena.Minions());
        Assert.True(arena.Hit(arena.Owner, foe), "the totem's burst missed the foe beside it");
    }
}
