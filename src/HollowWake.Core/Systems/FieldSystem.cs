using System.Numerics;
using HollowWake.Core.Attributes;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Effects.Handlers;
using HollowWake.Core.Expressions;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;
using HollowWake.Core.World;

namespace HollowWake.Core.Systems;

/// <summary>
/// Tends standing fields: lands delayed ones, carries followers, drifters and seekers, turns and sizes
/// their shapes, keeps track of who is inside when a field cares (entering, leaving, domains, exposure,
/// strike-once, pull), pulses on the bodies inside, and ends them when their time or their pulses run out.
/// </summary>
public sealed class FieldSystem : SimSystem
{
    public override string Name => "fields";
    public override SimPhase Phase => SimPhase.Move;
    public override int Order => 11;

    private static readonly TagSet FieldTags = TagSet.Of("field");

    // The bodies in one field's shape this tick, copied out of the spatial grid before any effect runs.
    private readonly List<(Entity Entity, Vector3 Position)> _near = new();
    private readonly List<Entity> _left = new();

    public override void Update(Simulation sim)
    {
        if (sim.World.CountOf<Field>() == 0)
        {
            return;
        }
        var now = sim.Clock.Time;
        foreach (var (e, t, f) in sim.World.Query<Transform, Field>())
        {
            if (!f.Ended)
            {
                Tend(sim, e, t, f, now);
            }
        }
    }

    /// <summary>Puts a field into the world at a point: the entity, its body, its index entry and its spawn event.</summary>
    public static Entity Spawn(Simulation sim, Field f, Vector3 at, Entity origin, string? look)
    {
        var e = sim.World.Create();
        var transform = new Transform { Position = at, Radius = f.Radius };
        if (f.Spec.Shape != Shapes.Circle)
        {
            transform.Yaw = f.Facing;
        }
        sim.World.Add(e, transform);
        sim.World.Add(e, new Identity { Name = "field", Tags = FieldTags, Faction = sim.World.Get<Identity>(origin)?.Faction, Owner = origin, Look = look });
        if (f.Spec.Domain.Length > 0)
        {
            f.DomainSource = "field:" + e.Key;
            f.DomainModifiers = new Modifier[f.Spec.Domain.Length];
            for (var i = 0; i < f.DomainModifiers.Length; i++)
            {
                f.DomainModifiers[i] = f.Spec.Domain[i].WithSource(f.DomainSource);
            }
        }
        sim.World.Add(e, f);
        sim.Spatial.Update(e, at);
        sim.Events.Publish(new EntitySpawned(e, "field", at) { Tick = sim.Clock.Tick });
        return e;
    }

    /// <summary>Ends every field a caster placed under an exclusive key; how many there were.</summary>
    public static int EndExclusive(Simulation sim, Entity origin, string key)
    {
        if (sim.World.CountOf<Field>() == 0)
        {
            return 0;
        }
        var ending = EffectHelpers.Rent();
        try
        {
            foreach (var (e, f) in sim.World.Query<Field>())
            {
                if (!f.Ended && f.Key == key && f.Context.Origin == origin)
                {
                    ending.Add(e);
                }
            }
            foreach (var e in ending)
            {
                End(sim, e);
            }
            return ending.Count;
        }
        finally
        {
            EffectHelpers.Return(ending);
        }
    }

    /// <summary>
    /// Ends a field now: its end hooks run (unless its source is gone, when nothing it carries may run),
    /// its domain leaves the bodies inside, and it is gone. False when it is no field in play.
    /// </summary>
    public static bool End(Simulation sim, Entity e)
    {
        if (!sim.World.TryGet<Field>(e, out var f) || f.Ended || !sim.World.TryGet<Transform>(e, out var t))
        {
            return false;
        }
        f.Ended = true;
        if (f.Spec.OnEnd.Length > 0 && !Orphaned(sim, f))
        {
            CompiledEffect.ExecuteAll(f.Spec.OnEnd, EffectHelpers.At(f.Context, Entity.None, t.Position, FacingOf(f), t.Position, f.Context.Depth));
        }
        if (f.Occupants is { } occupants)
        {
            foreach (var (body, occ) in occupants)
            {
                if (occ.InDomain && sim.World.TryGet<Stats>(body, out var stats))
                {
                    stats.Attributes.RemoveSource(f.DomainSource);
                }
            }
            occupants.Clear();
        }
        sim.Spatial.Remove(e);
        sim.Events.Publish(new EntityDespawned(e, "field") { Tick = sim.Clock.Tick });
        sim.World.DestroyDeferred(e);
        return true;
    }

    private static Vector2 FacingOf(Field f) => new(MathF.Cos(f.Facing), MathF.Sin(f.Facing));

