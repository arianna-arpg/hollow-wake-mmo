using System.Numerics;
using System.Text.Json.Nodes;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Intents;
using HollowWake.Core.Maths;
using HollowWake.Core.Minions;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Ai;

/// <summary>What an order does with one think: keep standing, or end because it was fulfilled.</summary>
public enum OrderStep
{
    Continue,
    Done,
}

/// <summary>Drives one think of an AI standing under an order of this kind.</summary>
public delegate OrderStep CommandKind(AiContext context, MinionOrder order);

/// <summary>
/// Command kinds by name, and the issuing of orders. A command in content (<c>minions.commands</c>) names a
/// kind; issuing puts each recipient that heeds it (its <c>obedience</c> plus the issuer's discipline, rolled
/// on the issuer's random stream) under an order that <c>obey_command</c> steps every think until it is
/// fulfilled or expires. Built-in kinds: <c>assault</c> (march on the mark and kill whatever holds it, the
/// pinned quarry first; an empty mark reached is done), <c>hold</c> (stand the mark and repel what comes
/// until it expires) and <c>recall</c> (break off and keep to the issuer's side). New kinds register here.
/// </summary>
public static class MinionCommands
{
    private static readonly Dictionary<string, CommandKind> Kinds = new(StringComparer.Ordinal);

    static MinionCommands()
    {
        Register("assault", Assault);
        Register("hold", Hold);
        Register("recall", Recall);
    }

    public static void Register(string kind, CommandKind step) => Kinds[kind] = step;

    public static bool TryGet(string kind, out CommandKind step) => Kinds.TryGetValue(kind, out step!);

    public static IEnumerable<string> Names => Kinds.Keys.OrderBy(k => k, StringComparer.Ordinal);

    /// <summary>
    /// Puts recipients under an order of the command at a mark, pinning <paramref name="pinned"/> when it is a
    /// live foe of the issuer, else the nearest foe within the command's pin radius of the mark. Each recipient
    /// drops its own target and plan. Returns how many heeded.
    /// </summary>
    internal static int Issue(Simulation sim, Entity issuer, CommandDef command, Vector3 point, Entity pinned, double duration, double discipline, List<Entity> recipients, Rng rng)
    {
        var quarry = IsFoe(sim, issuer, pinned) ? pinned : NearestFoe(sim, issuer, point, (float)command.PinRadius);
        var until = sim.Clock.Time + Math.Max(0, duration);
        var heeded = 0;
        var defied = 0;
        foreach (var r in recipients)
        {
            if (!sim.World.TryGet<Behavior>(r, out var b))
            {
                continue;
            }
            var obedience = sim.World.Get<Stats>(r)?.Get("obedience") ?? 1;
            if (!rng.Chance(Math.Min(1, obedience + discipline)))
            {
                defied++;
                continue;
            }
            b.Order = new MinionOrder
            {
                Command = command.Name,
                Kind = command.Kind,
                Point = point,
                Target = quarry,
                Issuer = issuer,
                Until = until,
                MarkRadius = (float)command.MarkRadius,
                Arrive = (float)command.Arrive,
            };
            b.Target = Entity.None;
            if (sim.World.TryGet<Motion>(r, out var motion))
            {
                motion.Planned = false;
            }
            heeded++;
        }
        sim.Events.Publish(new MinionsCommanded(issuer, command.Name, point, quarry, heeded, defied) { Tick = sim.Clock.Tick });
        return heeded;
    }

    private static OrderStep Assault(AiContext c, MinionOrder o)
    {
        var foe = IsFoe(c.Sim, c.Entity, o.Target) ? o.Target : NearestFoe(c.Sim, c.Entity, o.Point, o.MarkRadius);
        if (!foe.IsNone)
        {
            c.Behavior.Target = foe;
            AiActions.Engage(c);
            return OrderStep.Continue;
        }
        if (c.Transform.GroundDistanceTo(o.Point) > o.Arrive)
        {
            AiActions.MoveTo(c, o.Point, o.Arrive * 0.5f);
            return OrderStep.Continue;
        }
        return OrderStep.Done;
    }

