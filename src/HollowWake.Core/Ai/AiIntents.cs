using HollowWake.Core.Intents;
using HollowWake.Core.Systems;

namespace HollowWake.Core.Ai;

/// <summary>What became of an intent an actor sent: accepted, or refused with the handler's reason.</summary>
public readonly record struct IntentOutcome(string? Refusal)
{
    /// <summary>True when the handler took the intent.</summary>
    public bool Accepted => Refusal is null;
}

/// <summary>An intent a think sent that was refused, and the handler's reason, kept for the think's decision record.</summary>
public readonly record struct AiRefusal(IIntent Intent, string Reason);

/// <summary>
/// How the AI acts: through the intents a player sends, validated by the same handlers, so an AI body can do
/// nothing a player cannot and every rule a handler checks applies to it. An intent is dispatched at once,
/// during the think, through the simulation's <see cref="IntentHandlers"/> with a context built for the
/// actor; it is never queued, since a queued intent would act a tick late, after the input phase. A refusal
/// comes back as the outcome and is recorded on the think's context (<see cref="AiContext.Refusals"/>); it is
/// not published, so no client hears of an AI's refusals.
/// </summary>
public static class AiIntents
{
    /// <summary>A stop carries nothing, so every stop sends this one.</summary>
    public static readonly StopIntent Stop = new();

    public static IntentOutcome Send(AiContext c, IIntent intent)
    {
        c.BeginSend();
        var refusal = c.Handlers.Dispatch(new IntentContext(c.Sim, c.Entity, intent, c));
        if (refusal is not null)
        {
            ((IIntentSender)c).Refused(intent, refusal);
        }
        return new IntentOutcome(c.SendRefusal);
    }
}
