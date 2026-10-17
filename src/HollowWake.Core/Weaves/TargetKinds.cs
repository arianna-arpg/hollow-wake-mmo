using System.Collections.Concurrent;
using System.Numerics;
using HollowWake.Core.Combat;
using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Weaves;

/// <summary>Whether a body is something a target kind accepts for a caster.</summary>
public delegate bool TargetPredicate(Simulation sim, Entity caster, Entity candidate);

/// <summary>A fitting body near an aim point when the one given does not fit, or <see cref="Entity.None"/>.</summary>
public delegate Entity TargetSearch(Simulation sim, Entity caster, Entity given, Vector3 point, float radius);

/// <summary>A registered target kind: what it accepts, how it searches, and what it means.</summary>
public sealed record TargetKind(string Name, TargetPredicate Accepts, TargetSearch? Search, string Description);

/// <summary>
/// The registry of target kinds a form's <c>target</c> block names. The weave runtime checks a cast's target
/// against its kind before any cost is paid, and searches near the aim when the given body does not fit.
/// Built-in kinds are <c>enemy</c>, <c>ally</c> and <c>any</c> (living bodies by stance); packages register
/// theirs (corpses, minions) from their content kind files.
/// </summary>
public static class TargetKinds
{
    private static readonly ConcurrentDictionary<string, TargetKind> Kinds = new(StringComparer.Ordinal);

    static TargetKinds()
    {
        Register("enemy", static (sim, caster, e) => Living(sim, e) && Relations.IsHostile(sim, caster, e), null, "a living hostile body");
        Register("ally", static (sim, caster, e) => Living(sim, e) && !Relations.IsHostile(sim, caster, e), null, "a living body that is not hostile, the caster included");
        Register("any", static (sim, _, e) => Living(sim, e), null, "any living body");
    }

    public static void Register(string name, TargetPredicate accepts, TargetSearch? search = null, string description = "") =>
        Kinds[name] = new TargetKind(name, accepts, search, description);

    public static bool TryGet(string name, out TargetKind kind) => Kinds.TryGetValue(name, out kind!);

    public static IEnumerable<TargetKind> All => Kinds.Values.OrderBy(k => k.Name, StringComparer.Ordinal);

    /// <summary>A body with stats that is not dead.</summary>
    public static bool Living(Simulation sim, Entity e) => !e.IsNone && sim.World.IsAlive(e) && sim.World.Has<Stats>(e) && !sim.World.Has<Dead>(e);

    /// <summary>The nearest body to a point within a radius that a kind accepts, other than the given one.</summary>
    public static Entity NearestAccepted(Simulation sim, TargetKind kind, Entity caster, Entity given, Vector3 point, float radius)
    {
        var best = Entity.None;
        var bestDistance = float.MaxValue;
        foreach (var (e, _, distance) in sim.Spatial.Within(point, radius))
        {
            if (e == given || distance >= bestDistance || !kind.Accepts(sim, caster, e))
            {
                continue;
            }
            best = e;
            bestDistance = distance;
        }
        return best;
    }

    /// <summary>A kind's search: its own when it registered one that finds something, else the nearest body it accepts.</summary>
    public static Entity SearchFor(Simulation sim, TargetKind kind, Entity caster, Entity given, Vector3 point, float radius)
    {
        if (kind.Search is { } search && search(sim, caster, given, point, radius) is { IsNone: false } found)
        {
            return found;
        }
        return NearestAccepted(sim, kind, caster, given, point, radius);
    }
}
