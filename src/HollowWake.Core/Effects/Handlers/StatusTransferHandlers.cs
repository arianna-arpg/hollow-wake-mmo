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

/// <summary>
/// Effects that move statuses between bodies or spend them: consuming, spreading, moving and rupturing
/// (<c>consume_status</c>, <c>spread_status</c>, <c>move_status</c>, <c>rupture</c>).
/// </summary>
public static class StatusTransferHandlers
{
    public static void Register(EffectRegistry r)
    {
        r.Register(new ConsumeStatusHandler());
        r.Register(new SpreadStatusHandler());
        r.Register(new MoveStatusHandler());
        r.Register(new RuptureHandler());
    }

    /// <summary>Which statuses a transfer reads: by id (<c>status</c> or <c>statuses</c>) or by any of <c>tags</c>.</summary>
    internal sealed class Selector
    {
        public string[] Ids = Array.Empty<string>();
        public TagSet Tags = TagSet.Empty;
        public StatusKind Kind = StatusKind.Harmful;

        public static Selector Of(JsonObject p, EffectCompileContext ctx)
        {
            var ids = new List<string>();
            if (ctx.StringOf(p["status"]) is { } one)
            {
                ids.Add(one);
            }
            if (p["statuses"] is JsonArray many)
            {
                foreach (var item in many)
                {
                    if (ctx.StringOf(item) is { } id)
                    {
                        ids.Add(id);
                    }
                }
            }
            return new Selector
            {
                Ids = ids.ToArray(),
                Tags = ctx.TagsOf(p["tags"]),
                Kind = ctx.ChoiceOf(p["kind"], "kind", "harmful", "harmful", "beneficial", "any") switch
                {
                    "beneficial" => StatusKind.Beneficial,
                    "any" => StatusKind.Any,
                    _ => StatusKind.Harmful,
                },
            };
        }

        public bool Matches(Simulation sim, StatusInstance s)
        {
            if (s.Removed)
            {
                return false;
            }
            if (Ids.Length > 0)
            {
                return Array.IndexOf(Ids, s.Id) >= 0;
            }
            var def = sim.Content.Get<StatusDef>(s.Id);
            if (def is null || (!Tags.IsEmpty && !def.Tags.HasAny(Tags)))
            {
                return false;
            }
            return Kind switch
            {
                StatusKind.Harmful => !def.Beneficial,
                StatusKind.Beneficial => def.Beneficial,
                _ => true,
            };
        }

        /// <summary>The matching instances on a body, newest first.</summary>
        public List<StatusInstance> On(Simulation sim, Entity body)
        {
            var list = new List<StatusInstance>();
            if (sim.World.TryGet<Statuses>(body, out var statuses))
            {
                for (var i = statuses.Active.Count - 1; i >= 0; i--)
                {
                    if (Matches(sim, statuses.Active[i]))
                    {
                        list.Add(statuses.Active[i]);
                    }
                }
            }
            return list;
        }
    }

    /// <summary>Bodies within a radius of a point passing a relation filter to <paramref name="relativeTo"/>, nearest first.</summary>
    internal static List<Entity> Near(Simulation sim, System.Numerics.Vector3 center, double radius, Entity relativeTo, string filter, Entity exclude, int max)
    {
        var found = new List<(Entity Entity, float Distance)>();
        foreach (var (e, _, d) in sim.Spatial.Within(center, (float)radius))
        {
            found.Add((e, d));
        }
        found.Sort(static (a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance) : a.Entity.Key.CompareTo(b.Entity.Key));
        var list = new List<Entity>();
        foreach (var (e, _) in found)
        {
            if (e == exclude || !sim.World.Has<Stats>(e) || sim.World.Has<Dead>(e))
            {
                continue;
            }
            var hostile = Relations.IsHostile(sim, relativeTo, e);
            var keep = filter switch
            {
                "enemies" => hostile,
                "allies" => !hostile,
                _ => true,
            };
            if (!keep)
            {
                continue;
            }
            list.Add(e);
            if (max > 0 && list.Count >= max)
            {
                break;
            }
        }
        return list;
    }

