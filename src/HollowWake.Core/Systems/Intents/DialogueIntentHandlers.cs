using HollowWake.Core.Intents;

namespace HollowWake.Core.Systems.Intents;

/// <summary>Choosing lines in a conversation, ending it, and closing the world panes a conversation or an entity opened.</summary>
public static class DialogueIntentHandlers
{
    public static void Register(IntentHandlers handlers)
    {
        handlers.Register<ChooseDialogueIntent>(static (in IntentContext ctx, ChooseDialogueIntent _) => ctx.Reject("not_ready"));
        handlers.Register<CloseDialogueIntent>(static (in IntentContext _, CloseDialogueIntent _) => { });
        handlers.Register<ClosePaneIntent>(static (in IntentContext _, ClosePaneIntent _) => { });
    }
}