    private static OrderStep Hold(AiContext c, MinionOrder o)
    {
        var foe = NearestFoe(c.Sim, c.Entity, o.Point, o.MarkRadius);
        if (!foe.IsNone)
        {
            c.Behavior.Target = foe;
            AiActions.Engage(c);
            return OrderStep.Continue;
        }
        c.Behavior.Target = Entity.None;
        if (c.Transform.GroundDistanceTo(o.Point) > o.Arrive)
        {
            AiActions.MoveTo(c, o.Point, o.Arrive * 0.5f);
        }
        return OrderStep.Continue;
    }

    private static OrderStep Recall(AiContext c, MinionOrder o)
    {
        var keeper = o.Issuer;
        if (keeper.IsNone || !c.Sim.World.IsAlive(keeper))
        {
            keeper = c.Sim.World.Get<Minion>(c.Entity)?.Owner ?? Entity.None;
        }
        if (keeper.IsNone || !c.Sim.World.TryGet<Transform>(keeper, out var kt) || c.Sim.World.Has<Dead>(keeper))
        {
            return OrderStep.Done;
        }
        c.Behavior.Target = Entity.None;
        if (c.Transform.GroundDistanceTo(kt.Position) > o.Arrive)
        {
            AiActions.MoveTo(c, kt.Position, o.Arrive * 0.5f);
        }
        return OrderStep.Continue;
    }

    /// <summary>A live, visible body hostile to <paramref name="of"/>.</summary>
    internal static bool IsFoe(Simulation sim, Entity of, Entity e)
    {
        if (e.IsNone || e == of || !sim.World.IsAlive(e) || sim.World.Has<Dead>(e) || !sim.World.TryGet<Stats>(e, out var stats))
        {
            return false;
        }
        return !stats.HasFlag("hidden") && Relations.IsHostile(sim, of, e);
    }

    /// <summary>The nearest live, visible foe of <paramref name="of"/> within <paramref name="radius"/> of a point.</summary>
    internal static Entity NearestFoe(Simulation sim, Entity of, Vector3 point, float radius)
    {
        var best = Entity.None;
        var bestDistance = float.MaxValue;
        foreach (var (e, _, d) in sim.Spatial.Within(point, radius, 6f))
        {
            if (d < bestDistance && IsFoe(sim, of, e))
            {
                best = e;
                bestDistance = d;
            }
        }
        return best;
    }
}

/// <summary>
/// The AI actions minions and constructs compose in content: following and assisting their owner, heeling
/// home on a leash, obeying orders, and the device actions constructs use to cast with their owner's
/// numbers, spring on what comes near and echo their owner's casts. Walking and a device's casting leave
/// through the intents any actor sends; the leash recall's teleport, a spent trap's going and an echo's replay
/// of a use its owner paid for are world rules no intent asks for, so they stay service calls.
/// </summary>
internal static class MinionActions
{
    private const string EchoAction = "echo_owner";

    public static void Register()
    {
        AiActions.Register("follow_owner", FollowOwner);
        AiActions.Register("assist_owner", AssistOwner);
        AiActions.Register("return_to_owner", ReturnToOwner);
        AiActions.Register("obey_command", ObeyCommand);
        AiActions.Register("construct_cast", ConstructCast);
        AiActions.Register("spring", Spring);
        AiActions.Register(EchoAction, EchoOwner);
    }

    /// <summary>Read the behavior's action graph once at birth, without naming any pattern or weave.</summary>
    internal static bool ObservesOwnerCasts(Simulation sim, Entity entity)
    {
        if (!sim.World.TryGet<Behavior>(entity, out var behavior)
            || sim.Content.Get<BehaviorDef>(behavior.BehaviorId) is not { } definition) return false;
        foreach (var state in definition.States.Values)
        {
            if (state.Enter.Any(a => a.Type == EchoAction) || state.Tick.Any(a => a.Type == EchoAction)
                || state.Exit.Any(a => a.Type == EchoAction)) return true;
        }
        return false;
    }

