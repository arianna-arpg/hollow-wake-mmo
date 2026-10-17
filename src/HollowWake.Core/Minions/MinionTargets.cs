using HollowWake.Core.Components;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Minions;

/// <summary>
/// The target kinds minions bring to a form's <c>target</c> block: <c>corpse</c> (a corpse that can still be
/// raised or consumed) and <c>minion</c> (one of the caster's living minions or constructs). The weave
/// runtime checks them before any cost is paid and searches near the aim when the body given does not fit;
/// a corpse summon reads its form's <c>consume</c>.
/// </summary>
public static class MinionTargets
{
    public static void Register()
    {
        TargetKinds.Register("corpse", static (sim, _, e) => Corpses.Is(sim.World, e), null, "a corpse that can still be raised or consumed");
        TargetKinds.Register("minion", static (sim, caster, e) => Owns(sim, caster, e), null, "one of the caster's living minions or constructs");
    }

    /// <summary>Whether a body is one of the caster's living minions or constructs.</summary>
    public static bool Owns(Simulation sim, Entity caster, Entity e) =>
        TargetKinds.Living(sim, e) && !sim.World.IsDeferredDestroy(e) && sim.World.TryGet<Minion>(e, out var m) && m.Owner == caster;
}
