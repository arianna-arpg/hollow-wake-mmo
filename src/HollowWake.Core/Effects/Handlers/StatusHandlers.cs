using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Effects.Handlers;

/// <summary>Status application, removal and temporary modifiers.</summary>
public static class StatusHandlers
{
    /// <summary>The hook moments <c>apply_status</c> exposes: effects stamped on the instance it applies.</summary>
    public const string OnStatusTick = "on_status_tick";
    public const string OnStatusExpire = "on_status_expire";

    public static void Register(EffectRegistry r)
    {
        r.Register(new ApplyStatusHandler());
        r.Register(new RemoveStatusHandler());
        r.Register(new ModifyHandler());
    }

    /// <summary>The bodies an effect reaches by <c>to</c>: the target, the source, the source's minions, or the source's allies near the focus.</summary>
    internal static List<Entity> Recipients(EffectContext ctx, string to, double radius, int maxTargets)
    {
        var sim = ctx.Sim;
        var list = new List<Entity>();
        switch (to)
        {
            case "self":
                list.Add(ctx.Source);
                break;
            case "minions":
                foreach (var (e, minion) in sim.World.Query<Minion>())
                {
                    if (minion.Owner == ctx.Source && sim.World.Has<Stats>(e) && !sim.World.Has<Dead>(e))
                    {
                        list.Add(e);
                    }
                }
                list.Sort(static (a, b) => a.Key.CompareTo(b.Key));
                break;
            case "allies":
            {
                var center = sim.World.Get<Transform>(ctx.Source)?.Position ?? ctx.Focus;
                var found = new List<(Entity Entity, float Distance)>();
                foreach (var (e, _, d) in sim.Spatial.Within(center, (float)radius))
                {
                    found.Add((e, d));
                }
                found.Sort(static (a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Entity.Key.CompareTo(b.Entity.Key));
                foreach (var (e, _) in found)
                {
                    if (sim.World.Has<Stats>(e) && !sim.World.Has<Dead>(e) && (e == ctx.Source || !Relations.IsHostile(sim, ctx.Source, e)))
                    {
                        list.Add(e);
                    }
                }
                break;
            }
            default:
                list.Add(ctx.Target.IsNone ? ctx.Source : ctx.Target);
                break;
        }
        if (maxTargets > 0 && list.Count > maxTargets)
        {
            list.RemoveRange(maxTargets, list.Count - maxTargets);
        }
        return list;
    }
}

/// <summary>
/// <c>apply_status</c>: a status by id (or the Thread's own with <c>$thread_status</c>) on the target, the
/// source (<c>self</c>, or <c>to: self</c>), the source's minions (<c>to: minions</c>) or its allies within
/// <c>radius</c> (<c>to: allies</c>). <c>duration</c> is scaled by the applier's effect duration and the bearer's
/// status resistance; <c>fixed_duration</c> is not. Without a <c>magnitude</c>, damaging statuses take theirs
/// from the damage just dealt, or from their baseline when nothing was dealt. Its <c>on_status_tick</c> and
/// <c>on_status_expire</c> lists (where glyph hooks of those names land) ride on the instance it applies.
/// </summary>
internal sealed class ApplyStatusHandler : IEffectHandler
{
    public string Type => "apply_status";

    public EffectSignature Signature { get; } = EffectSignature.Of("status", "duration", "fixed_duration", "chance", "stacks", "magnitude", "threads", "self", "to", "radius", "max_targets")
        .Hook(StatusHandlers.OnStatusTick, StatusHandlers.OnStatusTick)
        .Hook(StatusHandlers.OnStatusExpire, StatusHandlers.OnStatusExpire);

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var self = ctx.BoolOf(p["self"], false);
        var tick = ctx.CompileList(p[StatusHandlers.OnStatusTick], StatusHandlers.OnStatusTick);
        var expire = ctx.CompileList(p[StatusHandlers.OnStatusExpire], StatusHandlers.OnStatusExpire);
        return new Effect
        {
            Type = Type,
            Status = ctx.StringOf(p["status"]) ?? "$thread_status",
            Duration = ctx.ExprOf(p["duration"]),
            FixedDuration = ctx.ExprOf(p["fixed_duration"]),
            Chance = ctx.ExprOf(p["chance"], Expr.Constant(1))!,
            Stacks = ctx.ExprOf(p["stacks"], Expr.Constant(1))!,
            Magnitude = ctx.ExprOf(p["magnitude"]),
            Threads = ctx.ResolveThreads(p["threads"]),
            To = ctx.ChoiceOf(p["to"], "to", self ? "self" : "target", "target", "self", "minions", "allies"),
            Radius = ctx.ExprOf(p["radius"], Expr.Constant(8))!,
            MaxTargets = ctx.IntOf(p["max_targets"], 0),
            Riders = tick.Length == 0 && expire.Length == 0 ? null : new StatusRiders { Tick = tick, Expire = expire },
        };
    }

