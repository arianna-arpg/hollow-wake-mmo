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

/// <summary>Status application, removal and temporary modifiers.</summary>
public static class StatusHandlers
{
    public static void Register(EffectRegistry r)
    {
        r.Register(new ApplyStatusHandler());
        r.Register(new RemoveStatusHandler());
        r.Register(new ModifyHandler());
    }
}

internal sealed class ApplyStatusHandler : IEffectHandler
{
    public string Type => "apply_status";

    public EffectSignature Signature { get; } = EffectSignature.Of("status", "duration", "chance", "stacks", "magnitude", "threads", "self");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Status = ctx.StringOf(p["status"]) ?? "$thread_status",
        Duration = ctx.ExprOf(p["duration"]),
        Chance = ctx.ExprOf(p["chance"], Expr.Constant(1))!,
        Stacks = ctx.ExprOf(p["stacks"], Expr.Constant(1))!,
        Magnitude = ctx.ExprOf(p["magnitude"]),
        Threads = ctx.ResolveThreads(p["threads"]),
        OnSelf = ctx.BoolOf(p["self"], false),
    };

    private sealed class Effect : CompiledEffect
    {
        public required string Status;
        public Expr? Duration;
        public required Expr Chance;
        public required Expr Stacks;
        public Expr? Magnitude;
        public List<string> Threads = new();
        public bool OnSelf;

        /// <summary>Damaging statuses take their magnitude from the damage just dealt through the ailment rules.</summary>
        private static double DefaultMagnitude(EffectContext ctx, string statusId)
        {
            var amount = ctx.Vars.GetValueOrDefault("last_damage", ctx.Vars.GetValueOrDefault("amount", 0));
            var scope = new ScopeOverlay(ctx.Scope).Set("amount", amount);
            return ctx.Sim.Content.Rules.Eval("combat.ailment_magnitude." + statusId, "1", scope);
        }

        public override void Execute(EffectContext ctx)
        {
            var target = OnSelf || ctx.Target.IsNone ? ctx.Source : ctx.Target;
            var statusId = Status;
            if (statusId == "$thread_status")
            {
                var thread = Threads.FirstOrDefault();
                statusId = thread is null ? null : ctx.Sim.Content.Get<ThreadDef>(thread)?.Status;
                if (statusId is null)
                {
                    return;
                }
            }
            if (!ctx.Rng.Chance(ctx.Eval(Chance, 1)))
            {
                return;
            }
            var service = ctx.Sim.Service<StatusService>();
            service.Apply(ctx.Sim, target, statusId, ctx.Source, new StatusApplication
            {
                Duration = Duration is null ? null : ctx.Eval(Duration),
                Stacks = Math.Max(1, (int)Math.Round(ctx.Eval(Stacks, 1))),
                Magnitude = (Magnitude is null ? DefaultMagnitude(ctx, statusId) : ctx.Eval(Magnitude)) * ctx.Potency,
                Potency = ctx.Potency,
                SourceKey = ctx.Weave?.Key ?? "effect",
                Tags = ctx.Tags,
                Depth = ctx.Depth,
            });
        }
    }
}

internal sealed class RemoveStatusHandler : IEffectHandler
{
    public string Type => "remove_status";

    public EffectSignature Signature { get; } = EffectSignature.Of("status", "tags", "count", "self");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Status = ctx.StringOf(p["status"]),
        Tags = ctx.TagsOf(p["tags"]),
        Count = ctx.IntOf(p["count"], int.MaxValue),
        OnSelf = ctx.BoolOf(p["self"], false),
    };

    private sealed class Effect : CompiledEffect
    {
        public string? Status;
        public TagSet Tags = TagSet.Empty;
        public int Count = int.MaxValue;
        public bool OnSelf;

        public override void Execute(EffectContext ctx)
        {
            var target = OnSelf || ctx.Target.IsNone ? ctx.Source : ctx.Target;
            ctx.Sim.Service<StatusService>().Remove(ctx.Sim, target, Status, Tags, Count);
        }
    }
}

internal sealed class ModifyHandler : IEffectHandler
{
    public string Type => "modify";

    public EffectSignature Signature { get; } = EffectSignature.Of("attribute", "form", "value", "duration", "scope", "self", "key");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var form = ctx.StringOf(p["form"]) ?? "flat";
        return new Effect
        {
            Type = Type,
            Attribute = ctx.StringOf(p["attribute"]) ?? throw new InvalidOperationException("modify needs 'attribute'"),
            Form = Enum.Parse<Attributes.ModifierForm>(form, ignoreCase: true),
            Value = ctx.ExprOf(p["value"], Expr.Constant(0))!,
            Duration = ctx.ExprOf(p["duration"], Expr.Constant(5))!,
            Scope = ctx.TagsOf(p["scope"]),
            OnSelf = ctx.BoolOf(p["self"], false),
            Key = ctx.StringOf(p["key"]),
        };
    }

    private sealed class Effect : CompiledEffect
    {
        public required string Attribute;
        public Attributes.ModifierForm Form;
        public required Expr Value;
        public required Expr Duration;
        public TagSet Scope = TagSet.Empty;
        public bool OnSelf;
        public string? Key;

        public override void Execute(EffectContext ctx)
        {
            var target = OnSelf || ctx.Target.IsNone ? ctx.Source : ctx.Target;
            if (ctx.Sim.World.Get<Stats>(target) is not { } stats)
            {
                return;
            }
            var source = "temp:" + (Key ?? $"{ctx.Weave?.Key ?? "effect"}:{Attribute}");
            stats.Attributes.RemoveSource(source);
            stats.Attributes.Add(new Attributes.Modifier { Attribute = Attribute, Form = Form, Value = ctx.Eval(Value) * ctx.Potency, Scope = Scope, Source = source });
            var duration = ctx.Eval(Duration, 5);
            // Each application takes a new lease on the source; a timer removes the modifier only while its
            // own lease is the latest, so refreshing a temporary modifier gives it its full time again.
            var lease = stats.Lease(source);
            var sim = ctx.Sim;
            sim.Scheduler.After(duration, () =>
            {
                if (sim.World.IsAlive(target) && stats.EndLease(source, lease))
                {
                    stats.Attributes.RemoveSource(source);
                }
            });
        }
    }
}



