using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

public class AiApproachTests
{
    [Fact]
    public void RetreatingTargetsRequireACloserApproachBeforeACommittedSwing()
    {
        var a = CastForecastContent.Arena(1.5f);
        a.Book.Slots.Clear();
        a.Slot("strike");
        var standing = a.Runtime.PreferredRange(a.Sim, a.Caster, a.Enemy);
        Assert.NotNull(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
        a.Sim.World.Get<Motion>(a.Enemy)!.Velocity = Vector3.UnitX;
        Assert.True(a.Runtime.PreferredRange(a.Sim, a.Caster, a.Enemy) < standing);
        Assert.Null(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
        CastForecastContent.Move(a, a.Enemy, new(10.9f, 10, 0));
        Assert.NotNull(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
    }

    [Fact]
    public void AnticipationIsAModifiableChoiceHintAndDoesNotExtendReachOrTrackTheSwing()
    {
        var a = CastForecastContent.Arena(1.9f);
        a.Book.Slots.Clear();
        var slot = a.Slot("strike");
        a.Sim.World.Get<Motion>(a.Enemy)!.Velocity = Vector3.UnitX;
        Assert.Null(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
        a.Stats.Attributes.Add(new Modifier { Attribute = "ai.anticipation", Form = ModifierForm.Override, Value = 0 });
        Assert.NotNull(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
        a.Begin(slot);
        var direction = a.Book.Casting!.Direction;
        CastForecastContent.Move(a, a.Enemy, new(20, 14, 0));
        a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy);
        Assert.Equal(direction, a.Book.Casting!.Direction);
        a.RunSeconds(3);
        Assert.DoesNotContain(a.Of<DamageDealt>(), hit => hit.Target == a.Enemy);
        Assert.Contains(a.Of<WeaveMissed>(), miss => miss.Reason == "out_of_reach");
    }

    [Fact]
    public void FasterWindupsNeedLessApproachAndApproachingTargetsDoNotPushTheCasterAway()
    {
        var a = CastForecastContent.Arena(2);
        a.Book.Slots.Clear();
        a.Slot("strike");
        a.Sim.World.Get<Motion>(a.Enemy)!.Velocity = new(0.5f, 0, 0);
        var slow = a.Runtime.PreferredRange(a.Sim, a.Caster, a.Enemy);
        a.Stats.Attributes.Add(new Modifier { Attribute = "attack.speed", Form = ModifierForm.More, Value = 1 });
        Assert.True(a.Runtime.PreferredRange(a.Sim, a.Caster, a.Enemy) > slow);
        a.Sim.World.Get<Motion>(a.Enemy)!.Velocity = -Vector3.UnitX;
        Assert.Equal(a.Runtime.PreferredRange(a.Sim, a.Caster), a.Runtime.PreferredRange(a.Sim, a.Caster, a.Enemy));
    }

    [Fact]
    public void BrieflyStoppedTargetsStillRequireTheAuthoredApproachDistance()
    {
        var a = CastForecastContent.Arena(2);
        a.Book.Slots.Clear();
        a.Slot("strike");
        Assert.Null(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
        CastForecastContent.Move(a, a.Enemy, new(11.5f, 10, 0));
        Assert.NotNull(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
    }

    [Fact]
    public void BusyActorsKeepInstantReflexOptionsWhileSkippingOrdinaryAttacks()
    {
        var a = CastingArena.Create();
        a.Book.Slots.Clear();
        var cast = a.Slot("t_shot");
        a.Begin(cast);
        Assert.Null(a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy));
        var instant = a.Slot("t_hit");
        a.Stats.Attributes.Add(new Modifier { Attribute = "reflex", Value = 1 });
        Assert.Equal(instant, a.Runtime.ChooseForAi(a.Sim, a.Caster, a.Enemy)!.Value.Slot);
    }
}
