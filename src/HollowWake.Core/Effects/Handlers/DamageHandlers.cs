using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Effects.Handlers;

/// <summary>Damage, healing and resource effects.</summary>
public static class DamageHandlers
{
    public static void Register(EffectRegistry r)
    {
        r.Register(new DamageHandler());
        r.Register(new HealHandler());
        r.Register(new ResourceHandler());
    }
}

/// <summary>
/// <c>damage</c>: <c>amount</c> split over <c>threads</c>, plus per-Thread <c>amounts</c> (<c>{ ember: expr, stone:
/// expr }</c>) for mixed bases; <c>effectiveness</c> scales the amount and the attacker's added damage;
/// <c>cannot_kill</c> leaves the target a point of life; <c>leech</c> adds to the attacker's life leech for this
/// hit. An effect whose formulas read var <c>roll</c> draws it once before anything else, the position within the
/// amount's range (<c>lerp(lo, hi, roll)</c> draws exactly as <c>lerp(lo, hi, rand())</c> would), and the hit
/// carries it. Sets <c>last_damage</c>, <c>amount</c> and <c>roll</c> for the effects after it.
/// </summary>
internal sealed class DamageHandler : IEffectHandler
{
    public string Type => "damage";

    public EffectSignature Signature { get; } = EffectSignature.Of("amount", "amounts", "threads", "tags", "can_crit", "effectiveness", "cannot_kill", "leech")
        .Hook(EffectHooks.OnKill, "on_kill")
        .Hook("after_damage", "after_damage");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var amounts = new List<(string, Expr)>();
        if (p["amounts"] is JsonObject map)
        {
            foreach (var (thread, value) in map)
            {
                amounts.Add((thread, ctx.ExprOf(value, Expr.Constant(0))!));
            }
        }
        else if (p["amounts"] is not null)
        {
            throw new InvalidOperationException($"'amounts' must map Threads to amounts in {ctx.Owner}.");
        }
        // Per-Thread amounts alone mean no split amount: an absent amount is nothing rather than the default ten.
        var amount = p["amount"] is null && amounts.Count > 0 ? Expr.Constant(0) : ctx.ExprOf(p["amount"], Expr.Constant(10))!;
        var effectiveness = ctx.ExprOf(p["effectiveness"]);
        var leech = ctx.ExprOf(p["leech"]);
        var rolls = ReadsRoll(amount) || ReadsRoll(effectiveness) || ReadsRoll(leech);
        foreach (var (_, share) in amounts)
        {
            rolls |= ReadsRoll(share);
        }
        return new Effect
        {
            Type = Type,
            Amount = amount,
            Amounts = amounts.ToArray(),
            Shares = new (string, double)[amounts.Count],
            Threads = p["threads"] is null && amounts.Count > 0 ? new List<string>() : ctx.ResolveThreads(p["threads"]),
            Splits = !(p["threads"] is null && amounts.Count > 0),
            Tags = ctx.TagsOf(p["tags"]),
            CanCrit = ctx.BoolOf(p["can_crit"], true),
            Effectiveness = effectiveness,
            CannotKill = ctx.BoolOf(p["cannot_kill"], false),
            Leech = leech,
            Rolls = rolls,
            OnKill = ctx.CompileList(p["on_kill"], "on_kill"),
            AfterDamage = ctx.CompileList(p["after_damage"], "after_damage"),
        };
    }

    /// <summary>The var a damage effect draws once when its formulas read it: the position within the amount's range.</summary>
    internal const string Roll = "roll";

    private static bool ReadsRoll(Expr? expr)
    {
        if (expr is null)
        {
            return false;
        }
        foreach (var name in expr.Identifiers)
        {
            if (name == Roll)
            {
                return true;
            }
        }
        return false;
    }

    private sealed class Effect : CompiledEffect
    {
        public required Expr Amount;
        public (string Thread, Expr Amount)[] Amounts = Array.Empty<(string, Expr)>();
        // Per-Thread amounts are evaluated into this buffer and read by the pipeline before anything can run again.
        public (string, double)[] Shares = Array.Empty<(string, double)>();
        public required List<string> Threads;
        public bool Splits = true;
        public TagSet Tags = TagSet.Empty;
        public bool CanCrit = true;
        public Expr? Effectiveness;
        public bool CannotKill;
        public Expr? Leech;
        public bool Rolls;
        public CompiledEffect[] OnKill = Array.Empty<CompiledEffect>();
        public CompiledEffect[] AfterDamage = Array.Empty<CompiledEffect>();

        // The rule's default thread as a one-element list, kept while the rule text stays the same.
        private string? _defaultThread;
        private List<string>? _defaultThreads;

        public override void Execute(EffectContext ctx)
        {
            if (ctx.Target.IsNone)
            {
                return;
            }
            // The roll first when a formula reads it, then the amount, then its effectiveness, in the order their
            // formulas draw random numbers.
            double? roll = null;
            if (Rolls)
            {
                roll = ctx.Scope.NextRandom();
                ctx.Vars[DamageHandler.Roll] = roll.Value;
            }
            var amount = ctx.Eval(Amount) * ctx.Potency;
            double? effectiveness = null;
            if (Effectiveness is not null)
            {
                effectiveness = ctx.Eval(Effectiveness, 1);
                amount *= effectiveness.Value;
            }
            if (ctx.DamageScale != 1)
            {
                amount *= ctx.DamageScale;
            }
            var shares = Shares;
            for (var i = 0; i < Amounts.Length; i++)
            {
                var share = ctx.Eval(Amounts[i].Amount) * ctx.Potency * (effectiveness ?? 1);
                shares[i] = (Amounts[i].Thread, ctx.DamageScale != 1 ? share * ctx.DamageScale : share);
            }
            var threads = Threads.Count > 0 || !Splits ? Threads : DefaultThreads(ctx);
            var packet = DamagePipeline.Build(ctx, amount, threads, shares, Tags, CanCrit, effectiveness);
            packet.CannotKill = CannotKill;
            packet.Roll = roll;
            if (Leech is not null)
            {
                packet.Leech = Math.Max(0, ctx.Eval(Leech));
            }
            var result = DamagePipeline.Apply(ctx, packet);
            ctx.Vars["last_damage"] = result.Total;
            ctx.Vars["amount"] = result.Total;
            if (OnKill.Length > 0 && result.Killed)
            {
                ExecuteAll(OnKill, ctx.Deeper());
            }
            // Payload riders run for each positive packet on its actual recipient, at payload depth.
            // Keep the parent result stable even if kill hooks or these riders deal more damage.
            if (AfterDamage.Length > 0 && result.Total > 0)
                ExecuteAll(AfterDamage, ctx.WithVars(v =>
                {
                    v["last_damage"] = result.Total;
                    v["amount"] = result.Total;
                }));
        }

        private List<string> DefaultThreads(EffectContext ctx)
        {
            var thread = ctx.Sim.Content.Rules.Text("combat.default_thread", "stone");
            if (_defaultThreads is null || !string.Equals(_defaultThread, thread, StringComparison.Ordinal))
            {
                _defaultThread = thread;
                _defaultThreads = new List<string> { thread };
            }
            return _defaultThreads;
        }
    }
}

