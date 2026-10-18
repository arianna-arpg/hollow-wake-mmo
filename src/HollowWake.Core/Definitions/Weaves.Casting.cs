using System.Text.Json.Serialization;
using HollowWake.Core.Attributes;
using HollowWake.Core.Expressions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Definitions;

// Casting fields of a preset: the mechanisms it declares beside its form's, and the same behaviors a glyph
// can carry, folded between the form and the glyphs. Where a weave holds only one of a kind (a bank of
// uses, a spend, a convert, a cycle, a self stack, a combo chain) the first active glyph wins over the
// preset and the preset over the form; triggers, gates, metas, follow-ups and worn modifiers add up, the
// form's first, then the preset's. A preset's own tags join the weave's tags with preset.{id}. A preset may
// also carry the held-mode blocks of its form's cast mode (a channel, a charge-up, a guard, strike timing):
// each replaces the form's block whole, never field by field, so what the preset says is what the weave
// does. The cast mode stays the form's, and validation refuses a block the form's mode would not read.
public sealed partial record WeaveDef
{
    [JsonPropertyName("release_check")]
    public ReleaseCheckSpec? ReleaseCheck { get; init; }

    /// <summary>Mechanisms the preset declares beside its form's.</summary>
    public TagSet Mechanisms { get; init; } = TagSet.Empty;

    /// <summary>Casts the weave when an event of the named trigger kind happens to its caster.</summary>
    public TriggerSpec? Trigger { get; init; }

    /// <summary>Conditions on the caster the weave must meet to be used, beside the form's.</summary>
    public List<Expr> Gates { get; init; } = new();

    /// <summary>Charges the weave requires and spends when it resolves.</summary>
    public SpendSpec? Spend { get; init; }

    /// <summary>A bank of use charges.</summary>
    public UsesSpec? Uses { get; init; }

    /// <summary>While the expression holds, pressing the slot casts another weave instead.</summary>
    public ConvertSpec? Convert { get; init; }

    /// <summary>A meta action run after the form's and before the glyphs'.</summary>
    [JsonPropertyName("meta_action")]
    public MetaSpec? MetaAction { get; init; }

    /// <summary>Modifiers the caster wears while the weave sits in a slot.</summary>
    [JsonPropertyName("equip_modifiers")]
    public List<Modifier> EquipModifiers { get; init; } = new();

    /// <summary>Another weave cast a beat after each use.</summary>
    [JsonPropertyName("follow_up")]
    public FollowUpSpec? FollowUp { get; init; }

    public CycleSpec? Cycle { get; init; }

    [JsonPropertyName("self_stack")]
    public SelfStackSpec? SelfStack { get; init; }

    public ComboSpec? Chain { get; init; }

    /// <summary>The preset's own channel, in place of its form's whole; the form must be a channel.</summary>
    public ChannelSpec? Channel { get; init; }

    /// <summary>The preset's own charge-up, in place of its form's whole; the form must be a charge.</summary>
    public ChargeUpSpec? Charge { get; init; }

    /// <summary>The preset's own guard, in place of its form's whole; the form must be a guard.</summary>
    public GuardSpec? Guard { get; init; }

    /// <summary>The preset's own strike timing, in place of its form's whole; the form must be a cast with a bar.</summary>
    public TimingSpec? Timing { get; init; }
}
