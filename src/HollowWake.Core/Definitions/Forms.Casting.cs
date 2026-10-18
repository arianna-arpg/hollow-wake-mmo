using System.Text.Json.Serialization;
using HollowWake.Core.Attributes;
using HollowWake.Core.Expressions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Definitions;

/// <summary>
/// How a weave is committed to. The compiler adds a cast-scope tag from it so modifiers can scope by
/// commitment: <c>cast.instant</c>, <c>cast.timed</c> (a cast with a bar), <c>cast.channel</c> or
/// <c>cast.held</c> (charge-up, overcharge, guard and other held modes).
/// </summary>
public enum CastMode
{
    Instant,
    Cast,
    Channel,
    /// <summary>Hold to charge, release to cast; the release scales by how full the charge was (<see cref="FormDef.Charge"/>).</summary>
    Charge,
    /// <summary>Hold through stages, each banking more; the release scales by the stage reached (<see cref="FormDef.Overcharge"/>).</summary>
    Overcharge,
    /// <summary>A held frontal guard that absorbs hits into its own pool and may bash when it ends (<see cref="FormDef.Guard"/>).</summary>
    Guard,
}

// Casting fields of a form: moving while casting, the cooldown clock, the held modes (channel, charge-up,
// overcharge, guard), strike timing, triggers, use banks, gates, costs in charges, toggles with their
// reservation and upkeep, and the slot behaviors (converts, metas, follow-ups, cycles, self stacks,
// combo chains, modifiers worn while slotted). The weave runtime reads them through the compiled weave.
public sealed partial record FormDef
{
    /// <summary>The fraction of movement speed kept while casting; joins attribute <c>cast.mobility</c>. Null takes rule <c>casting.cast_move</c>.</summary>
    [JsonPropertyName("cast_move")]
    public Expr? CastMove { get; init; }

    /// <summary>When the cooldown clock starts: <c>press</c> or <c>complete</c>. Null takes rule <c>combat.cooldown_at</c>.</summary>
    [JsonPropertyName("cooldown_at")]
    public string? CooldownAt { get; init; }

    public ChannelSpec? Channel { get; init; }

    /// <summary>The charge-up of <see cref="CastMode.Charge"/>.</summary>
    public ChargeUpSpec? Charge { get; init; }

    /// <summary>The stages of <see cref="CastMode.Overcharge"/>.</summary>
    public OverchargeSpec? Overcharge { get; init; }

    /// <summary>The stance of <see cref="CastMode.Guard"/>.</summary>
    public GuardSpec? Guard { get; init; }

    /// <summary>A second press during the cast bar: a golden tail, a random marker, or one hit per press.</summary>
    public TimingSpec? Timing { get; init; }

    /// <summary>Casts this weave when an event of the named trigger kind happens to its caster.</summary>
    public TriggerSpec? Trigger { get; init; }

    /// <summary>A bank of use charges that replaces the cooldown between uses.</summary>
    public UsesSpec? Uses { get; init; }

    /// <summary>Conditions on the caster, each an expression, that must all hold for the weave to be used.</summary>
    public List<Expr> Gates { get; init; } = new();

    /// <summary>Charges the weave requires and spends when it resolves, and what each charge spent adds.</summary>
    public SpendSpec? Spend { get; init; }

    /// <summary>An instant weave that resolves past the caster's own running cast without touching it.</summary>
    public bool Reflex { get; init; }

    /// <summary>Seconds after the weave resolves before its caster may begin another.</summary>
    public Expr? Recovery { get; init; }

    /// <summary>Pressing the weave turns it on; pressing again, starving or running out turns it off.</summary>
    public ToggleSpec? Toggle { get; init; }

    /// <summary>Maximum of each resource held back while a toggle is on: <c>{ mana: "mana.max * 0.2" }</c>.</summary>
    public Dictionary<string, Expr> Reserve { get; init; } = new();