/// <summary>
/// <c>heal</c>: an amount folded through the healer's <c>healing</c>, on the target (the healer when there is none);
/// <c>self</c> heals the healer instead, as a status tick that heals whoever applied it; <c>exclude_self</c> skips
/// the healer.
/// </summary>
internal sealed class HealHandler : IEffectHandler
{
    public string Type => "heal";

    public EffectSignature Signature { get; } = EffectSignature.Of("amount", "resource", "tags", "self", "exclude_self")
        .Checking((p, content) => LethalResourceGuard.Validate(p, content, "life"));

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Amount = ctx.ExprOf(p["amount"], Expr.Constant(10))!,
        Resource = ctx.StringOf(p["resource"]) ?? "life",
        Tags = ctx.TagsOf(p["tags"]),
        Self = ctx.BoolOf(p["self"], false),
        ExcludeSelf = ctx.BoolOf(p["exclude_self"], false),
    };

    private sealed class Effect : CompiledEffect
    {
        public required Expr Amount;
        public string Resource = "life";
        public TagSet Tags = TagSet.Empty;
        public bool Self;
        public bool ExcludeSelf;
        private readonly LethalResourceGuard _guard = new();

        public override void Execute(EffectContext ctx)
        {
            var target = Self || ctx.Target.IsNone ? ctx.Source : ctx.Target;
            if (ExcludeSelf && target == ctx.Source)
            {
                return;
            }
            if (ctx.Sim.World.Get<Stats>(target) is not { } stats || ctx.Sim.World.Has<Dead>(target))
            {
                return;
            }
            var amount = ctx.Eval(Amount) * ctx.Potency;
            if (ctx.SourceStats is { } src)
            {
                amount = src.Attributes.Apply("healing", amount, src, ctx.Tags.Union(Tags), ctx.Weave?.Modifiers);
            }
            // The final fold can turn a positive base into a drain. Check after potency and every modifier.
            if (_guard.Refuses(ctx, Type, Resource, amount))
            {
                return;
            }
            var healed = stats.AddResource(Resource, amount);
            if (healed > 0)
            {
                stats.Stamp("healed", ctx.Sim.Clock.Time);
                ctx.Sim.Events.Publish(new Healed(ctx.Source, target, healed, Resource) { Tick = ctx.Sim.Clock.Tick, Depth = ctx.Depth });
            }
        }
    }
}

