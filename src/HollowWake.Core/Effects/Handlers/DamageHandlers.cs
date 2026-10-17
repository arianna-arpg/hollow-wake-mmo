using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;
using HollowWake.Core.Tags;
using static HollowWake.Core.Effects.Handlers.EffectHelpers;

namespace HollowWake.Core.Effects.Handlers;

// Damage, healing and resource effects.
internal sealed class DamageHandler : IEffectHandler
{
    public string Type => "damage";

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Amount = ctx.ExprOf(p["amount"], Expr.Constant(10))!,
        Threads = ctx.ResolveThreads(p["threads"]),
        Tags = ctx.TagsOf(p["tags"]),
        CanCrit = ctx.BoolOf(p["can_crit"], true),
        Effectiveness = ctx.ExprOf(p["effectiveness"]),
    };

    private sealed class Effect : CompiledEffect
    {
        public required Expr Amount;
        public required List<string> Threads;
        public TagSet Tags = TagSet.Empty;
        public bool CanCrit = true;
        public Expr? Effectiveness;

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
            var threads = Threads.Count > 0 ? Threads : new List<string> { ctx.Sim.Content.Rules.Text("combat.default_thread", "stone") };
            var packet = DamagePipeline.Build(ctx, amount, threads, Tags, CanCrit);
            var result = DamagePipeline.Apply(ctx, packet);
            ctx.Vars["last_damage"] = result.Total;
            ctx.Vars["amount"] = result.Total;
        }
    }
}

internal sealed class HealHandler : IEffectHandler
{
    public string Type => "heal";

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
                ctx.Sim.Events.Publish(new Healed(ctx.Source, target, healed, Resource) { Tick = ctx.Sim.Clock.Tick });
            }
        }
    }
}

internal sealed class ResourceHandler : IEffectHandler
{
    public string Type => "resource";

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



