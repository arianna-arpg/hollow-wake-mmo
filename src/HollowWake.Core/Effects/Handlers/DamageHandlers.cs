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

internal sealed class DamageHandler : IEffectHandler
{
    public string Type => "damage";

    public EffectSignature Signature { get; } = EffectSignature.Of("amount", "threads", "tags", "can_crit", "effectiveness")
        .Hook(EffectHooks.OnKill, "on_kill");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Amount = ctx.ExprOf(p["amount"], Expr.Constant(10))!,
        Threads = ctx.ResolveThreads(p["threads"]),
        Tags = ctx.TagsOf(p["tags"]),
        CanCrit = ctx.BoolOf(p["can_crit"], true),
        Effectiveness = ctx.ExprOf(p["effectiveness"]),
        OnKill = ctx.CompileList(p["on_kill"], "on_kill"),
    };

    private sealed class Effect : CompiledEffect
    {
        public required Expr Amount;
        public required List<string> Threads;
        public TagSet Tags = TagSet.Empty;
        public bool CanCrit = true;
        public Expr? Effectiveness;
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
            var amount = ctx.Eval(Amount) * ctx.Potency;
            if (Effectiveness is not null)
            {
                amount *= ctx.Eval(Effectiveness, 1);
            }
            if (ctx.DamageScale != 1)
            {
                amount *= ctx.DamageScale;
            }
            var threads = Threads.Count > 0 ? Threads : DefaultThreads(ctx);
            var packet = DamagePipeline.Build(ctx, amount, threads, Tags, CanCrit);
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

internal sealed class HealHandler : IEffectHandler
{
    public string Type => "heal";

    public EffectSignature Signature { get; } = EffectSignature.Of("amount", "resource", "tags");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Amount = ctx.ExprOf(p["amount"], Expr.Constant(10))!,
        Resource = ctx.StringOf(p["resource"]) ?? "life",
        Tags = ctx.TagsOf(p["tags"]),
    };

    private sealed class Effect : CompiledEffect
    {
        public required Expr Amount;
        public string Resource = "life";
        public TagSet Tags = TagSet.Empty;

        public override void Execute(EffectContext ctx)
        {
            var target = ctx.Target.IsNone ? ctx.Source : ctx.Target;
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
