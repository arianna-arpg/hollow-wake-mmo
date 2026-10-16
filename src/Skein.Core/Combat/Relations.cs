using Skein.Core.Components;
using Skein.Core.Definitions;
using Skein.Core.Ecs;
using Skein.Core.Sim;

namespace Skein.Core.Combat;

/// <summary>Who may hurt whom. Factions come from content; minions borrow their owner's faction.</summary>
public static class Relations
{
    public static string? FactionOf(Simulation sim, Entity e)
    {
        var guard = 0;
        while (sim.World.TryGet<Identity>(e, out var id) && guard++ < 8)
        {
            if (sim.World.TryGet<Minion>(e, out var minion) && !minion.Owner.IsNone && sim.World.IsAlive(minion.Owner))
            {
                e = minion.Owner;
                continue;
            }
            if (id.Faction is not null)
            {
                return id.Faction;
            }
            if (!id.Owner.IsNone && sim.World.IsAlive(id.Owner))
            {
                e = id.Owner;
                continue;
            }
            return id.IsPlayer ? "players" : null;
        }
        return null;
    }

    public static Stance StanceBetween(Simulation sim, string? a, string? b)
    {
        if (a is null || b is null)
        {
            return Stance.Neutral;
        }
        if (string.Equals(a, b, StringComparison.Ordinal))
        {
            return Stance.Allied;
        }
        if (sim.Content.TryGet<FactionDef>(a, out var fa) && fa is not null)
        {
            if (fa.Relations.TryGetValue(b, out var s))
            {
                return s;
            }
            if (sim.Content.TryGet<FactionDef>(b, out var fb) && fb is not null && fb.Relations.TryGetValue(a, out var s2))
            {
                return s2;
            }
            return fa.Default;
        }
        return Stance.Neutral;
    }

    public static bool IsHostile(Simulation sim, Entity a, Entity b)
    {
        if (a == b)
        {
            return false;
        }
        var fa = FactionOf(sim, a);
        var fb = FactionOf(sim, b);
        if (fa is null || fb is null)
        {
            // Unaffiliated things (wild beasts without a faction) are hostile to everyone but their own kind.
            return !(fa is null && fb is null && SamePattern(sim, a, b));
        }
        return StanceBetween(sim, fa, fb) == Stance.Hostile;
    }

    public static bool IsAlly(Simulation sim, Entity a, Entity b)
    {
        if (a == b)
        {
            return true;
        }
        var fa = FactionOf(sim, a);
        var fb = FactionOf(sim, b);
        return fa is not null && fb is not null && StanceBetween(sim, fa, fb) is Stance.Allied or Stance.Friendly;
    }

    private static bool SamePattern(Simulation sim, Entity a, Entity b) =>
        sim.World.Get<Identity>(a)?.Pattern is { } pa && sim.World.Get<Identity>(b)?.Pattern is { } pb && pa == pb;
}
