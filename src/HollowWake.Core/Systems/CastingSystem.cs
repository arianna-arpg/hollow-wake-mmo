using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Systems;

/// <summary>Advances casts and channels; resolves them when their time comes.</summary>
public sealed class CastingSystem : SimSystem
{
    public override string Name => "casting";
    public override SimPhase Phase => SimPhase.Act;

    public override void Update(Simulation sim)
    {
        var runtime = sim.Service<WeaveRuntime>();
        var now = sim.Clock.Time;
        foreach (var (e, book) in sim.World.Query<WeaveBook>())
        {
            var cast = book.Casting;
            if (cast is null)
            {
                continue;
            }
            if (sim.World.Has<Dead>(e))
            {
                book.Casting = null;
                continue;
            }
            var weave = cast.Slot.Compiled;
            if (weave is null)
            {
                book.Casting = null;
                continue;
            }
            if (sim.World.TryGet<Stats>(e, out var stats) && (stats.HasFlag("stunned") || stats.HasFlag("frozen")))
            {
                runtime.Interrupt(sim, e);
                continue;
            }
            if (cast.Channeling)
            {
                var interval = Math.Max(sim.Clock.Dt, weave.Form.ChannelInterval?.Evaluate(weave.ScopeFor(stats!)) ?? 0.5);
                if (now >= cast.NextChannelAt)
                {
                    if (cast.Pulses > 0 && !runtime.CanAfford(weave, stats!, out _))
                    {
                        book.Casting = null;
                        continue;
                    }
                    if (cast.Pulses > 0)
                    {
                        foreach (var (resource, cost) in runtime.Costs(weave, stats!))
                        {
                            stats!.AddResource(resource, -cost);
                        }
                    }
                    runtime.Execute(sim, e, cast, weave, cooldownAlreadyStarted: cast.Pulses > 0);
                    cast.Pulses++;
                    cast.NextChannelAt = now + interval;
                }
                if (cast.Duration > 0 && now - cast.StartedAt >= cast.Duration)
                {
                    book.Casting = null;
                }
                continue;
            }
            if (now - cast.StartedAt >= cast.Duration)
            {
                book.Casting = null;
                runtime.Execute(sim, e, cast, weave, cooldownAlreadyStarted: sim.Content.Rules.Text("combat.cooldown_at", "complete") == "press");
            }
        }
    }
}
