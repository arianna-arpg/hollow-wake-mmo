using System.Text.Json.Serialization;
using Skein.Core.Expressions;
using Skein.Core.Tags;

namespace Skein.Core.Attributes;

/// <summary>
/// How a modifier combines. The engine applies every modifier stack in one fixed order:
/// <c>(base + flat + links) * (1 + sum of increased) * product of (1 + more)</c>, then override, then clamp.
/// Content composes all numeric behavior out of these forms and tag scopes.
/// </summary>
public enum ModifierForm
{
    /// <summary>Adds to the base before any multiplication.</summary>
    Flat,
    /// <summary>Additive percentage; all "increased" and "reduced" modifiers sum together.</summary>
    Increased,
    /// <summary>Multiplicative percentage; each "more" or "less" multiplies separately.</summary>
    More,
    /// <summary>Replaces the result outright; the highest priority wins.</summary>
    Override,
    /// <summary>
    /// Gains <see cref="Modifier.Value"/> times another attribute as flat value on this one
    /// ("gain 40% of life regeneration as thorns"). Links are single hop: the source is read with
    /// links disabled, so chains and cycles cannot compound.
    /// </summary>
    Link,
}

/// <summary>
/// One contribution to one attribute. The same record describes a passive node, an affix, a status,
/// a glyph on a weave, a decree on a holding or a zone rule, which is what lets every system share
/// one modifier engine.
/// </summary>
public record Modifier
{
    /// <summary>Attribute id this modifies, for example <c>damage</c>, <c>life.max</c>, <c>projectile.count</c>.</summary>
    public required string Attribute { get; init; }

    public ModifierForm Form { get; init; } = ModifierForm.Flat;

    /// <summary>Constant value. Percentages are fractions: 0.2 means 20%. For links, the ratio.</summary>
    public double Value { get; init; }

    /// <summary>Optional formula evaluated against the owner at query time. Wins over <see cref="Value"/>.</summary>
    [JsonPropertyName("value_expr")]
    public Expr? ValueExpr { get; init; }

    /// <summary>For <see cref="ModifierForm.Link"/>: the attribute read from.</summary>
    public string? From { get; init; }

    /// <summary>
    /// Tags the query context must contain for this modifier to apply. "20% increased Ember spell damage"
    /// is a modifier on <c>damage</c> scoped to <c>[ember spell]</c>; it applies only when the thing
    /// being computed carries both tags. Victim-facing scopes use the <c>vs.</c> prefix: <c>[vs.burning]</c>.
    /// </summary>
    public TagSet Scope { get; init; } = TagSet.Empty;

    /// <summary>Optional condition on the owner, for example <c>"life_pct &lt; 0.35"</c> or <c>"has(\"burning\")"</c>.</summary>
    public Expr? Condition { get; init; }

    /// <summary>
    /// Optional live quantity read from the owner scope that scales the value per unit
    /// ("2% increased damage per poison stack" reads <c>status.poison.stacks</c>). A zero gauge makes
    /// the modifier inert. With <see cref="GaugeAt"/> it becomes a threshold instead of a scale.
    /// </summary>
    public string? Gauge { get; init; }

    /// <summary>With <see cref="Gauge"/>: apply the full value only while the gauge reads at least this.</summary>
    [JsonPropertyName("gauge_at")]
    public double? GaugeAt { get; init; }

    /// <summary>Who granted it. Used for display and for removing everything from one source at once.</summary>
    public string Source { get; init; } = "";

    /// <summary>Ordering among overrides; higher wins.</summary>
    public int Priority { get; init; }

    public Modifier WithSource(string source) => this with { Source = source };

    public Modifier Scaled(double factor) => ValueExpr is null ? this with { Value = Value * factor } : this;

    public override string ToString()
    {
        var v = ValueExpr?.Source ?? (Form is ModifierForm.Increased or ModifierForm.More or ModifierForm.Link
            ? $"{Value * 100:+0.##;-0.##}%"
            : Value.ToString("+0.##;-0.##"));
        var scope = Scope.IsEmpty ? "" : " " + Scope;
        var from = Form == ModifierForm.Link ? $" of {From}" : "";
        return $"{v} {Form.ToString().ToLowerInvariant()} {Attribute}{from}{scope}";
    }
}