    /// <summary>The Thread a status's damage belongs to: the Thread whose ailment it is, else one of its tags, else the default.</summary>
    internal static string ThreadOf(Simulation sim, string status)
    {
        foreach (var t in sim.Content.Each<ThreadDef>())
        {
            if (t.Status == status)
            {
                return t.Id;
            }
        }
        if (sim.Content.Get<StatusDef>(status) is { } def)
        {
            foreach (var tag in def.Tags)
            {
                if (sim.Content.Has<ThreadDef>(tag))
                {
                    return tag;
                }
            }
        }
        return sim.Content.Rules.Text("combat.default_thread", "stone");
    }
}

/// <summary>
/// <c>consume_status</c>: spends up to <c>count</c> stacks (all by default) of the selected statuses on the
/// target (or the source with <c>self</c>), newest first, and sets the variable named by <c>as</c> (default
/// <c>consumed</c>) to the stacks spent and <c>{as}_damage</c> to the damage they still had to deal, for the
/// effects after it (a shatter that hits harder per stack).
/// </summary>
internal sealed class ConsumeStatusHandler : IEffectHandler
{
    public string Type => "consume_status";

    public EffectSignature Signature { get; } = EffectSignature.Of("status", "statuses", "tags", "kind", "count", "as", "self");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var name = ctx.StringOf(p["as"]) ?? "consumed";
        return new Effect
        {
            Type = Type,
            Select = StatusTransferHandlers.Selector.Of(p, ctx),
            Count = ctx.IntOf(p["count"], int.MaxValue),
            As = name,
            AsDamage = name + "_damage",
            OnSelf = ctx.BoolOf(p["self"], false),
        };
    }

    private sealed class Effect : CompiledEffect
    {
        public required StatusTransferHandlers.Selector Select;
        public int Count = int.MaxValue;
        public required string As;
        public required string AsDamage;
        public bool OnSelf;

        public override void Execute(EffectContext ctx)
        {
            var sim = ctx.Sim;
            EffectVars.Set(ctx, As, 0);
            EffectVars.Set(ctx, AsDamage, 0);
            if (!OnSelf && ctx.Target.IsNone)
            {
                return;
            }
            var bearer = OnSelf ? ctx.Source : ctx.Target;
            var service = sim.Service<StatusService>();
            var left = Count;
            var stacks = 0;
            var damage = 0.0;
            foreach (var s in Select.On(sim, bearer))
            {
                if (left <= 0)
                {
                    break;
                }
                var def = sim.Content.Get<StatusDef>(s.Id);
                var take = Math.Min(left, s.Stacks);
                if (def is not null && s.Stacks > 0)
                {
                    damage += StatusService.RemainingDamage(s, def) * take / s.Stacks;
                }
                var spent = service.Consume(sim, bearer, s, take, ctx.Source, "consumed");
                stacks += spent;
                left -= spent;
            }
            EffectVars.Set(ctx, As, stacks);
            EffectVars.Set(ctx, AsDamage, damage);
        }
    }
}

/// <summary>
/// <c>spread_status</c>: copies the selected statuses from the target (the struck body) to up to
/// <c>max_targets</c> bodies within <c>radius</c> of it that pass <c>filter</c> (its allies by default), with
/// <c>strength</c> times the magnitude, keeping the time each had left (<c>duration: remaining</c>) or starting
/// it afresh (<c>refresh</c>). The status's applier keeps the credit. Sets <c>spread</c> to the copies made.
/// </summary>
internal sealed class SpreadStatusHandler : IEffectHandler
{
    public string Type => "spread_status";

