using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Patterns;
using HollowWake.Core.Sim;
using HollowWake.Core.World;

namespace HollowWake.Core.Movement;

/// <summary>
/// Walking, running, path following, gravity, jumping and landing on stacked surfaces. Horizontal
/// motion is validated against the terrain cell by cell (walls, cliffs, deep water, blocking props)
/// with sliding along obstacles; vertical motion uses the terrain's ground search so bridges, floors
/// and caves resolve naturally. All tunables come from the move rules.
/// </summary>
public sealed class MovementSystem : SimSystem
{
    public override string Name => "movement";
    public override SimPhase Phase => SimPhase.Move;
    public override int Order => 0;

    public override void Update(Simulation sim)
    {
        var rules = sim.Content.Rules;
        var dt = sim.Clock.Dt;
        var now = sim.Clock.Time;
        var gravity = (float)rules.Number("move.gravity", 24);
        var jumpVelocity = (float)rules.Number("move.jump_velocity", 7.5);
        var stepUp = rules.Number("move.step_up", 0.6);
        var defaultSpeed = rules.Number("move.default_speed", 4.5);
        var blocked = sim.TryService<BlockedCells>(out var bc) ? bc : null;

        foreach (var (e, t, m) in sim.World.Query<Transform, Motion>())
        {
            if (sim.World.Has<Dead>(e))
            {
                m.Stop();
                continue;
            }
            var stats = sim.World.Get<Stats>(e);
            var casting = sim.World.Get<WeaveBook>(e)?.Casting;
            var mobileCast = casting?.Slot.Compiled?.Tags.Has("mobile") ?? false;
            var immobile = stats is not null && (stats.HasFlag("stunned") || stats.HasFlag("rooted") || stats.HasFlag("frozen"));
            var locked = immobile || (casting is not null && !mobileCast);

            // Path and target following produce the desired direction.
            if (!locked)
            {
                FollowPath(t, m);
            }
            var dir = locked ? Vector2.Zero : m.MoveDir;
            var speed = stats?.Get("move.speed") ?? defaultSpeed;
            if (speed <= 0)
            {
                dir = Vector2.Zero;
            }
            var horizontal = dir * (float)speed;

            if (m.Flying)
            {
                var fly = new Vector3(horizontal.X, horizontal.Y, 0) * (float)dt;
                var target = t.Position + fly;
                if (m.MoveTarget is { } mt)
                {
                    target.Z = mt.Z;
                }
                Move(sim, e, t, m, target, now, dir);
                continue;
            }

            // Jump and gravity.
            if (m.JumpRequested && m.Grounded && !immobile)
            {
                var jumpMult = stats is null ? 1 : Math.Max(0.1, stats.Get("jump.height"));
                m.Velocity.Z = jumpVelocity * MathF.Sqrt((float)jumpMult);
                m.Grounded = false;
                sim.Events.Publish(new EntityJumped(e) { Tick = sim.Clock.Tick });
            }
            m.JumpRequested = false;
            if (!m.Grounded)
            {
                m.Velocity.Z -= gravity * (float)dt;
            }
            m.Velocity.X = horizontal.X;
            m.Velocity.Y = horizontal.Y;

            var proposed = t.Position + m.Velocity * (float)dt;
            var from = t.Position;

            // Horizontal validation with sliding.
            var feet = from.Z;
            var ok = CanOccupy(sim, blocked, proposed.X, proposed.Y, feet, stepUp, out var ground);
            if (!ok)
            {
                var slideX = new Vector3(proposed.X, from.Y, proposed.Z);
                var slideY = new Vector3(from.X, proposed.Y, proposed.Z);
                if (CanOccupy(sim, blocked, slideX.X, slideX.Y, feet, stepUp, out ground))
                {
                    proposed = slideX;
                    ok = true;
                }
                else if (CanOccupy(sim, blocked, slideY.X, slideY.Y, feet, stepUp, out ground))
                {
                    proposed = slideY;
                    ok = true;
                }
                else
                {
                    proposed = new Vector3(from.X, from.Y, proposed.Z);
                    m.Velocity.X = 0;
                    m.Velocity.Y = 0;
                    sim.Terrain.TryFindGround(from.X, from.Y, feet, stepUp, out ground, out _);
                    ok = true;
                    if (m.Path.Count > 0 || m.MoveTarget is not null)
                    {
                        // Stuck on a path: drop it so the AI or player can re-plan.
                        m.Path.Clear();
                        m.MoveTarget = null;
                    }
                }
            }

            // Vertical resolution against the ground under the new position.
            var groundHeight = ground.Height;
            if (m.Velocity.Z <= 0 && proposed.Z <= groundHeight + 0.001f)
            {
                if (!m.Grounded && from.Z - groundHeight > 3 && stats is not null)
                {
                    var fall = from.Z - groundHeight;
                    var perUnit = rules.Number("move.fall_damage_per_unit", 0);
                    var threshold = rules.Number("move.fall_damage_threshold", 6);
                    if (perUnit > 0 && fall > threshold)
                    {
                        stats.AddResource("life", -(fall - threshold) * perUnit * stats.ResourceMax("life") * 0.01);
                    }
                }
                proposed.Z = groundHeight;
                m.Velocity.Z = 0;
                m.Grounded = true;
            }
            else
            {
                m.Grounded = false;
            }
            if (!ok)
            {
                proposed = from;
            }
            Move(sim, e, t, m, proposed, now, dir);
        }
    }

