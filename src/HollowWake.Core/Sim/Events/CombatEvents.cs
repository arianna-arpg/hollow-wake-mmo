using System.Globalization;
using HollowWake.Core.Ecs;

namespace HollowWake.Core.Sim;

// Events of the damage pipeline and statuses beyond the core: leech paid, pools broken and recharged,
// deaths cheated, statuses ruptured and consumed, procs fired and refused, charges gained, spent and lost.
// Declared here so no package edits Sim/Events.cs.

/// <summary>
/// A combat happening worth a line in a trace. Every event of this file derives from it, so a tool prints all
/// of them with one subscription and a new one needs no tool change.
/// </summary>
public abstract record CombatNote : GameEvent
{
    /// <summary>The effect depth it happened at, as damage and status events carry it.</summary>
    public int Depth { get; init; }

    /// <summary>One line, with bodies named by <paramref name="name"/>.</summary>
    public abstract string Describe(Func<Entity, string> name);

    protected static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}

/// <summary>
/// An attacker regained a resource from a hit it landed. <see cref="Kind"/> is <c>leech</c> (a fraction of the
/// damage), <c>hit</c> (a flat gain per hit) or <c>kill</c> (a flat gain per kill).
/// </summary>
public sealed record LeechPaid(Entity Source, Entity Target, string Resource, double Amount, string Kind) : CombatNote
{
    public override string Describe(Func<Entity, string> name) => Kind switch
    {
        "leech" => $"{name(Source)} leeched {Num(Amount)} {Resource} from {name(Target)}",
        "kill" => $"{name(Source)} gained {Num(Amount)} {Resource} killing {name(Target)}",
        _ => $"{name(Source)} gained {Num(Amount)} {Resource} hitting {name(Target)}",
    };
}

/// <summary>A pool before life (a ward, a shield) or a break bar (poise) was emptied by a hit.</summary>
public sealed record PoolBroken(Entity Entity, Entity By, string Resource) : CombatNote
{
    public override string Describe(Func<Entity, string> name) => $"{name(By)} broke {name(Entity)}'s {Resource}";
}

/// <summary>A pool that recharges began refilling after its delay.</summary>
public sealed record PoolRecharging(Entity Entity, string Resource, double From) : CombatNote
{
    public override string Describe(Func<Entity, string> name) => $"{name(Entity)}'s {Resource} recharging from {Num(From)}";
}

/// <summary>A pool that recharges is full again.</summary>
public sealed record PoolRestored(Entity Entity, string Resource, double Amount) : CombatNote
{
    public override string Describe(Func<Entity, string> name) => $"{name(Entity)}'s {Resource} restored to {Num(Amount)}";
}

/// <summary>A blow that would have killed left the body standing.</summary>
public sealed record DeathCheated(Entity Entity, Entity By) : CombatNote
{
    public override string Describe(Func<Entity, string> name) => $"{name(Entity)} cheated death against {name(By)}";
}

/// <summary>Part of a hit was deferred into a status that deals it over time.</summary>
public sealed record HitStaggered(Entity Entity, Entity By, double Amount) : CombatNote
{
    public override string Describe(Func<Entity, string> name) => $"{name(Entity)} staggered {Num(Amount)} of {name(By)}'s hit";
}

/// <summary>A status's banked or remaining damage burst at once.</summary>
public sealed record StatusRuptured(Entity Source, Entity Target, string Status, double Amount) : CombatNote
{
    public override string Describe(Func<Entity, string> name) => $"{name(Source)} ruptured {Status} on {name(Target)} for {Num(Amount)}";
}

/// <summary>Stacks of a status were spent: consumed by its trigger, by an effect, or moved to another body.</summary>
public sealed record StatusConsumed(Entity Bearer, Entity By, string Status, int Stacks, string Reason) : CombatNote
{
    public override string Describe(Func<Entity, string> name) => $"{name(Bearer)} spent {Stacks} {Status} ({Reason})";
}

/// <summary>A proc rolled and ran its effects.</summary>
public sealed record ProcFired(Entity Owner, string Proc, Entity Target) : CombatNote
{
    public override string Describe(Func<Entity, string> name) => $"{name(Owner)} proc {Proc} on {name(Target)} at depth {Depth}";
}

/// <summary>A proc that would have rolled was refused: too deep in a chain.</summary>
public sealed record ProcRefused(Entity Owner, string Proc, string Reason) : CombatNote
{
    public override string Describe(Func<Entity, string> name) => $"{name(Owner)} proc {Proc} refused at depth {Depth}: {Reason}";
}

/// <summary>Charges were banked.</summary>
public sealed record ChargeGained(Entity Holder, string Charge, double Amount, double Total) : CombatNote
{
    public override string Describe(Func<Entity, string> name) => $"{name(Holder)} gained {Num(Amount)} {Charge} ({Num(Total)})";
}

/// <summary>Charges were spent by a cast or an effect.</summary>
public sealed record ChargeSpent(Entity Holder, string Charge, double Amount, double Total) : CombatNote
{
    public override string Describe(Func<Entity, string> name) => $"{name(Holder)} spent {Num(Amount)} {Charge} ({Num(Total)})";
}

/// <summary>Charges ran out: they decayed after their duration or a drain burned them down.</summary>
public sealed record ChargeLost(Entity Holder, string Charge, double Amount, double Total, string Reason) : CombatNote
{
    public override string Describe(Func<Entity, string> name) => $"{name(Holder)} lost {Num(Amount)} {Charge} ({Num(Total)}, {Reason})";
}
