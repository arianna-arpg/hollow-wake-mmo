using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Content;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Effects.Handlers;
using HollowWake.Core.Maths;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.World;

namespace HollowWake.Core.Systems;

/// <summary>
/// Flies projectiles: steers them along their trajectory axes, tests their hit shapes against hostile
/// bodies, runs their hooks, and decides what a spent flight does next. A flight spent on a hit forks
/// while it has forks, then chains while it has chains, then turns back when it returns, else ends;
/// rehit flights and returning legs pass through bodies. Range, duration and the ground end a flight
/// (or turn it back, or bounce it while it has bounces). Every number is integrated deterministically
/// from the flight's own state; erratic flights roll on their own random stream.
/// </summary>
public sealed class ProjectileSystem : SimSystem
{
    public override string Name => "projectiles";
    public override SimPhase Phase => SimPhase.Move;
    public override int Order => 10;

    private static readonly TagSet ProjectileTags = TagSet.Of("projectile");

    // The bodies near one flight this tick, copied out of the spatial grid before any hit runs effects.
    private readonly List<(Entity Entity, float Time)> _near = new();

    // The delivery rules flights read, and the rule set they were read from: rule sets never change in place,
    // so they are read again only when the content brings a new one.
    private FlightRules _rules;
    private Rules? _rulesOf;

    /// <summary>The delivery rules a tick of flights reads.</summary>
    private readonly struct FlightRules
    {
        public FlightRules(Rules rules)
        {
            Gravity = (float)rules.Number("move.gravity", 24);
            ChainRange = (float)rules.Number("delivery.chain_range", 8);
            ForkChildren = Math.Max(1, (int)rules.Number("delivery.fork_children", 2));
            ForkAngle = (float)(rules.Number("delivery.fork_angle", 30) * Math.PI / 180.0);
            HomingRadius = (float)rules.Number("delivery.homing_radius", 10);
            HomingReacquire = rules.Number("delivery.homing_reacquire", 0.25);
            MinSpeed = (float)rules.Number("delivery.min_speed", 0.5);
            ReturnArrive = (float)rules.Number("delivery.return_arrive", 0.6);
            ReturnMaxSeconds = rules.Number("delivery.return_max_seconds", 6);
            Restitution = (float)rules.Number("delivery.bounce_restitution", 0.6);
        }

        public readonly float Gravity;
        public readonly float ChainRange;
        public readonly int ForkChildren;
        public readonly float ForkAngle;
        public readonly float HomingRadius;
        public readonly double HomingReacquire;
        public readonly float MinSpeed;
        public readonly float ReturnArrive;
        public readonly double ReturnMaxSeconds;
        public readonly float Restitution;
    }

    public override void Update(Simulation sim)
    {
        if (sim.World.CountOf<Projectile>() == 0)
        {
            return;
        }
        if (!ReferenceEquals(_rulesOf, sim.Content.Rules))
        {
            _rulesOf = sim.Content.Rules;
            _rules = new FlightRules(_rulesOf);
        }
        foreach (var (e, t, p) in sim.World.Query<Transform, Projectile>())
        {
            if (!p.Ended)
            {
                Fly(sim, e, t, p, in _rules);
            }
        }
    }

    /// <summary>Puts a flight into the world at a point: the entity, its body, its index entry and its spawn event.</summary>
    public static Entity Spawn(Simulation sim, Projectile p, Vector3 at, Entity origin, string? look)
    {
        var e = sim.World.Create();
        sim.World.Add(e, new Transform { Position = at, Yaw = p.Heading, Radius = p.Radius,
            Height = (float)sim.Content.Rules.Number("delivery.projectile_height", 0.2) });
        sim.World.Add(e, new Identity { Name = "projectile", Pattern = null, Tags = ProjectileTags, Faction = Relations.FactionOf(sim, origin), Owner = origin, Look = look });
        sim.World.Add(e, p);
        sim.Spatial.Update(e, at);
        sim.Events.Publish(new EntitySpawned(e, "projectile", at) { Tick = sim.Clock.Tick });
        return e;
    }

    /// <summary>
    /// Ends a flight where it is now, as if it had run its course there: its end hooks run and it is gone.
    /// What detonations do to owned projectiles. False when the entity is no flight in progress.
    /// </summary>
    public static bool Detonate(Simulation sim, Entity e)
    {
        if (!sim.World.TryGet<Projectile>(e, out var p) || p.Ended || !sim.World.TryGet<Transform>(e, out var t))
        {
            return false;
        }
        Finish(sim, e, p, t.Position, Orphaned(sim, p) ? null : p.Spec.OnEnd);
        return true;
    }

