using HollowWake.Core.Intents;
using HollowWake.Core.Interaction;
using HollowWake.Core.Sim;
using HollowWake.Core.Quests;
using HollowWake.Core.Territory;

namespace HollowWake.Core.Systems.Intents;

/// <summary>Interacting, quests, founts, decrees, structures and respawning, which are players' only, and orders to minions, which any owner may give.</summary>
public static class WorldIntentHandlers
{
    public static void Register(IntentHandlers handlers)
    {
        handlers.Register<InteractIntent>(Interact, playerOnly: true);
        handlers.Register<AcceptQuestIntent>(AcceptQuest, playerOnly: true);
        handlers.Register<TurnInQuestIntent>(TurnInQuest, playerOnly: true);
        handlers.Register<BindFountIntent>(BindFount, playerOnly: true);
        handlers.Register<EnactDecreeIntent>(EnactDecree, playerOnly: true);
        handlers.Register<BuildStructureIntent>(BuildStructure, playerOnly: true);
        handlers.Register<RespawnIntent>(Respawn, whileDead: true, playerOnly: true);
        handlers.Register<CommandMinionsIntent>(CommandMinions);
        // Conversations and world panes have their own file; the registry's default set reaches them through here.
        DialogueIntentHandlers.Register(handlers);
    }

    /// <summary>
    /// Checks the order and hands it to the minion system as an event: the command names and what they do are
    /// the minion system's, so an unknown command is refused there, not here.
    /// </summary>
    private static void CommandMinions(in IntentContext ctx, CommandMinionsIntent cm)
    {
        // A command is a content id; anything longer than an id could be is noise from the wire.
        var command = cm.Command.Trim();
        if (command.Length == 0 || command.Length > 64)
        {
            ctx.Reject("bad_command");
            return;
        }
        if (!float.IsFinite(cm.Point.X) || !float.IsFinite(cm.Point.Y) || !float.IsFinite(cm.Point.Z))
        {
            ctx.Reject("bad_point");
            return;
        }
        var target = ctx.Sim.World.IsAlive(cm.Target) ? cm.Target : Ecs.Entity.None;
        ctx.Sim.Events.Publish(new MinionCommandRequested(ctx.Entity, command, target, cm.Point, string.IsNullOrWhiteSpace(cm.Group) ? null : cm.Group) { Tick = ctx.Sim.Clock.Tick });
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
