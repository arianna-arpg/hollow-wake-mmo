using HollowWake.Core.Intents;
using HollowWake.Core.Progression;

namespace HollowWake.Core.Systems.Intents;

/// <summary>Allocating passives (with an option on choice nodes) and refunding them at a font.</summary>
public static class ProgressionIntentHandlers
{
    public static void Register(IntentHandlers handlers)
    {
        handlers.Register<AllocatePassiveIntent>(AllocatePassive);
        handlers.Register<RefundPassiveIntent>(RefundPassive);
    }

    private static void AllocatePassive(in IntentContext ctx, AllocatePassiveIntent ap)
    {
        if (!ctx.Sim.Service<ProgressionService>().Allocate(ctx.Sim, ctx.Entity, ap.NodeId, ap.Option, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void RefundPassive(in IntentContext ctx, RefundPassiveIntent rp)
    {
        if (!ctx.Sim.Service<ProgressionService>().RefundAtFont(ctx.Sim, ctx.Entity, rp.NodeId, out var reason))
        {
            ctx.Reject(reason);
        }
    }
}
