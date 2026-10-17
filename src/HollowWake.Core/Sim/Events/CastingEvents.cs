using HollowWake.Core.Ecs;

namespace HollowWake.Core.Sim;

// Events of the cast modes: channel beats and releases, charge-up and overcharge stages, triggered casts,
// repeats, use charges spent and refilled, toggles, guards and their surges, timed presses, metas, rack swaps
// and tree resets. Declared here so no package edits Sim/Events.cs.

/// <summary>A channel beat: the pulse's number from zero and the seconds held when it came.</summary>
public sealed record ChannelPulsed(Entity Caster, string Weave, int Pulse, double Held) : GameEvent;

/// <summary>
/// A held cast ended without being interrupted: a channel, charge-up, overcharge or guard. <c>Fired</c> says
/// whether its release payload ran; <c>Reason</c> is <c>release</c>, <c>max_hold</c>, <c>full</c>, <c>dry</c>,
/// <c>fizzle</c>, <c>break</c> or <c>parry</c>.
/// </summary>
public sealed record WeaveReleased(Entity Caster, string Weave, string Mode, double Held, double Fill, int Stage, bool Fired, string Reason) : GameEvent;

/// <summary>An overcharge reached a stage.</summary>
public sealed record OverchargeStaged(Entity Caster, string Weave, int Stage, int Stages) : GameEvent;

/// <summary>A trigger cast a weave: the kind that fired, at whom, and the effect depth the cast ran at.</summary>
public sealed record WeaveTriggered(Entity Caster, string Weave, string Kind, Entity Target, int Depth) : GameEvent;

/// <summary>A triggered weave's slot was armed or disarmed by its key.</summary>
public sealed record TriggerArmed(Entity Caster, string Weave, bool Armed) : GameEvent;

/// <summary>A use repeated: the repeat's number from one.</summary>
public sealed record WeaveRepeated(Entity Caster, string Weave, int Index, Entity Target) : GameEvent;

/// <summary>A slot's bank of use charges changed.</summary>
public sealed record UsesChanged(Entity Caster, string Weave, double Uses, int Max) : GameEvent;

/// <summary>A toggle turned on or off; <c>Reason</c> is <c>press</c>, <c>starved</c>, <c>expired</c> or <c>death</c>.</summary>
public sealed record WeaveToggled(Entity Caster, string Weave, bool On, string Reason) : GameEvent;

/// <summary>A guard parried a hit whole.</summary>
public sealed record GuardParried(Entity Bearer, Entity Attacker, string Weave) : GameEvent;

/// <summary>A guard's pool broke.</summary>
public sealed record GuardBroken(Entity Bearer, string Weave, double Absorbed) : GameEvent;

/// <summary>An effect surged a raised guard: how much it added and the pool it left.</summary>
public sealed record GuardSurged(Entity Bearer, string Weave, double Amount, double Pool) : GameEvent;

/// <summary>A press during a cast bar: whether a timed press landed (flawless), or a multitude strike.</summary>
public sealed record WeavePressed(Entity Caster, string Weave, int Presses, bool Flawless) : GameEvent;

/// <summary>A meta action of a slot ran: its place in the slot's chain and the preset it cast.</summary>
public sealed record MetaUsed(Entity Caster, string Weave, string Meta, int Index) : GameEvent;

/// <summary>Two rack seats swapped.</summary>
public sealed record WeaveSlotsSwapped(Entity Caster, int A, int B) : GameEvent;
