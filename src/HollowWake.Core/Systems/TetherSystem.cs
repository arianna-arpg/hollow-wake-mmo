using System.Numerics;
using HollowWake.Core.Components;
using HollowWake.Core.Combat;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Effects.Handlers;
using HollowWake.Core.Sim;
using HollowWake.Core.Tags;

namespace HollowWake.Core.Systems;

/// <summary>
/// Pulses the live lines between linked bodies and points, testing each as a segment against whoever
/// crosses it. Runs after projectiles and fields have moved, so the ends are where they now stand. A tether
/// whose source is gone ends quietly; one that loses an end, runs out of time or is stretched past its
/// length ends with its <c>on_end</c>.
/// </summary>
public sealed class TetherSystem : SimSystem
{
    public override string Name => "tethers";
    public override SimPhase Phase => SimPhase.Move;
    public override int Order => 12;

    private static readonly TagSet TetherTags = TagSet.Of("tether");

    private readonly List<Entity> _near = new();

    public override void Update(Simulation sim)
    {
        if (sim.World.CountOf<Tether>() == 0)
        {
            return;
        }
        var now = sim.Clock.Time;
        foreach (var (e, t, tether) in sim.World.Query<Transform, Tether>())
        {
            if (!tether.Ended)
            {
                Tend(sim, e, t, tether, now);
            }
        }
    }

    /// <summary>Puts a tether into the world: an entity at the line's middle, as long as the line, facing along it.</summary>
    public static Entity Spawn(Simulation sim, Tether tether, Entity origin, string? look)
    {
        var e = sim.World.Create();
        var transform = new Transform();
        sim.World.Add(e, transform);
        sim.World.Add(e, new Identity { Name = "tether", Tags = TetherTags, Faction = Relations.FactionOf(sim, origin), Owner = origin, Look = look });
        sim.World.Add(e, tether);
        if (Ends(sim, tether, out var a, out var b))
        {
            Place(sim, e, transform, a, b);
        }
        sim.Events.Publish(new EntitySpawned(e, "tether", transform.Position) { Tick = sim.Clock.Tick });
        return e;
    }

    /// <summary>Where the line runs now; false when an end it is bound to is gone or dead.</summary>
    private static bool Ends(Simulation sim, Tether tether, out Vector3 a, out Vector3 b)
    {
        a = default;
        b = tether.Point;
        if (!Alive(sim, tether.From, out var from))
        {
            return false;
        }
        a = from;
        if (!tether.To.IsNone)
        {
            if (!Alive(sim, tether.To, out var to))
            {
                return false;
            }
            b = to;
        }
        return true;
    }

    private static bool Alive(Simulation sim, Entity e, out Vector3 at)
    {
        if (sim.World.IsAlive(e) && !sim.World.Has<Dead>(e) && sim.World.TryGet<Transform>(e, out var t))
        {
            at = t.Position;
            return true;
        }
        at = default;
        return false;
    }

    private static void Place(Simulation sim, Entity e, Transform t, Vector3 a, Vector3 b)
    {
        t.Position = (a + b) * 0.5f;
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        t.Radius = MathF.Sqrt(dx * dx + dy * dy) * 0.5f;
        if (dx * dx + dy * dy > 1e-8f)
        {
            t.Yaw = MathF.Atan2(dy, dx);
        }
        sim.Spatial.Update(e, t.Position);
    }

    private void Tend(Simulation sim, Entity e, Transform t, Tether tether, double now)
    {
        if (!tether.Context.Source.IsNone && !sim.World.IsAlive(tether.Context.Source))
        {
            End(sim, e, t, tether, quiet: true);
            return;
        }
        if (now >= tether.ExpiresAt || !Ends(sim, tether, out var a, out var b))
        {
            End(sim, e, t, tether, quiet: false);
            return;
        }
        var span = new Vector2(b.X - a.X, b.Y - a.Y);
        var length = span.Length();
        if (length > tether.MaxLength)
        {
            End(sim, e, t, tether, quiet: false);
            return;
        }
        Place(sim, e, t, a, b);
        if (now < tether.NextPulseAt || tether.Pulse.Length == 0)
        {
            return;
        }
        tether.NextPulseAt = now + tether.Interval;
        var half = tether.Width * 0.5f;
        _near.Clear();
        foreach (var (other, pos, _) in sim.Spatial.Within(t.Position, length * 0.5f + half + 1.5f, 4f))
        {
            if (other == e || other == tether.From || other == tether.To || !sim.World.TryGet<Transform>(other, out var ot))
            {
                continue;
            }
            var along = length < 1e-5f ? 0 : Math.Clamp(Vector2.Dot(new Vector2(pos.X - a.X, pos.Y - a.Y), span) / (length * length), 0, 1);
            var nearest = new Vector2(a.X + span.X * along, a.Y + span.Y * along);
            if (Vector2.Distance(nearest, new Vector2(pos.X, pos.Y)) <= half + ot.Radius)
            {
                _near.Add(other);
            }
        }
        foreach (var other in _near)
        {
            if (tether.Ended)
            {
                break;
            }
            if (EffectHelpers.Passes(tether.Context, other, tether.Filter))
            {
                CompiledEffect.ExecuteAll(tether.Pulse, tether.Context.WithTarget(other));
            }
        }
        _near.Clear();
    }

    /// <summary>Ends a tether now: its <c>on_end</c> runs at its middle unless it ends quietly, and it is gone.</summary>
    public static bool End(Simulation sim, Entity e)
    {
        if (!sim.World.TryGet<Tether>(e, out var tether) || tether.Ended || !sim.World.TryGet<Transform>(e, out var t))
        {
            return false;
        }
        End(sim, e, t, tether, quiet: !tether.Context.Source.IsNone && !sim.World.IsAlive(tether.Context.Source));
        return true;
    }

    private static void End(Simulation sim, Entity e, Transform t, Tether tether, bool quiet)
    {
        tether.Ended = true;
        if (!quiet && tether.OnEnd.Length > 0)
        {
            CompiledEffect.ExecuteAll(tether.OnEnd, EffectHelpers.At(tether.Context, Entity.None, t.Position, t.Forward, t.Position, tether.Context.Depth));
        }
        sim.Spatial.Remove(e);
        sim.Events.Publish(new EntityDespawned(e, "tether") { Tick = sim.Clock.Tick });
        sim.World.DestroyDeferred(e);
    }
}
