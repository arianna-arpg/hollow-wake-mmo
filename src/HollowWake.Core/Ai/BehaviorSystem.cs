using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;
using HollowWake.Core.World;

namespace HollowWake.Core.Ai;

/// <summary>What an AI action sees when it runs.</summary>
public sealed class AiContext
{
    public required Simulation Sim { get; init; }
    public required Entity Entity { get; init; }
    public required Behavior Behavior { get; init; }
    public required BehaviorDef Def { get; init; }
    public required Transform Transform { get; init; }
    public required Motion Motion { get; init; }
    public required Stats Stats { get; init; }
    public required IExprScope Scope { get; init; }
    public required Maths.Rng Rng { get; init; }

    public Transform? TargetTransform => Sim.World.Get<Transform>(Behavior.Target);
    public bool HasTarget => !Behavior.Target.IsNone && Sim.World.IsAlive(Behavior.Target) && !Sim.World.Has<Dead>(Behavior.Target);

    public double Num(JsonObject p, string key, double fallback) => Content.JsonNum.TryDouble(p[key], out var d) ? d : (p[key] is JsonValue sv && sv.TryGetValue<string>(out var s) && Expr.TryParse(s, out var e, out _) ? e!.Evaluate(Scope) : fallback);
    public string? Str(JsonObject p, string key) => p[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}

/// <summary>Registry of AI actions. Behaviors in content compose these by name.</summary>
public static class AiActions
{
    private static readonly Dictionary<string, Action<AiContext, JsonObject>> Actions = new(StringComparer.Ordinal);

    static AiActions()
    {
        Register("idle", (c, p) => c.Motion.Stop());
        Register("stop", (c, p) => c.Motion.Stop());
        Register("drop_target", (c, p) => c.Behavior.Target = Entity.None);
        Register("acquire_target", (c, p) =>
        {
            var range = (float)c.Num(p, "range", c.Def.AggroRange?.Evaluate(c.Scope) ?? 10);
            var filter = c.Str(p, "filter") ?? "enemy";
            Entity best = Entity.None;
            var bestDist = float.MaxValue;
            foreach (var (other, _, dist) in c.Sim.Spatial.Within(c.Transform.Position, range, 6f))
            {
                if (other == c.Entity || !c.Sim.World.Has<Stats>(other) || c.Sim.World.Has<Dead>(other))
                {
                    continue;
                }
                if (c.Sim.World.Get<Stats>(other)!.HasFlag("hidden"))
                {
                    continue;
                }
                var hostile = Relations.IsHostile(c.Sim, c.Entity, other);
                if (filter == "enemy" && !hostile)
                {
                    continue;
                }
                if (filter == "ally" && hostile)
                {
                    continue;
                }
                // Prefer whoever hurt us recently.
                var score = dist - (c.Behavior.Memory.TryGetValue("attacker:" + other.Key, out var at) && c.Sim.Clock.Time - at < 6 ? 100 : 0);
                if (score < bestDist)
                {
                    bestDist = score;
                    best = other;
                }
            }
            if (!best.IsNone)
            {
                c.Behavior.Target = best;
            }
        });
        Register("chase", (c, p) =>
        {
            if (!c.HasTarget || c.TargetTransform is not { } tt)
            {
                return;
            }
            var stop = (float)c.Num(p, "stop", c.Sim.Service<WeaveRuntime>().PreferredRange(c.Sim, c.Entity) * 0.9);
            var dist = c.Transform.GroundDistanceTo(tt.Position);
            if (dist <= stop)
            {
                c.Motion.Stop();
                c.Transform.Face(tt.Position);
                return;
            }
            SetDestination(c, tt.Position, stop);
        });
        Register("attack", (c, p) =>
        {
            if (!c.HasTarget)
            {
                return;
            }
            var runtime = c.Sim.Service<WeaveRuntime>();
            var choice = runtime.ChooseForAi(c.Sim, c.Entity, c.Behavior.Target);
            if (choice is null)
            {
                return;
            }
            var (slot, weave) = choice.Value;
            var tt = c.TargetTransform!;
            var dir = new Vector2(tt.Position.X - c.Transform.Position.X, tt.Position.Y - c.Transform.Position.Y);
            var target = weave.Targeting is Targeting.Self or Targeting.Ally ? c.Entity : c.Behavior.Target;
            if (runtime.TryBegin(c.Sim, c.Entity, slot, target, tt.Position, dir, out _))
            {
                c.Motion.Stop();
            }
        });
        Register("wander", (c, p) =>
        {
            if (c.Motion.WantsToMove || c.Rng.NextDouble() > c.Num(p, "chance", 0.3))
            {
                return;
            }
            var radius = c.Num(p, "radius", 6);
            var ang = c.Rng.NextDouble() * Math.PI * 2;
            var dist = c.Rng.Range(1.0, radius);
            var dest = c.Behavior.Home + new Vector3((float)(Math.Cos(ang) * dist), (float)(Math.Sin(ang) * dist), 0);
            SetDestination(c, dest, 0.3f);
        });
        Register("return_home", (c, p) => SetDestination(c, c.Behavior.Home, 0.5f));
        Register("flee", (c, p) =>
        {
            if (c.TargetTransform is not { } tt)
            {
                return;
            }
            var away = new Vector2(c.Transform.Position.X - tt.Position.X, c.Transform.Position.Y - tt.Position.Y);
            if (away.LengthSquared() < 0.01f)
            {
                away = Vector2.UnitX;
            }
            away = Vector2.Normalize(away) * (float)c.Num(p, "distance", 8);
            SetDestination(c, c.Transform.Position + new Vector3(away.X, away.Y, 0), 0.5f);
        });
        Register("face_target", (c, p) =>
        {
            if (c.TargetTransform is { } tt)
            {
                c.Transform.Face(tt.Position);
            }
        });
        Register("use_weave", (c, p) =>
        {
            var key = c.Str(p, "weave");
            if (key is null || !c.Sim.World.TryGet<WeaveBook>(c.Entity, out var book))
            {
                return;
            }
            var slot = book.Find(key);
            if (slot is null)
            {
                return;
            }
            var onSelf = (c.Str(p, "target") ?? "target") == "self";
            var target = onSelf || !c.HasTarget ? c.Entity : c.Behavior.Target;
            var point = c.TargetTransform?.Position ?? c.Transform.Position;
            c.Sim.Service<WeaveRuntime>().TryBegin(c.Sim, c.Entity, slot, target, point, Vector2.Zero, out _);
        });
        Register("remember", (c, p) =>
        {
            var key = c.Str(p, "key");
            if (key is not null)
            {
                c.Behavior.Memory[key] = c.Num(p, "value", c.Sim.Clock.Time);
            }
        });
        Register("heal_self", (c, p) => c.Stats.AddResource("life", c.Num(p, "amount", 1)));
    }