    /// <summary>The minion's living owner and where it stands.</summary>
    private static bool OwnerOf(AiContext c, out Entity owner, out Transform transform)
    {
        owner = c.Sim.World.Get<Minion>(c.Entity)?.Owner ?? Entity.None;
        if (owner.IsNone || !c.Sim.World.IsAlive(owner) || !c.Sim.World.TryGet<Transform>(owner, out transform!))
        {
            transform = null!;
            return false;
        }
        return true;
    }

    /// <summary><c>follow_owner {distance, stop}</c>: walks back to within <c>stop</c> of the owner once farther than <c>distance</c>.</summary>
    private static void FollowOwner(AiContext c, JsonObject p)
    {
        if (!OwnerOf(c, out _, out var ot))
        {
            return;
        }
        var distance = (float)c.Num(p, "distance", c.Sim.Content.Rules.Number("minions.follow_distance", 3));
        if (c.Transform.GroundDistanceTo(ot.Position) > distance)
        {
            AiActions.MoveTo(c, ot.Position, (float)c.Num(p, "stop", distance * 0.5));
        }
    }

    /// <summary>
    /// <c>assist_owner {range, switch}</c>: takes up what the owner is fighting when it stands within
    /// <c>range</c> of the owner; with nothing to fight already, turns on whoever last hurt the owner. With
    /// <c>switch: false</c> a minion keeps a target it already has.
    /// </summary>
    private static void AssistOwner(AiContext c, JsonObject p)
    {
        if (!OwnerOf(c, out var owner, out var ot) || !c.Sim.TryService<MinionService>(out var service))
        {
            return;
        }
        var range = c.Num(p, "range", c.Sim.Content.Rules.Number("minions.assist_range", 14));
        var target = service.OwnerTarget(owner);
        if (!Assistable(c, target, ot, range))
        {
            target = c.HasTarget ? Entity.None : service.OwnerAttacker(owner);
            if (!Assistable(c, target, ot, range))
            {
                return;
            }
        }
        if (c.Behavior.Target == target || (c.HasTarget && !Flag(p, "switch", true)))
        {
            return;
        }
        c.Behavior.Target = target;
    }

    private static bool Assistable(AiContext c, Entity target, Transform ownerAt, double range) =>
        MinionCommands.IsFoe(c.Sim, c.Entity, target)
        && c.Sim.World.TryGet<Transform>(target, out var tt)
        && ownerAt.GroundDistanceTo(tt.Position) <= range;

    /// <summary>
    /// <c>return_to_owner {stop}</c>: heads back to the owner. A minion farther than <c>minions.recall_distance</c>,
    /// or one that has made no headway for <c>minions.stuck_seconds</c>, teleports to its owner's side instead,
    /// at most once per <c>minions.recall_cooldown</c>.
    /// </summary>
    private static void ReturnToOwner(AiContext c, JsonObject p)
    {
        if (!OwnerOf(c, out var owner, out var ot))
        {
            return;
        }
        var stop = (float)c.Num(p, "stop", 1.5);
        var distance = c.Transform.GroundDistanceTo(ot.Position);
        if (c.Sim.World.TryGet<Minion>(c.Entity, out var minion) && LeashRecall(c, minion, owner, ot, distance))
        {
            return;
        }
        if (distance > stop)
        {
            AiActions.MoveTo(c, ot.Position, stop);
        }
    }

