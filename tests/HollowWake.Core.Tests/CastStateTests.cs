using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using Xunit;

namespace HollowWake.Core.Tests;

/// <summary>Slot state, cast state, moving while casting and forbidden tags.</summary>
public class CastStateTests
{
    private static (Simulation Sim, Entity Caster) Arena(Vector3 at)
    {
        var sim = ZoneBuilder.CreateArena(CoreContent.Registry, 5);
        var caster = sim.Service<PatternFactory>().Spawn(sim, "loosed_kindler", at, new SpawnOptions { Faction = "red" });
        sim.World.Remove<Behavior>(caster);
        return (sim, caster);
    }

    [Fact]
    public void SlotsAndCastsKeepStateCreatedOnFirstUse()
    {
        var slot = new WeaveSlot { Spec = new WeaveSpec { Preset = "ember_bolt" } };
        Assert.False(slot.HasState);
        slot.State["uses"] = 2;
        Assert.True(slot.HasState);
        var cast = new CastState { Slot = slot };
        Assert.False(cast.HasModifiers);
        cast.Modifiers.Add(new Modifier { Attribute = "damage", Form = ModifierForm.More, Value = 0.2 });
        Assert.True(cast.HasModifiers);
        Assert.Equal(0, cast.MoveFactor);
    }

    [Fact]
    public void CastsRootUnlessTheirWeaveIsMobile()
    {
        var (sim, caster) = Arena(new Vector3(10, 10, 0));
        var runtime = sim.Service<WeaveRuntime>();
        var compiler = sim.Service<WeaveCompiler>();
        Assert.Equal(1f, WeaveRuntime.MoveFactorOf(compiler.Compile(new WeaveSpec { Preset = "lumen_ward" })));
        Assert.Equal(0f, WeaveRuntime.MoveFactorOf(compiler.Compile(new WeaveSpec { Preset = "ember_bolt" })));

        // A timed cast aimed by direction, so it needs no target.
        var book = sim.World.Get<WeaveBook>(caster)!;
        var slot = book.Add(new WeaveSpec { Preset = "mind_lash" });
        Assert.True(runtime.TryBegin(sim, caster, slot, Entity.None, new Vector3(20, 10, 0), Vector2.UnitX, out var reason), reason);
        Assert.NotNull(book.Casting);
        Assert.Equal(0f, book.Casting!.MoveFactor);
        var motion = sim.World.Get<Motion>(caster)!;
        motion.MoveDir = Vector2.UnitY;
        var start = sim.World.Get<Transform>(caster)!.Position;
        sim.Tick();
        Assert.Equal(start, sim.World.Get<Transform>(caster)!.Position);
    }

    [Fact]
    public void MovementKeepsTheCastsFractionOfSpeed()
    {
        float Traveled(float? factor)
        {
            var (sim, caster) = Arena(new Vector3(10, 10, 0));
            var book = sim.World.Get<WeaveBook>(caster)!;
            if (factor is { } f)
            {
                var slot = book.Add(new WeaveSpec { Preset = "ember_bolt" });
                sim.Service<WeaveRuntime>().Resolve(sim, slot, out _);
                book.Casting = new CastState { Slot = slot, StartedAt = 0, Duration = 1000, MoveFactor = f };
            }
            sim.World.Get<Motion>(caster)!.MoveDir = Vector2.UnitX;
            var start = sim.World.Get<Transform>(caster)!.Position;
            sim.Run(20);
            return sim.World.Get<Transform>(caster)!.Position.X - start.X;
        }
        var free = Traveled(null);
        Assert.True(free > 1);
        Assert.Equal(free, Traveled(1f), 3);
        Assert.Equal(free / 2, Traveled(0.5f), 2);
        Assert.Equal(0, Traveled(0f), 3);
    }

    [Fact]
    public void ForbiddenTagsRefuseTheCastThroughCachedFlags()
    {
        var (sim, caster) = Arena(new Vector3(10, 10, 0));
        var book = sim.World.Get<WeaveBook>(caster)!;
        var slot = book.Add(new WeaveSpec { Preset = "ember_bolt" });
        var weave = sim.Service<WeaveRuntime>().Resolve(sim, slot, out _)!;
        Assert.Equal(weave.Tags.Select(t => "forbid:" + t), weave.ForbidFlags);
        Assert.Same(weave.ForbidFlags, weave.ForbidFlags);
        sim.World.Get<Stats>(caster)!.Flags.Add("forbid:spell");
        var failed = new List<WeaveFailed>();
        sim.Events.Subscribe<WeaveFailed>(failed.Add);
        Assert.False(sim.Service<WeaveRuntime>().TryBegin(sim, caster, slot, Entity.None, new Vector3(20, 10, 0), Vector2.UnitX, out var reason));
        Assert.Equal("forbidden:spell", reason);
        Assert.Equal("forbidden:spell", Assert.Single(failed).Reason);
    }
}
