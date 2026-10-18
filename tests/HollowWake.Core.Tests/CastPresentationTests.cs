using HollowWake.Core.Components;
using HollowWake.Core.Attributes;
using HollowWake.Core.Sim;
using Xunit;

namespace HollowWake.Core.Tests;

public class CastPresentationTests
{
    [Fact]
    public void No_observers_emit_no_records_but_late_observation_keeps_the_cast_identity()
    {
        var a = CastingArena.Create();
        a.Sim.EmitCastPresentation = false;
        a.Begin(a.Slot("t_walk"));
        var id = a.Book.Casting!.CastId;
        Assert.True(id > 0);
        a.RunSeconds(0.5);
        Assert.Empty(a.Of<CastPresentationEvent>());
        a.Sim.EmitCastPresentation = true;
        a.RunSeconds(2);
        Assert.Equal(new[] { "release", "end" }, a.Of<CastPresentationEvent>().Select(e => e.Phase));
        Assert.All(a.Of<CastPresentationEvent>(), e => Assert.Equal(id, e.CastId));
        a.Events.Clear();
        a.Sim.EmitCastPresentation = false;
        a.Begin(a.Slot("t_hit"));
        Assert.Single(a.Of<DamageDealt>());
        Assert.Empty(a.Of<CastPresentationEvent>());
    }

    [Fact]
    public void Interrupting_a_channel_does_not_cancel_a_projectile_already_in_flight()
    {
        var a = CastingArena.Create();
        a.Begin(a.Slot("t_channel_bolt"));
        a.RunSeconds(0.2);
        var release = Assert.Single(a.Of<CastPresentationEvent>(), e => e.Phase == "release");
        Assert.DoesNotContain(a.Of<CastPresentationEvent>(), e => e.Phase == "impact");
        a.Runtime.Interrupt(a.Sim, a.Caster);
        var interrupted = Assert.Single(a.Of<CastPresentationEvent>(), e => e.Phase == "interrupt");
        Assert.True(interrupted.Fired);
        a.RunSeconds(4);
        var impact = Assert.Single(a.Of<CastPresentationEvent>(), e => e.Phase == "impact");
        Assert.Equal(release.CastId, impact.CastId);
        Assert.True(impact.Time > interrupted.Time);
        Assert.Single(a.Of<CastPresentationEvent>(), e => e.Phase == "release");
    }

    [Fact]
    public void Blocked_damage_never_gets_the_successful_contact_flourish()
    {
        var a = CastingArena.Create();
        a.EnemyStats.Attributes.Add(new Modifier { Attribute = "block.chance", Form = ModifierForm.Override, Value = 1, Source = "test" });
        a.Begin(a.Slot("t_hit"));
        Assert.True(Assert.Single(a.Of<DamageDealt>()).Blocked);
        Assert.DoesNotContain(a.Of<CastPresentationEvent>(), c => c.Phase == "impact");
    }

    [Fact]
    public void Fizzled_attempt_never_presents_a_cast_or_hit()
    {
        var a = CastingArena.Create();
        a.Stats.Attributes.Add(new Modifier { Attribute = "cast.fizzle_chance", Form = ModifierForm.Flat, Value = 1, Source = "test" });
        Assert.False(a.Begin(a.Slot("t_free"), out var reason));
        Assert.Equal("fizzled", reason);
        Assert.Empty(a.Of<CastPresentationEvent>());
    }

    [Fact]
    public void Losing_the_target_during_windup_never_invents_an_impact()
    {
        var a = CastingArena.Create();
        a.Begin(a.Slot("t_timed"));
        a.Sim.World.Add(a.Enemy, new Dead());
        a.RunSeconds(2);
        Assert.DoesNotContain(a.Of<CastPresentationEvent>(), c => c.Phase == "impact");
    }

    [Fact]
    public void Guard_preparation_precedes_its_raise_payload()
    {
        var a = CastingArena.Create();
        a.Begin(a.Slot("t_guard"));
        Assert.Equal(new[] { "start", "release" }, a.Of<CastPresentationEvent>().Select(c => c.Phase));
        Assert.Single(a.Of<CastPresentationEvent>().Select(c => c.CastId).Distinct());
    }

