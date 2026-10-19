using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Expressions;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Effects;

internal sealed record ForecastRestrictions(IReadOnlySet<string> Attributes, IReadOnlySet<string> Flags, bool Resources);

/// <summary>
/// Conservative dependency preflight before invoking the real resolver. Checks formulas, modifiers,
/// links, gauges and resource-derived flags without evaluating them. It is not a second attribute fold.
/// Opaque mounted scopes and untrusted function/curve registrations decline observation.
/// </summary>
internal sealed class ForecastInputs : IDisposable
{
    [ThreadStatic] private static ForecastInputs? _spare;
    private EffectContext _context = null!;
    private Stats? _source;
    private Stats? _target;
    private readonly HashSet<(Stats, string, bool, bool, bool)> _visiting = new();
    private int _depth;
    public string? Failure { get; private set; }

    private ForecastInputs() { }

    // Reuse scratch storage, never a prior safety result. A nested query rents a distinct instance.
    // Return clears actor/context references, and only one spare is retained per simulation thread.
    public static ForecastInputs Rent(EffectContext context)
    {
        var inputs = _spare ?? new ForecastInputs();
        _spare = null;
        inputs._context = context;
        inputs._source = context.SourceStats;
        inputs._target = context.TargetStats;
        return inputs;
    }

    public void Dispose()
    {
        _context = null!;
        _source = null;
        _target = null;
        _depth = 0;
        Failure = null;
        _visiting.Clear();
        _spare = this;
    }

    private bool Refuse(string reason)
    {
        Failure ??= reason;
        return false;
    }

    public bool ParameterOrAttribute(Expr? parameter, string attribute) =>
        parameter is null ? WeaveAttribute(attribute) : Expression(parameter);

    public bool WeaveAttribute(string attribute)
    {
        var weave = _context.Weave;
        return weave is null || _source is not { } stats ||
            Attribute(stats, attribute, _context.Tags, weave.Modifiers, weave.Bases.ContainsKey(attribute));
    }

    public bool Expression(Expr? expression) => Expression(expression, null);

    public bool AttributeWithBase(string attribute) => _source is not { } stats ||
        Attribute(stats, attribute, _context.Tags, _context.Weave?.Modifiers, suppliedBase: true);

    private bool Expression(Expr? expression, Stats? owner)
    {
        if (expression is null) return true;
        if (!expression.IsPreviewSafe) return Refuse("random_or_opaque_expression");
        if (++_depth > 64) { _depth--; return Refuse("dependency_depth"); }
        try
        {
            var identifiers = expression.Identifiers;
            for (var i = 0; i < identifiers.Count; i++)
                if (!(owner is null ? EffectName(identifiers[i]) : StatsName(owner, identifiers[i]))) return false;
            var flags = expression.Flags;
            for (var i = 0; i < flags.Count; i++)
                if (!(owner is null ? EffectFlag(flags[i]) : StatsFlag(owner, flags[i]))) return false;
            return true;
        }
        finally { _depth--; }
    }

    private bool EffectName(string name)
    {
        if (name is "depth" or "time" || name == "distance" && _context.SourceTransform is not null && _context.TargetTransform is not null)
            return true;
        if (_context.Vars.ContainsKey(name)) return true;
        if ((name.StartsWith("source.", StringComparison.Ordinal) || name.StartsWith("caster.", StringComparison.Ordinal)) && _source is { } source)
            return StatsName(source, EffectScope.Suffix(name, 6));
        if (name.StartsWith("target.", StringComparison.Ordinal) && _target is { } target)
            return StatsName(target, EffectScope.Suffix(name, 6));
        if (name.StartsWith("weave.", StringComparison.Ordinal) && _context.Weave is not null && _source is not null)
            return name == "weave.level" || WeaveAttribute(EffectScope.Suffix(name, 5));
        return _source is not { } stats || StatsName(stats, name);
    }

    private bool EffectFlag(string flag)
    {
        if ((flag.StartsWith("source.", StringComparison.Ordinal) || flag.StartsWith("caster.", StringComparison.Ordinal)) && _source is { } source)
            return StatsFlag(source, EffectScope.Suffix(flag, 6));
        if (flag.StartsWith("target.", StringComparison.Ordinal) && _target is { } target)
            return StatsFlag(target, EffectScope.Suffix(flag, 6));
        if (flag.StartsWith("weave.", StringComparison.Ordinal) && _context.Weave is not null && _source is not null)
            return true; // The effect scope tests already-compiled tags/mechanisms, not a probe.
        return _source is not { } stats || StatsFlag(stats, flag);
    }

