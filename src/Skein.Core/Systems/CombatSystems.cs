using System.Numerics;
using Skein.Core.Combat;
using Skein.Core.Components;
using Skein.Core.Definitions;
using Skein.Core.Ecs;
using Skein.Core.Effects;
using Skein.Core.Sim;
using Skein.Core.StatusEffects;
using Skein.Core.Weaves;

namespace Skein.Core.Systems;

/// <summary>Advances casts and channels; resolves them when their time comes.</summary>
public sealed class CastingSystem : SimSystem
{
    public override string Name => "casting";
    public override SimPhase Phase => SimPhase.Act;

    public override void Update(Simulation sim)
    {
        var runtime = sim.Service<WeaveRuntime>();
        var now = sim.Clock.Time;
        foreach (var (e, book) in sim.World.Query<WeaveBook>())
        {
            var cast = book.Casting;
            if (cast is null)
            {
                continue;
            }
            if (sim.World.Has<Dead>(e))
            {
                book.Casting = null;
                continue;
            }
            var weave = cast.Slot.Compiled;
            if (weave is null)
            {
                book.Casting = null;
                continue;
            }
            if (sim.World.TryGet<Stats>(e, out var stats) && (stats.HasFlag("stunned") || stats.HasFlag("frozen")))
            {
                runtime.Interrupt(sim, e);
                continue;
            }
            if (cast.Channeling)
            {
                var interval = Math.Max(sim.Clock.Dt, weave.Form.ChannelInterval?.Evaluate(weave.ScopeFor(stats!)) ?? 0.5);
                if (now >= cast.NextChannelAt)
                {
                    if (cast.Pulses > 0 && !runtime.CanAfford(weave, stats!, out _))
                    {
                        book.Casting = null;
                        continue;
                    }
                    if (cast.Pulses > 0)
                    {
                        foreach (var (resource, cost) in runtime.Costs(weave, stats!))
                        {
                            stats!.AddResource(resource, -cost);
                        }
                    }
                    runtime.Execute(sim, e, cast, weave, cooldownAlreadyStarted: cast.Pulses > 0);
                    cast.Pulses++;
                    cast.NextChannelAt = now + interval;
                }
                if (cast.Duration > 0 && now - cast.StartedAt >= cast.Duration)
                {
                    book.Casting = null;
                }
                continue;
            }
            if (now - cast.StartedAt >= cast.Duration)
            {
                book.Casting = null;
                runtime.Execute(sim, e, cast, weave, cooldownAlreadyStarted: sim.Content.Rules.Text("combat.cooldown_at", "complete") == "press");
            }
        }
    }
}

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

/// <summary>Pulses standing fields on whoever stands inside them until they expire.</summary>
public sealed class FieldSystem : SimSystem
{
    public override string Name => "fields";
    public override SimPhase Phase => SimPhase.Move;
    public override int Order => 11;

    public override void Update(Simulation sim)
    {
        var now = sim.Clock.Time;
        foreach (var (e, t, f) in sim.World.Query<Transform, Field>())
        {
            if (!f.Follow.IsNone && sim.World.TryGet<Transform>(f.Follow, out var ft))
            {
                t.Position = ft.Position;
                sim.Spatial.Update(e, t.Position);
            }
            if (now >= f.ExpiresAt)
            {
                sim.Spatial.Remove(e);
                sim.Events.Publish(new EntityDespawned(e, "field") { Tick = sim.Clock.Tick });
                sim.World.DestroyDeferred(e);
                continue;
            }
            if (now < f.NextPulseAt)
            {
                continue;
            }
            f.NextPulseAt = now + f.Interval;
            foreach (var (other, _, _) in sim.Spatial.Within(t.Position, f.Radius, 3f).ToList())
            {
                if (other == e || !sim.World.Has<Stats>(other) || sim.World.Has<Dead>(other))
                {
                    continue;
                }
                var hostile = Relations.IsHostile(sim, f.Context.Origin, other);
                var pass = f.Filter switch
                {
                    "enemy" => hostile,
                    "ally" => !hostile && other != f.Context.Origin,
                    "ally_or_self" => !hostile,
                    _ => true,
                };
                if (pass)
                {
                    CompiledEffect.ExecuteAll(f.Pulse, f.Context.WithTarget(other));
                }
            }
        }
    }
}

/// <summary>Ticks statuses, regenerates resources, maintains condition flags, clears expired temporaries.</summary>
public sealed class StatusSystem : SimSystem
{
    public override string Name => "statuses";
    public override SimPhase Phase => SimPhase.Status;