    public EffectSignature Signature { get; } = EffectSignature.Of("status", "statuses", "tags", "kind", "radius", "strength", "duration", "max_targets", "filter");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Select = StatusTransferHandlers.Selector.Of(p, ctx),
        Radius = ctx.ExprOf(p["radius"], Expr.Constant(4))!,
        Strength = ctx.ExprOf(p["strength"], Expr.Constant(1))!,
        Remaining = ctx.ChoiceOf(p["duration"], "duration", "remaining", "remaining", "refresh") == "remaining",
        MaxTargets = ctx.IntOf(p["max_targets"], 0),
        Filter = ctx.ChoiceOf(p["filter"], "filter", "allies", "allies", "enemies", "any"),
    };

    private sealed class Effect : CompiledEffect
    {
        public required StatusTransferHandlers.Selector Select;
        public required Expr Radius;
        public required Expr Strength;
        public bool Remaining = true;
        public int MaxTargets;
        public string Filter = "allies";

        public override void Execute(EffectContext ctx)
        {
            var sim = ctx.Sim;
            EffectVars.Set(ctx, "spread", 0);
            var bearer = ctx.Target;
            if (bearer.IsNone || sim.World.Get<Transform>(bearer) is not { } t)
            {
                return;
            }
            var spreading = Select.On(sim, bearer);
            if (spreading.Count == 0)
            {
                EffectVars.Set(ctx, "spread", 0);
                return;
            }
            var strength = ctx.Eval(Strength, 1);
            var service = sim.Service<StatusService>();
            var copies = 0;
            foreach (var other in StatusTransferHandlers.Near(sim, t.Position, ctx.Eval(Radius, 4), bearer, Filter, bearer, MaxTargets))
            {
                foreach (var s in spreading)
                {
                    var applier = sim.World.IsAlive(s.Source) ? s.Source : ctx.Source;
                    var applied = service.Apply(sim, other, s.Id, applier, new StatusApplication
                    {
                        Duration = Remaining ? Math.Max(0.05, s.Remaining) : null,
                        FixedDuration = Remaining,
                        Magnitude = s.Magnitude * strength,
                        Stacks = Math.Max(1, s.Stacks),
                        Potency = s.Values.GetValueOrDefault("potency", 1),
                        BankAmount = s.Values.TryGetValue("bank", out var bank) ? bank * strength : null,
                        SourceKey = "spread",
                        Tags = ctx.Tags,
                        Depth = ctx.Depth + 1,
                    });
                    if (applied is not null)
                    {
                        copies++;
                    }
                }
            }
            EffectVars.Set(ctx, "spread", copies);
        }
    }
}

/// <summary>
/// <c>move_status</c>: takes the selected statuses off bodies and lays them on another, keeping their time,
/// strength and stacks. <c>from</c> is the target, the source (<c>self</c>) or every body within <c>radius</c> of
/// the source passing <c>filter</c> (<c>area</c>: a siphon draws afflictions from allies onto the caster);
/// <c>to</c> is the source (<c>self</c>) or the target (a transfusion), with copies on bodies within <c>splash</c>
/// of it that are hostile to the source. Moved onto the source a status keeps its applier; moved onto others the
/// source becomes the applier. At most <c>count</c> instances move. Sets <c>moved</c> to how many did.
/// </summary>
internal sealed class MoveStatusHandler : IEffectHandler
{
    public string Type => "move_status";

    public EffectSignature Signature { get; } = EffectSignature.Of("status", "statuses", "tags", "kind", "from", "to", "radius", "filter", "count", "splash");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx) => new Effect
    {
        Type = Type,
        Select = StatusTransferHandlers.Selector.Of(p, ctx),
        From = ctx.ChoiceOf(p["from"], "from", "target", "target", "self", "area"),
        To = ctx.ChoiceOf(p["to"], "to", "self", "self", "target"),
        Radius = ctx.ExprOf(p["radius"], Expr.Constant(6))!,
        Filter = ctx.ChoiceOf(p["filter"], "filter", "allies", "allies", "enemies", "any"),
        Count = ctx.IntOf(p["count"], int.MaxValue),
        Splash = ctx.ExprOf(p["splash"]),
    };

    private sealed class Effect : CompiledEffect
    {
        public required StatusTransferHandlers.Selector Select;
        public string From = "target";
        public string To = "self";
        public required Expr Radius;
        public string Filter = "allies";
        public int Count = int.MaxValue;
        public Expr? Splash;

        public override void Execute(EffectContext ctx)
        {
            var sim = ctx.Sim;
            var destination = To == "self" || ctx.Target.IsNone ? ctx.Source : ctx.Target;
            var bodies = new List<Entity>();
            switch (From)
            {
                case "self":
                    bodies.Add(ctx.Source);
                    break;
                case "area":
                    if (sim.World.Get<Transform>(ctx.Source) is { } st)
                    {
                        bodies.AddRange(StatusTransferHandlers.Near(sim, st.Position, ctx.Eval(Radius, 6), ctx.Source, Filter, destination, 0));
                    }
                    break;
                default:
                    if (!ctx.Target.IsNone)
                    {
                        bodies.Add(ctx.Target);
                    }
                    break;
            }
            var splashed = new List<Entity>();
            if (Splash is not null && sim.World.Get<Transform>(destination) is { } dt)
            {
                splashed.AddRange(StatusTransferHandlers.Near(sim, dt.Position, ctx.Eval(Splash), ctx.Source, "enemies", destination, 0));
            }
            var service = sim.Service<StatusService>();
            var moved = 0;
            foreach (var body in bodies)
            {
                if (body == destination)
                {
                    continue;
                }
                foreach (var s in Select.On(sim, body))
                {
                    if (moved >= Count)
                    {
                        break;
                    }
                    var applier = destination == ctx.Source && sim.World.IsAlive(s.Source) ? s.Source : ctx.Source;
                    var app = new StatusApplication
                    {
                        Duration = Math.Max(0.05, s.Remaining),
                        FixedDuration = true,
                        Magnitude = s.Magnitude,
                        Stacks = Math.Max(1, s.Stacks),
                        Potency = s.Values.GetValueOrDefault("potency", 1),
                        BankAmount = s.Values.TryGetValue("bank", out var bank) ? bank : null,
                        SourceKey = "moved",
                        Tags = ctx.Tags,
                        Depth = ctx.Depth + 1,
                    };
                    sim.Events.Publish(new StatusConsumed(body, ctx.Source, s.Id, s.Stacks, "moved") { Tick = sim.Clock.Tick, Depth = ctx.Depth });
                    service.Detach(sim, body, s);
                    service.Apply(sim, destination, s.Id, applier, app);
                    foreach (var other in splashed)
                    {
                        service.Apply(sim, other, s.Id, applier, app);
                    }
                    moved++;
                }
            }
            EffectVars.Set(ctx, "moved", moved);
        }
    }
}