    /// <summary>
    /// True when the flight's source is gone: its effects read the source's stats and weave, so nothing it
    /// carries may run any more. Such a flight ends quietly, as delayed effects do.
    /// </summary>
    private static bool Orphaned(Simulation sim, Projectile p) => !p.Context.Source.IsNone && !sim.World.IsAlive(p.Context.Source);

    private void Fly(Simulation sim, Entity e, Transform t, Projectile p, in FlightRules r)
    {
        if (Orphaned(sim, p))
        {
            Finish(sim, e, p, t.Position, null);
            return;
        }
        var dt = (float)sim.Clock.Dt;
        var now = sim.Clock.Time;
        var spec = p.Spec;
        var returningAtStart = p.Returning;
        Vector3 next;
        Vector3 step;
        if (p.Orbit != 0 && !p.Returning)
        {
            if (!sim.World.TryGet<Transform>(p.Context.Source, out var ct))
            {
                Finish(sim, e, p, t.Position, spec.OnExpire, spec.OnEnd);
                return;
            }
            p.SpiralAngle += p.Orbit * dt;
            next = new Vector3(ct.Position.X + MathF.Cos(p.SpiralAngle) * p.OrbitRadius, ct.Position.Y + MathF.Sin(p.SpiralAngle) * p.OrbitRadius, HitBodies.AimPoint(sim, p.Context.Source).Z);
            p.Heading = p.SpiralAngle + (p.Orbit > 0 ? MathF.PI / 2 : -MathF.PI / 2);
            step = next - p.Carrier;
            p.Velocity = step / dt;
        }
        else if (p.Spiral != 0 && !p.Returning)
        {
            if (p.Accel != 0)
            {
                p.Speed = Math.Max(r.MinSpeed, p.Speed + p.Accel * dt);
            }
            p.SpiralAngle += p.Spiral * dt;
            p.SpiralRadius += p.Speed * dt;
            next = new Vector3(p.Origin.X + MathF.Cos(p.SpiralAngle) * p.SpiralRadius, p.Origin.Y + MathF.Sin(p.SpiralAngle) * p.SpiralRadius, p.Carrier.Z);
            step = next - p.Carrier;
            if (step.X * step.X + step.Y * step.Y > 1e-10f)
            {
                p.Heading = MathF.Atan2(step.Y, step.X);
            }
            p.Velocity = step / dt;
        }
        else
        {
            Adopt(p);
            if (p.Returning || p.Steers || p.Accel != 0)
            {
                Steer(sim, e, p, dt, now, in r);
                p.Velocity.X = MathF.Cos(p.Heading) * p.Speed;
                p.Velocity.Y = MathF.Sin(p.Heading) * p.Speed;
            }
            if (spec.Gravity)
            {
                p.Velocity.Z -= r.Gravity * dt;
            }
            step = p.Velocity * dt;
            next = p.Carrier + step;
            p.LastVelocity = new Vector2(p.Velocity.X, p.Velocity.Y);
        }
        var flat = new Vector2(step.X, step.Y).Length();
        p.Traveled += flat;
        p.Odometer += flat;
        p.Carrier = next;
        var at = next;
        if (p.Wave != 0)
        {
            var offset = MathF.Sin(2 * MathF.PI * p.Wave * (float)(now - p.BornAt)) * p.Amplitude;
            at = new Vector3(next.X - MathF.Sin(p.Heading) * offset, next.Y + MathF.Cos(p.Heading) * offset, next.Z);
        }
        p.SpinAngle += p.Spin * dt;

        // A wall stops the flight before it can reach anything behind it: it ends against the wall, where it
        // last flew free, or glances off while it has bounces.
        if (!p.Phases && HitsWall(sim.Terrain, t.Position, at))
        {
            if (p.Bounces <= 0)
            {
                Finish(sim, e, p, t.Position, spec.OnExpire, spec.OnEnd);
                return;
            }
            p.Bounces--;
            Reflect(sim, p, t.Position, at);
            at = t.Position;
            p.Carrier = at;
            sim.Events.Publish(new ProjectileBounced(e, p.Context.Origin, at) { Tick = sim.Clock.Tick });
        }

        var contactTime = Strikes(sim, e, p, t.Position, at, now, in r);
        if (p.Ended)
        {
            return;
        }
        if (contactTime < 1)
        {
            // A chain or return starts at contact; the unused part of this tick's old path is discarded.
            at = Vector3.Lerp(t.Position, at, contactTime);
            p.Carrier = at;
            p.Odometer -= flat * (1 - contactTime);
            if (p.Returning) p.Traveled -= flat * (1 - contactTime);
        }

        var ground = sim.Terrain.HeightAt(at.X, at.Y);
        if (at.Z < ground - 0.05f)
        {
            if (spec.Gravity && p.Velocity.Z <= 0)
            {
                if (p.Bounces > 0)
                {
                    p.Bounces--;
                    p.Velocity.Z = -p.Velocity.Z * r.Restitution;
                    at.Z = ground;
                    p.Carrier = new Vector3(p.Carrier.X, p.Carrier.Y, ground);
                    sim.Events.Publish(new ProjectileBounced(e, p.Context.Origin, at) { Tick = sim.Clock.Tick });
                }
                else
                {
                    var landing = new Vector3(at.X, at.Y, ground);
                    Finish(sim, e, p, landing, spec.OnLand, spec.OnExpire, spec.OnEnd);
                    return;
                }
            }
            else if (p.Bounces > 0)
            {
                p.Bounces--;
                Reflect(sim, p, t.Position, at);
                at = t.Position;
                p.Carrier = at;
                sim.Events.Publish(new ProjectileBounced(e, p.Context.Origin, at) { Tick = sim.Clock.Tick });
            }
            else
            {
                Finish(sim, e, p, new Vector3(at.X, at.Y, Math.Max(ground, at.Z)), spec.OnExpire, spec.OnEnd);
                return;
            }
        }
        else if (p.Returning)
        {
            var home = Destination(sim, p);
            var reach = Math.Max(p.Speed * dt, r.ReturnArrive);
            if (returningAtStart && new Vector2(home.X - at.X, home.Y - at.Y).LengthSquared() <= reach * reach)
            {
                sim.Events.Publish(new ProjectileReturned(e, p.Context.Origin, at) { Tick = sim.Clock.Tick });
                Finish(sim, e, p, at, spec.OnReturn, spec.OnEnd);
                return;
            }
            if (now - p.TurnedAt > r.ReturnMaxSeconds)
            {
                Finish(sim, e, p, at, spec.OnExpire, spec.OnEnd);
                return;
            }
        }
        else if (p.Traveled >= p.Range || now - p.BornAt >= p.Duration)
        {
            if (Returns(p))
            {
                Turn(sim, e, p, at, now);
            }
            else
            {
                Finish(sim, e, p, new Vector3(at.X, at.Y, Math.Max(ground, at.Z)), spec.OnExpire, spec.OnEnd);
                return;
            }
        }

        if (spec.OnTravel.Length > 0)
        {
            for (var guard = 0; p.Odometer >= p.NextTravelAt && guard < 8 && !p.Ended; guard++)
            {
                p.NextTravelAt += spec.TravelEvery;
                CompiledEffect.ExecuteAll(spec.OnTravel, EffectHelpers.At(p.Context, Entity.None, at, p.Direction, at, p.Context.Depth));
            }
        }
        if (spec.OnTick.Length > 0)
        {
            for (var guard = 0; now >= p.NextTickAt && guard < 8 && !p.Ended; guard++)
            {
                p.NextTickAt += spec.TickInterval;
                CompiledEffect.ExecuteAll(spec.OnTick, EffectHelpers.At(p.Context, Entity.None, at, p.Direction, at, p.Context.Depth));
            }
        }
        if (p.Ended)
        {
            return;
        }
        t.Position = at;
        t.Yaw = p.Heading;
        sim.Spatial.Update(e, at);
    }

