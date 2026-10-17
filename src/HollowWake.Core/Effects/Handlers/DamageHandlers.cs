using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
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
/// hit. Sets <c>last_damage</c> and <c>amount</c> to what landed for the effects after it.
/// </summary>
internal sealed class DamageHandler : IEffectHandler
{
    public string Type => "damage";

    public EffectSignature Signature { get; } = EffectSignature.Of("amount", "amounts", "threads", "tags", "can_crit", "effectiveness", "cannot_kill", "leech")
        .Hook(EffectHooks.OnKill, "on_kill");

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
            Effectiveness = ctx.ExprOf(p["effectiveness"]),
            CannotKill = ctx.BoolOf(p["cannot_kill"], false),
            Leech = ctx.ExprOf(p["leech"]),
            OnKill = ctx.CompileList(p["on_kill"], "on_kill"),
        };
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
        public CompiledEffect[] OnKill = Array.Empty<CompiledEffect>();

        // The rule's default thread as a one-element list, kept while the rule text stays the same.
        private string? _defaultThread;
        private List<string>? _defaultThreads;

        public override void Execute(EffectContext ctx)
        {
            if (ctx.Target.IsNone)
            {
                return;
            }
            // The amount first, then its effectiveness, in the order their formulas draw random numbers.
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

/// <summary><c>heal</c>: an amount folded through the healer's <c>healing</c>; <c>exclude_self</c> skips the healer.</summary>
internal sealed class HealHandler : IEffectHandler
{
    public string Type => "heal";

    public EffectSignature Signature { get; } = EffectSignature.Of("amount", "resource", "tags", "exclude_self");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Amount = ctx.ExprOf(p["amount"], Expr.Constant(10))!,
        Resource = ctx.StringOf(p["resource"]) ?? "life",
        Tags = ctx.TagsOf(p["tags"]),
        ExcludeSelf = ctx.BoolOf(p["exclude_self"], false),
    };

    private sealed class Effect : CompiledEffect
    {
        public required Expr Amount;
        public string Resource = "life";
        public TagSet Tags = TagSet.Empty;
        public bool ExcludeSelf;

        public override void Execute(EffectContext ctx)
        {
            var target = ctx.Target.IsNone ? ctx.Source : ctx.Target;
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
            var healed = stats.AddResource(Resource, amount);
            if (healed > 0)
            {
                stats.Stamp("healed", ctx.Sim.Clock.Time);
                ctx.Sim.Events.Publish(new Healed(ctx.Source, target, healed, Resource) { Tick = ctx.Sim.Clock.Tick, Depth = ctx.Depth });
            }
        }
    }
}

internal sealed class ResourceHandler : IEffectHandler
{
    public string Type => "resource";

    public EffectSignature Signature { get; } = EffectSignature.Of("amount", "resource");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Amount = ctx.ExprOf(p["amount"], Expr.Constant(10))!,
        Resource = ctx.StringOf(p["resource"]) ?? "mana",
    };

    private sealed class Effect : CompiledEffect
    {
        public required Expr Amount;
        public string Resource = "mana";

        public override void Execute(EffectContext ctx)
        {
            var target = ctx.Target.IsNone ? ctx.Source : ctx.Target;
            ctx.Sim.World.Get<Stats>(target)?.AddResource(Resource, ctx.Eval(Amount) * ctx.Potency);
        }
    }
}