    /// <summary>True when the field's source is gone: its pulses read the source's stats and weave, so the field ends quietly.</summary>
    private static bool Orphaned(Simulation sim, Field f) => !f.Context.Source.IsNone && !sim.World.IsAlive(f.Context.Source);

    private void Tend(Simulation sim, Entity e, Transform t, Field f, double now)
    {
        if (Orphaned(sim, f))
        {
            End(sim, e);
            return;
        }
        var spec = f.Spec;
        if (!f.Follow.IsNone && sim.World.TryGet<Transform>(f.Follow, out var ft))
        {
            t.Position = ft.Position;
            sim.Spatial.Update(e, t.Position);
        }
        if (!f.Landed)
        {
            if (now < f.LandsAt)
            {
                return;
            }
            f.Landed = true;
            sim.Events.Publish(new PlacementLanded(e, f.Context.Origin, t.Position) { Tick = sim.Clock.Tick });
            if (spec.OnLand.Length > 0)
            {
                CompiledEffect.ExecuteAll(spec.OnLand, EffectHelpers.At(f.Context, Entity.None, t.Position, FacingOf(f), t.Position, f.Context.Depth));
                if (f.Ended)
                {
                    return;
                }
            }
        }
        var dt = (float)sim.Clock.Dt;
        if (f.Drift != 0 || f.Seek != 0)
        {
            Wander(sim, e, t, f, dt);
        }
        if (f.Rotate != 0)
        {
            f.Facing += f.Rotate * dt;
            t.Yaw = f.Facing;
        }
        if (spec.SizeCurve is { } curve)
        {
            var life = Math.Max(1e-6, f.ExpiresAt - f.BornAt);
            var u = Math.Clamp((now - f.BornAt) / life, 0, 1);
            var factor = spec.SizeFrom + (spec.SizeTo - spec.SizeFrom) * (float)Curves.Evaluate(curve, u);
            f.Size = f.Size with { Radius = Math.Max(0.05f, f.Radius * factor) };
            t.Radius = f.Size.Radius;
        }
        if (now >= f.ExpiresAt || f.PulsesLeft <= 0)
        {
            End(sim, e);
            return;
        }
        var tracked = spec.TracksOccupants(f);
        if (tracked)
        {
            Track(sim, e, t, f, now, dt);
            if (f.Ended)
            {
                return;
            }
        }
        if (now < f.NextPulseAt)
        {
            return;
        }
        f.NextPulseAt = now + f.Interval;
        Pulse(sim, e, t, f, now, tracked);
        if (f.PulsesLeft != int.MaxValue && --f.PulsesLeft <= 0 && !f.Ended)
        {
            End(sim, e);
        }
    }

    /// <summary>Drifts along the facing and seeks the nearest enemy, keeping to the ground.</summary>
    private static void Wander(Simulation sim, Entity e, Transform t, Field f, float dt)
    {
        var move = Vector2.Zero;
        if (f.Drift != 0)
        {
            move += FacingOf(f) * (f.Drift * dt);
        }
        if (f.Seek != 0)
        {
            var radius = (float)sim.Content.Rules.Number("delivery.seek_radius", 8);
            var best = Vector3.Zero;
            var bestDistance = float.MaxValue;
            foreach (var (other, pos, distance) in sim.Spatial.Within(t.Position, radius))
            {
                if (distance < bestDistance && other != e && EffectHelpers.Passes(f.Context, other, "enemy"))
                {
                    best = pos;
                    bestDistance = distance;
                }
            }
            if (bestDistance < float.MaxValue && bestDistance > 0.3f)
            {
                var to = new Vector2(best.X - t.Position.X, best.Y - t.Position.Y);
                move += to / bestDistance * Math.Min(f.Seek * dt, bestDistance - 0.3f);
            }
        }
        if (move.LengthSquared() < 1e-10f)
        {
            return;
        }
        var x = t.Position.X + move.X;
        var y = t.Position.Y + move.Y;
        t.Position = new Vector3(x, y, sim.Terrain.HeightAt(x, y));
        sim.Spatial.Update(e, t.Position);
    }

