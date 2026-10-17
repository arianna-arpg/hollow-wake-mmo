namespace HollowWake.Core.Intents;

/// <summary>Allocates a passive node; on a choice node, <see cref="Option"/> names the option picked from the node's deal.</summary>
public sealed record AllocatePassiveIntent(string NodeId) : IIntent
{
    public string? Option { get; init; }
}

/// <summary>Refunds one allocated node at a font within reach, with its picks; the points return to their own currency.</summary>
public sealed record RefundPassiveIntent(string NodeId) : IIntent;