    private static void Move(Simulation sim, Ecs.Entity e, Transform t, Motion m, Vector3 to, double now, Vector2 dir)
    {
        var from = t.Position;
        if (dir.LengthSquared() > 0.0001f)
        {
            t.Yaw = MathF.Atan2(dir.Y, dir.X);
            m.LastMoveTime = now;
        }
        else if (now - m.LastMoveTime > 0.15 && m.StationarySince < m.LastMoveTime)
        {
            m.StationarySince = now;
        }
        if (to != from)
        {
            t.Position = to;
            sim.Spatial.Update(e, to);
        }
        else if (!sim.Spatial.TryGetPosition(e, out _))
        {
            sim.Spatial.Update(e, to);
        }
    }

    private static void FollowPath(Transform t, Motion m)
    {
        if (m.Path.Count > 0)
        {
            var next = m.Path[0];
            var to = new Vector2(next.X - t.Position.X, next.Y - t.Position.Y);
            var dist = to.Length();
            var tolerance = m.Path.Count == 1 ? m.StopDistance : 0.35f;
            if (dist <= tolerance)
            {
                m.Path.RemoveAt(0);
                if (m.Path.Count == 0)
                {
                    m.MoveDir = Vector2.Zero;
                    m.MoveTarget = null;
                }
                return;
            }
            m.MoveDir = to / dist;
            return;
        }
        if (m.MoveTarget is { } target)
        {
            var to = new Vector2(target.X - t.Position.X, target.Y - t.Position.Y);
            var dist = to.Length();
            if (dist <= m.StopDistance)
            {
                m.MoveTarget = null;
                m.MoveDir = Vector2.Zero;
                return;
            }
            m.MoveDir = to / dist;
        }
    }

    public static bool CanOccupy(Simulation sim, BlockedCells? blocked, float x, float y, double feet, double stepUp, out Surface ground)
    {
        if (!sim.Terrain.TryFindGround(x, y, feet, stepUp, out ground, out _, out var flags))
        {
            return false;
        }
        if (blocked is not null && blocked.IsBlocked(CellCoord.Of(x, y)))
        {
            return false;
        }
        if ((flags & CellFlags.Blocked) != 0 && ground.Tags.Has("ground"))
        {
            return false;
        }
        return true;
    }

    /// <summary>Walks a straight segment and returns the furthest reachable point, for dashes and teleports.</summary>
    public static Vector3 ResolveDestination(Simulation sim, Vector3 from, Vector3 to)
    {
        var blocked = sim.TryService<BlockedCells>(out var bc) ? bc : null;
        var stepUp = sim.Content.Rules.Number("move.step_up", 0.6) + 0.6;
        var delta = to - from;
        var length = new Vector2(delta.X, delta.Y).Length();
        if (length < 0.01f)
        {
            return from;
        }
        var steps = (int)Math.Ceiling(length / 0.5f);
        var last = from;
        var feet = from.Z;
        for (var i = 1; i <= steps; i++)
        {
            var p = from + delta * (i / (float)steps);
            if (!CanOccupy(sim, blocked, p.X, p.Y, feet, stepUp, out var ground))
            {
                break;
            }
            feet = ground.Height;
            last = new Vector3(p.X, p.Y, ground.Height);
        }
        return last;
    }
}
