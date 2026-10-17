using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Effects.Handlers;
using HollowWake.Core.Movement;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Systems;

/// <summary>
/// Moves bodies that are being displaced over time. A displacement carries a body instead of its walking:
/// a dash along its way at its speed, striking bodies in its corridor once each; a leap along an arc,
/// untargetable until it lands; a blink that waits, then arrives. Impulses such as knockback, pulls and
/// buffets decay on top of a body's own movement, and a body shoved into something it cannot enter stops
/// there and collides. Runs after ordinary movement and before projectiles. Bodies enter through
/// <see cref="Carry"/> and <see cref="Shove"/>, which mark them <see cref="Displaced"/> so only they are visited.
/// </summary>
public sealed class DisplacementSystem : SimSystem
{
    public override string Name => "displacement";
    public override SimPhase Phase => SimPhase.Move;
    public override int Order => 5;

    /// <summary>The trigger kind raised for a shoved body and for its shover when a shove slams it into something.</summary>
    public static readonly TriggerKind Collision = TriggerKinds.Register("collision", "a body the owner shoved, or the owner itself, is slammed into something it cannot enter");

    private readonly List<Entity> _settled = new();
    private readonly List<Entity> _contacts = new();

    public override void Update(Simulation sim)
    {
        if (sim.World.CountOf<Displaced>() == 0)
        {
            return;
        }
        var rules = sim.Content.Rules;
        var dt = (float)sim.Clock.Dt;
        var decay = MathF.Exp(-(float)rules.Number("delivery.impulse_decay", 8) * dt);
        var min = (float)rules.Number("delivery.impulse_min", 0.3);
        var collisionSpeed = (float)rules.Number("delivery.collision_speed", 2);
        var stepUp = rules.Number("move.step_up", 0.6);
        var blocked = sim.TryService<BlockedCells>(out var bc) ? bc : null;
        _settled.Clear();
        foreach (var (e, t, m, _) in sim.World.Query<Transform, Motion, Displaced>())
        {
            if (sim.World.Has<Dead>(e))
            {
                Settle(m);
                if (m.Displacement is { } carried)
                {
                    Finish(sim, e, t, m, carried, arrived: false, quiet: true);
                }
                _settled.Add(e);
                continue;
            }
            if (m.Displacement is { } d)
            {
                Advance(sim, e, t, m, d, stepUp, blocked);
            }
            else if (m.Impulse.X != 0 || m.Impulse.Y != 0)
            {
                Shift(sim, e, t, m, dt, decay, min, collisionSpeed, stepUp, blocked);
            }
            if (m.Impulse.X == 0 && m.Impulse.Y == 0 && m.Displacement is null)
            {
                _settled.Add(e);
            }
        }
        foreach (var e in _settled)
        {
            sim.World.Remove<Displaced>(e);
        }
        _settled.Clear();
    }

    // ------------------------------------------------------------ displacements

    /// <summary>
    /// Starts carrying a body: it stops walking, wears the displacement's flags, and the system moves it from
    /// <see cref="Displacement.From"/> to <see cref="Displacement.To"/> until <see cref="Displacement.EndsAt"/>.
    /// A displacement already carrying it ends where it is.
    /// </summary>
    public static void Carry(Simulation sim, Entity body, Motion m, Displacement d)
    {
        if (m.Displacement is { } old && sim.World.TryGet<Transform>(body, out var t))
        {
            Finish(sim, body, t, m, old, arrived: false, quiet: true);
        }
        m.Stop();
        m.Displacement = d;
        if (d.Kind == "leap")
        {
            m.Grounded = false;
        }
        Wear(sim, body, d, on: true);
        if (!sim.World.Has<Displaced>(body))
        {
            sim.World.Add(body, new Displaced());
        }
        sim.Events.Publish(new DisplacementStarted(body, d.Kind, d.From, d.To) { Tick = sim.Clock.Tick });
    }