    private bool StatsName(Stats stats, string name)
    {
        if (name is "level" or "xp") return true;
        if (name.EndsWith("_pct", StringComparison.Ordinal))
        {
            var resource = ResourceNames.OfFraction(name);
            if (_context.Sim.Content.Has<ResourceDef>(resource) || stats.Resources.ContainsKey(resource))
                return ResourceStable(stats) && ResourceMaximum(stats, resource);
        }
        if (name.StartsWith("gauge.last_cost.", StringComparison.Ordinal)) return ResourceStable(stats);
        if (name.StartsWith("gauge.", StringComparison.Ordinal) || name.StartsWith("status.", StringComparison.Ordinal) ||
            name.StartsWith("charge.", StringComparison.Ordinal)) return true;
        if (stats.Resources.ContainsKey(name)) return ResourceStable(stats);
        if (stats.Mounted is not null) return Refuse("opaque_mounted_scope");
        return Attribute(stats, name, TagSet.Empty, null);
    }

    private bool StatsFlag(Stats stats, string flag)
    {
        if (ReferenceEquals(stats, _source) && _context.ForecastRestrictions?.Flags.Contains(flag) == true)
            return Refuse("pending_cast_state");
        if (stats.Flags.Contains(flag) || stats.Tags.Has(flag) || flag.StartsWith("recently_", StringComparison.Ordinal) ||
            flag is "combo_varied" or "combo_repeated") return true;
        if (ResourceNames.ConditionOf(flag) is { } condition &&
            (_context.Sim.Content.Has<ResourceDef>(condition.Resource) || condition.Resource is "life" or "mana"))
        {
            if (!ResourceStable(stats) || !ResourceMaximum(stats, condition.Resource) || !Attribute(stats, condition.Line, TagSet.Empty, null)) return false;
        }
        return stats.Mounted is null || Refuse("opaque_mounted_scope");
    }

    private bool ResourceMaximum(Stats stats, string resource)
    {
        var maximum = _context.Sim.Content.TryGet<ResourceDef>(resource, out var def) ? def!.Max : resource + ".max";
        return Attribute(stats, maximum, TagSet.Empty, null) &&
            Attribute(stats, ResourceNames.ReservedOf(resource), TagSet.Empty, null);
    }

    private bool Attribute(Stats stats, string name, TagSet tags, IReadOnlyList<Modifier>? extra, bool suppliedBase = false, bool noLinks = false)
    {
        if (ReferenceEquals(stats, _source) && _context.ForecastRestrictions?.Attributes.Contains(name) == true)
            return Refuse("pending_caster_modifiers");
        var key = (stats, name, extra is not null, suppliedBase, noLinks);
        if (!_visiting.Add(key)) return Refuse("cyclic_attribute_dependency");
        if (++_depth > 64) { _depth--; _visiting.Remove(key); return Refuse("dependency_depth"); }
        try
        {
            var attributes = stats.Attributes;
            if (!suppliedBase && !attributes.TryGetBaseOverride(name, out _) &&
                !Expression(attributes.Catalog.Get(name)?.Formula, stats)) return false;
            foreach (var modifier in attributes.ModifiersFor(name))
                if (!Modifier(modifier)) return false;
            if (extra is not null)
                for (var i = 0; i < extra.Count; i++)
                {
                    var modifier = extra[i];
                    if (modifier.Attribute == name && !Modifier(modifier)) return false;
                }
            return true;

            bool Modifier(Modifier modifier)
            {
                if (!tags.HasAll(modifier.Scope)) return true;
                if (!Expression(modifier.Condition, stats) || !Expression(modifier.ValueExpr, stats)) return false;
                if (modifier.Gauge is { } gauge && !StatsName(stats, gauge)) return false;
                return noLinks || modifier.Form != ModifierForm.Link || string.IsNullOrEmpty(modifier.From) ||
                    Attribute(stats, modifier.From, tags, extra, noLinks: true);
            }
        }
        finally { _depth--; _visiting.Remove(key); }
    }

    private bool ResourceStable(Stats stats) => !ReferenceEquals(stats, _source) ||
        _context.ForecastRestrictions?.Resources != true || Refuse("pending_resource_payment");
}
