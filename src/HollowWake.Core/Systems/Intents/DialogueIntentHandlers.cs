using HollowWake.Core.Components;
using HollowWake.Core.Crafting;
using HollowWake.Core.Dialogue;
using HollowWake.Core.Intents;
using HollowWake.Core.Items;

namespace HollowWake.Core.Systems.Intents;

/// <summary>Choosing lines in a conversation, ending it, and closing the world panes a conversation or an entity opened.</summary>
public static class DialogueIntentHandlers
{
    public static void Register(IntentHandlers handlers)
    {
        handlers.Register<ChooseDialogueIntent>(Choose);
        handlers.Register<CloseDialogueIntent>(CloseDialogue, whileDead: true);
        handlers.Register<ClosePaneIntent>(ClosePane, whileDead: true);
    }

    private static void Choose(in IntentContext ctx, ChooseDialogueIntent c)
    {
        if (!ctx.Sim.Service<DialogueService>().Choose(ctx.Sim, ctx.Entity, c.Conversation, c.Choice, c.Node, out var reason))
        {
            ctx.Reject(reason);
        }
    }

    private static void CloseDialogue(in IntentContext ctx, CloseDialogueIntent c)
    {
        if (ctx.Sim.World.Get<Panes>(ctx.Entity)?.Conversation?.Dialogue == c.Conversation)
        {
            ctx.Sim.Service<DialogueService>().Close(ctx.Sim, ctx.Entity, "closed");
        }
    }

    /// <summary>Closes the hero's pane of a kind when the entity named opened it; a pane already closed is left be.</summary>
    private static void ClosePane(in IntentContext ctx, ClosePaneIntent c)
    {
        var panes = ctx.Sim.World.Get<Panes>(ctx.Entity);
        switch (c.Pane)
        {
            case "dialogue" when panes?.Conversation?.Source == c.Source:
                ctx.Sim.Service<DialogueService>().Close(ctx.Sim, ctx.Entity, "closed");
                break;
            case "vendor" when panes?.Vendor?.Source == c.Source:
                ctx.Sim.Service<VendorService>().Close(ctx.Sim, ctx.Entity, "closed");
                break;
            case "station" when panes?.Station?.Source == c.Source:
                ctx.Sim.Service<StationService>().Close(ctx.Sim, ctx.Entity, "closed");
                break;
        }
    }
}