    private static void Wear(Simulation sim, Entity body, Displacement d, bool on)
    {
        if (d.Flags.Length == 0 || !sim.World.TryGet<Stats>(body, out var stats))
        {
            return;
        }
        foreach (var flag in d.Flags)
        {
            if (on)
            {
                stats.Flags.Add(flag);
            }
            else
            {
                stats.Flags.Remove(flag);
            }
        }
    }

    private void Advance(Simulation sim, Entity e, Transform t, Motion m, Displacement d, double stepUp, BlockedCells? blocked)
    {
        var now = sim.Clock.Time;
        if (d.Context is { } source && !source.Source.IsNone && !sim.World.IsAlive(source.Source) && source.Source != e)
        {
            // What the move carries reads its caster's weave; with the caster gone the body simply stops.
            Finish(sim, e, t, m, d, arrived: false, quiet: true);
            return;
        }
        if (d.Kind == "blink")
        {
            if (now >= d.EndsAt)
            {
                var from = t.Position;
                t.Position = sim.Terrain.SnapToGround(d.To, 1.5);
                sim.Spatial.Update(e, t.Position);
                sim.Events.Publish(new EntityMoved(e, from, t.Position) { Tick = sim.Clock.Tick });
                Finish(sim, e, t, m, d, arrived: true, quiet: false);
            }
            return;
        }
        Wear(sim, e, d, on: true);
        var span = Math.Max(1e-6, d.EndsAt - d.StartedAt);
        var u = (float)Math.Clamp((now - d.StartedAt) / span, 0, 1);
        var x = d.From.X + (d.To.X - d.From.X) * u;
        var y = d.From.Y + (d.To.Y - d.From.Y) * u;
        var previous = t.Position;
        Vector3 next;
        if (d.Kind == "leap")
        {
            var z = d.From.Z + (d.To.Z - d.From.Z) * u + 4 * d.Arc * u * (1 - u);
            next = new Vector3(x, y, z);
        }
        else
        {
            if (!MovementSystem.CanOccupy(sim, d.Phase ? null : blocked, x, y, previous.Z, stepUp + 0.6, out var ground))
            {
                Finish(sim, e, t, m, d, arrived: false, quiet: false);
                return;
            }
            next = new Vector3(x, y, ground.Height);
        }
        var stepLength = new Vector2(next.X - previous.X, next.Y - previous.Y).Length();
        t.Position = next;
        if (stepLength > 1e-5f)
        {
            t.Yaw = MathF.Atan2(next.Y - previous.Y, next.X - previous.X);
        }
        sim.Spatial.Update(e, next);
        if (d.Context is { } ctx)
        {
            if (d.OnContact.Length > 0 && d.Width > 0 && d.Kind != "leap")
            {
                Contacts(sim, e, d, ctx, previous, next, now);
            }
            if (d.OnTravel.Length > 0)
            {
                d.Traveled += stepLength;
                for (var guard = 0; d.Traveled >= d.NextTravelAt && guard < 8; guard++)
                {
                    d.NextTravelAt += d.TravelEvery;
                    CompiledEffect.ExecuteAll(d.OnTravel, EffectHelpers.At(ctx, Entity.None, next, Heading(t), next, ctx.Depth));
                }
            }
        }
        if (u >= 1 && m.Displacement == d)
        {
            if (d.Kind == "leap")
            {
                m.Grounded = true;
                m.Velocity.Z = 0;
            }
            Finish(sim, e, t, m, d, arrived: true, quiet: false);
        }
    }

    private static Vector2 Heading(Transform t) => t.Forward;

