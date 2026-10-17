using HollowWake.Core.Intents;
using HollowWake.Core.Interaction;
using HollowWake.Core.Quests;
using HollowWake.Core.Territory;

namespace HollowWake.Core.Systems.Intents;

/// <summary>Interacting, quests, founts, decrees and structures, and respawning.</summary>
public static class WorldIntentHandlers
{
    public static void Register(IntentHandlers handlers)
    {
        handlers.Register<InteractIntent>(Interact);
        handlers.Register<AcceptQuestIntent>(AcceptQuest);
        handlers.Register<TurnInQuestIntent>(TurnInQuest);
        handlers.Register<BindFountIntent>(BindFount);
        handlers.Register<EnactDecreeIntent>(EnactDecree);
        handlers.Register<BuildStructureIntent>(BuildStructure);
        handlers.Register<RespawnIntent>(Respawn, whileDead: true);
    }

    private static void Interact(in IntentContext ctx, InteractIntent i)
    {
        if (!ctx.Sim.Service<InteractionService>().Interact(ctx.Sim, ctx.Entity, i.Target, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void AcceptQuest(in IntentContext ctx, AcceptQuestIntent aq)
    {
        if (!ctx.Sim.Service<QuestService>().Accept(ctx.Sim, ctx.Entity, aq.QuestId, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void TurnInQuest(in IntentContext ctx, TurnInQuestIntent tq)
    {
        if (!ctx.Sim.Service<QuestService>().TurnIn(ctx.Sim, ctx.Entity, tq.QuestId, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void BindFount(in IntentContext ctx, BindFountIntent bf)
    {
        if (!ctx.Sim.Service<TerritoryService>().StartBinding(ctx.Sim, ctx.Entity, bf.Fount, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void EnactDecree(in IntentContext ctx, EnactDecreeIntent ed)
    {
        if (!ctx.Sim.Service<TerritoryService>().Enact(ctx.Sim, ctx.Entity, ed.HoldingId, ed.DecreeId, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void BuildStructure(in IntentContext ctx, BuildStructureIntent bs)
    {
        if (!ctx.Sim.Service<TerritoryService>().Build(ctx.Sim, ctx.Entity, bs.HoldingId, bs.StructureId, bs.Point, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    /// <summary>A dead player respawns now; a living one's request does nothing.</summary>
    private static void Respawn(in IntentContext ctx, RespawnIntent _)
    {
        if (ctx.Dead)
        {
            IntentSystem.Respawn(ctx.Sim, ctx.Entity);
        }
    }
}