    private static bool LeashRecall(AiContext c, Minion minion, Entity owner, Transform ot, float distance)
    {
        var rules = c.Sim.Content.Rules;
        var now = c.Sim.Clock.Time;
        var position = c.Transform.Position;
        // Headway is measured from the moment this heel began.
        if (minion.ProgressAt < c.Behavior.StateEnteredAt || Vector3.Distance(position, minion.ProgressFrom) >= rules.Number("minions.stuck_progress", 1))
        {
            minion.ProgressFrom = position;
            minion.ProgressAt = now;
        }
        if (now < minion.RecallReadyAt)
        {
            return false;
        }
        var stuck = now - minion.ProgressAt >= rules.Number("minions.stuck_seconds", 3);
        if (distance <= rules.Number("minions.recall_distance", 40) && !stuck)
        {
            return false;
        }
        var radius = rules.Number("minions.recall_radius", 1.5);
        var ang = c.Rng.NextDouble() * Math.PI * 2;
        var to = ot.Position + new Vector3((float)(Math.Cos(ang) * radius), (float)(Math.Sin(ang) * radius), 0);
        // The leash is a world rule a minion is under, not something it does: no intent teleports, so the
        // recall stays a service call.
        MinionService.Of(c.Sim).Teleport(c.Entity, ot.Position, to);
        minion.RecallReadyAt = now + rules.Number("minions.recall_cooldown", 5);
        minion.ProgressFrom = c.Transform.Position;
        minion.ProgressAt = now;
        _ = owner;
        return true;
    }

    /// <summary><c>obey_command</c>: steps the standing order's kind; a fulfilled or expired order is cleared.</summary>
    private static void ObeyCommand(AiContext c, JsonObject p)
    {
        var order = c.Behavior.Order;
        if (order is null)
        {
            return;
        }
        if (c.Sim.Clock.Time >= order.Until || !MinionCommands.TryGet(order.Kind, out var kind))
        {
            c.Behavior.Order = null;
            return;
        }
        if (kind(c, order) == OrderStep.Done)
        {
            c.Behavior.Order = null;
        }
    }

    /// <summary><c>construct_cast {weave}</c>: begins a ready weave of the construct (or the named one) at its target.</summary>
    private static void ConstructCast(AiContext c, JsonObject p)
    {
        if (!c.HasTarget)
        {
            return;
        }
        ConstructCasting.CastAt(c, c.Str(p, "weave"), c.Behavior.Target);
    }

    /// <summary>The memory a trap keeps once it has sprung: when it did.</summary>
    private const string Sprung = "sprung";

    /// <summary>
    /// <c>spring {radius, weave, consume}</c>: a trap. When a foe comes within <c>radius</c>, begins its weave at
    /// it; with <c>consume</c> (the default) the trap is spent, and goes once that cast is over: at once for an
    /// instant weave, as the shipped snare's is, after its cast time otherwise. A spent trap's going is a world
    /// rule, not something it asks for, so it stays a service call.
    /// </summary>
    private static void Spring(AiContext c, JsonObject p)
    {
        var consume = Flag(p, "consume", true);
        if (consume && c.Behavior.Memory.ContainsKey(Sprung))
        {
            if (c.Sim.World.Get<WeaveBook>(c.Entity)?.Casting is null)
            {
                MinionService.Of(c.Sim).Dismiss(c.Entity);
            }
            return;
        }
        var foe = MinionCommands.NearestFoe(c.Sim, c.Entity, c.Transform.Position, (float)c.Num(p, "radius", 1.5));
        if (foe.IsNone)
        {
            return;
        }
        c.Behavior.Target = foe;
        if (!ConstructCasting.CastAt(c, c.Str(p, "weave"), foe) || !consume)
        {
            return;
        }
        if (c.Sim.World.Get<WeaveBook>(c.Entity)?.Casting is null)
        {
            MinionService.Of(c.Sim).Dismiss(c.Entity);
            return;
        }
        c.Behavior.Memory[Sprung] = c.Sim.Clock.Time;
    }