    /// <summary>Resources paid every second while a toggle is on.</summary>
    public UpkeepSpec? Upkeep { get; init; }

    /// <summary>While the expression holds, pressing the slot casts another weave instead.</summary>
    public ConvertSpec? Convert { get; init; }

    /// <summary>The second ability on the weave's slot (<c>meta_action</c>: <c>meta</c> holds free-form tool metadata).</summary>
    [JsonPropertyName("meta_action")]
    public MetaSpec? MetaAction { get; init; }

    /// <summary>Modifiers the caster wears while the weave sits in a slot.</summary>
    [JsonPropertyName("equip_modifiers")]
    public List<Modifier> EquipModifiers { get; init; } = new();

    /// <summary>Another weave cast a beat after each use.</summary>
    [JsonPropertyName("follow_up")]
    public FollowUpSpec? FollowUp { get; init; }

    /// <summary>Effects run every so many uses.</summary>
    public CycleSpec? Cycle { get; init; }

    /// <summary>A pile of stacks that each use grows and that empowers the weave.</summary>
    [JsonPropertyName("self_stack")]
    public SelfStackSpec? SelfStack { get; init; }

    /// <summary>Successive presses within a window walk a sequence of weaves.</summary>
    public ComboSpec? Chain { get; init; }

    /// <summary>What the weave must be aimed at, by a registered target kind (a corpse, a minion), checked before costs are paid.</summary>
    public TargetSpec? Target { get; init; }

    /// <summary>The form's own tree of nodes, bought with points the weave's level grants (<see cref="WeaveSpec.Tree"/> lists what was spent).</summary>
    public TreeSpec? Tree { get; init; }
}

/// <summary>
/// A form's tree (row D23). A weave spends points on its nodes, one per rank: rule
/// <c>casting.tree_points</c>, an expression of the weave's own level, says how many it has, and nothing can
/// be bought below <c>opens_at</c>. A node with <c>links</c> needs one of them bought first; a node without is
/// a root. <c>excludes</c> names nodes it cannot be held with, either way round.
/// </summary>
public sealed record TreeSpec
{
    [JsonPropertyName("opens_at")]
    public int OpensAt { get; init; } = 1;

    public List<TreeNodeSpec> Nodes { get; init; } = new();
}

/// <summary>
/// One node of a form's tree. Each rank bought adds its <c>modifiers</c> once more; from its first rank its
/// <c>patches</c> edit the weave's graph as glyph patches do (after the glyphs', so a socketed glyph's choice
/// wins) and its <c>glyph</c> joins the weave as a graft does, beyond the sockets.
/// </summary>
public sealed record TreeNodeSpec
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public List<string> Links { get; init; } = new();
    public List<string> Excludes { get; init; } = new();
    public int Ranks { get; init; } = 1;
    public List<Modifier> Modifiers { get; init; } = new();
    public List<EffectPatch> Patches { get; init; } = new();
    public GlyphRef? Glyph { get; init; }
}

/// <summary>
/// A form's target requirement: a registered target kind (<c>enemy</c>, <c>ally</c>, <c>any</c>, and the
/// kinds other packages register, such as <c>corpse</c> and <c>minion</c>), an optional per-target condition
/// over <c>target.*</c>, a search radius for a fitting body near the aim when the one given does not fit
/// (the weave's range by default), what to do when none fits (<c>fail</c>, <c>self</c> or <c>aim</c>), and
/// whether the weave consumes what it targets (the kind's owner reads it).
/// </summary>
public sealed record TargetSpec
{
    public required string Kind { get; init; }
    public Expr? Require { get; init; }
    [JsonPropertyName("search_radius")]
    public Expr? SearchRadius { get; init; }
    public string Fallback { get; init; } = "fail";
    public bool Consume { get; init; }
}

