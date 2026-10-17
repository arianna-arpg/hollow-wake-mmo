using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;

namespace HollowWake.Core.Systems;

/// <summary>Flies projectiles, tests hits against hostile bodies and terrain, runs their effects.</summary>
public sealed class ProjectileSystem : SimSystem
{
    public override string Name => "projectiles";
    public override SimPhase Phase => SimPhase.Move;
    public override int Order => 10;

    public override void Update(Simulation sim)
    {
        var dt = (float)sim.Clock.Dt;
        var gravity = (float)sim.Content.Rules.Number("move.gravity", 24);
        foreach (var (e, t, p) in sim.World.Query<Transform, Projectile>())
        {
            if (p.Gravity)
            {
                p.Velocity.Z -= gravity * dt;
            }
            var step = p.Velocity * dt;
            var next = t.Position + step;
            p.Traveled += new Vector2(step.X, step.Y).Length();
            var owner = p.Context.Origin;
            var hitSomething = false;
            foreach (var (other, pos, _) in sim.Spatial.Within(next, p.Radius + 1.5f, 3f).ToList())
            {
                if (other == e || other == owner || p.Hit.Contains(other))
                {
                    continue;
                }
                if (!sim.World.TryGet<Transform>(other, out var ot) || !sim.World.Has<Stats>(other) || sim.World.Has<Dead>(other))
                {
                    continue;
                }
                if (sim.World.Has<Projectile>(other) || sim.World.Has<Field>(other))
                {
                    continue;
                }
                if (!Relations.IsHostile(sim, owner, other))
                {
                    continue;
                }
                var dx = pos.X - next.X;
                var dy = pos.Y - next.Y;
                var reach = p.Radius + ot.Radius;
                if (dx * dx + dy * dy > reach * reach || Math.Abs(pos.Z + ot.Height * 0.5f - next.Z) > ot.Height)
                {
                    continue;
                }
                p.Hit.Add(other);
                CompiledEffect.ExecuteAll(p.OnHit, p.Context.WithTarget(other).WithPoint(next));
                hitSomething = true;
                if (p.Hit.Count > p.Pierce)
                {
                    Expire(sim, e, next);
                    break;
                }
            }
            if (!sim.World.IsAlive(e))
            {
                continue;
            }
            var ground = sim.Terrain.HeightAt(next.X, next.Y);
            if (next.Z < ground - 0.05f || p.Traveled >= p.Range)
            {
                CompiledEffect.ExecuteAll(p.OnExpire, p.Context.Targetless().WithPoint(new Vector3(next.X, next.Y, Math.Max(ground, next.Z))));
                Expire(sim, e, next);
                continue;
            }
            t.Position = next;
            sim.Spatial.Update(e, next);
            _ = hitSomething;
        }
    }

    private static void Expire(Simulation sim, Entity e, Vector3 at)
    {
        sim.Spatial.Remove(e);
        sim.Events.Publish(new EntityDespawned(e, "projectile") { Tick = sim.Clock.Tick });
        sim.World.DestroyDeferred(e);
    }
}