    private sealed class Effect : CompiledEffect
    {
        public required string Status;
        public Expr? Duration;
        public Expr? FixedDuration;
        public required Expr Chance;
        public required Expr Stacks;
        public Expr? Magnitude;
        public List<string> Threads = new();
        public string To = "target";
        public required Expr Radius;
        public int MaxTargets;
        public StatusRiders? Riders;

        private static readonly Expr One = Expr.Constant(1);

        /// <summary>
        /// Damaging statuses take their magnitude from the damage just dealt through the ailment rules; with
        /// nothing dealt, from their baseline.
        /// </summary>
        private static double DefaultMagnitude(EffectContext ctx, string statusId)
        {
            var dealt = ctx.Vars.TryGetValue("last_damage", out var amount) || ctx.Vars.TryGetValue("amount", out amount);
            if ((!dealt || amount <= 0) && ctx.Sim.Content.Get<StatusDef>(statusId)?.Baseline is { } baseline)
            {
                return baseline.Evaluate(ctx.Scope);
            }
            var scope = new ScopeOverlay(ctx.Scope).Set("amount", dealt ? amount : 0);
            return (HitPlan.For(ctx.Sim.Content.Rules).AilmentMagnitude(statusId) ?? One).Evaluate(scope);
        }

        public override void Execute(EffectContext ctx)
        {
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
            var duration = FixedDuration is not null ? ctx.Eval(FixedDuration) : Duration is null ? (double?)null : ctx.Eval(Duration);
            var app = new StatusApplication
            {
                Duration = duration,
                FixedDuration = FixedDuration is not null,
                Stacks = Math.Max(1, (int)Math.Round(ctx.Eval(Stacks, 1))),
                Magnitude = (Magnitude is null ? DefaultMagnitude(ctx, statusId) : ctx.Eval(Magnitude)) * ctx.Potency,
                Potency = ctx.Potency,
                SourceKey = ctx.Weave?.Key ?? "effect",
                Tags = ctx.Tags,
                Depth = ctx.Depth,
                Riders = Riders,
            };
            if (To == "target")
            {
                service.Apply(ctx.Sim, ctx.Target.IsNone ? ctx.Source : ctx.Target, statusId, ctx.Source, app);
                return;
            }
            foreach (var who in StatusHandlers.Recipients(ctx, To, ctx.Eval(Radius, 8), MaxTargets))
            {
                service.Apply(ctx.Sim, who, statusId, ctx.Source, app);
            }
        }
    }
}

/// <summary>
/// <c>remove_status</c>: a named status, or statuses carrying any of <c>tags</c> (any status when none are
/// given), up to <c>count</c>, newest first unless <c>order: oldest</c>. Without a name a removal is a cleanse
/// and takes only harmful statuses; <c>kind: beneficial</c> purges buffs, <c>kind: any</c> takes both.
/// </summary>
internal sealed class RemoveStatusHandler : IEffectHandler
{
    public string Type => "remove_status";

    public EffectSignature Signature { get; } = EffectSignature.Of("status", "tags", "count", "self", "order", "kind");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Status = ctx.StringOf(p["status"]),
        Tags = ctx.TagsOf(p["tags"]),
        Count = ctx.IntOf(p["count"], int.MaxValue),
        OnSelf = ctx.BoolOf(p["self"], false),
        OldestFirst = ctx.ChoiceOf(p["order"], "order", "newest", "newest", "oldest") == "oldest",
        Kind = ctx.ChoiceOf(p["kind"], "kind", "harmful", "harmful", "beneficial", "any") switch
        {
            "beneficial" => StatusKind.Beneficial,
            "any" => StatusKind.Any,
            _ => StatusKind.Harmful,
        },
    };

    private sealed class Effect : CompiledEffect
    {
        public string? Status;
        public TagSet Tags = TagSet.Empty;
        public int Count = int.MaxValue;
        public bool OnSelf;
        public bool OldestFirst;
        public StatusKind Kind = StatusKind.Harmful;

        public override void Execute(EffectContext ctx)
        {
            var target = OnSelf || ctx.Target.IsNone ? ctx.Source : ctx.Target;
            ctx.Sim.Service<StatusService>().Remove(ctx.Sim, target, Status, Tags, Count, OldestFirst, Kind);
        }
    }
}

/// <summary>
/// <c>modify</c>: one temporary modifier on the target (or the source with <c>self</c>) for <c>duration</c>
/// seconds, under a source keyed by <c>key</c> (or the weave and attribute). Applying it again replaces the
/// value and restarts the time: each application takes a new lease and only the latest one's timer removes it.
/// </summary>
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