    // ------------------------------------------------------------ steering

    /// <summary>
    /// Takes up a velocity something else gave the flight (a dome turning it back or slowing it): the heading
    /// and speed follow the ground velocity when they no longer match it, so steering carries on from there.
    /// </summary>
    private static void Adopt(Projectile p)
    {
        var vx = p.Velocity.X;
        var vy = p.Velocity.Y;
        if (vx == p.LastVelocity.X && vy == p.LastVelocity.Y)
        {
            return;
        }
        var squared = vx * vx + vy * vy;
        if (squared < 1e-8f)
        {
            return;
        }
        var speed = MathF.Sqrt(squared);
        if (MathF.Abs(speed - p.Speed) > 1e-3f)
        {
            p.Speed = speed;
        }
        var cos = MathF.Cos(p.Heading);
        var sin = MathF.Sin(p.Heading);
        if (MathF.Abs(vx - cos * speed) > 1e-3f * speed || MathF.Abs(vy - sin * speed) > 1e-3f * speed)
        {
            p.Heading = MathF.Atan2(vy, vx);
        }
    }

    private static void Steer(Simulation sim, Entity e, Projectile p, float dt, double now, in FlightRules r)
    {
        if (p.Accel != 0)
        {
            p.Speed = Math.Max(r.MinSpeed, p.Speed + p.Accel * dt);
        }
        var at = p.Carrier;
        if (p.Returning)
        {
            var home = Destination(sim, p);
            p.Heading = Toward(at, home, p.Heading);
            // Climb or sink toward the return point so the way back meets it.
            var distance = new Vector2(home.X - at.X, home.Y - at.Y).Length();
            p.Velocity.Z = (home.Z - at.Z) / Math.Max(distance / Math.Max(p.Speed, 0.01f), dt);
            return;
        }
        if (p.Chasing)
        {
            if (Quarry(sim, p, p.SeekTarget, now) && sim.World.TryGet<Transform>(p.SeekTarget, out var chased))
            {
                var aim = HitBodies.AimPoint(sim, p.SeekTarget);
                p.Heading = Toward(at, aim, p.Heading);
                if (!p.Spec.Gravity) p.Velocity.Z = (aim.Z - at.Z) / MathF.Max(dt, Vector2.Distance(new(at.X, at.Y), new(aim.X, aim.Y)) / MathF.Max(.01f, p.Speed));
                return;
            }
            p.Chasing = false;
        }
        if (p.Homing != 0)
        {
            var quarry = Seek(sim, e, p, at, now, in r);
            if (!quarry.IsNone && sim.World.TryGet<Transform>(quarry, out var qt))
            {
                var aim = HitBodies.AimPoint(sim, quarry);
                Turn(ref p.Heading, Toward(at, aim, p.Heading), p.Homing * dt);
                if (!p.Spec.Gravity) p.Velocity.Z = (aim.Z - at.Z) / MathF.Max(dt, Vector2.Distance(new(at.X, at.Y), new(aim.X, aim.Y)) / MathF.Max(.01f, p.Speed));
            }
        }
        if (p.Guide != 0)
        {
            var aim = LiveAim.TryGet(sim, p.Context.Source, p.BornAt, out var live, out _) ? live : p.AimPoint;
            Turn(ref p.Heading, Toward(at, aim, p.Heading), p.Guide * dt);
        }
        if (p.Converging)
        {
            var to = new Vector2(p.AimPoint.X - at.X, p.AimPoint.Y - at.Y);
            if (to.LengthSquared() < 0.5625f)
            {
                p.Converging = false;
            }
            else
            {
                Turn(ref p.Heading, MathF.Atan2(to.Y, to.X), p.Converge * dt);
            }
        }
        if (p.Recurve != 0)
        {
            p.Heading += p.Recurve * p.Side * dt;
        }
        if (p.Erratic != 0 && p.Rng is { } rng)
        {
            p.Heading += (float)((rng.NextDouble() * 2 - 1) * p.Erratic * dt);
        }
        if (p.Zigzag != 0 && p.Odometer >= p.ZigzagAt)
        {
            var swing = p.ZigzagCount == 0 ? p.Zigzag : 2 * p.Zigzag;
            p.Heading += p.ZigzagCount % 2 == 0 ? swing : -swing;
            p.ZigzagCount++;
            p.ZigzagAt += p.ZigzagEvery;
        }
    }

