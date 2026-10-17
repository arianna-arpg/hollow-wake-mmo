using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;

namespace HollowWake.Core.Components;

// Quests and the player. Weaves and casting live in Weaves.cs, items in Inventory.cs, AI state in Ai.cs
// and minions in Minions.cs.

public enum QuestState
{
    Offered,
    Active,
    Complete,
    TurnedIn,
    Failed,
}

public sealed class QuestProgress
{
    public required string QuestId;
    public QuestState State = QuestState.Active;
    public Dictionary<string, int> Counts = new(StringComparer.Ordinal);
    public double StartedAt;
    public QuestDef? Generated;
}

public sealed class QuestLog : IComponent
{
    public Dictionary<string, QuestProgress> Quests { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Completed { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Discovered { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, double> Reputation { get; } = new(StringComparer.Ordinal);
}

/// <summary>A player-controlled entity and its pending intents.</summary>
public sealed class Player : IComponent
{
    public required string AccountId;
    public required string CharacterId;
    public Queue<Intents.IIntent> Intents { get; } = new();
    public double LastInputAt;
    public double RespawnAt = double.NegativeInfinity;
}