/// <summary>
/// <c>resource</c>: adds an amount of a resource to the target (the source when there is none); <c>self</c> gives it
/// to the source. It never lowers a lethal resource (life): a body loses life only through the damage ladder, so a
/// decrease of it is a <c>damage</c> effect. Validation refuses a literal one, and one a formula reaches is refused
/// when it runs and logged once per weave.
/// </summary>
internal sealed class ResourceHandler : IEffectHandler
{
    private const string DefaultResource = "mana";

    public string Type => "resource";

    public EffectSignature Signature { get; } = EffectSignature.Of("amount", "resource", "self")
        .Checking((p, content) => LethalResourceGuard.Validate(p, content, DefaultResource));

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Amount = ctx.ExprOf(p["amount"], Expr.Constant(10))!,
        Resource = ctx.StringOf(p["resource"]) ?? DefaultResource,
        Self = ctx.BoolOf(p["self"], false),
    };

    private sealed class Effect : CompiledEffect
    {
        public required Expr Amount;
        public string Resource = DefaultResource;
        public bool Self;
        private readonly LethalResourceGuard _guard = new();

        public override void Execute(EffectContext ctx)
        {
            var target = Self || ctx.Target.IsNone ? ctx.Source : ctx.Target;
            if (ctx.Sim.World.Get<Stats>(target) is not { } stats)
            {
                return;
            }
            var amount = ctx.Eval(Amount) * ctx.Potency;
            if (!_guard.Refuses(ctx, Type, Resource, amount))
            {
                stats.AddResource(Resource, amount);
            }
        }
    }
}

/// <summary>Direct resource writers share the content-defined lethal-resource boundary.</summary>
internal sealed class LethalResourceGuard
{
    // Allocated only for an actual refusal, once per compiled effect; content is shared across simulations.
    private ConcurrentDictionary<string, bool>? _refused;

    private static bool Lethal(ContentRegistry content, string resource) => content.Get<ResourceDef>(resource)?.Lethal == true;

    /// <summary>Refuses an amount written as a negative number, or a formula that is one, of a lethal resource.</summary>
    public static string? Validate(JsonObject p, ContentRegistry content, string defaultResource)
    {
        var resource = JsonNum.StringOf(p["resource"]) ?? defaultResource;
        if (!Lethal(content, resource) || p["amount"] is not { } amount)
        {
            return null;
        }
        double value;
        if (JsonNum.StringOf(amount) is { } text)
        {
            if (!Expr.TryParse(text, out var expr, out _) || !expr!.IsConstant)
            {
                return null;
            }
            value = expr.ConstantValue;
        }
        else if (!JsonNum.TryDouble(amount, out value))
        {
            return null;
        }
        return value < 0
            ? $"lowers {resource} by {(-value).ToString("0.###", CultureInfo.InvariantCulture)}, but {resource} falls only through the damage ladder: write a 'damage' effect instead"
            : null;
    }

    public bool Refuses(EffectContext ctx, string type, string resource, double amount)
    {
        if (!(amount < 0) || !Lethal(ctx.Sim.Content, resource))
        {
            return false;
        }
        var weave = ctx.Weave?.Key ?? "none";
        var refused = LazyInitializer.EnsureInitialized(ref _refused, () => new ConcurrentDictionary<string, bool>(StringComparer.Ordinal));
        if (refused.TryAdd(weave, true))
        {
            ctx.Sim.Log.Warn($"the {type} effect of weave '{weave}' would lower {resource} by {(-amount).ToString("0.###", CultureInfo.InvariantCulture)}; refused, since {resource} falls only through the damage ladder: a decrease of it is a damage effect");
        }
        return true;
    }
}