    /// <summary>Strikes the bodies a dash's way passes within its width of, each once, in grid order.</summary>
    private void Contacts(Simulation sim, Entity e, Displacement d, EffectContext ctx, Vector3 from, Vector3 to, double now)
    {
        var segment = new Vector2(to.X - from.X, to.Y - from.Y);
        var length = segment.Length();
        var mid = (from + to) * 0.5f;
        var half = d.Width * 0.5f;
        _contacts.Clear();
        foreach (var (other, pos, _) in sim.Spatial.Within(mid, length * 0.5f + half + 1.5f))
        {
            if (other == e || d.Struck.Contains(other) || !sim.World.TryGet<Transform>(other, out var ot))
            {
                continue;
            }
            if (DistanceToSegment(new Vector2(pos.X, pos.Y), new Vector2(from.X, from.Y), segment, length) > half + ot.Radius)
            {
                continue;
            }
            _contacts.Add(other);
        }
        foreach (var other in _contacts)
        {
            if (!EffectHelpers.Passes(ctx, other, d.Filter) || (sim.World.Get<Stats>(other)?.HasFlag("untargetable") ?? false))
            {
                continue;
            }
            d.Struck.Record(other, now);
            CompiledEffect.ExecuteAll(d.OnContact, EffectHelpers.At(ctx, other, to, ctx.Direction, to, ctx.Depth));
        }
        _contacts.Clear();
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 ab, float length)
    {
        if (length < 1e-5f)
        {
            return Vector2.Distance(p, a);
        }
        var t = Math.Clamp(Vector2.Dot(p - a, ab) / (length * length), 0, 1);
        return Vector2.Distance(p, a + ab * t);
    }

    /// <summary>Ends a displacement: flags come off, the end hooks run where it stopped (leaps land first), and walking resumes.</summary>
    private static void Finish(Simulation sim, Entity e, Transform t, Motion m, Displacement d, bool arrived, bool quiet)
    {
        if (m.Displacement == d)
        {
            m.Displacement = null;
        }
        Wear(sim, e, d, on: false);
        if (d.Kind == "leap" && !arrived)
        {
            m.Grounded = false;
        }
        sim.Events.Publish(new DisplacementEnded(e, d.Kind, t.Position, arrived) { Tick = sim.Clock.Tick });
        if (quiet || d.Context is not { } ctx)
        {
            return;
        }
        var at = t.Position;
        var end = EffectHelpers.At(ctx, Entity.None, at, t.Forward, at, ctx.Depth);
        if (d.OnLand.Length > 0 && (d.Kind != "leap" || arrived))
        {
            CompiledEffect.ExecuteAll(d.OnLand, end);
        }
        if (d.OnEnd.Length > 0)
        {
            CompiledEffect.ExecuteAll(d.OnEnd, end);
        }
    }

    // ------------------------------------------------------------ impulses

    /// <summary>
    /// Shoves a body: adds a ground velocity that decays at rule <c>delivery.impulse_decay</c> per second, with
    /// a rider saying who shoved and what a collision does. The velocity that carries a body a distance
    /// before it settles is <see cref="SpeedFor"/>.
    /// </summary>
    public static void Shove(Simulation sim, Entity body, Vector2 velocity, ShoveRider? rider = null)
    {
        if (!sim.World.TryGet<Motion>(body, out var m))
        {
            return;
        }
        m.Impulse += new Vector3(velocity.X, velocity.Y, 0);
        m.Shove = rider ?? m.Shove;
        if (!sim.World.Has<Displaced>(body))
        {
            sim.World.Add(body, new Displaced());
        }
    }

    /// <summary>The starting speed of an impulse that carries a body <paramref name="distance"/> units before it settles.</summary>
    public static float SpeedFor(Simulation sim, float distance)
    {
        var dt = (float)sim.Clock.Dt;
        var k = (float)sim.Content.Rules.Number("delivery.impulse_decay", 8);
        // A step of v dt each tick, decaying by e^-k dt: the steps sum to v dt / (1 - e^-k dt).
        return distance * (1 - MathF.Exp(-k * dt)) / dt;
    }

    /// <summary>Seconds an impulse of a speed takes to settle.</summary>
    public static double SettleSeconds(Simulation sim, float speed)
    {
        var k = sim.Content.Rules.Number("delivery.impulse_decay", 8);
        var min = sim.Content.Rules.Number("delivery.impulse_min", 0.3);
        return speed <= min || k <= 0 ? 0 : Math.Log(speed / min) / k;
    }