// Preset overrides of the casting fields.
public sealed partial record WeaveDef
{
    /// <summary>Overrides the form's <see cref="FormDef.CooldownAt"/>.</summary>
    [JsonPropertyName("cooldown_at")]
    public string? CooldownAt { get; init; }
}

/// <summary>A channel: pulses while held, with windup, jitter, movement, turning, a hold limit, ramps, a release and a persisting field.</summary>
public sealed record ChannelSpec
{
    /// <summary>Seconds between pulses, divided by the speed attribute like cast time.</summary>
    public Expr? Interval { get; init; }
    /// <summary>Seconds before the first pulse, divided by the speed attribute.</summary>
    public Expr? Windup { get; init; }
    /// <summary>Random spread of each interval in seconds, drawn from the cast's random stream.</summary>
    public Expr? Jitter { get; init; }
    /// <summary>The fraction of movement speed kept while channeling.</summary>
    public Expr? Move { get; init; }
    /// <summary>Degrees per second the channeler may turn while it tracks its aim.</summary>
    [JsonPropertyName("turn_rate")]
    public Expr? TurnRate { get; init; }
    /// <summary>Follows the live aim while held (the aim intent, or the focus of a caster without one).</summary>
    [JsonPropertyName("track_aim")]
    public bool TrackAim { get; init; }
    /// <summary>Seconds the channel may be held at most; reaching it releases.</summary>
    [JsonPropertyName("max_hold")]
    public Expr? MaxHold { get; init; }
    /// <summary>Starts the cooldown when the channel ends rather than when it begins.</summary>
    [JsonPropertyName("cooldown_on_end")]
    public bool CooldownOnEnd { get; init; }
    /// <summary>Numbers that grow with held time, as cast-local modifiers built each pulse.</summary>
    public List<RampSpec> Ramps { get; init; } = new();
    /// <summary>A payload when the hold ends.</summary>
    public ReleaseSpec? Release { get; init; }
    /// <summary>Pulses that keep going after the release.</summary>
    public PersistSpec? Persist { get; init; }
    /// <summary>A gauge kept by the slot between channels, filled by holding and spent by releasing.</summary>
    public BrimSpec? Brim { get; init; }
}

/// <summary>One number ramping with held seconds: <c>{ attribute, form, per, max or min, curve, on }</c>.</summary>
public sealed record RampSpec
{
    public required string Attribute { get; init; }
    public ModifierForm Form { get; init; } = ModifierForm.Increased;
    /// <summary>Value per held second, shaped by <see cref="Curve"/>.</summary>
    public double Per { get; init; }
    public double? Max { get; init; }
    public double? Min { get; init; }
    /// <summary>A named curve from the curve registry: <c>linear</c>, <c>quadratic</c>, <c>exponential</c> ...</summary>
    public string Curve { get; init; } = "linear";
    /// <summary><c>weave</c> joins the weave's fold; <c>caster</c> sits on the caster while the channel lasts.</summary>
    public string On { get; init; } = "weave";
    /// <summary>Tags the modifier is scoped to.</summary>
    public TagSet Scope { get; init; } = TagSet.Empty;
}

/// <summary>What ending a hold fires.</summary>
public sealed record ReleaseSpec
{
    /// <summary>Effects run on release; empty runs the form's own graph.</summary>
    public List<TypedSpec> Effects { get; init; } = new();
    /// <summary>Seconds that must be held for the release to fire.</summary>
    [JsonPropertyName("min_hold")]
    public Expr? MinHold { get; init; }
    /// <summary>Fires only on a full hold.</summary>
    [JsonPropertyName("require_full")]
    public bool RequireFull { get; init; }
    /// <summary>Whether the channel's beats run its graph while held; false gathers silently and pays only.</summary>
    public bool Pulses { get; init; } = true;
}

