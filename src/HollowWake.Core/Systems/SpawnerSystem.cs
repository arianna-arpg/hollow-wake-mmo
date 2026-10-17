using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Effects;
using HollowWake.Core.Sim;
using HollowWake.Core.StatusEffects;

namespace HollowWake.Core.Systems;

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