    public static void Register(string name, Action<AiContext, JsonObject> action) => Actions[name] = action;

    public static bool TryGet(string name, out Action<AiContext, JsonObject>? action) => Actions.TryGetValue(name, out action);

    public static IEnumerable<string> Names => Actions.Keys;

    private static void SetDestination(AiContext c, Vector3 dest, float stop)
    {
        var pathfinder = c.Sim.Service<Pathfinder>();
        var path = pathfinder.FindPath(c.Transform.Position, dest);
        c.Motion.Path.Clear();
        if (path.Count > 0)
        {
            c.Motion.Path.AddRange(path);
        }
        else
        {
            c.Motion.MoveTarget = dest;
        }
        c.Motion.StopDistance = stop;
    }
}

/// <summary>
/// Runs data-driven state machines. Each think: evaluate the current state's transitions against the
/// perception scope, switch if one fires (running exit and enter actions), then run tick actions.
/// </summary>
public sealed class BehaviorSystem : SimSystem
{
    public override string Name => "behavior";
    public override SimPhase Phase => SimPhase.Think;

    public override void Update(Simulation sim)
    {
        var now = sim.Clock.Time;
        foreach (var (e, b, t, m) in sim.World.Query<Behavior, Transform, Motion>())
        {
            if (now < b.NextThinkAt || sim.World.Has<Dead>(e))
            {
                continue;
            }
            if (!sim.World.TryGet<Stats>(e, out var stats))
            {
                continue;
            }
            if (!sim.Content.TryGet<BehaviorDef>(b.BehaviorId, out var def) || def is null)
            {
                continue;
            }
            b.NextThinkAt = now + Math.Max(sim.Clock.Dt, def.TickInterval);
            if (stats.HasFlag("stunned") || stats.HasFlag("frozen"))
            {
                m.Stop();
                continue;
            }
            var scope = BuildScope(sim, e, b, def, t, stats);
            var ctx = new AiContext { Sim = sim, Entity = e, Behavior = b, Def = def, Transform = t, Motion = m, Stats = stats, Scope = scope, Rng = sim.RngFor(e, "ai") };
            if (b.State.Length == 0 || !def.States.ContainsKey(b.State))
            {
                b.State = def.Initial;
                b.Entered = false;
            }
            var state = def.States[b.State];
            if (!b.Entered)
            {
                b.Entered = true;
                b.StateEnteredAt = now;
                Run(ctx, state.Enter);
            }
            foreach (var tr in state.Transitions.OrderByDescending(x => x.Priority))
            {
                if (tr.When is null || tr.When.IsTrue(scope))
                {
                    if (def.States.TryGetValue(tr.To, out var next))
                    {
                        Run(ctx, state.Exit);
                        b.State = tr.To;
                        b.StateEnteredAt = now;
                        b.Entered = true;
                        state = next;
                        Run(ctx, state.Enter);
                    }
                    break;
                }
            }
            Run(ctx, state.Tick);
        }
    }