/// <summary>Pulses that outlast a released channel, following the caster: seconds per second held, a cap and a fade.</summary>
public sealed record PersistSpec
{
    /// <summary>Seconds the pulses persist per second the channel was held.</summary>
    [JsonPropertyName("per_held_second")]
    public Expr? PerHeldSecond { get; init; }
    [JsonPropertyName("max_duration")]
    public Expr? MaxDuration { get; init; }
    /// <summary>The damage scale the last persisting pulse keeps, falling linearly from one.</summary>
    public Expr? Fade { get; init; }
}

/// <summary>
/// The brim of a channel: a gauge per slot filled by holding over <c>fill_time</c> (the channel's longest hold
/// without one), decaying at rest by <c>decay</c> per second down to <c>bank_at</c> once reached, fizzling
/// below <c>min_release</c>, spent by a release when <c>spend</c>, released by itself when full with
/// <c>auto_release</c>, and scaling the release's damage from <c>scale.at_empty</c> to <c>scale.at_full</c>
/// along a curve.
/// </summary>
public sealed record BrimSpec
{
    [JsonPropertyName("fill_time")]
    public Expr? FillTime { get; init; }
    public double Decay { get; init; } = 0.2;
    [JsonPropertyName("bank_at")]
    public double BankAt { get; init; }
    [JsonPropertyName("min_release")]
    public double MinRelease { get; init; }
    public bool Spend { get; init; } = true;
    [JsonPropertyName("auto_release")]
    public bool AutoRelease { get; init; }
    public BrimScale Scale { get; init; } = new();
}

public sealed record BrimScale
{
    [JsonPropertyName("at_empty")]
    public double AtEmpty { get; init; } = 1;
    [JsonPropertyName("at_full")]
    public double AtFull { get; init; } = 1;
    public string Curve { get; init; } = "linear";
}

/// <summary>Hold to charge: a maximum charge time, the numbers the fill scales, and how long a full charge holds.</summary>
public sealed record ChargeUpSpec
{
    /// <summary>Seconds to a full charge, divided by the speed attribute like cast time.</summary>
    [JsonPropertyName("max_time")]
    public Expr? MaxTime { get; init; }
    public List<ChargeScale> Scales { get; init; } = new();
    /// <summary>Seconds a full charge is held before it releases by itself. Null takes rule <c>casting.full_hold</c>.</summary>
    [JsonPropertyName("full_hold")]
    public Expr? FullHold { get; init; }
}

/// <summary>One number a charge-up scales, from its value at an empty charge to its value at a full one.</summary>
public sealed record ChargeScale
{
    public required string Attribute { get; init; }
    public ModifierForm Form { get; init; } = ModifierForm.More;
    [JsonPropertyName("at_zero")]
    public double AtZero { get; init; }
    [JsonPropertyName("at_full")]
    public double AtFull { get; init; }
    public TagSet Scope { get; init; } = TagSet.Empty;
}

/// <summary>
/// An overcharge: the bar refills up to a number of stages (the base of attribute <c>overcharge.stages</c>),
/// each taking <c>time</c> seconds and paying <c>cost_per_stage</c>, each banking <c>per_stage</c>
/// modifiers; releasing within <c>spark.window</c> seconds of a stage adds <c>spark.bonus</c> more damage.
/// </summary>
public sealed record OverchargeSpec
{
    public int Stages { get; init; } = 3;
    public Expr? Time { get; init; }
    [JsonPropertyName("per_stage")]
    public List<Modifier> PerStage { get; init; } = new();
    [JsonPropertyName("cost_per_stage")]
    public Dictionary<string, Expr> CostPerStage { get; init; } = new();
    public SparkSpec? Spark { get; init; }
    /// <summary>Seconds the last stage is held before it releases by itself. Null takes rule <c>casting.full_hold</c>.</summary>
    [JsonPropertyName("full_hold")]
    public Expr? FullHold { get; init; }
}

public sealed record SparkSpec
{
    public double Window { get; init; } = 0.2;
    public double Bonus { get; init; } = 0.25;
}

