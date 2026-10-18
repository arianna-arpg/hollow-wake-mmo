using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Expressions;
using HollowWake.Core.Minions;
using HollowWake.Core.Movement;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
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

    /// <summary>How movement actions plan this think: the move rules the behavior system read for the tick.</summary>
    internal AiNavigation Navigation { get; init; } = AiNavigation.Defaults;

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
            // Bodies wearing any of these tags are passed over: a minion leaves incidental prey alone.
            var ignore = p["ignore"] is null ? null : IgnoredTags.GetValue(p, static q => q["ignore"].Deserialize<TagSet>(ContentJson.Options) ?? TagSet.Empty);
            Entity best = Entity.None;
            var bestDist = float.MaxValue;
            // Only memory can make someone closer than they are; without it, nobody farther than the best can win.
            var attackers = HasAttackerMemory(c.Behavior.Memory);
            foreach (var (other, _, dist) in c.Sim.Spatial.Within(c.Transform.Position, range, 6f))
            {
                if (!attackers && dist >= bestDist)
                {
                    continue;
                }
                if (other == c.Entity || !c.Sim.World.Has<Stats>(other) || c.Sim.World.Has<Dead>(other))
                {
                    continue;
                }
                var otherStats = c.Sim.World.Get<Stats>(other)!;
                if (otherStats.HasFlag("hidden") || (ignore is not null && otherStats.Tags.HasAny(ignore)))
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
                var score = dist - (attackers && c.Behavior.Memory.TryGetValue("attacker:" + other.Key, out var at) && c.Sim.Clock.Time - at < 6 ? 100 : 0);
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
            var stop = (float)c.Num(p, "stop", c.Sim.Service<WeaveRuntime>().PreferredRange(c.Sim, c.Entity, c.Behavior.Target) * 0.9);
            ChaseTo(c, tt, stop);
        });
        Register("attack", (c, p) => AttackTarget(c));
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
        MinionActions.Register();
    }

    /// <summary>The <c>ignore</c> tags of an <c>acquire_target</c>, parsed once per action spec.</summary>
    private static readonly ConditionalWeakTable<JsonObject, TagSet> IgnoredTags = new();

    public static void Register(string name, Action<AiContext, JsonObject> action) => Actions[name] = action;

    /// <summary>Moves toward a point under the shared planning policy (see <see cref="SetDestination"/>).</summary>
    internal static void MoveTo(AiContext c, Vector3 destination, float stop) => SetDestination(c, destination, stop);

    /// <summary>Closes on the target until within <paramref name="stop"/>, then stops and faces it.</summary>
    internal static void ChaseTo(AiContext c, Transform tt, float stop)
    {
        var dist = c.Transform.GroundDistanceTo(tt.Position);
        if (dist <= stop)
        {
            c.Motion.Stop();
            c.Transform.Face(tt.Position);
            return;
        }
        SetDestination(c, tt.Position, stop);
    }

    /// <summary>Casts the weave the AI's hints choose at its target, through the shared runtime; true when a cast began.</summary>
    internal static bool AttackTarget(AiContext c)
    {
        if (!c.HasTarget)
        {
            return false;
        }
        var runtime = c.Sim.Service<WeaveRuntime>();
        var choice = runtime.ChooseForAi(c.Sim, c.Entity, c.Behavior.Target, c.Scope);
        if (choice is null)
        {
            return false;
        }
        var (slot, weave) = choice.Value;
        var tt = c.TargetTransform!;
        var dir = new Vector2(tt.Position.X - c.Transform.Position.X, tt.Position.Y - c.Transform.Position.Y);
        var target = weave.Targeting is Targeting.Self or Targeting.Ally ? c.Entity : c.Behavior.Target;
        if (runtime.TryBegin(c.Sim, c.Entity, slot, target, tt.Position, dir, out _))
        {
            c.Motion.Stop();
            return true;
        }
        return false;
    }

    /// <summary>Fights the target as <c>chase</c> then <c>attack</c> would: closes to its preferred range and casts.</summary>
    internal static void Engage(AiContext c)
    {
        if (!c.HasTarget || c.TargetTransform is not { } tt)
        {
            return;
        }
        ChaseTo(c, tt, (float)(c.Sim.Service<WeaveRuntime>().PreferredRange(c.Sim, c.Entity, c.Behavior.Target) * 0.9));
        AttackTarget(c);
    }

    public static bool TryGet(string name, out Action<AiContext, JsonObject>? action) => Actions.TryGetValue(name, out action);

    public static IEnumerable<string> Names => Actions.Keys;

    private static bool HasAttackerMemory(Dictionary<string, double> memory)
    {
        if (memory.Count == 0)
        {
            return false;
        }
        foreach (var key in memory.Keys)
        {
            if (key.StartsWith("attacker:", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Moves the AI toward a destination, re-planning only when it must: when it has no path or move
    /// target left, when the destination has moved more than <c>move.ai_repath_distance</c> from the planned
    /// goal, or when the plan is older than <c>move.ai_repath_seconds</c>. A new plan walks straight when the
    /// segment is clear; otherwise it searches with the AI node budget. When a search cannot bring the AI
    /// closer (no step, or a partial path that ends no nearer the goal), the AI follows what it found and
    /// then walks straight at the goal, and does not search for that goal again until the plan is renewed:
    /// an unreachable target costs one search per renewal instead of one per think.
    /// </summary>
    private static void SetDestination(AiContext c, Vector3 dest, float stop)
    {
        var nav = c.Navigation;
        var b = c.Behavior;
        var m = c.Motion;
        m.StopDistance = stop;
        if (b.Planned && c.Sim.Clock.Time - b.PlannedAt < nav.RepathSeconds && GroundDistance(b.PlanGoal, dest) <= nav.RepathDistance)
        {
            if (m.Path.Count > 0 || m.MoveTarget is not null)
            {
                return;
            }
            if (b.PlanFailed)
            {
                m.MoveTarget = dest;
                return;
            }
        }
        b.Planned = true;
        b.PlanGoal = dest;
        b.PlannedAt = c.Sim.Clock.Time;
        b.PlanFailed = false;
        var pathfinder = c.Sim.Service<Pathfinder>();
        var from = c.Transform.Position;
        if (nav.CanWalkStraight(c.Sim, pathfinder, from, dest))
        {
            m.Path.Clear();
            m.MoveTarget = dest;
            return;
        }
        switch (pathfinder.FindPath(from, dest, m.Path, nav.MaxNodes))
        {
            case PathResult.None:
                m.MoveTarget = dest;
                b.PlanFailed = true;
                break;
            case PathResult.Partial:
                // Progress means ending at least a cell nearer the goal than the search started.
                b.PlanFailed = GroundDistance(m.Path[^1], dest) > GroundDistance(from, dest) - 1;
                break;
        }
    }

    private static float GroundDistance(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}

/// <summary>The move rules AI planning reads, refreshed once per behavior update, and the straight-walk check.</summary>
internal sealed class AiNavigation
{
    public static readonly AiNavigation Defaults = new();

    /// <summary>Spacing of the straight-walk samples: about a walker's step per tick.</summary>
    private const float StraightStep = 0.25f;

    public double RepathSeconds = 1.5;
    public double RepathDistance = 2;
    public int MaxNodes = 500;
    public double StepUp = 0.6;
    public double MaxDrop = 3;

    public void Read(Rules rules)
    {
        RepathSeconds = rules.Number("move.ai_repath_seconds", 1.5);
        RepathDistance = rules.Number("move.ai_repath_distance", 2);
        MaxNodes = (int)rules.Number("move.ai_path_max_nodes", 500);
        StepUp = rules.Number("move.step_up", 0.6);
        MaxDrop = rules.Number("move.max_drop", 3);
    }

    /// <summary>
    /// Whether walking straight from one point to another works: every sample along the segment can be
    /// occupied as the movement system judges it, no step drops further than <c>move.max_drop</c>, and no
    /// cell crossed costs more than plain ground (so dear ground such as bog still goes to the pathfinder).
    /// </summary>
    public bool CanWalkStraight(Simulation sim, Pathfinder pathfinder, Vector3 from, Vector3 to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var steps = (int)MathF.Ceiling(MathF.Sqrt(dx * dx + dy * dy) / StraightStep);
        var blocked = sim.TryService<BlockedCells>(out var bc) ? bc : null;
        double feet = from.Z;
        var cell = CellCoord.Of(from.X, from.Y);
        for (var i = 1; i <= steps; i++)
        {
            var f = i / (float)steps;
            var x = from.X + dx * f;
            var y = from.Y + dy * f;
            if (!MovementSystem.CanOccupy(sim, blocked, x, y, feet, StepUp, out var ground) || feet - ground.Height > MaxDrop)
            {
                return false;
            }
            var at = CellCoord.Of(x, y);
            if (at != cell)
            {
                if (pathfinder.CellCost(at) > 1)
                {
                    return false;
                }
                cell = at;
            }
            feet = ground.Height;
        }
        return true;
    }
}

/// <summary>
/// The perception an AI thinks with, kept on the behavior and refilled every think so thinking builds no
/// dictionaries. It resolves exactly as an overlay over the entity's stats holding the perception values
/// would, with the target's stats mounted under <c>target.</c> and memory values under <c>memory.</c>.
/// Values are measured when the think starts; actions during the think do not change them.
/// </summary>
internal sealed class PerceptionScope : IExprScope
{
    private readonly List<KeyValuePair<string, double>> _memory = new();
    private Stats _self = null!;
    private Stats? _target;
    private Stats? _owner;

    public double HasTarget;
    public double TargetDistance;
    public double TargetLifePct;
    public double HomeDistance;
    public double StateTime;
    public double AggroRange;
    public double LeashRange;
    public double Taunted;
    public double NearestEnemyDistance;
    public double PreferredRange;

    // What a minion perceives of its owner and of the order it stands under.
    public double HasOwner;
    public double OwnerDistance;
    public double OwnerHasTarget;
    public double OwnerTargetDistance;
    public double Commanded;
    public double CommandDistance;

    public void Begin(Stats self)
    {
        _self = self;
        _target = null;
        _owner = null;
        _memory.Clear();
    }

    public void MountTarget(Stats? target) => _target = target;

    public void MountOwner(Stats? owner) => _owner = owner;

    public void Remember(Dictionary<string, double> memory)
    {
        foreach (var entry in memory)
        {
            _memory.Add(entry);
        }
    }

    public bool TryResolve(string name, out double value)
    {
        switch (name)
        {
            case "has_target":
                value = HasTarget;
                return true;
            case "target_distance":
                value = TargetDistance;
                return true;
            case "target_life_pct":
                value = TargetLifePct;
                return true;
            case "home_distance":
                value = HomeDistance;
                return true;
            case "state_time":
                value = StateTime;
                return true;
            case "aggro_range":
                value = AggroRange;
                return true;
            case "leash_range":
                value = LeashRange;
                return true;
            case "taunted":
                value = Taunted;
                return true;
            case "nearest_enemy_distance":
                value = NearestEnemyDistance;
                return true;
            case "preferred_range":
                value = PreferredRange;
                return true;
            case "has_owner":
                value = HasOwner;
                return true;
            case "owner_distance":
                value = OwnerDistance;
                return true;
            case "owner_has_target":
                value = OwnerHasTarget;
                return true;
            case "owner_target_distance":
                value = OwnerTargetDistance;
                return true;
            case "commanded":
                value = Commanded;
                return true;
            case "command_distance":
                value = CommandDistance;
                return true;
        }
        if (_memory.Count > 0 && name.StartsWith("memory.", StringComparison.Ordinal))
        {
            var key = name.AsSpan(7);
            foreach (var (k, v) in _memory)
            {
                if (key.SequenceEqual(k))
                {
                    value = v;
                    return true;
                }
            }
        }
        if (IsTargetName(name))
        {
            return _target!.TryResolve(name.Substring(7), out value);
        }
        if (IsOwnerName(name))
        {
            return _owner!.TryResolve(name.Substring(6), out value);
        }
        return _self.TryResolve(name, out value);
    }

    public bool HasFlag(string flag) => IsTargetName(flag) ? _target!.HasFlag(flag.Substring(7)) : IsOwnerName(flag) ? _owner!.HasFlag(flag.Substring(6)) : _self.HasFlag(flag);

    public double NextRandom() => _self.NextRandom();

    /// <summary>A name under the target mount: the text before its first dot is "target" and a target is mounted.</summary>
    private bool IsTargetName(string name) => _target is not null && name.Length >= 7 && name[6] == '.' && name.StartsWith("target", StringComparison.Ordinal);

    /// <summary>A name under the owner mount: the text before its first dot is "owner" and an owner is mounted.</summary>
    private bool IsOwnerName(string name) => _owner is not null && name.Length >= 6 && name[5] == '.' && name.StartsWith("owner", StringComparison.Ordinal);
}

/// <summary>
/// Runs data-driven state machines. Each think: evaluate the current state's transitions against the
/// perception scope, switch if one fires (running exit and enter actions), then run tick actions.
/// </summary>
public sealed class BehaviorSystem : SimSystem
{
    /// <summary>Each state's transitions, highest priority first (stable for equal priorities), ordered once.</summary>
    private static readonly ConditionalWeakTable<BehaviorState, BehaviorTransition[]> OrderedTransitions = new();

    private readonly AiNavigation _navigation = new();

    public override string Name => "behavior";
    public override SimPhase Phase => SimPhase.Think;

    public override void Update(Simulation sim)
    {
        var now = sim.Clock.Time;
        _navigation.Read(sim.Content.Rules);
        // The loop stays small and the think lives in its own method, so the runtime can optimize a
        // long-running update without recompiling the whole think in the middle of a tick.
        foreach (var (e, b, t, m) in sim.World.Query<Behavior, Transform, Motion>())
        {
            if (now < b.NextThinkAt)
            {
                continue;
            }
            Think(sim, now, e, b, t, m);
        }
    }

    private void Think(Simulation sim, double now, Entity e, Behavior b, Transform t, Motion m)
    {
        // The dead do not think, nor do bodies dismissed earlier this tick that the world has yet to flush.
        if (sim.World.Has<Dead>(e) || sim.World.IsDeferredDestroy(e))
        {
            return;
        }
        if (!sim.World.TryGet<Stats>(e, out var stats))
        {
            return;
        }
        if (!sim.Content.TryGet<BehaviorDef>(b.BehaviorId, out var def) || def is null)
        {
            return;
        }
        b.NextThinkAt = now + Math.Max(sim.Clock.Dt, def.TickInterval);
        if (stats.HasFlag("stunned") || stats.HasFlag("frozen"))
        {
            m.Stop();
            return;
        }
        var scope = BuildScope(sim, e, b, def, t, stats);
        var ctx = new AiContext { Sim = sim, Entity = e, Behavior = b, Def = def, Transform = t, Motion = m, Stats = stats, Scope = scope, Rng = sim.RngFor(e, "ai"), Navigation = _navigation };
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
        foreach (var tr in Ordered(state))
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

    private static BehaviorTransition[] Ordered(BehaviorState state)
    {
        if (!OrderedTransitions.TryGetValue(state, out var ordered) || ordered.Length != state.Transitions.Count)
        {
            ordered = state.Transitions.OrderByDescending(x => x.Priority).ToArray();
            OrderedTransitions.AddOrUpdate(state, ordered);
        }
        return ordered;
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
        var scope = b.Perception ??= new PerceptionScope();
        scope.Begin(stats);
        var hasTarget = !b.Target.IsNone && sim.World.IsAlive(b.Target) && !sim.World.Has<Dead>(b.Target);
        if (!hasTarget)
        {
            b.Target = Entity.None;
        }
        scope.HasTarget = hasTarget ? 1 : 0;
        var tt = hasTarget ? sim.World.Get<Transform>(b.Target) : null;
        scope.TargetDistance = tt is null ? 9999 : t.GroundDistanceTo(tt.Position);
        scope.TargetLifePct = hasTarget ? sim.World.Get<Stats>(b.Target)?.ResourceFraction("life") ?? 1 : 1;
        scope.HomeDistance = t.GroundDistanceTo(b.Home);
        scope.StateTime = sim.Clock.Time - b.StateEnteredAt;
        var aggro = def.AggroRange?.Evaluate(stats) ?? 10;
        var leash = def.LeashRange?.Evaluate(stats) ?? 40;
        scope.AggroRange = aggro;
        scope.LeashRange = leash;
        scope.Taunted = b.Memory.TryGetValue("taunted_until", out var tu) && tu > sim.Clock.Time ? 1 : 0;
        var nearest = 9999.0;
        foreach (var (other, _, dist) in sim.Spatial.Within(t.Position, (float)Math.Max(aggro, 2), 6f))
        {
            // Only someone nearer than the nearest so far can change the answer.
            if (dist >= nearest || other == e || !sim.World.Has<Stats>(other) || sim.World.Has<Dead>(other))
            {
                continue;
            }
            if (Relations.IsHostile(sim, e, other) && !sim.World.Get<Stats>(other)!.HasFlag("hidden"))
            {
                nearest = dist;
            }
        }
        scope.NearestEnemyDistance = nearest;
        scope.PreferredRange = sim.Service<WeaveRuntime>().PreferredRange(sim, e, b.Target);
        if (hasTarget && sim.World.Get<Stats>(b.Target) is { } ts)
        {
            scope.MountTarget(ts);
        }
        PerceiveOwner(sim, e, b, t, scope);
        scope.Remember(b.Memory);
        return scope;
    }

    /// <summary>
    /// What a minion perceives of its owner (whether it has one, how far it is, what it fights and how far
    /// that is, its stats under <c>owner.</c>) and of its standing order (whether one stands, how far its mark
    /// is). An expired order is cleared here. Bodies without an owner perceive nothing of it.
    /// </summary>
    private static void PerceiveOwner(Simulation sim, Entity e, Behavior b, Transform t, PerceptionScope scope)
    {
        scope.HasOwner = 0;
        scope.OwnerDistance = 9999;
        scope.OwnerHasTarget = 0;
        scope.OwnerTargetDistance = 9999;
        if (sim.World.TryGet<Minion>(e, out var minion) && !minion.Owner.IsNone && sim.World.IsAlive(minion.Owner) && sim.World.TryGet<Transform>(minion.Owner, out var ot))
        {
            scope.HasOwner = 1;
            scope.OwnerDistance = t.GroundDistanceTo(ot.Position);
            scope.MountOwner(sim.World.Get<Stats>(minion.Owner));
            if (sim.TryService<MinionService>(out var minions) && minions.OwnerTarget(minion.Owner) is { IsNone: false } fought && sim.World.TryGet<Transform>(fought, out var ft))
            {
                scope.OwnerHasTarget = 1;
                scope.OwnerTargetDistance = t.GroundDistanceTo(ft.Position);
            }
        }
        if (b.Order is { } order && order.Until <= sim.Clock.Time)
        {
            b.Order = null;
        }
        scope.Commanded = b.Order is null ? 0 : 1;
        scope.CommandDistance = b.Order is { } standing ? t.GroundDistanceTo(standing.Point) : 9999;
    }
}