    /// <summary>The heading from one point toward another, or the current heading when they coincide.</summary>
    private static float Toward(Vector3 from, Vector3 to, float current)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        return dx * dx + dy * dy < 1e-8f ? current : MathF.Atan2(dy, dx);
    }

    /// <summary>Turns a heading toward a desired one by at most <paramref name="max"/> radians, the short way round.</summary>
    private static void Turn(ref float heading, float desired, float max)
    {
        var delta = desired - heading;
        delta -= MathF.Floor((delta + MathF.PI) / (2 * MathF.PI)) * 2 * MathF.PI;
        heading += Math.Clamp(delta, -max, max);
    }

    /// <summary>The body a homing flight steers for: its quarry while that stays fair game, else the nearest fair body, searched now and then.</summary>
    private static Entity Seek(Simulation sim, Entity e, Projectile p, Vector3 at, double now, in FlightRules r)
    {
        if (!p.SeekTarget.IsNone && Quarry(sim, p, p.SeekTarget, now))
        {
            return p.SeekTarget;
        }
        if (p.SeekTarget.IsNone && !p.CastTarget.IsNone && Quarry(sim, p, p.CastTarget, now))
        {
            p.SeekTarget = p.CastTarget;
            return p.SeekTarget;
        }
        p.SeekTarget = Entity.None;
        if (now < p.NextSeekAt)
        {
            return Entity.None;
        }
        p.NextSeekAt = now + r.HomingReacquire;
        var best = Entity.None;
        var bestDistance = float.MaxValue;
        foreach (var (other, _, distance) in sim.Spatial.Within(at, r.HomingRadius))
        {
            if (distance < bestDistance && other != e && Quarry(sim, p, other, now))
            {
                best = other;
                bestDistance = distance;
            }
        }
        p.SeekTarget = best;
        return best;
    }

    /// <summary>Whether a body is fair game for this flight: alive, a hostile body with stats, not a delivery, not locked out by the ledger.</summary>
    private static bool Quarry(Simulation sim, Projectile p, Entity body, double now)
    {
        var world = sim.World;
        if (!world.IsAlive(body) || body == p.Context.Origin || !world.Has<Transform>(body) || !world.TryGet<Stats>(body, out var stats) || world.Has<Dead>(body) || stats.Flags.Contains(EffectHelpers.Untargetable))
        {
            return false;
        }
        if (world.Has<Projectile>(body) || world.Has<Field>(body) || p.Hits.Holds(body, now, p.Rehit))
        {
            return false;
        }
        return Relations.IsHostile(sim, p.Context.Origin, body);
    }

    /// <summary>Whether a step ends in a wall, or for a long step passes one halfway.</summary>
    private static bool HitsWall(Terrain terrain, Vector3 from, Vector3 to)
    {
        if (LineOfSight.IsWall(terrain, CellCoord.Of(to.X, to.Y)))
        {
            return true;
        }
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        return dx * dx + dy * dy > 0.81f && LineOfSight.IsWall(terrain, CellCoord.Of(from.X + dx * 0.5f, from.Y + dy * 0.5f));
    }

    /// <summary>Whether a flight may be at a point: above the ground there and, unless it phases, not in a wall.</summary>
    private static bool Open(Simulation sim, Projectile p, float x, float y, float z) =>
        sim.Terrain.HeightAt(x, y) <= z && (p.Phases || !LineOfSight.IsWall(sim.Terrain, CellCoord.Of(x, y)));

    /// <summary>Bounces a flight off rising ground or a wall: mirrors the heading across the side it struck.</summary>
    private static void Reflect(Simulation sim, Projectile p, Vector3 from, Vector3 to)
    {
        var alongX = Open(sim, p, to.X, from.Y, to.Z);
        var alongY = Open(sim, p, from.X, to.Y, to.Z);
        if (alongX && !alongY)
        {
            p.Heading = -p.Heading;
        }
        else if (alongY && !alongX)
        {
            p.Heading = MathF.PI - p.Heading;
        }
        else
        {
            p.Heading += MathF.PI;
        }
        p.Velocity.X = MathF.Cos(p.Heading) * p.Speed;
        p.Velocity.Y = MathF.Sin(p.Heading) * p.Speed;
    }

    // ------------------------------------------------------------ hits

    /// <summary>
    /// Strikes the bodies the flight's shape touches this tick. The grid is walked in place and only the
    /// bodies the shape touches are copied out (striking runs effects that may move or spawn bodies); the
    /// rest of the tests, all of them pure, run on the copies in contact order as the hits land, so a body an
    /// earlier hit killed is spared.
    /// </summary>
    private float Strikes(Simulation sim, Entity e, Projectile p, Vector3 from, Vector3 at, double now, in FlightRules r)
    {
        var owner = p.Context.Origin;
        var shape = p.Spec.Shape;
        var facing = !shape.Turns ? Vector2.UnitX : p.SpinAngle == 0 ? p.Direction : new Vector2(MathF.Cos(p.Heading + p.SpinAngle), MathF.Sin(p.Heading + p.SpinAngle));
        var bounds = HitBodies.Bounds(sim);
        var midpoint = (from + at) * 0.5f;
        var halfHeight = sim.World.Get<Transform>(e)!.Height * 0.5f;
        var distance = Vector2.Distance(new(from.X, from.Y), new(at.X, at.Y));
        _near.Clear();
        foreach (var (other, _, _) in sim.Spatial.Within(midpoint, shape.Reach(p.Size) + bounds.Radius + distance * 0.5f,
            bounds.Height + halfHeight + MathF.Abs(at.Z - from.Z) * 0.5f))
        {
            if (other == e || other == owner || !sim.World.Has<Stats>(other))
            {
                continue;
            }
            if (HitBodies.Sweep(sim, other, shape, p.Size, from, at, facing, halfHeight) is not { } time)
            {
                continue;
            }
            _near.Add((other, time));
        }
        _near.Sort(static (a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : a.Entity.Key.CompareTo(b.Entity.Key));
        for (var i = 0; i < _near.Count && !p.Ended; i++)
        {
            var other = _near[i].Entity;
            if (p.Hits.Holds(other, now, p.Rehit) || !sim.World.TryGet<Stats>(other, out var stats) || sim.World.Has<Dead>(other) || stats.Flags.Contains(EffectHelpers.Untargetable))
            {
                continue;
            }
            if (sim.World.Has<Projectile>(other) || sim.World.Has<Field>(other) || !Relations.IsHostile(sim, owner, other))
            {
                continue;
            }
            var contact = Vector3.Lerp(from, at, _near[i].Time);
            var chains = p.Chains;
            var returning = p.Returning;
            Strike(sim, e, p, other, contact, now, in r);
            if (!p.Ended && (p.Chains != chains || p.Returning != returning))
            {
                var time = _near[i].Time;
                _near.Clear();
                return time;
            }
        }
        _near.Clear();
        return 1;
    }

    private static void Strike(Simulation sim, Entity e, Projectile p, Entity other, Vector3 at, double now, in FlightRules r)
    {
        p.Hits.Record(other, now);
        var spec = p.Spec;
        var hit = EffectHelpers.At(p.Context, other, at, p.Direction, at, p.Context.Depth);
        CompiledEffect.ExecuteAll(spec.OnHit, hit);
        if (!p.FirstHitDone)
        {
            p.FirstHitDone = true;
            if (spec.OnFirstHit.Length > 0)
            {
                CompiledEffect.ExecuteAll(spec.OnFirstHit, hit);
            }
        }
        if (p.Ended || p.Returning || p.Rehit > 0)
        {
            return;
        }
        p.HitsTaken++;
        if (p.HitsTaken <= p.Pierce)
        {
            return;
        }
        if (p.Forks > 0)
        {
            Fork(sim, e, p, at, in r);
            return;
        }
        if (p.Chains > 0 && Chain(sim, e, p, other, at, now, in r))
        {
            return;
        }
        if (Returns(p))
        {
            Turn(sim, e, p, at, now);
            return;
        }
        Finish(sim, e, p, at, spec.OnEnd);
    }

    private static bool Returns(Projectile p) => p.Spec.Return != ReturnMode.None && !p.Spec.Gravity && p.Orbit == 0;

    /// <summary>Splits a spent flight into children that fly on from where it hit, fanned across the fork angle.</summary>
    private static void Fork(Simulation sim, Entity e, Projectile p, Vector3 at, in FlightRules r)
    {
        var n = r.ForkChildren;
        var look = sim.World.Get<Identity>(e)?.Look;
        for (var i = 0; i < n; i++)
        {
            var offset = n == 1 ? 0 : -r.ForkAngle + 2 * r.ForkAngle * i / (n - 1);
            var child = p.CloneFlight();
            child.Heading = p.Heading + offset;
            child.Velocity = new Vector3(MathF.Cos(child.Heading) * p.Speed, MathF.Sin(child.Heading) * p.Speed, p.Velocity.Z);
            child.Forks = p.Forks - 1;
            child.Traveled = 0;
            child.HitsTaken = 0;
            child.FirstHitDone = true;
            child.Carrier = at;
            child.SeekTarget = Entity.None;
            child.Chasing = false;
            child.Converging = false;
            child.SpiralAngle = child.Heading;
            child.SpiralRadius = 0;
            child.Side = offset < 0 ? -1 : 1;
            var ce = Spawn(sim, child, at, p.Context.Origin, look);
            if (p.Rng is { } rng)
            {
                child.Rng = new Rng(Rng.Combine(rng.Seed, (ulong)ce.Key));
            }
        }
        sim.Events.Publish(new ProjectileForked(e, p.Context.Origin, n, at) { Tick = sim.Clock.Tick });
        Finish(sim, e, p, at, null);
    }

    /// <summary>Turns a spent flight toward the nearest fair body it has not struck within the chain range; false when there is none.</summary>
    private static bool Chain(Simulation sim, Entity e, Projectile p, Entity from, Vector3 at, double now, in FlightRules r)
    {
        var best = Entity.None;
        var bestDistance = float.MaxValue;
        var bestPosition = at;
        foreach (var (other, pos, distance) in sim.Spatial.Within(at, r.ChainRange, 4f))
        {
            if (distance < bestDistance && other != e && other != from && Quarry(sim, p, other, now))
            {
                best = other;
                bestDistance = distance;
                bestPosition = pos;
            }
        }
        if (best.IsNone)
        {
            return false;
        }
        p.Chains--;
        p.HitsTaken = 0;
        p.Traveled = 0;
        p.SeekTarget = best;
        p.Chasing = true;
        p.Converging = false;
        p.Heading = Toward(at, bestPosition, p.Heading);
        p.Velocity.X = MathF.Cos(p.Heading) * p.Speed;
        p.Velocity.Y = MathF.Sin(p.Heading) * p.Speed;
        sim.Events.Publish(new ProjectileChained(e, p.Context.Origin, from, best) { Tick = sim.Clock.Tick });
        return true;
    }

    /// <summary>Starts the way back: the ledger clears so bodies struck going out can be struck again.</summary>
    private static void Turn(Simulation sim, Entity e, Projectile p, Vector3 at, double now)
    {
        p.Returning = true;
        p.TurnedAt = now;
        p.Hits.Clear();
        p.HitsTaken = 0;
        p.Chasing = false;
        p.Converging = false;
        p.Heading = Toward(at, Destination(sim, p), p.Heading + MathF.PI);
        p.Velocity.X = MathF.Cos(p.Heading) * p.Speed;
        p.Velocity.Y = MathF.Sin(p.Heading) * p.Speed;
        sim.Events.Publish(new ProjectileTurned(e, p.Context.Origin, at) { Tick = sim.Clock.Tick });
    }

    /// <summary>Where a returning flight is going: its caster's body when it returns to the caster and the caster stands, else its origin.</summary>
    private static Vector3 Destination(Simulation sim, Projectile p)
    {
        if (p.Spec.Return == ReturnMode.Caster && sim.World.TryGet<Transform>(p.Context.Source, out var ct) && !sim.World.Has<Dead>(p.Context.Source))
        {
            return HitBodies.AimPoint(sim, p.Context.Source);
        }
        return p.Origin;
    }

    /// <summary>Ends a flight at a point: runs the given lists there in order, then the projectile is gone.</summary>
    private static void Finish(Simulation sim, Entity e, Projectile p, Vector3 at, CompiledEffect[]? first, CompiledEffect[]? second = null, CompiledEffect[]? third = null)
    {
        p.Ended = true;
        if (first is { Length: > 0 } || second is { Length: > 0 } || third is { Length: > 0 })
        {
            var end = EffectHelpers.At(p.Context, Entity.None, at, p.Direction, at, p.Context.Depth);
            if (first is { Length: > 0 })
            {
                CompiledEffect.ExecuteAll(first, end);
            }
            if (second is { Length: > 0 })
            {
                CompiledEffect.ExecuteAll(second, end);
            }
            if (third is { Length: > 0 })
            {
                CompiledEffect.ExecuteAll(third, end);
            }
        }
        sim.Spatial.Remove(e);
        sim.Events.Publish(new EntityDespawned(e, "projectile") { Tick = sim.Clock.Tick });
        sim.World.DestroyDeferred(e);
    }
}