/// <summary>
/// A guard: a frontal <c>arc</c> in degrees that absorbs hits into a <c>pool</c> (scaled by attribute
/// <c>guard.strength</c>), a parry window at the start, a movement factor and turn rate, a longest hold,
/// a bash when it ends, and a weave pulsed while it is held.
/// </summary>
public sealed record GuardSpec
{
    public double Arc { get; init; } = 120;
    public Expr? Pool { get; init; }
    public Expr? Move { get; init; }
    /// <summary>Degrees per second the guard may turn toward the bearer's aim.</summary>
    [JsonPropertyName("turn_rate")]
    public Expr? TurnRate { get; init; }
    /// <summary>Seconds at the start of the guard in which a hit is parried whole.</summary>
    public Expr? Parry { get; init; }
    [JsonPropertyName("max_duration")]
    public Expr? MaxDuration { get; init; }
    /// <summary>A parry ends the guard (and may bash).</summary>
    [JsonPropertyName("end_on_parry")]
    public bool EndOnParry { get; init; }
    public BashSpec? Bash { get; init; }
    public GuardPulseSpec? Pulse { get; init; }
}

/// <summary>The bash a guard fires when it ends: armed by holding <c>arm_time</c> and absorbing <c>threshold</c> of the pool; <c>on_break</c> also fires it when the pool breaks.</summary>
public sealed record BashSpec
{
    public List<TypedSpec> Effects { get; init; } = new();
    /// <summary>The fraction of the pool that must have been absorbed for the bash to fire.</summary>
    public double Threshold { get; init; }
    [JsonPropertyName("arm_time")]
    public Expr? ArmTime { get; init; }
    [JsonPropertyName("on_break")]
    public bool OnBreak { get; init; }
}

/// <summary>A preset weave a guard casts every <c>interval</c> seconds while held (the channel beat of rule <c>casting.channel_interval</c> without one).</summary>
public sealed record GuardPulseSpec
{
    public required string Weave { get; init; }
    public Expr? Interval { get; init; }
}

/// <summary>Strike timing: <c>perfect</c> (a golden tail of the bar), <c>timed</c> (a random marker) or <c>multitude</c> (one hit per press).</summary>
public enum TimingKind
{
    Perfect,
    Timed,
    Multitude,
}

/// <summary>
/// A second press during the cast bar. For <c>perfect</c> and <c>timed</c>, <c>window</c> is the seconds a
/// press may land in and <c>bonus</c> the more damage a flawless press adds; for <c>multitude</c>,
/// <c>window</c> is the most presses that each strike once.
/// </summary>
public sealed record TimingSpec
{
    public TimingKind Kind { get; init; } = TimingKind.Perfect;
    public double Window { get; init; } = 0.2;
    public double Bonus { get; init; } = 0.5;
}

/// <summary>
/// A bank of use charges in the slot: <c>max</c> is the base of attribute <c>uses.max</c>; a spent use
/// comes back after <c>recharge</c> seconds (divided by attribute <c>uses.rate</c>, the weave's cooldown by
/// default). A <c>magazine</c> refills all at once when empty, or one at a time with <c>drip</c>;
/// <c>empower</c> makes uses optional fuel that adds its modifiers; <c>still</c> recovers only while the
/// caster stands still and bleeds uses while it moves; a <c>manual</c> bank never recovers on its own and only
/// <c>restore_uses</c> and converts refill it (munitions reloaded by hand); <c>vent_all</c> spends the whole bank on
/// one cast; <c>steps_from_bank</c> casts once more for every extra use vented.
/// </summary>
public sealed record UsesSpec
{
    public double Max { get; init; } = 3;
    public Expr? Recharge { get; init; }
    public MagazineSpec? Magazine { get; init; }
    public List<Modifier> Empower { get; init; } = new();
    public StillSpec? Still { get; init; }
    public bool Manual { get; init; }
    [JsonPropertyName("vent_all")]
    public bool VentAll { get; init; }
    [JsonPropertyName("steps_from_bank")]
    public bool StepsFromBank { get; init; }
}