    /// <summary>
    /// Moves a body by a small ground offset if it may stand there, sliding along an axis when it may not
    /// go straight. What fields pulling bodies in use. False when it could not move at all.
    /// </summary>
    public static bool Nudge(Simulation sim, Entity body, Transform t, Vector2 delta)
    {
        if (sim.World.Get<Motion>(body)?.Displacement is not null)
        {
            return false;
        }
        var blocked = sim.TryService<BlockedCells>(out var bc) ? bc : null;
        var stepUp = sim.Content.Rules.Number("move.step_up", 0.6);
        var from = t.Position;
        var x = from.X + delta.X;
        var y = from.Y + delta.Y;
        if (!MovementSystem.CanOccupy(sim, blocked, x, y, from.Z, stepUp, out var ground))
        {
            if (MovementSystem.CanOccupy(sim, blocked, x, from.Y, from.Z, stepUp, out ground))
            {
                y = from.Y;
            }
            else if (MovementSystem.CanOccupy(sim, blocked, from.X, y, from.Z, stepUp, out ground))
            {
                x = from.X;
            }
            else
            {
                return false;
            }
        }
        t.Position = new Vector3(x, y, ground.Height);
        sim.Spatial.Update(body, t.Position);
        return true;
    }

    private static void Shift(Simulation sim, Entity e, Transform t, Motion m, float dt, float decay, float min, float collisionSpeed, double stepUp, BlockedCells? blocked)
    {
        var velocity = new Vector2(m.Impulse.X, m.Impulse.Y);
        var speed = velocity.Length();
        if (speed < min)
        {
            Settle(m);
            return;
        }
        var from = t.Position;
        var x = from.X + velocity.X * dt;
        var y = from.Y + velocity.Y * dt;
        if (!MovementSystem.CanOccupy(sim, blocked, x, y, from.Z, stepUp, out var ground))
        {
            Collide(sim, e, t, m, speed, collisionSpeed);
            return;
        }
        if (ground.Height < from.Z - 0.05f)
        {
            // Shoved off a ledge: keep the height and let gravity take it.
            t.Position = new Vector3(x, y, from.Z);
            m.Grounded = false;
        }
        else
        {
            t.Position = new Vector3(x, y, ground.Height);
        }
        sim.Spatial.Update(e, t.Position);
        m.Impulse *= decay;
        if (m.Impulse.X * m.Impulse.X + m.Impulse.Y * m.Impulse.Y < min * min)
        {
            Settle(m);
        }
    }

    private static void Settle(Motion m)
    {
        m.Impulse = Vector3.Zero;
        m.Shove = null;
    }

    /// <summary>A shove stopped by a wall, a cliff or a placed blocker: the impulse ends and, fast enough, the body collides.</summary>
    private static void Collide(Simulation sim, Entity e, Transform t, Motion m, float speed, float collisionSpeed)
    {
        var rider = m.Shove;
        Settle(m);
        if (speed < collisionSpeed)
        {
            return;
        }
        var source = rider?.Source ?? Entity.None;
        sim.Events.Publish(new BodyCollided(e, source, t.Position, speed) { Tick = sim.Clock.Tick });
        if (sim.TryService<TriggerHub>(out var hub) && hub.HasSubscribers(Collision))
        {
            var raised = new TriggerEvent(source, e, speed, depth: rider?.Context?.Depth ?? 0);
            hub.Raise(e, Collision, raised);
            if (!source.IsNone && source != e)
            {
                hub.Raise(source, Collision, raised);
            }
        }
        // What a collision does reads the shover's stats and weave, so it runs only while the shover exists.
        if (rider is { Context: { } ctx, OnCollide.Length: > 0 } && (ctx.Source.IsNone || sim.World.IsAlive(ctx.Source)))
        {
            CompiledEffect.ExecuteAll(rider.OnCollide, EffectHelpers.At(ctx, e, t.Position, ctx.Direction, t.Position, ctx.Depth + 1));
        }
    }
}
