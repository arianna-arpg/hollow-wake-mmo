using HollowWake.Core.Intents;
using HollowWake.Core.Progression;

namespace HollowWake.Core.Systems.Intents;

/// <summary>Allocating passives; choices, refunds and realms join here.</summary>
public static class ProgressionIntentHandlers
{
    public static void Register(IntentHandlers handlers)
    {
        handlers.Register<AllocatePassiveIntent>(AllocatePassive);
    }

    private static void AllocatePassive(in IntentContext ctx, AllocatePassiveIntent ap)
    {
        if (!ctx.Sim.Service<ProgressionService>().Allocate(ctx.Sim, ctx.Entity, ap.NodeId, out var reason))
        {
            ctx.Reject(reason);
        }
    }
}