    [Fact]
    public void Caster_death_clears_preparation_once_without_a_payload()
    {
        var a = CastingArena.Create();
        a.Begin(a.Slot("t_walk"));
        a.Sim.World.Add(a.Caster, new Dead());
        a.RunSeconds(3);
        var cues = a.Of<CastPresentationEvent>().ToArray();
        Assert.Equal(new[] { "start", "interrupt" }, cues.Select(c => c.Phase));
        Assert.False(cues[1].Fired);
    }

    [Fact]
    public void Channel_beats_keep_one_cast_identity()
    {
        var a = CastingArena.Create();
        a.Begin(a.Slot("t_channel"));
        a.RunSeconds(3);
        var cues = a.Of<CastPresentationEvent>().ToArray();
        Assert.True(cues.Count(c => c.Phase == "release") > 1);
        Assert.Single(cues.Select(c => c.CastId).Distinct());
        Assert.Equal("end", cues[^1].Phase);
        Assert.True(cues[^1].Fired);
    }

    [Fact]
    public void Timed_cast_reports_preparation_release_and_end_with_one_identity()
    {
        var a = CastingArena.Create();
        a.Begin(a.Slot("t_walk"));
        Assert.Equal("start", Assert.Single(a.Of<CastPresentationEvent>()).Phase);
        a.RunSeconds(2.1);
        var cues = a.Of<CastPresentationEvent>().ToArray();
        Assert.Equal(new[] { "start", "release", "end" }, cues.Select(c => c.Phase));
        Assert.Single(cues.Select(c => c.CastId).Distinct());
        Assert.True(cues[0].CastId > 0);
        Assert.False(cues[0].Fired);
        Assert.True(cues[2].Fired);
    }

    [Fact]
    public void Interrupt_and_external_clear_never_claim_a_release()
    {
        foreach (var external in new[] { false, true })
        {
            var a = CastingArena.Create();
            a.Begin(a.Slot("t_walk"));
            if (external) a.Book.Casting = null;
            else a.Runtime.Interrupt(a.Sim, a.Caster);
            a.RunSeconds(0.2);
            var cues = a.Of<CastPresentationEvent>().ToArray();
            Assert.Equal(new[] { "start", "interrupt" }, cues.Select(c => c.Phase));
            Assert.False(cues[1].Fired);
        }
    }

    [Fact]
    public void Underfilled_channel_ends_without_launching_a_payload()
    {
        var a = CastingArena.Create();
        var slot = a.Slot("t_channel");
        a.Begin(slot);
        Assert.True(a.Runtime.Release(a.Sim, a.Caster, a.SlotIndex(slot)));
        var end = Assert.Single(a.Of<CastPresentationEvent>(), c => c.Phase == "end");
        Assert.False(end.Fired);
        Assert.DoesNotContain(a.Of<CastPresentationEvent>(), c => c.Phase is "release" or "impact");
    }

    [Fact]
    public void Instant_damage_contact_follows_release_and_keeps_cast_identity()
    {
        var a = CastingArena.Create();
        a.Begin(a.Slot("t_hit"));
        var cues = a.Of<CastPresentationEvent>().ToArray();
        Assert.Equal(new[] { "release", "impact", "end" }, cues.Select(c => c.Phase));
        Assert.Single(cues.Select(c => c.CastId).Distinct());
        Assert.Equal(a.Enemy, cues[1].Target);
        Assert.Equal(a.Sim.World.Get<Transform>(a.Enemy)!.Position, cues[1].Position);
    }

    [Fact]
    public void Projectile_contact_keeps_identity_after_the_cast_ends()
    {
        var a = CastingArena.Create();
        a.Begin(a.Slot("ember_bolt"));
        a.RunSeconds(3);
        var cues = a.Of<CastPresentationEvent>().ToArray();
        var impact = Assert.Single(cues, c => c.Phase == "impact");
        var release = Assert.Single(cues, c => c.Phase == "release");
        Assert.Equal(release.CastId, impact.CastId);
        Assert.True(impact.Time >= release.Time);
        Assert.True(release.Tags.Has("ember"));
        Assert.True(release.Mechanisms.Has("projectile"));
        Assert.True(impact.Mechanisms.Has("projectile"));
        Assert.Contains("Ember", release.Name);
    }
}
