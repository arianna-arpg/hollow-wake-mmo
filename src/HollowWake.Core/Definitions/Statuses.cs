using System.Text.Json.Serialization;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Definitions;

// Status fields beyond the core: consumable stacks and next-hit riders. Parsed and validated here; the
// status service reads them as the status mechanics land.
public sealed partial record StatusDef
{
    /// <summary>Stacks spent when the bearer's trigger event happens (a hit, a crit, a kill ...), filtered by tags.</summary>
    [JsonPropertyName("consume_on")]
    public ConsumeOnSpec? ConsumeOn { get; init; }

    /// <summary>Effects the bearer's next landed hits carry, run against the struck target.</summary>
    [JsonPropertyName("on_bearer_hit")]
    public List<TypedSpec> OnBearerHit { get; init; } = new();
}

/// <summary>When a status spends its stacks: <c>{ event, tags, count }</c>, with <c>event</c> a trigger kind.</summary>
public sealed record ConsumeOnSpec
{
    public required string Event { get; init; }
    /// <summary>The event must carry all of these tags (a melee hit, a spell crit).</summary>
    public TagSet Tags { get; init; } = TagSet.Empty;
    /// <summary>Stacks spent per event.</summary>
    public int Count { get; init; } = 1;
}