    public override void Update(Simulation sim)
    {
        var dt = sim.Clock.Dt;
        var now = sim.Clock.Time;
        var service = sim.Service<StatusService>();
        var resources = sim.Content.Each<ResourceDef>().ToList();
        foreach (var (e, stats) in sim.World.Query<Stats>())
        {
            stats.Now = now;
            if (sim.World.Has<Dead>(e))
            {
                continue;
            }
            if (sim.World.TryGet<Statuses>(e, out var statuses))
            {
                service.Tick(sim, e, stats, statuses, dt);
                if (!sim.World.IsAlive(e) || sim.World.Has<Dead>(e))
                {
                    continue;
                }
            }
            foreach (var r in resources)
            {
                if (r.Regen is null)
                {
                    continue;
                }
                var regen = stats.Get(r.Regen);
                if (regen != 0)
                {
                    stats.AddResource(r.Id, regen * dt);
                }
            }
            if (sim.World.TryGet<Motion>(e, out var m))
            {
                var moving = now - m.LastMoveTime <= 0.15;
                var stationary = now - m.LastMoveTime >= 0.6;
                Set(stats, "moving", moving);
                Set(stats, "stationary", stationary);
                Set(stats, "airborne", !m.Grounded && !m.Flying);
            }
        }
    }

    private static void Set(Stats stats, string flag, bool on)
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

/// <summary>Expires lifetimes and minions, clears corpses, removes despawned bodies from the index.</summary>
public sealed class LifetimeSystem : SimSystem
{
    public override string Name => "lifetimes";
    public override SimPhase Phase => SimPhase.Cleanup;

    public override void Update(Simulation sim)
    {
        var now = sim.Clock.Time;
        var corpseSeconds = sim.Content.Rules.Number("combat.corpse_seconds", 12);
        foreach (var (e, life) in sim.World.Query<Lifetime>())
        {
            if (now >= life.ExpiresAt)
            {
                Despawn(sim, e);
            }
        }
        foreach (var (e, minion) in sim.World.Query<Minion>())
        {
            if (now >= minion.ExpiresAt || (!minion.Owner.IsNone && !sim.World.IsAlive(minion.Owner)))
            {
                Despawn(sim, e);
            }
        }
        foreach (var (e, dead) in sim.World.Query<Dead>())
        {
            if (sim.World.Has<Player>(e))
            {
                continue;
            }
            if (now - dead.DiedAt >= corpseSeconds)
            {
                Despawn(sim, e);
            }
        }
        foreach (var (e, drop) in sim.World.Query<LootDrop>())
        {
            if (now >= drop.ExpiresAt)
            {
                Despawn(sim, e);
            }
        }
    }

    public static void Despawn(Simulation sim, Entity e)
    {
        if (!sim.World.IsAlive(e))
        {
            return;
        }
        sim.Spatial.Remove(e);
        if (sim.TryService<Patterns.BlockedCells>(out var blocked) && sim.World.Has<Prop>(e))
        {
            blocked.UnblockAll(e);
        }
        sim.Events.Publish(new EntityDespawned(e, sim.World.Get<Identity>(e)?.Pattern) { Tick = sim.Clock.Tick });
        sim.World.DestroyDeferred(e);
    }
}

/// <summary>Keeps spawn points stocked and respawns their packs.</summary>
public sealed class SpawnerSystem : SimSystem
{
    public override string Name => "spawners";
    public override SimPhase Phase => SimPhase.Think;
    public override int Order => -10;

    public override void Update(Simulation sim)
    {
        var now = sim.Clock.Time;
        var factory = sim.Service<Patterns.PatternFactory>();
        foreach (var (e, t, s) in sim.World.Query<Transform, Spawner>())
        {
            if (!s.Active)
            {
                continue;
            }
            s.Alive.RemoveAll(a => !sim.World.IsAlive(a) || sim.World.Has<Dead>(a));
            if (s.Alive.Count >= s.PackSize)
            {
                s.RespawnAt = double.NegativeInfinity;
                continue;
            }
            if (double.IsNegativeInfinity(s.RespawnAt))
            {
                // The pack thinned since the last check: schedule a respawn. Zone loading sets RespawnAt to now for the first spawn.
                s.RespawnAt = now + Math.Max(0, s.RespawnSeconds);
                continue;
            }
            if (now < s.RespawnAt)
            {
                continue;
            }
            var rng = sim.RngFor(e, "spawner");
            while (s.Alive.Count < s.PackSize)
            {
                var ang = rng.NextDouble() * Math.PI * 2;
                var dist = rng.Range(0.0, s.Scatter);
                var pos = t.Position + new Vector3((float)(Math.Cos(ang) * dist), (float)(Math.Sin(ang) * dist), 0);
                var spawned = factory.Spawn(sim, s.Pattern, pos, new Patterns.SpawnOptions { RollRarity = s.RollRarity, Group = s.Group, Rng = rng.Fork((ulong)s.Alive.Count) });
                if (spawned.IsNone)
                {
                    s.Active = false;
                    break;
                }
                s.Alive.Add(spawned);
            }
            s.RespawnAt = double.NegativeInfinity;
        }
    }
}
