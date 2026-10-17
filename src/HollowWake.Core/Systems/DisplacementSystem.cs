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
/// Moves bodies that are being displaced over time: impulses such as knockback, pulls and buffets decay
/// on top of a body's own movement, and a body shoved into something it cannot enter stops there and
/// collides. Runs after ordinary movement and before projectiles. Bodies enter through
/// <see cref="Shove"/>, which marks them <see cref="Displaced"/> so only they are visited.
/// </summary>
public sealed class DisplacementSystem : SimSystem
{
    public override string Name => "displacement";
    public override SimPhase Phase => SimPhase.Move;
    public override int Order => 5;

    /// <summary>The trigger kind raised for a shoved body and for its shover when a shove slams it into something.</summary>
    public static readonly TriggerKind Collision = TriggerKinds.Register("collision", "a body the owner shoved, or the owner itself, is slammed into something it cannot enter");

    private readonly List<Entity> _settled = new();

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
                _settled.Add(e);
                continue;
            }
            if (m.Impulse.X != 0 || m.Impulse.Y != 0)
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
