using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Interaction;

/// <summary>
/// Finding the interactables a hero stands at: a font for refunds (passives), a stash chest, a station. An
/// interactable is in reach within its range plus both bodies' radii, as interacting with it requires.
/// </summary>
public static class Fonts
{
    /// <summary>The interactable kind of a font, where passives are refunded.</summary>
    public const string FontKind = "font";

    /// <summary>The nearest font the hero can reach, or null.</summary>
    public static Entity? InReach(Simulation sim, Entity hero) => Nearest(sim, hero, FontKind);

    /// <summary>The nearest interactable of a kind within the hero's reach of it, or null.</summary>
    public static Entity? Nearest(Simulation sim, Entity hero, string kind)
    {
        if (!sim.World.TryGet<Transform>(hero, out var t))
        {
            return null;
        }
        var search = (float)sim.Content.Rules.Number("inventory.reach_search", 12);
        Entity? best = null;
        var bestDistance = double.MaxValue;
        foreach (var (e, _, distance) in sim.Spatial.Within(t.Position, search))
        {
            if (e == hero || !sim.World.TryGet<Interactable>(e, out var i) || i.Kind != kind || !sim.World.TryGet<Transform>(e, out var et))
            {
                continue;
            }
            if (t.GroundDistanceTo(et.Position) <= i.Range + t.Radius + et.Radius && distance < bestDistance)
            {
                best = e;
                bestDistance = distance;
            }
        }
        return best;
    }
}
