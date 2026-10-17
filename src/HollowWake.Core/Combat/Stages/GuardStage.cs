using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Combat.Stages;

/// <summary>
/// A held guard absorbs hits inside its arc into its own pool before evasion is rolled. The arc is measured
/// from the bearer's facing toward where the hit came from (the point a cast at a point fired from, else the
/// attacker). While the guard is fresh (its parry window) a hit inside the arc is parried whole; otherwise
/// the pool takes what it can, a hit it takes whole is published as blocked and ends, and the rest goes on
/// through the ladder. A pool that runs dry breaks the guard. Bodies without a live guard pass untouched.
/// </summary>
internal sealed class GuardStage : IHitStage
{
    public string Name => "guard";

    public HitFlow Run(HitScope hit)
    {
        var ctx = hit.Context;
        var sim = ctx.Sim;
        var world = sim.World;
        if (!world.TryGet<Guard>(ctx.Target, out var guard) || guard.Broken)
        {
            return HitFlow.Continue;
        }
        if (!world.TryGet<WeaveBook>(ctx.Target, out var book) || !ReferenceEquals(book.Casting, guard.Cast) || !world.TryGet<Transform>(ctx.Target, out var bearer))
        {
            return HitFlow.Continue;
        }
        Vector3 from;
        if (ctx.From is { } origin)
        {
            from = origin;
        }
        else if (world.TryGet<Transform>(ctx.Source, out var attacker))
        {
            from = attacker.Position;
        }
        else
        {
            return HitFlow.Continue;
        }
        var dx = from.X - bearer.Position.X;
        var dy = from.Y - bearer.Position.Y;
        var length = MathF.Sqrt(dx * dx + dy * dy);
        if (length > 1e-4f)
        {
            var forward = bearer.Forward;
            if ((dx * forward.X + dy * forward.Y) / length < guard.ArcCos - 1e-9)
            {
                return HitFlow.Continue;
            }
        }
        var now = sim.Clock.Time;
        var weave = guard.Cast.Weave?.Key ?? guard.Slot.Key;
        if (now <= guard.ParryUntil)
        {
            guard.Parried = true;
            guard.Hits++;
            Absorb(hit, 0);
            hit.Target?.Stamp("blocked", now);
            hit.Publish();
            sim.Events.Publish(new GuardParried(ctx.Target, ctx.Source, weave) { Tick = sim.Clock.Tick });
            WeaveRuntime.RaiseKind(sim, ctx.Target, CastingTriggers.Parry, ctx.Source, 0, hit.Tags, ctx.Depth);
            return HitFlow.Stop;
        }
        var total = 0.0;
        for (var i = 0; i < hit.ThreadCount; i++)
        {
            if (hit.Live[i])
            {
                total += hit.Amounts[i];
            }
        }
        if (total <= 0)
        {
            return HitFlow.Continue;
        }
        var take = Math.Min(guard.Pool, total);
        guard.Pool -= take;
        guard.Absorbed += take;
        guard.Hits++;
        if (guard.Pool <= 1e-9)
        {
            guard.Broken = true;
        }
        if (take >= total - 1e-9)
        {
            Absorb(hit, 0);
            hit.Target?.Stamp("blocked", now);
            hit.Publish();
            return HitFlow.Stop;
        }
        Absorb(hit, (total - take) / total);
        return HitFlow.Continue;
    }

    /// <summary>Keeps a share of every live Thread; a hit kept at nothing is a blocked hit.</summary>
    private static void Absorb(HitScope hit, double keep)
    {
        for (var i = 0; i < hit.ThreadCount; i++)
        {
            if (hit.Live[i])
            {
                hit.Amounts[i] *= keep;
            }
        }
        if (keep <= 0)
        {
            hit.Result.Blocked = true;
        }
    }
}
