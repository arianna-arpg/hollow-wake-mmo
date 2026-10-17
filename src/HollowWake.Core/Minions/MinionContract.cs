using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Tags;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Minions;

/// <summary>
/// A summon that holds: while its contract runs, the owner carries its reservation (modifiers on
/// <c>{resource}.reserved</c> under <see cref="Source"/>), its weave slot reads <see cref="SlotState"/> as one,
/// and every body of it that falls comes back after the respawn time. Casting the weave again ends it,
/// as do replacing the weave in its slot, an exclusive summon of another group, and the owner leaving.
/// </summary>
public sealed class MinionContract
{
    /// <summary>The weave slot state that reads one while the slot's contract runs.</summary>
    public const string SlotState = "contract";

    public MinionContract(Entity owner, string key, string group)
    {
        Owner = owner;
        Key = key;
        Group = group;
        Source = "contract:" + key;
    }

    public Entity Owner { get; }

    /// <summary>The key of the weave that holds the contract.</summary>
    public string Key { get; }

    public string Group { get; }

    /// <summary>The modifier source of the reservation.</summary>
    public string Source { get; }

    public string? ExclusiveGroup { get; init; }

    /// <summary>The owner's weave slot holding the contract, when the weave was cast from one.</summary>
    public WeaveSlot? Slot { get; init; }

    public CompiledWeave? Weave { get; init; }

    /// <summary>The tags the owner's <c>minion.respawn_time</c> is folded with.</summary>
    public TagSet Context { get; init; } = TagSet.Empty;

    /// <summary>Seconds a fallen body takes to come back, before the owner's <c>minion.respawn_time</c>.</summary>
    public double Respawn { get; init; }

    /// <summary>When the fallen bodies come back, one entry per body.</summary>
    public List<double> Due { get; } = new();

    public bool Ended { get; internal set; }

    internal SummonRecipe Recipe { get; init; } = null!;
}
