using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;
using HollowWake.Core.Tags;
using static HollowWake.Core.Effects.Handlers.EffectHelpers;

namespace HollowWake.Core.Effects.Handlers;

/// <summary>Shared targeting helpers for effect handlers.</summary>
internal static class EffectHelpers
{
    internal static IEnumerable<Entity> Filter(EffectContext ctx, IEnumerable<Entity> candidates, string filter)
    {
        foreach (var e in candidates)
        {
            if (!ctx.Sim.World.IsAlive(e) || ctx.Sim.World.Has<Dead>(e))
            {
                continue;
            }
            if (!ctx.Sim.World.Has<Stats>(e))
            {
                continue;
            }
            switch (filter)
            {
                case "enemy":
                    if (Relations.IsHostile(ctx.Sim, ctx.Origin, e))
                    {
                        yield return e;
                    }
                    break;
                case "ally":
                    if (!Relations.IsHostile(ctx.Sim, ctx.Origin, e) && e != ctx.Origin)
                    {
                        yield return e;
                    }
                    break;
                case "ally_or_self":
                    if (!Relations.IsHostile(ctx.Sim, ctx.Origin, e))
                    {
                        yield return e;
                    }
                    break;
                case "self":
                    if (e == ctx.Source)
                    {
                        yield return e;
                    }
                    break;
                case "not_self":
                    if (e != ctx.Source)
                    {
                        yield return e;
                    }
                    break;
                default:
                    yield return e;
                    break;
            }
        }
    }

    internal static Vector3 Center(EffectContext ctx, string centerMode)
    {
        return centerMode switch
        {
            "source" => ctx.From ?? ctx.SourceTransform?.Position ?? ctx.Point,
            "point" => ctx.Point,
            _ => ctx.Focus,
        };
    }
}