    /// <summary>Who is inside now: entering bodies take the domain and run on_enter; leaving ones drop it and run on_exit; pull draws them in.</summary>
    private void Track(Simulation sim, Entity e, Transform t, Field f, double now, float dt)
    {
        var spec = f.Spec;
        var occupants = f.Occupants ??= new Dictionary<Entity, Occupant>();
        var tick = sim.Clock.Tick;
        var facing = FacingOf(f);
        Gather(sim, e, t, f, facing);
        foreach (var (body, _) in _near)
        {
            var passes = EffectHelpers.Passes(f.Context, body, f.Filter);
            var domain = spec.Domain.Length > 0 && EffectHelpers.Passes(f.Context, body, spec.DomainFilter);
            if (!passes && !domain)
            {
                continue;
            }
            occupants.TryGetValue(body, out var occ);
            var entering = !occ.Inside;
            occ.Inside = true;
            occ.Passes = passes;
            occ.SeenAt = tick;
            if (entering)
            {
                occ.EnteredAt = now;
            }
            if (domain && !occ.InDomain && sim.World.TryGet<Stats>(body, out var stats))
            {
                foreach (var m in f.DomainModifiers)
                {
                    stats.Attributes.Add(m);
                }
                occ.InDomain = true;
            }
            occupants[body] = occ;
            if (entering && passes && spec.OnEnter.Length > 0)
            {
                CompiledEffect.ExecuteAll(spec.OnEnter, f.Context.WithTarget(body));
                if (f.Ended)
                {
                    return;
                }
            }
        }
        _left.Clear();
        foreach (var (body, occ) in occupants)
        {
            if (occ.Inside && occ.SeenAt != tick)
            {
                _left.Add(body);
            }
        }
        foreach (var body in _left)
        {
            var occ = occupants[body];
            occ.Inside = false;
            if (occ.InDomain)
            {
                if (sim.World.TryGet<Stats>(body, out var stats))
                {
                    stats.Attributes.RemoveSource(f.DomainSource);
                }
                occ.InDomain = false;
            }
            if (spec.RearmOnExit)
            {
                occ.Struck = false;
            }
            if (spec.HitOnce && occ.Struck)
            {
                occupants[body] = occ;
            }
            else
            {
                occupants.Remove(body);
            }
            if (occ.Passes && spec.OnExit.Length > 0 && sim.World.IsAlive(body))
            {
                CompiledEffect.ExecuteAll(spec.OnExit, f.Context.WithTarget(body));
                if (f.Ended)
                {
                    return;
                }
            }
        }
        if (f.Pull != 0)
        {
            foreach (var (body, occ) in occupants)
            {
                if (!occ.Inside || !occ.Passes || !sim.World.TryGet<Transform>(body, out var bt))
                {
                    continue;
                }
                var to = new Vector2(t.Position.X - bt.Position.X, t.Position.Y - bt.Position.Y);
                var distance = to.Length();
                if (distance <= 0.3f)
                {
                    continue;
                }
                var amount = f.Pull > 0 ? Math.Min(f.Pull * dt, distance - 0.3f) : f.Pull * dt;
                DisplacementSystem.Nudge(sim, body, bt, to / distance * amount);
            }
        }
    }

    /// <summary>Copies the bodies inside the field's shape out of the grid, in grid order.</summary>
    private void Gather(Simulation sim, Entity e, Transform t, Field f, Vector2 facing)
    {
        _near.Clear();
        var shape = f.Spec.Shape;
        var center = t.Position;
        foreach (var (other, pos, _) in sim.Spatial.Within(center, shape.Reach(f.Size), 3f))
        {
            if (other == e || !shape.Contains(f.Size, new Vector2(pos.X - center.X, pos.Y - center.Y), facing, 0))
            {
                continue;
            }
            if (f.Occluded && !LineOfSight.Clear(sim.Terrain, center + EffectHelpers.Eye, pos + EffectHelpers.Eye))
            {
                continue;
            }
            _near.Add((other, pos));
        }
    }

    private void Pulse(Simulation sim, Entity e, Transform t, Field f, double now, bool tracked)
    {
        var spec = f.Spec;
        if (spec.OnTick.Length > 0)
        {
            CompiledEffect.ExecuteAll(spec.OnTick, EffectHelpers.At(f.Context, Entity.None, t.Position, FacingOf(f), t.Position, f.Context.Depth));
            if (f.Ended)
            {
                return;
            }
        }
        if (f.Pulse.Length == 0)
        {
            return;
        }
        if (!tracked)
        {
            Gather(sim, e, t, f, FacingOf(f));
        }
        for (var i = 0; i < _near.Count && !f.Ended; i++)
        {
            var other = _near[i].Entity;
            if (!EffectHelpers.Passes(f.Context, other, f.Filter))
            {
                continue;
            }
            if (tracked && f.Occupants is { } occupants)
            {
                if (!occupants.TryGetValue(other, out var occ) || !occ.Inside || now - occ.EnteredAt < f.Exposure || (spec.HitOnce && occ.Struck))
                {
                    continue;
                }
                occ.Struck = true;
                occupants[other] = occ;
            }
            CompiledEffect.ExecuteAll(f.Pulse, f.Context.WithTarget(other));
        }
        _near.Clear();
    }
}