    /// <summary>
    /// <c>echo_owner {damage_scale}</c>: replay eligible owner resolutions captured since birth, with the owner's
    /// numbers and credit, from the echo's position. Content's echo exclusions determine eligibility. Replays
    /// run the captured payload and cast variables without paying again or beginning a new cast bar.
    /// </summary>
    private static void EchoOwner(AiContext c, JsonObject p)
    {
        if (!OwnerOf(c, out var owner, out _) || MinionService.Of(c.Sim).CancelIncapacitatedEcho(c.Entity)
            || !c.Sim.World.TryGet<Minion>(c.Entity, out var minion)
            || minion.PendingOwnerCasts is not { Count: > 0 } pending)
        {
            return;
        }
        var runtime = c.Sim.Service<WeaveRuntime>();
        // A replay may trigger more owner uses. Those wait for the next think rather than extending this loop.
        var count = pending.Count;
        for (var i = 0; i < count && pending.TryDequeue(out var use); i++)
        {
            var rng = c.Sim.RngFor(c.Entity, "construct:" + use.Weave.Key);
            // Preserve the existing single-replay stream; additional uses in this think get distinct seeded streams.
            if (i > 0) rng = new Rng(Rng.Combine(rng.Seed, (ulong)use.Sequence));
            runtime.Replay(c.Sim, c.Entity, owner, use.Weave, use.Effects, use.Variables,
                use.Target, use.Point, use.TargetPoint, use.AimOffset, use.Direction, c.Num(p, "damage_scale", 0.5), rng);
        }
    }

    private static bool Flag(JsonObject p, string key, bool fallback) => p[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;
}

/// <summary>
/// How devices cast: each use is a real one, begun by the intent any caster sends (<see cref="UseWeaveIntent"/>,
/// through <see cref="AiIntents"/>), so the weave runtime validates and times it as it does a player's: its uses,
/// charges, gates, target, cooldown and cast time all apply, and its cost is paid through the ordinary costs (the
/// shipped devices cast free because their pattern folds every cost to nothing, not because code skips it). A
/// construct that inherits its caster casts with its owner as the origin (the runtime's <c>OriginOf</c>): the
/// effects carry the owner's numbers and credit, from where the construct stands, while the reach, the cast time
/// and the cooldown are the construct's own. A device begins each weave at most once per
/// <c>minions.construct_interval</c>, a pace it keeps on its own clock.
/// </summary>
internal static class ConstructCasting
{
    /// <summary>Begins a ready weave of the construct (the named one when given) that reaches the target; false when none began.</summary>
    public static bool CastAt(AiContext c, string? weaveKey, Entity target)
    {
        var sim = c.Sim;
        if (!sim.World.TryGet<WeaveBook>(c.Entity, out var book))
        {
            return false;
        }
        var runtime = sim.Service<WeaveRuntime>();
        var now = sim.Clock.Time;
        var tt = sim.World.Get<Transform>(target);
        var at = tt?.Position ?? c.Transform.Position;
        var paced = c.Behavior.PacedUntil;
        for (var i = 0; i < book.Slots.Count; i++)
        {
            var slot = book.Slots[i];
            if (weaveKey is not null && slot.Key != weaveKey && slot.Spec.Preset != weaveKey)
            {
                continue;
            }
            if (!book.IsReady(slot, now) || (paced is not null && paced.TryGetValue(slot.Key, out var until) && now < until))
            {
                continue;
            }
            var weave = runtime.Resolve(sim, slot, out _);
            if (weave is null)
            {
                continue;
            }
            var range = weave.Range(c.Stats);
            if (tt is not null && range > 0 && c.Transform.GroundDistanceTo(tt.Position) > range + tt.Radius + c.Transform.Radius)
            {
                continue;
            }
            if (AiIntents.Send(c, new UseWeaveIntent(i, target, at, Toward(c.Transform, at))).Accepted)
            {
                (c.Behavior.PacedUntil ??= new Dictionary<string, double>(StringComparer.Ordinal))[slot.Key] = now + sim.Content.Rules.Number("minions.construct_interval", 1);
                return true;
            }
        }
        return false;
    }

    /// <summary>The unit direction from a body to a point; the body's facing when the point is under it.</summary>
    private static Vector2 Toward(Transform from, Vector3 at)
    {
        var direction = new Vector2(at.X - from.Position.X, at.Y - from.Position.Y);
        return direction.LengthSquared() > 0.0001f ? Vector2.Normalize(direction) : from.Forward;
    }
}
