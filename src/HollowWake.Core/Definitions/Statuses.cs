using System.Text.Json.Serialization;
using HollowWake.Core.Expressions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Definitions;

// Status fields beyond the core: hooks fed by the bearer's triggers, consumable stacks, banks, pools,
// per-stack clocks, baselines and identity. The status service reads every field here.
public sealed partial record StatusDef
{
    /// <summary>Stacks spent when the bearer's trigger event happens (a hit, a crit, a kill ...), filtered by tags.</summary>
    [JsonPropertyName("consume_on")]
    public ConsumeOnSpec? ConsumeOn { get; init; }

    /// <summary>Effects the bearer's next landed hits carry, run against the struck target.</summary>
    [JsonPropertyName("on_bearer_hit")]
    public List<TypedSpec> OnBearerHit { get; init; } = new();

    /// <summary>Effects run when a hit lands on the bearer, with the striker as source (an impale's bank discharging).</summary>
    [JsonPropertyName("on_bearer_struck")]
    public List<TypedSpec> OnBearerStruck { get; init; } = new();

    /// <summary>
    /// Effects run when the status is applied again over a live instance, before the new application takes
    /// over: a pop. They read <c>remaining_damage</c> (what the instance still had to deal) and <c>pop</c> (the
    /// applier's <c>status.{id}.pop</c>).
    /// </summary>
    [JsonPropertyName("on_reapply")]
    public List<TypedSpec> OnReapply { get; init; } = new();

    /// <summary>Effects run once when the bank grows to at least the bearer's remaining life: doom detonating early.</summary>
    [JsonPropertyName("on_bank_lethal")]
    public List<TypedSpec> OnBankLethal { get; init; } = new();

    /// <summary>A standing bank of damage: fed by applications (a scalding bank) or by what the bearer takes (an iron ward).</summary>
    public BankSpec? Bank { get; init; }

    /// <summary>Whether stacks share one timer (the default) or each stack keeps its own.</summary>
    [JsonPropertyName("stack_clock")]
    public StackClock StackClock { get; init; } = StackClock.Shared;

    /// <summary>The magnitude used when an application has no hit to take one from (fields, zones, procs), read on the applier.</summary>
    public Expr? Baseline { get; init; }

    /// <summary>The first applier keeps the credit when others refresh or stack the status; by default the newest takes over.</summary>
    [JsonPropertyName("keep_source")]
    public bool KeepSource { get; init; }

    /// <summary>A status with a pool this one depends on: when that status ends, this one ends with it.</summary>
    [JsonPropertyName("bound_to")]
    public string? BoundTo { get; init; }

    /// <summary>
    /// An absorption pool the status carries, read on the applier when applied (with <c>magnitude</c> and
    /// <c>potency</c>). Rule <c>combat.pools</c> decides where status pools absorb; an emptied pool ends the status.
    /// </summary>
    public Expr? Pool { get; init; }
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

/// <summary>How a status's stacks keep time.</summary>
public enum StackClock
{
    /// <summary>One timer for the instance; applications refresh it.</summary>
    Shared,
    /// <summary>Each stack keeps its own timer and falls off alone.</summary>
    Independent,
}

/// <summary>What feeds a status's bank.</summary>
public enum BankSource
{
    /// <summary>Each application adds the damage it would deal over its duration; ticks draw the bank down.</summary>
    Applications,
    /// <summary>A fraction of the damage the bearer takes while the status lasts.</summary>
    Taken,
}

/// <summary>
/// A status's bank: <c>{ from, cap, fraction }</c>. Fed by applications, the bank is capped at <c>cap</c>
/// times the strongest single application and ticks deal it out over the remaining time; fed by damage
/// taken, it banks <c>fraction</c> of each hit up to <c>cap</c> (an expression on the bearer). Effects read
/// it as <c>banked</c>, and <c>rupture</c> spends it.
/// </summary>
public sealed record BankSpec
{
    public BankSource From { get; init; } = BankSource.Applications;
    public Expr? Cap { get; init; }
    public Expr? Fraction { get; init; }
}