    private static void Run(AiContext ctx, List<TypedSpec> actions)
    {
        foreach (var a in actions)
        {
            if (AiActions.TryGet(a.Type, out var action) && action is not null)
            {
                try
                {
                    action(ctx, a.Params);
                }
                catch (Exception ex)
                {
                    ctx.Sim.Log.Error($"ai action {a.Type} failed", ex);
                }
            }
            else
            {
                ctx.Sim.Log.Warn($"unknown ai action '{a.Type}'");
            }
        }
    }

    private static IExprScope BuildScope(Simulation sim, Entity e, Behavior b, BehaviorDef def, Transform t, Stats stats)
    {
        var overlay = new ScopeOverlay(stats);
        var hasTarget = !b.Target.IsNone && sim.World.IsAlive(b.Target) && !sim.World.Has<Dead>(b.Target);
        if (!hasTarget)
        {
            b.Target = Entity.None;
        }
        overlay.Set("has_target", hasTarget ? 1 : 0);
        var tt = hasTarget ? sim.World.Get<Transform>(b.Target) : null;
        overlay.Set("target_distance", tt is null ? 9999 : t.GroundDistanceTo(tt.Position));
        overlay.Set("target_life_pct", hasTarget ? sim.World.Get<Stats>(b.Target)?.ResourceFraction("life") ?? 1 : 1);
        overlay.Set("home_distance", t.GroundDistanceTo(b.Home));
        overlay.Set("state_time", sim.Clock.Time - b.StateEnteredAt);
        var aggro = def.AggroRange?.Evaluate(stats) ?? 10;
        var leash = def.LeashRange?.Evaluate(stats) ?? 40;
        overlay.Set("aggro_range", aggro);
        overlay.Set("leash_range", leash);
        overlay.Set("taunted", b.Memory.TryGetValue("taunted_until", out var tu) && tu > sim.Clock.Time ? 1 : 0);
        var nearest = 9999.0;
        foreach (var (other, _, dist) in sim.Spatial.Within(t.Position, (float)Math.Max(aggro, 2), 6f))
        {
            if (other == e || !sim.World.Has<Stats>(other) || sim.World.Has<Dead>(other))
            {
                continue;
            }
            if (Relations.IsHostile(sim, e, other) && !sim.World.Get<Stats>(other)!.HasFlag("hidden"))
            {
                nearest = Math.Min(nearest, dist);
            }
        }
        overlay.Set("nearest_enemy_distance", nearest);
        overlay.Set("preferred_range", sim.Service<WeaveRuntime>().PreferredRange(sim, e));
        if (hasTarget && sim.World.Get<Stats>(b.Target) is { } ts)
        {
            overlay.Mount("target", ts);
        }
        foreach (var (k, v) in b.Memory)
        {
            overlay.Set("memory." + k, v);
        }
        return overlay;
    }
}
