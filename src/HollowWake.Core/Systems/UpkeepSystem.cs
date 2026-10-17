using HollowWake.Core.Sim;
using HollowWake.Core.Weaves;

namespace HollowWake.Core.Systems;

/// <summary>
/// Charges the running costs of toggles each tick and turns off whatever its bearer can no longer pay for,
/// whose time ran out, whose slot left the rack or whose bearer died; recovers still banks while their
/// bearer stands still and bleeds them while it moves; lets brims at rest decay. Runs with the statuses,
/// after regeneration, and walks only the books that need it.
/// </summary>
public sealed class UpkeepSystem : SimSystem
{
    public override string Name => "upkeep";
    public override SimPhase Phase => SimPhase.Status;
    public override int Order => 5;

    public override void Update(Simulation sim) => sim.Service<WeaveRuntime>().Upkeep(sim);
}
