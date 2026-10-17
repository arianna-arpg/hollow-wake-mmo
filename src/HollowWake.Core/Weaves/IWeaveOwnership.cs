using HollowWake.Core.Definitions;
using HollowWake.Core.Ecs;
using HollowWake.Core.Sim;

namespace HollowWake.Core.Weaves;

/// <summary>
/// Decides whether a hero may slot a weave: the form or preset learned into that rack seat, the glyph stones
/// socketed there in order with their levels and rows, and the Threads the hero is attuned to. The items
/// package implements and registers it; the weave runtime asks it only while the rule
/// casting.require_ownership holds, and treats a missing service as permission (monsters, tools and tests
/// compose freely).
/// </summary>
public interface IWeaveOwnership
{
    /// <summary>True when the hero owns everything the spec names for that seat; otherwise the reason the slot refuses.</summary>
    bool Allows(Simulation sim, Entity hero, int slot, WeaveSpec spec, out string reason);
}
