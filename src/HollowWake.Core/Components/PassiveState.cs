using HollowWake.Core.Ecs;

namespace HollowWake.Core.Components;

/// <summary>
/// A character's passive picks beyond allocated nodes: the options chosen on choice nodes, the realms open to
/// them, the points granted and spent per currency beyond the level curve, and how many refunds they made.
/// Allocated nodes and the main currency's spending stay on <see cref="Stats"/>.
/// </summary>
public sealed class PassiveState : IComponent
{
    /// <summary>Node to the options picked there, in the order they were picked.</summary>
    public Dictionary<string, List<string>> Picks { get; } = new(StringComparer.Ordinal);

    /// <summary>Realms the hero opened (their roots granted).</summary>
    public HashSet<string> OpenRealms { get; } = new(StringComparer.Ordinal);

    /// <summary>Points granted per currency by quests and other rewards, beyond what levels give.</summary>
    public Dictionary<string, int> Granted { get; } = new(StringComparer.Ordinal);

    /// <summary>Points spent per currency other than the main one (which <see cref="Stats.PassivePointsSpent"/> counts).</summary>
    public Dictionary<string, int> Spent { get; } = new(StringComparer.Ordinal);

    /// <summary>Refunds made, which a refund cost formula may read.</summary>
    public int Refunds;

    /// <summary>
    /// Grafts bound to weave slots: the source (a node, or <c>{node}:{option}</c>) to the slot index it is bound to.
    /// A graft the hero owns but has not bound is absent.
    /// </summary>
    public Dictionary<string, int> Grafts { get; } = new(StringComparer.Ordinal);

    /// <summary>The modifier sources the bound grafts put on the hero, so they come off together.</summary>
    internal List<string> GraftSources { get; } = new();

    public int PicksOf(string node) => Picks.TryGetValue(node, out var p) ? p.Count : 0;
}