public sealed record MagazineSpec
{
    /// <summary>Seconds to refill an empty magazine; null uses the weave's cooldown.</summary>
    public Expr? Refill { get; init; }
    /// <summary>Refill one use at a time over the refill instead of all at its end.</summary>
    public bool Drip { get; init; }
}

public sealed record StillSpec
{
    /// <summary>Uses lost per second of movement.</summary>
    public double Bleed { get; init; }
}

/// <summary>
/// Charges a use requires and spends when it resolves: <c>amount</c> of <c>charge</c> (or <c>all</c>),
/// at least <c>minimum</c>; with <c>optional</c> the weave casts without them. Each spent charge adds
/// its <c>scales</c> as cast-local modifiers, and the count spent is the variable <c>charges_spent</c>.
/// </summary>
public sealed record SpendSpec
{
    public required string Charge { get; init; }
    public double Amount { get; init; } = 1;
    public bool All { get; init; }
    public double Minimum { get; init; }
    public bool Optional { get; init; }
    public List<SpendScale> Scales { get; init; } = new();
}

public sealed record SpendScale
{
    public required string Attribute { get; init; }
    public ModifierForm Form { get; init; } = ModifierForm.More;
    [JsonPropertyName("per_charge")]
    public double PerCharge { get; init; }
    public TagSet Scope { get; init; } = TagSet.Empty;
}

/// <summary>A toggle: effects run when it turns off, and the longest it may stay on.</summary>
public sealed record ToggleSpec
{
    [JsonPropertyName("on_deactivate")]
    public List<TypedSpec> OnDeactivate { get; init; } = new();
    [JsonPropertyName("max_duration")]
    public Expr? MaxDuration { get; init; }
}

/// <summary>Resources paid each second while a toggle is on, growing by <c>ramp</c> of itself per second on.</summary>
public sealed record UpkeepSpec
{
    [JsonPropertyName("per_second")]
    public Dictionary<string, Expr> PerSecond { get; init; } = new();
    public double Ramp { get; init; }
}

/// <summary>While <c>when</c> holds (over the caster and the slot's state as <c>slot.*</c>), pressing the slot casts preset <c>weave</c>.</summary>
public sealed record ConvertSpec
{
    public required Expr When { get; init; }
    public required string Weave { get; init; }
}

/// <summary>A second ability on the slot: a preset weave and the label the slot shows for it.</summary>
public sealed record MetaSpec
{
    public required string Weave { get; init; }
    public string Label { get; init; } = "";
}

/// <summary>A preset weave cast <c>delay</c> seconds after each use, with a chance.</summary>
public sealed record FollowUpSpec
{
    public required string Weave { get; init; }
    public Expr? Delay { get; init; }
    public Expr? Chance { get; init; }
}

/// <summary>Effects run on every <c>count</c>-th use.</summary>
public sealed record CycleSpec
{
    public int Count { get; init; } = 3;
    public List<TypedSpec> Effects { get; init; } = new();
}

/// <summary>
/// A self stack: each use adds a stack up to <c>max</c> and empowers the weave with <c>modifiers</c> per
/// stack held; the pile drops after <c>duration</c> seconds without a use, or peels one stack every
/// <c>decay</c> seconds.
/// </summary>
public sealed record SelfStackSpec
{
    public List<Modifier> Modifiers { get; init; } = new();
    public int Max { get; init; } = 5;
    public double Duration { get; init; } = 4;
    public double Decay { get; init; }
}

/// <summary>A combo chain: presses within <c>window</c> seconds of the last walk <c>steps</c>, preset weaves cast after the slot's own.</summary>
public sealed record ComboSpec
{
    public List<string> Steps { get; init; } = new();
    public double Window { get; init; } = 1.5;
}