/// <summary>
/// <c>rupture</c>: spends <c>fraction</c> of each instance of <c>status</c> on the target (or the source with
/// <c>self</c>) as damage now, times <c>scale</c>: a bank's banked damage, or the damage the status still had to
/// deal (whose time shrinks by the same fraction). The burst is damage over time arriving at once: the status's
/// Thread, tagged <c>rupture</c>, <c>dot</c> and <c>status.{id}</c>, unfolded by the attacker's damage again.
/// Sets <c>ruptured</c> to what landed.
/// </summary>
internal sealed class RuptureHandler : IEffectHandler
{
    public string Type => "rupture";

    public EffectSignature Signature { get; } = EffectSignature.Of("status", "fraction", "scale", "self");

    public CompiledEffect Compile(JsonObject p, EffectCompileContext ctx)
    {
        var status = ctx.StringOf(p["status"]) ?? throw new InvalidOperationException($"rupture needs 'status' in {ctx.Owner}.");
        return new Effect
        {
            Type = Type,
            Status = status,
            Fraction = ctx.ExprOf(p["fraction"], Expr.Constant(1))!,
            Scale = ctx.ExprOf(p["scale"], Expr.Constant(1))!,
            OnSelf = ctx.BoolOf(p["self"], false),
            Tags = TagSet.Of("rupture", "dot", "status", "status." + status),
        };
    }

    private sealed class Effect : CompiledEffect
    {
        public required string Status;
        public required Expr Fraction;
        public required Expr Scale;
        public bool OnSelf;
        public required TagSet Tags;

        public override void Execute(EffectContext ctx)
        {
            var sim = ctx.Sim;
            EffectVars.Set(ctx, "ruptured", 0);
            if (!OnSelf && ctx.Target.IsNone)
            {
                return;
            }
            var bearer = OnSelf ? ctx.Source : ctx.Target;
            var spent = sim.Service<StatusService>().Rupture(sim, bearer, Status, ctx.Eval(Fraction, 1)) * ctx.Eval(Scale, 1);
            if (spent <= 0 || sim.World.Has<Dead>(bearer))
            {
                return;
            }
            var at = bearer == ctx.Target ? ctx : ctx.WithTarget(bearer);
            var thread = StatusTransferHandlers.ThreadOf(sim, Status);
            var packet = DamagePipeline.BuildFixed(at, new[] { (thread, spent) }, Tags);
            var result = DamagePipeline.Apply(at, packet);
            EffectVars.Set(ctx, "ruptured", result.Total);
            sim.Events.Publish(new StatusRuptured(ctx.Source, bearer, Status, result.Total) { Tick = sim.Clock.Tick, Depth = ctx.Depth });
        }
    }
}
